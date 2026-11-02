using System.Security.Claims;
using Academic.Repositories.Students;
using Academic.Services.Lecturers;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;

namespace Academic.Services.Students;

public interface IStudentProfileService
{
    Task<StudentProfileResponse> GetMeAsync(ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Hồ sơ sinh viên bất kỳ (W10, S04). SĐT, email chỉ trả cho Admin và chính sinh viên đó.</summary>
    Task<StudentProfileResponse> GetAsync(Guid id, ClaimsPrincipal user, CancellationToken ct);

    Task<StudentProfileResponse> UpdateMeAsync(StudentProfileInput input, ClaimsPrincipal user, CancellationToken ct);
}

/// <summary>
/// Hồ sơ của sinh viên (BE-19, BE-67; A05, S02, S03; rule 11, 16). Hồ sơ = 5 kỹ năng 1–5 + mô tả ≤ 500 + công tắc tìm nhóm;
/// dữ liệu import chỉ đọc. Lưu đủ 5 kỹ năng lần đầu thì hoàn tất onboarding (<c>profile_completed_at</c>).
/// Mỗi lần sửa phát <c>StudentProfileUpdated</c> qua outbox (Matching cập nhật read model).
/// </summary>
public sealed class StudentProfileService(IStudentProfileRepository students, TimeProvider clock) : IStudentProfileService
{
    public async Task<StudentProfileResponse> GetMeAsync(ClaimsPrincipal user, CancellationToken ct) =>
        await ToResponseAsync(await FindMeAsync(user, ct), showContact: true, ct);

    /// <remarks>
    /// Rule 11 cho thêm đồng đội, giảng viên, mentor của nhóm xem liên lạc, nhưng thông tin nhóm thuộc Team Service
    /// và Team chưa có API này, nên tạm thời chỉ Admin và chính sinh viên đó (xem BE-18).
    /// </remarks>
    public async Task<StudentProfileResponse> GetAsync(Guid id, ClaimsPrincipal user, CancellationToken ct)
    {
        var student = await students.FindAsync(id, ct) ?? throw new NotFoundException("sinh viên", id);
        return await ToResponseAsync(student, user.IsInRole(Roles.Admin) || user.GetStudentId() == id, ct);
    }

    public async Task<StudentProfileResponse> UpdateMeAsync(StudentProfileInput input, ClaimsPrincipal user, CancellationToken ct)
    {
        var ratings = StudentProfileRules.CompleteSkills(input.Skills!);
        var student = await FindMeAsync(user, ct);

        var saved = (await students.ListSkillsAsync(student.Id, ct)).ToDictionary(s => s.Skill);
        foreach (var rating in ratings)
        {
            if (saved.TryGetValue(rating.Skill, out var skill))
            {
                skill.Level = (short)rating.Level;
            }
            else
            {
                students.AddSkill(new StudentSkill { StudentId = student.Id, Skill = rating.Skill, Level = (short)rating.Level });
            }
        }
        student.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        student.IsSeekingTeam = input.IsSeekingTeam ?? student.IsSeekingTeam;
        student.ProfileCompletedAt ??= clock.GetUtcNow();

        students.AddEvent(StudentProfileEventTypes.StudentProfileUpdated, student.Id, new StudentProfileUpdatedEvent(
            student.Id, student.FullName, student.Email, student.SpecializationId, ratings, student.IsSeekingTeam, ProfileCompleted: true));
        await students.SaveAsync(ct);
        return await ToResponseAsync(student, showContact: true, ct, ratings);
    }

    /// <summary>Theo claim <c>student_id</c>; token chưa có claim thì theo email. Không có hồ sơ → 404.</summary>
    private async Task<Student> FindMeAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var student = user.GetStudentId() is { } id
            ? await students.FindAsync(id, ct)
            : user.GetEmail() is { } email ? await students.FindByEmailAsync(email, ct) : null;
        return student ?? throw new NotFoundException("hồ sơ sinh viên của tài khoản này");
    }

    private async Task<StudentProfileResponse> ToResponseAsync(Student s, bool showContact, CancellationToken ct, IReadOnlyList<SkillRating>? ratings = null)
    {
        var specialization = await students.FindSpecializationAsync(s.SpecializationId, ct);
        var enrollment = await students.FindCurrentEnrollmentAsync(s.Id, ct);
        ratings ??= StudentProfileRules.Ordered((await students.ListSkillsAsync(s.Id, ct)).Select(k => new SkillRating(k.Skill, k.Level)));
        return new StudentProfileResponse(
            s.Id, s.StudentCode, s.FullName, showContact ? s.Email : string.Empty,
            new CatalogRef(s.SpecializationId, specialization?.Code ?? string.Empty, specialization?.Name ?? string.Empty),
            enrollment?.ClassCode, enrollment?.CourseCode, HasTeam: false, s.IsSeekingTeam, showContact ? s.Phone : null, ContactEmailVisible: showContact,
            ratings, s.Description, s.ProfileCompletedAt is not null, CurrentTeamId: null);
    }
}

public static class StudentProfileRules
{
    /// <summary>
    /// Đủ 5 kỹ năng, mỗi kỹ năng một lần (rule 16); thiếu hoặc trùng → 400 <c>skills-incomplete</c>.
    /// Kỹ năng và mức đã qua <see cref="StudentProfileInputValidator"/>. Trả theo thứ tự <see cref="SkillNames.All"/>.
    /// </summary>
    public static IReadOnlyList<SkillRating> CompleteSkills(IReadOnlyList<SkillRating> ratings)
    {
        if (ratings.Count != SkillNames.All.Count || ratings.Select(r => r.Skill).Distinct().Count() != SkillNames.All.Count)
        {
            throw new HttpStatusException(400, "skills-incomplete", "Chưa đánh giá đủ 5 kỹ năng",
                "Đánh giá mỗi kỹ năng một lần: Công nghệ, Thiết kế UI/UX, Quản trị, Marketing, Plan truyền thông.");
        }
        return Ordered(ratings);
    }

    public static IReadOnlyList<SkillRating> Ordered(IEnumerable<SkillRating> ratings) =>
        ratings.OrderBy(r => SkillNames.All.ToList().IndexOf(r.Skill)).ToList();
}
