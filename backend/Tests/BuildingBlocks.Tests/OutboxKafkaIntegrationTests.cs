using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Tests;

/// <summary>
/// Chạy với Postgres và Kafka thật (deploy/docker-compose.yml: <c>docker compose up -d postgres kafka</c>).
/// Bật bằng biến môi trường <c>EXE_INTEGRATION=1</c>; mặc định bỏ qua để test chạy được trên máy không có Docker.
/// </summary>
public sealed class OutboxKafkaIntegrationTests
{
    private const string Schema = "bb_test";
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("EXE_TEST_PG") ?? "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";

    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    [Fact]
    public async Task Outbox_message_reaches_handler_exactly_once()
    {
        if (!Enabled)
        {
            return;
        }

        var topic = "bb.test." + Guid.NewGuid().ToString("N")[..8];
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<TestDbContext>(o => o.UseNpgsql(ConnectionString));
        services.AddKafka(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kafka:BootstrapServers"] = "localhost:9092",
                ["Kafka:GroupId"] = "bb-test-" + topic,
            }).Build(), "bb-test");
        services.AddProcessedEventStore<TestDbContext>();
        services.AddSingleton<Received>();
        services.AddEventHandler<CaptureHandler>(topic, "ThingHappened");
        services.Configure<OutboxOptions>(_ => { });
        services.AddSingleton<OutboxDispatcher<TestDbContext>>();

        await using var provider = services.BuildServiceProvider();
        await ResetSchemaAsync(provider);

        var aggregateId = Guid.NewGuid();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            db.AddOutboxMessage(topic, "ThingHappened", "Thing", aggregateId, new { thingId = aggregateId, name = "A" });
            await db.SaveChangesAsync();
        }

        var dispatcher = provider.GetRequiredService<OutboxDispatcher<TestDbContext>>();
        Assert.Equal(1, await dispatcher.DispatchBatchAsync(CancellationToken.None));
        Assert.Equal(0, await dispatcher.DispatchBatchAsync(CancellationToken.None));

        // Phát lại cùng message (giả lập gửi trùng) – consumer phải bỏ qua lần thứ hai.
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var sent = await db.Set<OutboxMessage>().SingleAsync();
            Assert.NotNull(sent.ProcessedAt);
            var envelope = new EventEnvelope(sent.Id, sent.EventType, sent.OccurredAt, sent.CorrelationId, "bb-test",
                System.Text.Json.JsonDocument.Parse(sent.Payload).RootElement.Clone());
            await provider.GetRequiredService<IEventPublisher>().PublishAsync(topic, aggregateId.ToString(), envelope);
        }

        var consumer = provider.GetServices<IHostedService>().OfType<KafkaConsumerService>().Single();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await consumer.StartAsync(cts.Token);

        var received = provider.GetRequiredService<Received>();
        while (received.Count < 1 && !cts.IsCancellationRequested)
        {
            await Task.Delay(200);
        }
        await Task.Delay(3000); // chờ thêm để chắc message trùng đã được đọc
        await consumer.StopAsync(CancellationToken.None);

        Assert.Equal(1, received.Count);
        Assert.Equal(aggregateId, received.LastThingId);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            Assert.Equal(1, await db.Set<ProcessedEvent>().CountAsync());
        }
    }

    private static async Task ResetSchemaAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        await db.Database.ExecuteSqlRawAsync($"""
            DROP SCHEMA IF EXISTS {Schema} CASCADE;
            CREATE SCHEMA {Schema};
            CREATE TABLE {Schema}.outbox_messages (
                id uuid PRIMARY KEY, topic varchar(100) NOT NULL, event_type varchar(100) NOT NULL,
                aggregate_type varchar(50) NOT NULL, aggregate_id uuid NOT NULL, payload jsonb NOT NULL,
                correlation_id varchar(64), occurred_at timestamptz NOT NULL DEFAULT now(),
                processed_at timestamptz, attempts integer NOT NULL DEFAULT 0, last_error text);
            CREATE TABLE {Schema}.processed_events (
                event_id uuid PRIMARY KEY, event_type varchar(100) NOT NULL, processed_at timestamptz NOT NULL DEFAULT now());
            """);
    }

    public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.AddOutboxMessages(Schema).AddProcessedEvents(Schema);
    }

    public sealed class Received
    {
        private int _count;
        public int Count => _count;
        public Guid LastThingId { get; set; }
        public void Add() => Interlocked.Increment(ref _count);
    }

    private sealed record Thing(Guid ThingId, string Name);

    public sealed class CaptureHandler(Received received) : IIntegrationEventHandler
    {
        public Task HandleAsync(EventEnvelope envelope, CancellationToken cancellationToken)
        {
            received.LastThingId = envelope.GetPayload<Thing>().ThingId;
            received.Add();
            return Task.CompletedTask;
        }
    }
}
