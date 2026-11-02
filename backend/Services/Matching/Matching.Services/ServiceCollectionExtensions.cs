using BuildingBlocks.Messaging;
using Matching.Services.AdvisorMatching;
using Matching.Services.AutoAssignment;
using Matching.Services.Events;
using Matching.Services.ReadModel;
using Matching.Services.Suggestions;
using Matching.Services.TopicSuggestions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Matching.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Matching Service (BE-34, BE-35): read model Redis dựng từ Kafka, thuật toán gợi ý / kiểm tra / ghép tự động.
    /// Module AdvisorMatching (PlanAdvisorMatching – BE-79) đăng ký service của mình bên dưới.
    /// </summary>
    public static IServiceCollection AddMatchingServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MatchingOptions>(configuration.GetSection(MatchingOptions.SectionName));

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(configuration.GetConnectionString("Redis") ?? "localhost:6379");
            options.AbortOnConnectFail = false; // Redis khởi động chậm hơn service trong Docker
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton<IMatchingReadModel, RedisMatchingReadModel>();

        services.AddKafka(configuration, "matching");
        // Chống xử lý trùng bằng Redis thay cho bản NoOp mặc định của AddKafka.
        services.Replace(ServiceDescriptor.Scoped<IProcessedEventStore, RedisProcessedEventStore>());

        services.AddScoped<ReadModelProjector>();
        services.AddScoped<SuggestionService>();
        services.AddScoped<TopicSuggestionService>();
        services.AddScoped<AutoAssignmentPlanner>();
        services.AddSingleton<AdvisorMatchingPlanner>();

        if (configuration.GetValue("Kafka:ConsumersEnabled", true))
        {
            services.AddReadModelConsumers();
        }
        return services;
    }
}
