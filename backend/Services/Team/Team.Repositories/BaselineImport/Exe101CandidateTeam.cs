namespace Team.Repositories.BaselineImport;

public sealed record Exe101CandidateTeam(
    Guid Id,
    string Name,
    Guid SemesterId,
    Guid? ProjectId,
    IReadOnlyList<string> MemberStudentCodes);
