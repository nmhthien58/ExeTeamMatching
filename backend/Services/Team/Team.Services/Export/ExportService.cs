using BuildingBlocks.Errors;
using ClosedXML.Excel;
using FluentValidation;
using Team.Repositories.Export;
using Team.Repositories.Teams;
using Team.Services.Academic;
using Team.Services.AutoAssign;
using Team.Services.Teams;

namespace Team.Services.Export;

/// <summary>Query của <c>GET /api/teams/export</c> (W15, W22); không có <c>semesterId</c> = học kỳ hiện hành.</summary>
public sealed record TeamExportQuery
{
    public Guid? SemesterId { get; init; }
    public string? CourseCode { get; init; }
    public string? Status { get; init; }
}

public sealed record ExportFile(string FileName, byte[] Content)
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}

public sealed class TeamExportQueryValidator : AbstractValidator<TeamExportQuery>
{
    public TeamExportQueryValidator()
    {
        RuleFor(x => x.CourseCode).Must(c => c is null || CourseCodes.IsValid(c)).WithMessage(CourseCodes.Message);
        RuleFor(x => x.Status).Must(s => s is null || TeamStatuses.All.Contains(s)).WithMessage("status không hợp lệ.");
    }
}

public interface IExportService
{
    /// <summary>File xlsx danh sách nhóm: mỗi dòng một thành viên, trưởng nhóm trước.</summary>
    Task<ExportFile> ExportTeamsAsync(TeamExportQuery query, CancellationToken ct);
}

/// <summary>Module Export của Team Service (BE-50) – danh sách nhóm cuối để Bộ môn nộp / lưu.</summary>
public sealed class ExportService(IExportRepository repository, ITeamRepository teams, IAcademicClient academic, TeamReader reader, TimeProvider clock)
    : IExportService
{
    private static readonly string[] Headers =
        ["STT", "Môn", "Tên nhóm", "Trạng thái", "Đề tài", "MSSV", "Họ tên", "Chuyên ngành", "Vai trò", "Giảng viên hướng dẫn", "Mentor"];

    private static readonly Dictionary<string, string> StatusLabels = new()
    {
        [TeamStatuses.Draft] = "Nháp",
        [TeamStatuses.Active] = "Hoạt động",
        [TeamStatuses.NeedsReview] = "Cần xem lại",
        [TeamStatuses.Finalized] = "Đã chốt",
        [TeamStatuses.Disbanded] = "Đã giải tán",
    };

    public async Task<ExportFile> ExportTeamsAsync(TeamExportQuery query, CancellationToken ct)
    {
        // Không chọn học kỳ thì lấy học kỳ hiện hành (mã học kỳ dùng cho tên file).
        var semester = query.SemesterId is null
            ? await academic.GetCurrentSemesterAsync(ct) ?? throw new NotFoundException("học kỳ hiện hành")
            : null;
        var semesterId = query.SemesterId ?? semester!.Id;
        var graphs = await teams.ListGraphsAsync(await repository.ListTeamIdsAsync(semesterId, query.CourseCode, query.Status, ct), ct);
        var catalogs = await reader.LoadCatalogsAsync(ct);

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Danh sách nhóm");
        for (var c = 0; c < Headers.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = Headers[c];
        }
        var row = 2;
        for (var i = 0; i < graphs.Count; i++)
        {
            var g = graphs[i];
            foreach (var member in g.Members)
            {
                sheet.Cell(row, 1).Value = i + 1;
                sheet.Cell(row, 2).Value = g.Team.CourseCode;
                sheet.Cell(row, 3).Value = g.Team.Name;
                sheet.Cell(row, 4).Value = StatusLabels.GetValueOrDefault(g.Team.Status, g.Team.Status);
                sheet.Cell(row, 5).Value = g.Project?.Name ?? "";
                sheet.Cell(row, 6).Value = member.StudentCode;
                sheet.Cell(row, 7).Value = member.FullName;
                sheet.Cell(row, 8).Value = catalogs.Specialization(member.SpecializationId).Name ?? member.SpecializationId.ToString();
                sheet.Cell(row, 9).Value = member.Role == MemberRoles.Leader ? "Trưởng nhóm" : "Thành viên";
                sheet.Cell(row, 10).Value = g.LecturerName ?? "";
                sheet.Cell(row, 11).Value = g.MentorName ?? "";
                row++;
            }
        }

        var header = sheet.Range(1, 1, 1, Headers.Length);
        header.Style.Font.Bold = true;
        header.SetAutoFilter();
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var name = string.Join('-', new[] { "danh-sach-nhom", semester?.Code, query.CourseCode, query.Status?.ToLowerInvariant() }.Where(p => p is not null));
        return new ExportFile($"{name}-{clock.GetUtcNow():yyyyMMdd}.xlsx", stream.ToArray());
    }
}
