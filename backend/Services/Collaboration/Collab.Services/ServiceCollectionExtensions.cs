using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Collab.Repositories.Context;
using Collab.Repositories.Workspaces;
using Collab.Services.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Collab.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Khung Collaboration Service (BE-62): DbContext, Kafka + Outbox, consumer quyền workspace, policy WorkspaceMember.
    /// Mỗi module (Messages, Tasks, Files, Meetings, Checkpoints) tự đăng ký service của mình bên dưới.
    /// </summary>
    public static IServiceCollection AddCollabServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CollabDbContext>(options => options
            .UseNpgsql(configuration.GetConnectionString("CollabDb"))
            .UseSnakeCaseNamingConvention());

        services.AddKafka(configuration, "collab");
        services.AddOutbox<CollabDbContext>(configuration);
        services.AddProcessedEventStore<CollabDbContext>();

        services.AddHttpContextAccessor();
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IWorkspaceAccessService, WorkspaceAccessService>();
        services.AddScoped<WorkspaceMembershipService>();
        services.AddScoped<IAuthorizationHandler, WorkspaceMemberHandler>();
        services.AddAuthorizationBuilder()
            .AddPolicy(CollabPolicies.WorkspaceMember, p => p.RequireAuthenticatedUser().AddRequirements(new WorkspaceMemberRequirement()));

        if (configuration.GetValue("Kafka:ConsumersEnabled", true))
        {
            services.AddWorkspaceMembershipConsumers();
        }

        // Module: Messages (Cường), Tasks + Files (Nguyên), Meetings (Nam), Checkpoints (Thành) – đăng ký ở đây.
        return services;
    }
}
