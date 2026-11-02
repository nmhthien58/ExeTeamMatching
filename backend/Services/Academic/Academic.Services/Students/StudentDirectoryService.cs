using System.Security.Claims;
using Academic.Repositories.Semesters;
using Academic.Repositories.Students;
using Academic.Services.Lecturers;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;

namespace Academic.Services.Students;

public interface IStudentDirectoryService
{
    /// <summary>Danh sách sinh viên của học kỳ (mặc định học kỳ hiện hành); 404 khi chưa có học kỳ hiện hành.</summary>
    Task<PagedResponse<StudentSummaryResponse>> ListAsync(StudentQuery query, ClaimsPrincipal user, CancellationToken ct);
}

/// <summary>
/// Danh sách sinh viên (BE-18, W09, S19). Sinh viên, giảng viên, mentor không nhận email (rule 11).
/// Cờ có nhóm thuộc Team Service: khi Team có API trạng thái nhóm thì điền <c>hasTeam</c> và lọc theo nó tại đây.
/// </summary>
public sealed class StudentDirectoryService(IStudentRepository students, ISemesterRepository semesters) : IStudentDirectoryService
{
    public async Task<PagedResponse<StudentSummaryResponse>> ListAsync(StudentQuery query, ClaimsPrincipal user, CancellationToken ct)
    {
        var semesterId = query.SemesterId
            ?? (await semesters.GetCurrentAsync(ct))?.Id
            ?? throw new NotFoundException("học kỳ hiện hành");
        var (items, total) = await students.ListDirectoryAsync(new StudentDirectoryFilter(
            semesterId, query.CourseCode, query.Search, query.ClassCode, query.SpecializationId, query.Sort, query.Page, query.PageSize), ct);

        var showEmail = user.IsInRole(Roles.Admin);
        return PagedResponse<StudentSummaryResponse>.Create(
            items.Select(r => new StudentSummaryResponse(
                r.Id, r.StudentCode, r.FullName, showEmail ? r.Email : string.Empty,
                new CatalogRef(r.SpecializationId, r.SpecializationCode, r.SpecializationName),
                r.ClassCode, r.CourseCode, HasTeam: false, r.IsSeekingTeam)).ToList(),
            query.Page, query.PageSize, total);
    }
}
