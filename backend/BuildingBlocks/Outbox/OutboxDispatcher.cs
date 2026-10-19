using System.Text.Json;
using BuildingBlocks.Messaging;
using BuildingBlocks.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Outbox;

/// <summary>Cấu hình dispatcher (section <c>Outbox</c>).</summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int PollIntervalMs { get; set; } = 1000;
    public int BatchSize { get; set; } = 100;

    /// <summary>Quá số lần này thì bỏ qua dòng (xem <c>last_error</c> để xử lý tay).</summary>
    public int MaxAttempts { get; set; } = 10;
}

/// <summary>
/// Đọc <c>outbox_messages</c> chưa gửi theo thứ tự <c>occurred_at</c>, phát lên Kafka, đánh dấu
/// <c>processed_at</c>; lỗi thì tăng <c>attempts</c> và lưu <c>last_error</c> để lần sau thử lại.
/// </summary>
public sealed class OutboxDispatcher<TDbContext>(
    IServiceScopeFactory scopeFactory,
    IEventPublisher publisher,
    IOptions<OutboxOptions> outboxOptions,
    IOptions<KafkaOptions> kafkaOptions,
    ILogger<OutboxDispatcher<TDbContext>> logger) : BackgroundService
    where TDbContext : DbContext
{
    private readonly OutboxOptions _options = outboxOptions.Value;
    private readonly string _producer = kafkaOptions.Value.ServiceName;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var sent = 0;
            try
            {
                sent = await DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox dispatcher lỗi, thử lại sau");
            }

            // Còn nhiều thì chạy tiếp ngay, hết thì nghỉ.
            if (sent < _options.BatchSize)
            {
                await Task.Delay(_options.PollIntervalMs, stoppingToken);
            }
        }
    }

    /// <summary>Gửi một lô; trả về số dòng đã xử lý. Public để test gọi trực tiếp.</summary>
    public async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();

        var messages = await db.Set<OutboxMessage>()
            .Where(m => m.ProcessedAt == null && m.Attempts < _options.MaxAttempts)
            .OrderBy(m => m.OccurredAt)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            using var correlation = CorrelationContext.Begin(message.CorrelationId);
            try
            {
                using var payload = JsonDocument.Parse(message.Payload);
                var envelope = new EventEnvelope(message.Id, message.EventType, message.OccurredAt,
                    message.CorrelationId, _producer, payload.RootElement.Clone());
                await publisher.PublishAsync(message.Topic, message.AggregateId.ToString(), envelope, cancellationToken);
                message.ProcessedAt = DateTimeOffset.UtcNow;
                message.LastError = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.Attempts++;
                message.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                logger.LogWarning(ex, "Không phát được {EventType} {EventId} (lần {Attempts})", message.EventType, message.Id, message.Attempts);
            }
        }

        if (messages.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        return messages.Count;
    }
}

/// <summary>Chống xử lý trùng bằng bảng <c>processed_events</c> của chính service.</summary>
public sealed class EfProcessedEventStore<TDbContext>(TDbContext db) : IProcessedEventStore
    where TDbContext : DbContext
{
    public Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken) =>
        db.Set<ProcessedEvent>().AnyAsync(e => e.EventId == eventId, cancellationToken);

    public async Task MarkProcessedAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        db.Set<ProcessedEvent>().Add(new ProcessedEvent
        {
            EventId = envelope.EventId,
            EventType = envelope.EventType,
            ProcessedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}

public static class OutboxExtensions
{
    /// <summary>Bật dispatcher cho DbContext của service (cần <see cref="MessagingExtensions.AddKafka"/>).</summary>
    public static IServiceCollection AddOutbox<TDbContext>(this IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration)
        where TDbContext : DbContext
    {
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.AddHostedService<OutboxDispatcher<TDbContext>>();
        return services;
    }

    /// <summary>Dùng bảng <c>processed_events</c> của DbContext để chống xử lý trùng sự kiện.</summary>
    public static IServiceCollection AddProcessedEventStore<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddScoped<IProcessedEventStore, EfProcessedEventStore<TDbContext>>();
        return services;
    }
}
