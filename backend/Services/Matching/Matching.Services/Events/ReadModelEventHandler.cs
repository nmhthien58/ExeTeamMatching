using BuildingBlocks.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Matching.Services.Events;

/// <summary>Một handler cho mỗi sự kiện; đọc payload rồi chuyển cho <see cref="ReadModelProjector"/>.</summary>
public sealed class ReadModelEventHandler<TPayload>(ReadModelProjector projector) : IIntegrationEventHandler
{
    public Task HandleAsync(EventEnvelope envelope, CancellationToken cancellationToken) =>
        envelope.GetPayload<TPayload>() switch
        {
            StudentImported e => projector.HandleAsync(e, cancellationToken),
            StudentProfileUpdated e => projector.HandleAsync(e, cancellationToken),
            LecturerRegistered e => projector.HandleAsync(e, cancellationToken),
            LecturerProfileUpdated e => projector.HandleAsync(e, cancellationToken),
            MentorRegistered e => projector.HandleAsync(e, cancellationToken),
            TeamCreated e => projector.HandleAsync(e, cancellationToken),
            MemberJoined e => projector.HandleAsync(e, cancellationToken),
            MemberLeft e => projector.HandleAsync(e, cancellationToken),
            TeamFinalized e => projector.HandleAsync(e, cancellationToken),
            TeamNeedsReview e => projector.HandleAsync(e, cancellationToken),
            ProjectCreated e => projector.HandleAsync(e, cancellationToken),
            TeamLinkedToProject e => projector.HandleAsync(e, cancellationToken),
            TopicInterestOpened e => projector.HandleAsync(e, cancellationToken),
            TopicInterestClosed e => projector.HandleAsync(e, cancellationToken),
            LecturerAssigned e => projector.HandleAsync(e, cancellationToken),
            MentorAssigned e => projector.HandleAsync(e, cancellationToken),
            MentorUnassigned e => projector.HandleAsync(e, cancellationToken),
            _ => throw new NotSupportedException(typeof(TPayload).Name),
        };
}

public static class ReadModelEventRegistration
{
    /// <summary>Consumer exe.student, exe.team, exe.assignment dựng read model (BE-35, BE-71).</summary>
    public static IServiceCollection AddReadModelConsumers(this IServiceCollection services) => services
        .AddHandler<StudentImported>(Topics.Student)
        .AddHandler<StudentProfileUpdated>(Topics.Student)
        .AddHandler<LecturerRegistered>(Topics.Student)
        .AddHandler<LecturerProfileUpdated>(Topics.Student)
        .AddHandler<MentorRegistered>(Topics.Student)
        .AddHandler<TeamCreated>(Topics.Team)
        .AddHandler<MemberJoined>(Topics.Team)
        .AddHandler<MemberLeft>(Topics.Team)
        .AddHandler<TeamFinalized>(Topics.Team)
        .AddHandler<TeamNeedsReview>(Topics.Team)
        .AddHandler<ProjectCreated>(Topics.Team)
        .AddHandler<TeamLinkedToProject>(Topics.Team)
        .AddHandler<TopicInterestOpened>(Topics.Assignment)
        .AddHandler<TopicInterestClosed>(Topics.Assignment)
        .AddHandler<LecturerAssigned>(Topics.Assignment)
        .AddHandler<MentorAssigned>(Topics.Assignment)
        .AddHandler<MentorUnassigned>(Topics.Assignment);

    private static IServiceCollection AddHandler<TPayload>(this IServiceCollection services, string topic) =>
        services.AddEventHandler<ReadModelEventHandler<TPayload>>(topic, typeof(TPayload).Name);
}
