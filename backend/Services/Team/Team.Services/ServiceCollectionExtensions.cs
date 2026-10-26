using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Team.Repositories.Context;

namespace Team.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Khung Team Service (BE-24): DbContext <c>team_db</c>, Kafka + Outbox.
    /// Mỗi module (Teams, Members, Invitations, JoinRequests, Posts, AuditLog, Finalization, ChangeRequests…) tự đăng ký service của mình bên dưới.
    /// </summary>
    public static IServiceCollection AddTeamServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TeamDbContext>(options => options
            .UseNpgsql(configuration.GetConnectionString("TeamDb"))
            .UseSnakeCaseNamingConvention());

        services.AddKafka(configuration, "team");
        services.AddOutbox<TeamDbContext>(configuration);
        return services;
    }
}
