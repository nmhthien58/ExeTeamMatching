using Academic.Repositories.Context;
using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Academic.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Khung Academic Service (BE-15): DbContext <c>academic_db</c>, Kafka + Outbox.
    /// Mỗi module (Semesters, Rounds, Imports, Students, Lecturers, Mentors, Catalogs, Settings…) tự đăng ký service của mình bên dưới.
    /// </summary>
    public static IServiceCollection AddAcademicServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AcademicDbContext>(options => options
            .UseNpgsql(configuration.GetConnectionString("AcademicDb"))
            .UseSnakeCaseNamingConvention());

        services.AddKafka(configuration, "academic");
        services.AddOutbox<AcademicDbContext>(configuration);
        return services;
    }
}
