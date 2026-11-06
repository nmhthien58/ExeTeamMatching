using Microsoft.Extensions.DependencyInjection;
using Team.Repositories.Statistics;

namespace Team.Services.Statistics;

public static class StatisticsModule
{
    /// <summary>Module Statistics (BE-50): dashboard W02, sinh viên chưa có nhóm W20, đối tượng nhắc hạn. Cần AutoAssign.</summary>
    public static IServiceCollection AddStatisticsModule(this IServiceCollection services)
    {
        services.AddScoped<IStatisticsRepository, StatisticsRepository>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        return services;
    }
}
