using BuildingBlocks.Observability;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Phát message lên Kafka. Service có database dùng Outbox (BE-07) chứ không gọi trực tiếp;
/// publish trực tiếp chỉ dành cho Job (DeadlineApproaching, nhắc lịch) và DLQ.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(string topic, string key, EventEnvelope envelope, CancellationToken cancellationToken = default);

    /// <summary>Tạo envelope (producer = tên service, CorrelationId hiện tại) rồi phát.</summary>
    Task PublishAsync<TPayload>(string topic, string eventType, Guid aggregateId, TPayload payload, CancellationToken cancellationToken = default);
}

public sealed class KafkaEventPublisher : IEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaEventPublisher> _logger;

    public KafkaEventPublisher(IOptions<KafkaOptions> options, ILogger<KafkaEventPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            ClientId = _options.ServiceName,
            Acks = Acks.All,
            EnableIdempotence = true,
        }).Build();
    }

    public async Task PublishAsync(string topic, string key, EventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var message = new Message<string, string>
        {
            Key = key,
            Value = envelope.Serialize(),
            Headers = new Headers(),
        };
        if (envelope.CorrelationId is { } correlationId)
        {
            message.Headers.Add(CorrelationContext.GrpcHeaderName, System.Text.Encoding.UTF8.GetBytes(correlationId));
        }

        var result = await _producer.ProduceAsync(topic, message, cancellationToken);
        _logger.LogDebug("Đã phát {EventType} {EventId} lên {Topic} offset {Offset}",
            envelope.EventType, envelope.EventId, topic, result.Offset.Value);
    }

    public Task PublishAsync<TPayload>(string topic, string eventType, Guid aggregateId, TPayload payload, CancellationToken cancellationToken = default)
    {
        var envelope = EventEnvelope.Create(eventType, payload, _options.ServiceName, CorrelationContext.Id);
        return PublishAsync(topic, aggregateId.ToString(), envelope, cancellationToken);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
