using Academic.Repositories.Students;
using BuildingBlocks.Messaging;
using Microsoft.Extensions.Logging;

namespace Academic.Services.Students;

/// <summary>Phần payload Academic cần từ <c>TeamCreated</c> (trưởng nhóm) và <c>MemberJoined</c> (thành viên) của topic <c>exe.team</c>.</summary>
public sealed record TeamMembershipPayload(Guid? StudentId, Guid? LeaderStudentId);

/// <summary>
/// Sinh viên vào nhóm (tự tạo, được nhận, được mời, kế thừa, auto-assign…) thì tự tắt "Tôi đang tìm nhóm" và phát
/// <c>StudentProfileUpdated</c> để Matching cập nhật. Rời nhóm không bật lại.
/// </summary>
public sealed class TeamMembershipSeekingHandler(IStudentProfileRepository students, ILogger<TeamMembershipSeekingHandler> logger)
    : IIntegrationEventHandler
{
    public async Task HandleAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        var payload = envelope.GetPayload<TeamMembershipPayload>();
        if ((payload.StudentId ?? payload.LeaderStudentId) is not { } studentId)
        {
            return;
        }
        var student = await students.FindAsync(studentId, cancellationToken);
        if (student is null || !student.IsSeekingTeam)
        {
            return;
        }

        student.IsSeekingTeam = false;
        var ratings = StudentProfileRules.Ordered((await students.ListSkillsAsync(student.Id, cancellationToken))
            .Select(k => new SkillRating(k.Skill, k.Level)));
        students.AddEvent(StudentProfileEventTypes.StudentProfileUpdated, student.Id, new StudentProfileUpdatedEvent(
            student.Id, student.FullName, student.Email, student.SpecializationId, ratings, IsSeekingTeam: false,
            ProfileCompleted: student.SpecializationId is not null, MajorId: student.MajorId));
        await students.SaveAsync(cancellationToken);
        logger.LogInformation("Sinh viên {StudentId} đã vào nhóm ({EventType}): tắt cờ tìm nhóm.", studentId, envelope.EventType);
    }
}
