using BuildingBlocks.Observability;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Xử lý một loại sự kiện. Đăng ký bằng <c>services.AddEventHandler&lt;THandler&gt;(Topics.Team, "TeamCreated")</c>;
/// mỗi message được xử lý trong một DI scope riêng (dùng DbContext scoped bình thường).
/// </summary>
public interface IIntegrationEventHandler
{
    Task HandleAsync(EventEnvelope envelope, CancellationToken cancellationToken);
}

/// <summary>
/// Chống xử lý trùng theo <c>eventId</c> (bảng <c>processed_events</c>). Service có DB dùng
/// <see cref="Outbox.EfProcessedEventStore{TDbContext}"/>; Matching (chỉ Redis) tự cài.
/// </summary>
public interface IProcessedEventStore
{
    Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken);
    Task MarkProcessedAsync(EventEnvelope envelope, CancellationToken cancellationToken);
}

/// <summary>
/// Lỗi tạm thời đáng chờ lâu hơn (ví dụ sự kiện tới trước dữ liệu nó cần). Consumer thử lại tới
/// <see cref="KafkaOptions.RetryableMaxAttempts"/> lần với thời gian chờ tăng dần thay vì <see cref="KafkaOptions.MaxAttempts"/>.
/// </summary>
public interface IRetryableEventException;

/// <summary>Không chống trùng – chỉ dùng khi handler tự idempotent.</summary>
public sealed class NoOpProcessedEventStore : IProcessedEventStore
{
    public Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task MarkProcessedAsync(EventEnvelope envelope, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Consumer chạy nền: mỗi topic có handler được đọc bởi một vòng lặp riêng (message đang thử lại ở topic này
/// không chặn topic khác), bỏ qua message đã xử lý, thử tối đa <see cref="KafkaOptions.MaxAttempts"/> lần rồi đẩy
/// sang <c>&lt;topic&gt;.dlq</c>. Commit offset sau khi xong từng message; trong một topic, thứ tự theo key được giữ.
/// </summary>
public sealed class KafkaConsumerService(
    IServiceScopeFactory scopeFactory,
    IEnumerable<HandlerRegistration> registrations,
    IEventPublisher publisher,
    IOptions<KafkaOptions> options,
    ILogger<KafkaConsumerService> logger) : BackgroundService
{
    private readonly KafkaOptions _options = options.Value;
    private readonly List<HandlerRegistration> _registrations = registrations.ToList();

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var topics = _registrations.Select(r => r.Topic).Distinct().ToList();
        if (topics.Count == 0)
        {
            return Task.CompletedTask;
        }

        // Consume() là lời gọi chặn nên mỗi topic chạy trên thread riêng.
        var loops = topics.Select(topic => Task.Factory.StartNew(() => ConsumeLoop(topic, stoppingToken), stoppingToken,
            TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap());
        return Task.WhenAll(loops);
    }

    private async Task ConsumeLoop(string topic, CancellationToken stoppingToken)
    {
        var topics = new[] { topic };
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.GroupId ?? _options.ServiceName,
            ClientId = _options.ServiceName,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = true,
        }).Build();

        consumer.Subscribe(topics);
        logger.LogInformation("Consumer {Group} đọc các topic {Topics}", _options.GroupId ?? _options.ServiceName, topics);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result;
            try
            {
                result = consumer.Consume(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
            {
                // Topic chưa có (chưa service nào phát) – chờ, không ghi stack trace.
                logger.LogDebug("Chưa có topic: {Reason}", ex.Error.Reason);
                await Task.Delay(5000, stoppingToken);
                continue;
            }
            catch (ConsumeException ex)
            {
                logger.LogWarning(ex, "Lỗi đọc Kafka: {Reason}", ex.Error.Reason);
                await Task.Delay(1000, stoppingToken);
                continue;
            }

            if (result?.Message is null)
            {
                continue;
            }

            await HandleMessageAsync(result, stoppingToken);
            consumer.Commit(result);
        }

        consumer.Close();
    }

    private async Task HandleMessageAsync(ConsumeResult<string, string> result, CancellationToken stoppingToken)
    {
        EventEnvelope envelope;
        try
        {
            envelope = EventEnvelope.Deserialize(result.Message.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Message ở {Topic} offset {Offset} không đúng envelope, chuyển sang DLQ", result.Topic, result.Offset.Value);
            await SendToDeadLetterAsync(result, ex, stoppingToken);
            return;
        }

        var handlers = _registrations
            .Where(r => r.Topic == result.Topic && r.EventType == envelope.EventType)
            .ToList();
        if (handlers.Count == 0)
        {
            return;
        }

        using var correlation = CorrelationContext.Begin(envelope.CorrelationId);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<IProcessedEventStore>();
                if (await store.IsProcessedAsync(envelope.EventId, stoppingToken))
                {
                    logger.LogDebug("Bỏ qua {EventType} {EventId} vì đã xử lý", envelope.EventType, envelope.EventId);
                    return;
                }

                foreach (var registration in handlers)
                {
                    var handler = (IIntegrationEventHandler)scope.ServiceProvider.GetRequiredService(registration.HandlerType);
                    await handler.HandleAsync(envelope, stoppingToken);
                }

                await store.MarkProcessedAsync(envelope, stoppingToken);
                logger.LogInformation("Đã xử lý {EventType} {EventId} từ {Topic} offset {Offset}",
                    envelope.EventType, envelope.EventId, result.Topic, result.Offset.Value);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt < MaxAttemptsFor(ex))
            {
                logger.LogWarning(ex, "Xử lý {EventType} {EventId} lỗi lần {Attempt}, thử lại", envelope.EventType, envelope.EventId, attempt);
                await Task.Delay(Math.Min(_options.RetryDelayMs * attempt * attempt, 10_000), stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Xử lý {EventType} {EventId} lỗi {Attempt} lần, chuyển sang DLQ", envelope.EventType, envelope.EventId, attempt);
                await SendToDeadLetterAsync(result, ex, stoppingToken);
                return;
            }
        }
    }

    private int MaxAttemptsFor(Exception ex) =>
        ex is IRetryableEventException ? _options.RetryableMaxAttempts : _options.MaxAttempts;

    private async Task SendToDeadLetterAsync(ConsumeResult<string, string> result, Exception error, CancellationToken cancellationToken)
    {
        // Giữ nguyên message gốc; lý do lỗi nằm trong log của service.
        EventEnvelope envelope;
        try
        {
            envelope = EventEnvelope.Deserialize(result.Message.Value);
        }
        catch
        {
            envelope = EventEnvelope.Create("Unparseable", new { raw = result.Message.Value, error = error.Message },
                _options.ServiceName, CorrelationContext.Id);
        }

        try
        {
            await publisher.PublishAsync(Topics.DeadLetter(result.Topic), result.Message.Key ?? string.Empty, envelope, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không đẩy được sang DLQ {Topic}", Topics.DeadLetter(result.Topic));
        }
    }
}

/// <summary>Thông tin handler đăng ký, để consumer biết topic nào cần đọc.</summary>
public sealed record HandlerRegistration(string Topic, string EventType, Type HandlerType);
