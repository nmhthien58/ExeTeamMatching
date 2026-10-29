using Microsoft.Extensions.DependencyInjection;
using Team.Repositories.TopicInterests;

namespace Team.Services.TopicInterests;

public static class TopicInterestsModule
{
    public static IServiceCollection AddTopicInterestsModule(this IServiceCollection services)
    {
        services.AddScoped<ITopicInterestRepository, TopicInterestRepository>();
        services.AddScoped<ITopicInterestService, TopicInterestService>();
        return services;
    }
}
