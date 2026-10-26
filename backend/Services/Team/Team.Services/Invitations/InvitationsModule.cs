using Microsoft.Extensions.DependencyInjection;
using Team.Repositories.Invitations;

namespace Team.Services.Invitations;

public static class InvitationsModule
{
    /// <summary>Module Invitations (BE-28): gửi / nhận / thu hồi lời mời, hết hạn 72 giờ. Cần Members và JoinRequests.</summary>
    public static IServiceCollection AddInvitationsModule(this IServiceCollection services)
    {
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IInvitationService, InvitationService>();
        return services;
    }
}
