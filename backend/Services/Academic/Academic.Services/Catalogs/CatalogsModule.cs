using Academic.Repositories.Catalogs;
using Microsoft.Extensions.DependencyInjection;

namespace Academic.Services.Catalogs;

public static class CatalogsModule
{
    /// <summary>Module Catalogs (BE-21): chuyên ngành, domain, mã lớp – W12–W14.</summary>
    public static IServiceCollection AddCatalogsModule(this IServiceCollection services)
    {
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<ICatalogService, CatalogService>();
        return services;
    }
}
