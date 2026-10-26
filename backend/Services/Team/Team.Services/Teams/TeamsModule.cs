using Microsoft.Extensions.DependencyInjection;
using Team.Repositories.AuditLog;
using Team.Repositories.Teams;
using Team.Services.Academic;

namespace Team.Services.Teams;

public static class TeamsModule
{
    /// <summary>Module Teams (BE-25, BE-26, BE-27): tạo / sửa nhóm, chi tiết, nhóm của tôi, trang chủ, tìm kiếm.</summary>
    public static IServiceCollection AddTeamsModule(this IServiceCollection services)
    {
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<AuditLogWriter>();
        services.AddScoped<RoundGuard>();
        services.AddScoped<TeamReader>();
        services.AddScoped<ITeamService, TeamService>();
        return services;
    }
}
