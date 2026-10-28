using BuildingBlocks.Messaging;
using BuildingBlocks.Observability;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Team.Repositories.Context;
using Team.Services.Academic;
using Team.Services.AuditLog;
using Team.Services.AutoAssign;
using Team.Services.Finalization;
using Team.Services.Invitations;
using Team.Services.JoinRequests;
using Team.Services.Matching;
using Team.Services.Posts;
using Team.Services.Members;
using Team.Services.Teams;

namespace Team.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Khung Team Service (BE-24): DbContext <c>team_db</c>, Kafka + Outbox, client gọi Academic.
    /// Mỗi module (Teams, Members, Invitations, JoinRequests, Posts, AuditLog, Finalization, ChangeRequests…) tự đăng ký service của mình bên dưới.
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

        // Module: Teams, Members, Invitations, JoinRequests, Posts, AuditLog (Cường); Finalization, AutoAssign… (Nguyên); ChangeRequests… (Thành).
        services.AddTeamsModule().AddMembersModule().AddInvitationsModule().AddJoinRequestsModule().AddPostsModule()
            .AddAuditLogModule();
        services.AddMatchingClient(configuration).AddFinalizationModule().AddAutoAssignModule();
        return services;
    }
}
