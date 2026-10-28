using Academic.Repositories.Rounds;
using Academic.Repositories.Semesters;
using Academic.Services.Rounds;
using Microsoft.Extensions.DependencyInjection;

namespace Academic.Services.Semesters;

public static class SemestersModule
{
    /// <summary>Module Semesters và Rounds (BE-16): học kỳ, đợt đăng ký – W03, W04.</summary>
    public static IServiceCollection AddSemestersModule(this IServiceCollection services)
    {
        services.AddScoped<ISemesterRepository, SemesterRepository>();
        services.AddScoped<ISemesterService, SemesterService>();
        services.AddScoped<IRoundRepository, RoundRepository>();
        services.AddScoped<IRoundService, RoundService>();
        return services;
    }
}
