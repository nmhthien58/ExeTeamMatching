using Microsoft.Extensions.DependencyInjection;
using Team.Repositories.LecturerPreferences;

namespace Team.Services.LecturerPreferences;

public static class LecturerPreferencesModule
{
    public static IServiceCollection AddLecturerPreferencesModule(this IServiceCollection services)
    {
        services.AddScoped<ILecturerPreferenceRepository, LecturerPreferenceRepository>();
        services.AddScoped<ILecturerPreferencesService, LecturerPreferencesService>();
        return services;
    }
}
