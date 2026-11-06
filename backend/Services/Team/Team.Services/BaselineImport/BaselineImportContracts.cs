namespace Team.Services.BaselineImport;

public sealed record BaselineImportRowError(int Row, string Message);

public sealed record BaselineImportResponse(
    int TeamCount,
    int StudentCount,
    int LinkedProjectCount,
    int UnlinkedTeamCount,
    IReadOnlyList<BaselineImportRowError> Errors);

public sealed record ParsedBaselineRecord(
    int RowNumber,
    string TeamName,
    string? ProjectName,
    string StudentCode,
    bool IsLeader);
