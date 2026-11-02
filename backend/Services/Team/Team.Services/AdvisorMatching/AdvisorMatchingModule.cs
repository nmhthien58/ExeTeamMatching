using Microsoft.Extensions.DependencyInjection;
using Team.Repositories.AdvisorMatching;

namespace Team.Services.AdvisorMatching;

public static class AdvisorMatchingModule
{
    /// <summary>Ghép cố vấn tự động, Ghép sớm, đổi giảng viên tay (BE-80, Thiên). Cần <c>AddMatchingClient</c> và module TopicInterests.</summary>
    public static IServiceCollection AddAdvisorMatchingModule(this IServiceCollection services)
    {
        services.AddScoped<IAdvisorMatchingRepository, AdvisorMatchingRepository>();
        services.AddScoped<LecturerAssignmentWriter>();
        services.AddScoped<IAdvisorSource, LecturerAdvisorSource>();
        services.AddScoped<IAdvisorSource, MentorAdvisorSource>();
        services.AddScoped<IAdvisorMatchingService, AdvisorMatchingService>();
        return services;
    }
}
