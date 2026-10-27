using Microsoft.Extensions.DependencyInjection;
using Team.Repositories.AuditLog;

namespace Team.Services.AuditLog;

public static class AuditLogModule
{
    /// <summary>Module AuditLog (BE-32): đọc lịch sử nhóm và nhật ký hệ thống. Cần Teams.</summary>
    public static IServiceCollection AddAuditLogModule(this IServiceCollection services)
    {
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        return services;
    }
}
