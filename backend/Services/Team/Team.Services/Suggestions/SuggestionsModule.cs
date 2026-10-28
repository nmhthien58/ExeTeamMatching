using Microsoft.Extensions.DependencyInjection;
using Team.Repositories.Suggestions;

namespace Team.Services.Suggestions;

public static class SuggestionsModule
{
    /// <summary>Module Suggestions (BE-39): gợi ý thành viên S18, gợi ý nhóm S07. Cần <c>AddMatchingClient</c> và AutoAssign.</summary>
    public static IServiceCollection AddSuggestionsModule(this IServiceCollection services)
    {
        services.AddScoped<ISuggestionRepository, SuggestionRepository>();
        services.AddScoped<ISuggestionService, SuggestionService>();
        return services;
    }
}
