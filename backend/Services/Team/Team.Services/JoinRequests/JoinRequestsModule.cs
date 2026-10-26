using Microsoft.Extensions.DependencyInjection;
using Team.Repositories.JoinRequests;

namespace Team.Services.JoinRequests;

public static class JoinRequestsModule
{
    /// <summary>Module JoinRequests (BE-29): xin vào nhóm kèm ý tưởng, trưởng nhóm duyệt. Cần Members.</summary>
    public static IServiceCollection AddJoinRequestsModule(this IServiceCollection services)
    {
        services.AddScoped<IJoinRequestRepository, JoinRequestRepository>();
        services.AddScoped<IJoinRequestService, JoinRequestService>();
        return services;
    }
}
