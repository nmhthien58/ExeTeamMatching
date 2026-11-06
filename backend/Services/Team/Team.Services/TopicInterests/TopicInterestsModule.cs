using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Team.Repositories.TopicInterests;
using Team.Services.Academic;

namespace Team.Services.TopicInterests;

public static class TopicInterestsModule
{
    public static IServiceCollection AddTopicInterestsModule(this IServiceCollection services)
    {
        services.AddScoped<ITopicInterestRepository, TopicInterestRepository>();
        services.AddScoped<ITopicSuggestionClient, TopicSuggestionClient>();
        services.AddHttpClient<ITopicInterestSettingsClient, TopicInterestSettingsClient>((sp, http) =>
            http.BaseAddress = new Uri(sp.GetRequiredService<IConfiguration>()["Academic:BaseUrl"] ?? "http://localhost:5002"))
            .AddHttpMessageHandler<ServiceTokenHandler>();
        services.AddScoped<ITopicInterestService, TopicInterestService>();
        return services;
    }
}
