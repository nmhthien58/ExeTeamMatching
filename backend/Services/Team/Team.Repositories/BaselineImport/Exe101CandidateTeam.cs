namespace Team.Repositories.BaselineImport;

public sealed record Exe101CandidateTeam(
    Guid Id,
    string Name,
    Guid SemesterId,
    Guid? ProjectId,
    IReadOnlyList<string> MemberStudentCodes);

public sealed record OldTeamMemberInfo(
    Guid TeamId,
    Guid StudentId,
    string StudentCode,
    string FullName,
    int? SpecializationId,
    string Role,
    int MajorId = 1);

public sealed record Exe101TeamWithMembers(
    Guid Id,
    string Name,
    Guid SemesterId,
    Guid? ProjectId,
    string? ProjectName,
    IReadOnlyList<OldTeamMemberInfo> Members);
