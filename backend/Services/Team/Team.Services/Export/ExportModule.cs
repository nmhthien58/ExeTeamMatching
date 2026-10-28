using Microsoft.Extensions.DependencyInjection;
using Team.Repositories.Export;

namespace Team.Services.Export;

public static class ExportModule
{
    /// <summary>Module Export (BE-50): xuất Excel danh sách nhóm (W15, W22).</summary>
    public static IServiceCollection AddExportModule(this IServiceCollection services)
    {
        services.AddScoped<IExportRepository, ExportRepository>();
        services.AddScoped<IExportService, ExportService>();
        return services;
    }
}
