namespace Team.Services.BaselineImport;

public interface IBaselineImportService
{
    Task<BaselineImportResponse> ImportAsync(Guid semesterId, Stream fileStream, string fileName, bool dryRun, CancellationToken ct);

    Task<AutoInheritResponse> AutoInheritAsync(AutoInheritRequest request, CancellationToken ct);
}
