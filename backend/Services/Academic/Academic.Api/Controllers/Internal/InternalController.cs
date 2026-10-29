using Academic.Repositories.Rounds;
using Academic.Services.Rounds;
using Academic.Services.Students;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academic.Api.Controllers.Internal;

/// <summary>
/// Endpoint nội bộ cho Team và Job (BE-22) – academic.yaml tag Internal. Chỉ token dịch vụ (<c>roles = Service</c>);
/// Gateway không mở route <c>/internal/*</c>.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.Internal)]
[Route("internal")]
public sealed class InternalController(IRoundService rounds, IStudentLookupService students) : ControllerBase
{
    /// <summary>Đợt đang mở cho môn và loại đợt (rule 6); 404 khi không có.</summary>
    [HttpGet("registration-rounds/open")]
    public Task<RoundResponse> OpenRound([FromQuery] string? courseCode, [FromQuery] string? roundType, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (courseCode is not (CourseCodes.Exe101 or CourseCodes.Exe201))
        {
            errors["courseCode"] = ["Môn là EXE101 hoặc EXE201."];
        }
        if (roundType is not (RoundTypes.TeamFormation or RoundTypes.ChangeRequest))
        {
            errors["roundType"] = ["Loại đợt là TEAM_FORMATION hoặc CHANGE_REQUEST."];
        }
        return errors.Count > 0 ? throw new RequestValidationException(errors) : rounds.GetOpenAsync(courseCode!, roundType!, ct);
    }

    /// <summary>RoundAutoCloseJob khóa đợt khi tới hạn; khóa lại đợt đã khóa trả nguyên trạng.</summary>
    [HttpPost("registration-rounds/{id:guid}/close")]
    public Task<RoundResponse> CloseRound(Guid id, CancellationToken ct) => rounds.CloseAsync(id, ct);

    /// <summary>Snapshot sinh viên và cờ có trong danh sách học kỳ (rule 5).</summary>
    [HttpPost("students/lookup")]
    public Task<IReadOnlyList<StudentSnapshot>> LookupStudents(StudentLookupRequest request, CancellationToken ct) =>
        students.LookupAsync(request, ct);

    /// <summary>Toàn bộ sinh viên của học kỳ, lọc theo môn nếu có; sắp theo MSSV.</summary>
    [HttpGet("students")]
    public Task<IReadOnlyList<StudentSnapshot>> SemesterStudents([FromQuery] SemesterStudentsQuery query, CancellationToken ct) =>
        students.ListSemesterAsync(query, ct);

    /// <summary>Đổi MSSV sang snapshot (import mốc EXE101).</summary>
    [HttpPost("students/by-code")]
    public Task<IReadOnlyList<StudentSnapshot>> StudentsByCode(StudentByCodeRequest request, CancellationToken ct) =>
        students.LookupByCodeAsync(request, ct);
}
