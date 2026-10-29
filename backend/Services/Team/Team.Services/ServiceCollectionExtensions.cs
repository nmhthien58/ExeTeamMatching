using BuildingBlocks.Messaging;
using BuildingBlocks.Observability;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Team.Repositories.Context;
using Team.Services.Academic;
using Team.Services.AdvisorAssignments;
using Team.Services.AuditLog;
using Team.Services.AutoAssign;
using Team.Services.BaselineImport;
using Team.Services.ChangeRequests;
using Team.Services.Export;
using Team.Services.Finalization;
using Team.Services.Invitations;
using Team.Services.JoinRequests;
using Team.Services.LecturerPreferences;
using Team.Services.Matching;
using Team.Services.Members;
using Team.Services.Posts;
using Team.Services.Projects;
using Team.Services.Statistics;
using Team.Services.Suggestions;
using Team.Services.Teams;

namespace Team.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Khung Team Service (BE-24): DbContext <c>team_db</c>, Kafka + Outbox, client gọi Academic.
    /// Mỗi module (Teams, Members, Invitations, JoinRequests, Posts, AuditLog, Finalization, ChangeRequests, BaselineImport, AdvisorAssignments, LecturerPreferences…) tự đăng ký service của mình bên dưới.
    /// </summary>
    public static IServiceCollection AddTeamServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TeamDbContext>(options => options
            .UseNpgsql(configuration.GetConnectionString("TeamDb"))
            .UseSnakeCaseNamingConvention());

        services.AddKafka(configuration, "team");
        services.AddOutbox<TeamDbContext>(configuration);

        services.AddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        services.AddSingleton<ServiceTokenProvider>();
        services.AddTransient<ServiceTokenHandler>();
        services.AddHttpClient<IAcademicClient, AcademicClient>(http =>
                http.BaseAddress = new Uri(configuration["Academic:BaseUrl"] ?? "http://localhost:5002"))
            .AddHttpMessageHandler<CorrelationIdHandler>()
            .AddHttpMessageHandler<ServiceTokenHandler>();

        // Module: Teams, Members, Invitations, JoinRequests, Posts, AuditLog (Cường); Finalization, AutoAssign… (Nguyên); ChangeRequests, BaselineImport, AdvisorAssignments, LecturerPreferences… (Thành).
        services.AddTeamsModule().AddMembersModule().AddInvitationsModule().AddJoinRequestsModule().AddPostsModule()
            .AddAuditLogModule().AddChangeRequestsModule().AddBaselineImportModule()
            .AddAdvisorAssignmentsModule().AddLecturerPreferencesModule();
        services.AddProjectsModule(configuration);
        services.AddMatchingClient(configuration).AddFinalizationModule().AddAutoAssignModule()
            .AddStatisticsModule().AddExportModule().AddSuggestionsModule();
        return services;
    }
}
