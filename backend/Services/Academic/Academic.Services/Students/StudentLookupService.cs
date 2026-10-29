using Academic.Repositories.Students;
using Microsoft.Extensions.DependencyInjection;

namespace Academic.Services.Students;

public interface IStudentLookupService
{
    Task<IReadOnlyList<StudentSnapshot>> LookupAsync(StudentLookupRequest request, CancellationToken ct);

    Task<IReadOnlyList<StudentSnapshot>> LookupByCodeAsync(StudentByCodeRequest request, CancellationToken ct);

    /// <summary>Toàn bộ sinh viên của học kỳ (Team tính sinh viên chưa có nhóm cho W20, W21).</summary>
    Task<IReadOnlyList<StudentSnapshot>> ListSemesterAsync(SemesterStudentsQuery query, CancellationToken ct);
}

/// <summary>
/// "Sinh viên có thuộc học kỳ không?" cho Team Service (BE-22, rule 5). Đọc thẳng DB bằng một câu SQL có index,
/// không cache: danh sách học kỳ đổi theo mỗi lần import (BE-17) nên cache dễ trả dữ liệu cũ đúng lúc vừa import.
/// </summary>
public sealed class StudentLookupService(IStudentRepository students) : IStudentLookupService
{
    public async Task<IReadOnlyList<StudentSnapshot>> LookupAsync(StudentLookupRequest request, CancellationToken ct)
    {
        var ids = request.StudentIds!.Distinct().ToList();
        return ids.Count == 0 ? [] : ToSnapshots(await students.LookupAsync(request.SemesterId!.Value, ids, ct));
    }

    public async Task<IReadOnlyList<StudentSnapshot>> LookupByCodeAsync(StudentByCodeRequest request, CancellationToken ct)
    {
        var codes = request.StudentCodes!.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim().ToUpperInvariant()).Distinct().ToList();
        return codes.Count == 0 ? [] : ToSnapshots(await students.LookupByCodeAsync(request.SemesterId, codes, ct));
    }

    public async Task<IReadOnlyList<StudentSnapshot>> ListSemesterAsync(SemesterStudentsQuery query, CancellationToken ct) =>
        ToSnapshots(await students.ListEnrolledAsync(query.SemesterId!.Value, query.CourseCode, ct));

    private static List<StudentSnapshot> ToSnapshots(IEnumerable<StudentSnapshotRow> rows) =>
        rows.Select(r => new StudentSnapshot(r.Id, r.StudentCode, r.FullName, r.SpecializationId, r.ClassCode, r.CourseCode, r.CourseCode is not null)).ToList();
}

public static class StudentsModule
{
    /// <summary>Module Students: tra cứu nội bộ (BE-22); danh sách, chi tiết, hồ sơ (BE-18, BE-19) thêm sau.</summary>
    public static IServiceCollection AddStudentsModule(this IServiceCollection services)
    {
        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<IStudentLookupService, StudentLookupService>();
        return services;
    }
}
