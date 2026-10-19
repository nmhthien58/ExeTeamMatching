using BuildingBlocks.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Collab.Services.Workspaces;

/// <summary>Một handler cho mỗi sự kiện dựng quyền workspace; đọc payload rồi chuyển cho <see cref="WorkspaceMembershipService"/>.</summary>
public sealed class MembershipEventHandler<TPayload>(WorkspaceMembershipService service) : IIntegrationEventHandler
{
    public Task HandleAsync(EventEnvelope envelope, CancellationToken cancellationToken) =>
        envelope.GetPayload<TPayload>() switch
        {
            TeamCreated e => service.HandleAsync(e, cancellationToken),
            MemberJoined e => service.HandleAsync(e, cancellationToken),
            MemberLeft e => service.HandleAsync(e, cancellationToken),
            LeaderChanged e => service.HandleAsync(e, cancellationToken),
            TeamLinkedToProject e => service.HandleAsync(e, cancellationToken),
            LecturerAssigned e => service.HandleAsync(e, cancellationToken),
            MentorAssigned e => service.HandleAsync(e, cancellationToken),
            MentorUnassigned e => service.HandleAsync(e, cancellationToken),
            _ => throw new NotSupportedException(typeof(TPayload).Name),
        };
}

public static class MembershipEventRegistration
{
    /// <summary>Consumer exe.team và exe.assignment (BE-62).</summary>
    public static IServiceCollection AddWorkspaceMembershipConsumers(this IServiceCollection services) => services
        .AddEventHandler<MembershipEventHandler<TeamCreated>>(Topics.Team, nameof(TeamCreated))
        .AddEventHandler<MembershipEventHandler<MemberJoined>>(Topics.Team, nameof(MemberJoined))
        .AddEventHandler<MembershipEventHandler<MemberLeft>>(Topics.Team, nameof(MemberLeft))
        .AddEventHandler<MembershipEventHandler<LeaderChanged>>(Topics.Team, nameof(LeaderChanged))
        .AddEventHandler<MembershipEventHandler<TeamLinkedToProject>>(Topics.Team, nameof(TeamLinkedToProject))
        .AddEventHandler<MembershipEventHandler<LecturerAssigned>>(Topics.Assignment, nameof(LecturerAssigned))
        .AddEventHandler<MembershipEventHandler<MentorAssigned>>(Topics.Assignment, nameof(MentorAssigned))
        .AddEventHandler<MembershipEventHandler<MentorUnassigned>>(Topics.Assignment, nameof(MentorUnassigned));
}
