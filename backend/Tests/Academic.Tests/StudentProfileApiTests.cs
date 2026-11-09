using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Academic.Repositories.Imports;
using Academic.Repositories.Students;
using Academic.Services.Students;
using BuildingBlocks.Auth;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Academic.Tests;

/// <summary>
/// Hồ sơ của tôi (BE-19) với academic_db và Redis thật. Bật bằng <c>EXE_INTEGRATION=1</c>;
/// container <c>academic-api</c> phải tắt (xem <see cref="SemestersRoundsApiTests"/>). Dọn sinh viên, kỹ năng, outbox thử khi xong.
/// Chạy cùng nhóm với test học kỳ vì có test tạm đổi học kỳ hiện hành.
/// </summary>
[Collection(RoundTestsCollection.Name)]
public sealed class StudentProfileApiTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly AcademicApiFactory _api = new();
    private readonly List<Guid> _students = [];

    private static object Skills(int level) => new[] { "TECHNOLOGY", "UI_UX_DESIGN", "MANAGEMENT", "MARKETING", "COMMUNICATION_PLANNING" }
        .Select((skill, i) => new { skill, level = i == 0 ? level : 2 }).ToArray();

    [Fact]
    public async Task Student_completes_profile_with_five_skills_and_publishes_event()
    {
        if (!Enabled)
        {
            return;
        }

        var student = await CreateStudentAsync();
        var me = Me(student.Id);

        var before = (await me.GetFromJsonAsync<StudentProfileResponse>("/api/students/me"))!;
        Assert.Equal((student.StudentCode, "0901234567", false, 0, true), (before.StudentCode, before.Phone, before.ProfileCompleted, before.Skills.Count, before.ContactEmailVisible));
        Assert.False(string.IsNullOrEmpty(before.Specialization.Code));

        var incomplete = await me.PutAsJsonAsync("/api/students/me", new { skills = new[] { new { skill = "TECHNOLOGY", level = 3 } } });
        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        Assert.Contains("skills-incomplete", await incomplete.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await me.PutAsJsonAsync("/api/students/me", new { skills = Skills(9) })).StatusCode);

        var saved = await me.PutAsJsonAsync("/api/students/me", new { skills = Skills(4), description = "  Thích làm backend  ", isSeekingTeam = true });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var profile = (await saved.Content.ReadFromJsonAsync<StudentProfileResponse>())!;
        Assert.Equal((true, true, "Thích làm backend", 5, 4), (profile.ProfileCompleted, profile.IsSeekingTeam, profile.Description, profile.Skills.Count, profile.Skills[0].Level));
        Assert.Equal("TECHNOLOGY", profile.Skills[0].Skill);

        // Sửa lần 2: đổi mức, bỏ mô tả, isSeekingTeam bỏ trống thì giữ nguyên.
        var again = (await (await me.PutAsJsonAsync("/api/students/me", new { skills = Skills(1) })).Content.ReadFromJsonAsync<StudentProfileResponse>())!;
        Assert.Equal((1, (string?)null, true, true), (again.Skills[0].Level, again.Description, again.IsSeekingTeam, again.ProfileCompleted));
        var stored = await _api.QueryDbAsync(db => db.StudentSkills.Where(s => s.StudentId == student.Id).ToListAsync());
        Assert.Equal(5, stored.Count);

        var events = await _api.QueryDbAsync(db => db.Set<OutboxMessage>().Where(m => m.AggregateId == student.Id).OrderBy(m => m.OccurredAt).ToListAsync());
        Assert.Equal(2, events.Count);
        var payload = JsonDocument.Parse(events[0].Payload).RootElement;
        Assert.Equal(("StudentProfileUpdated", "exe.student", 5, true, true),
            (events[0].EventType, events[0].Topic, payload.GetProperty("skills").GetArrayLength(), payload.GetProperty("isSeekingTeam").GetBoolean(), payload.GetProperty("profileCompleted").GetBoolean()));
    }

    [Fact]
    public async Task Student_in_team_cannot_turn_on_seeking_flag()
    {
        if (!Enabled)
        {
            return;
        }

        var student = await CreateStudentAsync();
        var me = Me(student.Id);
        _api.Team.StudentsInTeam.Add(student.Id);

        var rejected = await me.PutAsJsonAsync("/api/students/me", new { skills = Skills(3), isSeekingTeam = true });
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Contains("already-in-team", await rejected.Content.ReadAsStringAsync());

        var saved = (await (await me.PutAsJsonAsync("/api/students/me", new { skills = Skills(3) })).Content.ReadFromJsonAsync<StudentProfileResponse>())!;
        Assert.Equal((true, false), (saved.HasTeam, saved.IsSeekingTeam));
    }

    [Fact]
    public async Task Profile_is_found_by_email_when_token_has_no_student_id()
    {
        if (!Enabled)
        {
            return;
        }

        var student = await CreateStudentAsync();
        var byEmail = _api.ClientWith(new Dictionary<string, object> { [ClaimNames.Email] = student.Email.ToUpperInvariant() }, Roles.Student);
        Assert.Equal(student.Id, (await byEmail.GetFromJsonAsync<StudentProfileResponse>("/api/students/me"))!.Id);

        Assert.Equal(HttpStatusCode.NotFound, (await Me(Guid.NewGuid()).GetAsync("/api/students/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.ClientAs(Roles.Admin).GetAsync("/api/students/me")).StatusCode);
    }

    [Fact]
    public async Task Enrolled_student_sees_class_and_course_of_current_semester()
    {
        if (!Enabled)
        {
            return;
        }

        var current = await _api.QueryDbAsync(db => db.Semesters.Where(s => s.IsCurrent).Select(s => (Guid?)s.Id).SingleOrDefaultAsync());
        if (current is null)
        {
            return; // DB chưa có học kỳ hiện hành
        }
        var student = await CreateStudentAsync(current);
        var classCode = await _api.QueryDbAsync(db => db.ClassCodes.OrderBy(c => c.Id).Select(c => c.Code).FirstAsync());
        var profile = (await Me(student.Id).GetFromJsonAsync<StudentProfileResponse>("/api/students/me"))!;
        Assert.Equal((classCode, "EXE201"), (profile.ClassCode, profile.CourseCode));
    }

    private HttpClient Me(Guid studentId) =>
        _api.ClientWith(new Dictionary<string, object> { [ClaimNames.StudentId] = studentId.ToString() }, Roles.Student);

    private async Task<Student> CreateStudentAsync(Guid? semesterId = null)
    {
        var code = $"ZW{Random.Shared.Next(0, 1_000_000):D6}";
        var student = await _api.QueryDbAsync(async db =>
        {
            var s = new Student
            {
                Id = Guid.NewGuid(), StudentCode = code, FullName = "Sinh Viên Hồ Sơ", Email = $"{code.ToLowerInvariant()}@example.test",
                Phone = "0901234567", SpecializationId = (await db.Specializations.OrderBy(x => x.Id).FirstAsync()).Id,
            };
            db.Students.Add(s);
            if (semesterId is { } id)
            {
                db.SemesterEnrollments.Add(new SemesterEnrollment { SemesterId = id, StudentId = s.Id, CourseCode = "EXE201", ClassCodeId = (await db.ClassCodes.OrderBy(c => c.Id).FirstAsync()).Id });
            }
            await db.SaveChangesAsync();
            return s;
        });
        _students.Add(student.Id);
        return student;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (Enabled)
        {
            await _api.QueryDbAsync(async db =>
            {
                await db.Set<OutboxMessage>().Where(m => _students.Contains(m.AggregateId)).ExecuteDeleteAsync();
                await db.SemesterEnrollments.Where(e => _students.Contains(e.StudentId)).ExecuteDeleteAsync();
                return await db.Students.Where(s => _students.Contains(s.Id)).ExecuteDeleteAsync(); // student_skills xóa theo ON DELETE CASCADE
            });
        }
        await _api.DisposeAsync();
    }
}
