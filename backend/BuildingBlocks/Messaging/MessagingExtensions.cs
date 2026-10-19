using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocks.Messaging;

public static class MessagingExtensions
{
    /// <summary>Đăng ký publisher Kafka (section <c>Kafka</c>). Gọi một lần trong Program.cs.</summary>
    public static IServiceCollection AddKafka(this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        services.AddOptions<KafkaOptions>()
            .Bind(configuration.GetSection(KafkaOptions.SectionName))
            .PostConfigure(o => o.ServiceName = string.IsNullOrWhiteSpace(o.ServiceName) || o.ServiceName == "unknown" ? serviceName : o.ServiceName);
        services.TryAddSingleton<IEventPublisher, KafkaEventPublisher>();
        services.TryAddScoped<IProcessedEventStore, NoOpProcessedEventStore>();
        return services;
    }

    /// <summary>
    /// Đăng ký handler cho một sự kiện. Lần đầu gọi sẽ bật consumer chạy nền.
    /// <code>services.AddEventHandler&lt;TeamCreatedHandler&gt;(Topics.Team, "TeamCreated");</code>
    /// </summary>
    public static IServiceCollection AddEventHandler<THandler>(this IServiceCollection services, string topic, string eventType)
        where THandler : class, IIntegrationEventHandler
    {
        services.AddScoped<THandler>();
        services.AddSingleton(new HandlerRegistration(topic, eventType, typeof(THandler)));
        if (!services.Any(d => d.ImplementationType == typeof(KafkaConsumerService)))
        {
            services.AddHostedService<KafkaConsumerService>();
        }
        return services;
    }
}
