namespace Matching.Services.Events;

// Payload sự kiện (docs/contracts/events.md) – chỉ lấy các trường read model của Matching cần.

// ---------- exe.student ----------
public sealed record StudentImported(Guid StudentId, string? FullName, int? SpecializationId, Guid SemesterId, string CourseCode);

public sealed record SkillRatingPayload(string Skill, int Level);

public sealed record StudentProfileUpdated(
    Guid StudentId, string? FullName, int? SpecializationId, IReadOnlyList<SkillRatingPayload>? Skills, bool IsSeekingTeam);

/// <summary><c>IsActive</c> thiếu trong payload thì coi là đang hoạt động.</summary>
public sealed record LecturerRegistered(Guid LecturerId, IReadOnlyList<int>? DomainIds, bool? IsActive);

public sealed record LecturerProfileUpdated(Guid LecturerId, IReadOnlyList<int>? DomainIds, bool? IsActive);

/// <summary>
/// Contract hiện chưa có <c>domainIds</c> cho mentor; nếu Academic bổ sung thì read model tự nhận
/// (gợi ý đề tài cho mentor cần domain chuyên môn – rule 19).
/// </summary>
public sealed record MentorRegistered(Guid MentorId, IReadOnlyList<int>? DomainIds, bool? IsActive);

// ---------- exe.team ----------
public sealed record TeamCreated(Guid TeamId, Guid SemesterId, string CourseCode, string Name, Guid LeaderStudentId, Guid? ProjectId);

public sealed record MemberJoined(Guid TeamId, Guid StudentId);

public sealed record MemberLeft(Guid TeamId, Guid StudentId);

public sealed record TeamFinalized(Guid TeamId);

public sealed record TeamNeedsReview(Guid TeamId);

public sealed record ProjectCreated(Guid ProjectId, Guid TeamId, IReadOnlyList<int>? DomainIds);

public sealed record TeamLinkedToProject(Guid TeamId, Guid ProjectId);

// ---------- exe.assignment ----------
public sealed record TopicInterestOpened(Guid TeamId, string AdvisorKind, Guid AdvisorId);

public sealed record TopicInterestClosed(Guid TeamId, string AdvisorKind, Guid AdvisorId);

public sealed record LecturerAssigned(Guid TeamId, Guid LecturerId);

public sealed record MentorAssigned(Guid TeamId, Guid MentorId);

public sealed record MentorUnassigned(Guid TeamId, Guid MentorId);
