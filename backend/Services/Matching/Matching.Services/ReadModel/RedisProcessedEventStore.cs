using BuildingBlocks.Messaging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Matching.Services.ReadModel;

/// <summary>Chống xử lý trùng theo <c>eventId</c> bằng key Redis có hạn (Matching không có SQL).</summary>
public sealed class RedisProcessedEventStore(IConnectionMultiplexer redis, IOptions<MatchingOptions> options) : IProcessedEventStore
{
    private readonly MatchingOptions _options = options.Value;

    private RedisKey Key(Guid eventId) => $"{_options.KeyPrefix}processed:{eventId}";

    public Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken) =>
        redis.GetDatabase().KeyExistsAsync(Key(eventId));

    public Task MarkProcessedAsync(EventEnvelope envelope, CancellationToken cancellationToken) =>
        redis.GetDatabase().StringSetAsync(Key(envelope.EventId), envelope.EventType, TimeSpan.FromDays(_options.ProcessedEventDays));
}
