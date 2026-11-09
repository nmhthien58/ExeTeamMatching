namespace Team.Services.BaselineImport;

public sealed record BaselineImportRowError(int Row, string Message);

public sealed record BaselineImportResponse(
    int TeamCount,
    int StudentCount,
    int LinkedProjectCount,
    int UnlinkedTeamCount,
    IReadOnlyList<BaselineImportRowError> Errors,
    int NeedsReviewTeamCount = 0,
    int SkippedTeamCount = 0,
    int FreeStudentCount = 0,
    IReadOnlyList<InheritedTeamSummary>? Teams = null,
    IReadOnlyList<BaselineImportRowError>? Warnings = null);

public sealed record ParsedBaselineRecord(
    int RowNumber,
    string TeamName,
    string? ProjectName,
    string? IdeaDescription,
    IReadOnlyList<string> DomainNames,
    string StudentCode,
    bool IsLeader);

public sealed record AutoInheritRequest(
    Guid SemesterId,
    bool DryRun = false,
    IReadOnlyList<string>? StudentCodes = null);

public sealed record InheritedTeamMemberSummary(
    Guid StudentId,
    string StudentCode,
    string FullName,
    int? SpecializationId,
    bool IsLeader);

public sealed record InheritedTeamSummary(
    Guid? TeamId,
    string TeamName,
    Guid? OldTeamId,
    Guid? ProjectId,
    string? ProjectName,
    string LeaderStudentCode,
    string LeaderName,
    int MemberCount,
    IReadOnlyList<InheritedTeamMemberSummary> Members,
    IReadOnlyList<string> MissingOldMemberStudentCodes,
    string Status,
    IReadOnlyList<string>? DomainCodes = null);

public sealed record AutoInheritResponse(
    int InheritedTeamCount,
    int InheritedStudentCount,
    int FreeStudentCount,
    int NeedsReviewTeamCount,
    IReadOnlyList<InheritedTeamSummary> Teams);
