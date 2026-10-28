using Microsoft.Extensions.DependencyInjection;
using Team.Repositories.ChangeRequests;

namespace Team.Services.ChangeRequests;

public static class ChangeRequestModule
{
    public static IServiceCollection AddChangeRequestsModule(this IServiceCollection services)
    {
        services.AddScoped<IChangeRequestRepository, ChangeRequestRepository>();
        services.AddScoped<IChangeRequestService, ChangeRequestService>();
        return services;
    }
}
