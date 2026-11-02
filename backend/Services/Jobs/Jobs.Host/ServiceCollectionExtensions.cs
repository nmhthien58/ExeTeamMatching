using BuildingBlocks.Auth;
using BuildingBlocks.Messaging;
using BuildingBlocks.Observability;
using ExeTeamMatching.Protos.Matching.V1;
using Hangfire;
using Hangfire.PostgreSql;
using Jobs.Host.Auth;
using Jobs.Host.Clients;
using Jobs.Host.Configuration;
using Jobs.Host.Events;
using Jobs.Host.Jobs;

namespace Jobs.Host;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Job Service (BE-51): Hangfire lưu trên PostgreSQL (schema <c>hangfire</c> của job_db), HTTP client có token dịch vụ
    /// gọi Academic / Team / Collaboration, gRPC client gọi Matching, Kafka để phát nhắc lịch và nhận RoundOpened.
    /// </summary>
    public static IServiceCollection AddJobsHost(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(JobsOptions.SectionName);
        services.Configure<JobsOptions>(section);
        var options = section.Get<JobsOptions>() ?? new JobsOptions();

        services.AddSingleton(TimeProvider.System);
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => o.SigningKey.Length >= 32, "Jwt:SigningKey phải có ít nhất 32 ký tự (đặt qua biến môi trường Jwt__SigningKey).")
            .ValidateOnStart();
        services.AddSingleton<ServiceTokenProvider>();
        services.AddTransient<ServiceTokenHandler>();

        services.AddInternalClient<IAcademicClient, AcademicClient>(options.AcademicUrl);
        services.AddInternalClient<ITeamClient, TeamClient>(options.TeamUrl);
        services.AddInternalClient<ICollabClient, CollabClient>(options.CollabUrl);

        services.AddTransient<GrpcClientCorrelationInterceptor>();
        services.AddGrpcClient<MatchingService.MatchingServiceClient>(o => o.Address = new Uri(options.MatchingUrl))
            .AddInterceptor<GrpcClientCorrelationInterceptor>();

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                bootstrapper => bootstrapper.UseNpgsqlConnection(configuration.GetConnectionString("JobDb")),
                new PostgreSqlStorageOptions { SchemaName = "hangfire", PrepareSchemaIfNecessary = true }));
        services.AddHangfireServer();

        services.AddScoped<DeadlineReminderJob>();
        services.AddScoped<RoundAutoCloseJob>();
        services.AddScoped<AutoAssignUnmatchedJob>();
        services.AddScoped<LecturerMatchingJob>();
        services.AddScoped<MentorMatchingJob>();
        services.AddScoped<InvitationExpiryJob>();
        services.AddScoped<MeetingReminderJob>();
        services.AddScoped<SlotBookingCloseJob>();
        services.AddScoped<TaskDueReminderJob>();

        services.AddKafka(configuration, "jobs");
        if (configuration.GetValue("Kafka:ConsumersEnabled", true))
        {
            services.AddEventHandler<RoundOpenedHandler>(Topics.Registration, JobEventTypes.RoundOpened);
        }
        return services;
    }

    private static void AddInternalClient<TClient, TImplementation>(this IServiceCollection services, string baseUrl)
        where TClient : class
        where TImplementation : class, TClient =>
        services.AddHttpClient<TClient, TImplementation>(http => http.BaseAddress = new Uri(baseUrl))
            .AddHttpMessageHandler<CorrelationIdHandler>()
            .AddHttpMessageHandler<ServiceTokenHandler>();
}
