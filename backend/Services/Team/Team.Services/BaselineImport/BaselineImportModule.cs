using Microsoft.Extensions.DependencyInjection;
using Team.Repositories.BaselineImport;

namespace Team.Services.BaselineImport;

public static class BaselineImportModule
{
    public static IServiceCollection AddBaselineImportModule(this IServiceCollection services)
    {
        services.AddScoped<IBaselineImportRepository, BaselineImportRepository>();
        services.AddScoped<IBaselineImportService, BaselineImportService>();
        return services;
    }
}
