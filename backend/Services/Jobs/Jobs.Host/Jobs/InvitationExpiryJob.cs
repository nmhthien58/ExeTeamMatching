using Jobs.Host.Clients;

namespace Jobs.Host.Jobs;

/// <summary>
/// InvitationExpiryJob (BE-55, rule 8): hằng giờ nhờ Team đánh dấu lời mời / yêu cầu tham gia quá 72 giờ là hết hạn;
/// Team phát <c>InvitationExpired</c> qua outbox (Job không đụng database của Team).
/// </summary>
public sealed class InvitationExpiryJob(ITeamClient team, ILogger<InvitationExpiryJob> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var result = await team.ExpireOverdueInvitationsAsync(ct);
        if (result.Invitations + result.JoinRequests > 0)
        {
            logger.LogInformation("Hết hạn {Invitations} lời mời, {JoinRequests} yêu cầu tham gia", result.Invitations, result.JoinRequests);
        }
    }
}
