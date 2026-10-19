using System.Text.Json;
using System.Text.Json.Serialization;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Envelope chung của mọi message Kafka (docs/contracts/events.md). Message key = aggregateId.
/// </summary>
public sealed record EventEnvelope(
    Guid EventId,
    string EventType,
    DateTimeOffset OccurredAt,
    string? CorrelationId,
    string Producer,
    JsonElement Payload)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static EventEnvelope Create<TPayload>(string eventType, TPayload payload, string producer, string? correlationId, Guid? eventId = null) =>
        new(eventId ?? Guid.NewGuid(), eventType, DateTimeOffset.UtcNow, correlationId, producer,
            JsonSerializer.SerializeToElement(payload, JsonOptions));

    /// <summary>Đọc payload thành kiểu cụ thể.</summary>
    public TPayload GetPayload<TPayload>() =>
        Payload.Deserialize<TPayload>(JsonOptions)
        ?? throw new JsonException($"Payload của {EventType} rỗng.");

    public string Serialize() => JsonSerializer.Serialize(this, JsonOptions);

    public static EventEnvelope Deserialize(string json) =>
        JsonSerializer.Deserialize<EventEnvelope>(json, JsonOptions)
        ?? throw new JsonException("Message không đúng envelope.");
}

/// <summary>Tên topic (docs/contracts/events.md).</summary>
public static class Topics
{
    public const string Student = "exe.student";
    public const string Registration = "exe.registration";
    public const string Team = "exe.team";
    public const string Invitation = "exe.invitation";
    public const string ChangeRequest = "exe.change-request";
    public const string Assignment = "exe.assignment";
    public const string Collab = "exe.collab";
    public const string Reminder = "exe.reminder";

    public static string DeadLetter(string topic) => topic + ".dlq";
}
