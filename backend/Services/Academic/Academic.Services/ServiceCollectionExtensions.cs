using Academic.Repositories.Context;
using Academic.Services.Caching;
using Academic.Services.Catalogs;
using Academic.Services.Imports;
using Academic.Services.Lecturers;
using Academic.Services.Semesters;
using Academic.Services.Settings;
using Academic.Services.Students;
using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Academic.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Khung Academic Service (BE-15): DbContext <c>academic_db</c>, Kafka + Outbox, cache Redis (BE-21).
    /// Mỗi module (Semesters, Rounds, Imports, Students, Lecturers, Mentors, Catalogs, Settings…) tự đăng ký service của mình bên dưới.
    /// </summary>
    public static IServiceCollection AddAcademicServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AcademicDbContext>(options => options
            .UseNpgsql(configuration.GetConnectionString("AcademicDb"))
            .UseSnakeCaseNamingConvention());

        services.AddKafka(configuration, "academic");
        services.AddOutbox<AcademicDbContext>(configuration);

        services.AddAcademicCache(configuration);
        services.TryAddSingleton(TimeProvider.System);

        // Module: Catalogs (BE-21), Semesters + Rounds (BE-16), Students – tra cứu nội bộ (BE-22), Imports (BE-17), Lecturers (BE-20), Settings (BE-23); Mentors (các task sau).
        services.AddCatalogsModule().AddSemestersModule().AddStudentsModule().AddImportsModule().AddLecturersModule().AddSettingsModule();
        return services;
    }

    /// <summary>
    /// Redis theo <c>ConnectionStrings:Redis</c>, key có tiền tố <c>academic:</c>. Không cấu hình Redis thì dùng cache trong bộ nhớ.
    /// Redis chưa chạy không chặn service khởi động (<c>AbortOnConnectFail = false</c>); <see cref="AcademicCache"/> đọc thẳng DB.
    /// </summary>
    private static IServiceCollection AddAcademicCache(this IServiceCollection services, IConfiguration configuration)
    {
        var redis = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redis))
        {
            services.AddDistributedMemoryCache();
        }
        else
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.InstanceName = "academic:";
                options.ConfigurationOptions = ConfigurationOptions.Parse(redis);
                options.ConfigurationOptions.AbortOnConnectFail = false;
                options.ConfigurationOptions.ConnectTimeout = 2000;
                options.ConfigurationOptions.SyncTimeout = 2000;
            });
        }
        services.AddSingleton<AcademicCache>();
        return services;
    }
}
