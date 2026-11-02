using System.Net;
using System.Net.Http.Json;
using Academic.Repositories.Catalogs;
using Academic.Repositories.Imports;
using Academic.Repositories.Students;
using Academic.Services;
using Academic.Services.Semesters;
using Academic.Services.Students;
using BuildingBlocks.Auth;
using Microsoft.EntityFrameworkCore;

namespace Academic.Tests;

/// <summary>
/// Danh sách, chi tiết sinh viên (BE-18) với academic_db và Redis thật. Bật bằng <c>EXE_INTEGRATION=1</c>.
/// Dùng học kỳ và sinh viên thử, dọn khi xong.
/// </summary>
[Collection(RoundTestsCollection.Name)]
public sealed class StudentsApiTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly AcademicApiFactory _api = new();
    private readonly List<Guid> _semesters = [];
    private readonly List<Guid> _students = [];
    private readonly string _tag = Random.Shared.Next(0, 1000).ToString("D3");
    private HttpClient Admin => _api.ClientAs(Roles.Admin);
    private HttpClient Student => _api.ClientAs(Roles.Student);

    private Task<PagedResponse<StudentSummaryResponse>> ListAsync(HttpClient client, Guid semesterId, string query = "") =>
        client.GetFromJsonAsync<PagedResponse<StudentSummaryResponse>>($"/api/students?semesterId={semesterId}{query}")!;

    [Fact]
    public async Task List_filters_sorts_and_pages_students_of_a_semester()
    {
        if (!Enabled)
        {
            return;
        }

        var semester = await CreateSemesterAsync();
        var (spec1, spec2) = await TwoSpecializationsAsync();
        var classes = await _api.QueryDbAsync(db => db.ClassCodes.OrderBy(c => c.Id).Take(2).ToListAsync());
        var binh = await CreateStudentAsync("001", "Bình An", spec1, semester.Id, "EXE101", classes[0]);
        var chi = await CreateStudentAsync("002", "Chi Lan", spec2, semester.Id, "EXE101", classes[1]);
        var an = await CreateStudentAsync("003", "An Khang", spec1, semester.Id, "EXE201", classes[0]);
        await CreateStudentAsync("004", "Không Trong Kỳ", spec1, null, null, null);

        var all = (await ListAsync(Admin, semester.Id))!;
        Assert.Equal(3, all.TotalCount);
        Assert.Equal([an.Id, binh.Id, chi.Id], all.Items.Select(s => s.Id)); // mặc định theo tên

        var first = all.Items[0];
        Assert.Equal((an.StudentCode, an.Email, classes[0].Code, "EXE201", spec1, false, false),
            (first.StudentCode, first.Email, first.ClassCode, first.CourseCode, first.Specialization.Id, first.HasTeam, first.IsSeekingTeam));

        Assert.Equal([chi.Id, binh.Id, an.Id], (await ListAsync(Admin, semester.Id, "&sort=-name"))!.Items.Select(s => s.Id));
        Assert.Equal([binh.Id, chi.Id, an.Id], (await ListAsync(Admin, semester.Id, "&sort=studentCode"))!.Items.Select(s => s.Id));
        Assert.Equal([an.Id, chi.Id, binh.Id], (await ListAsync(Admin, semester.Id, "&sort=-studentCode"))!.Items.Select(s => s.Id));

        Assert.Equal([an.Id], (await ListAsync(Admin, semester.Id, "&courseCode=EXE201"))!.Items.Select(s => s.Id));
        Assert.Equal([an.Id, binh.Id], (await ListAsync(Admin, semester.Id, $"&specializationId={spec1}"))!.Items.Select(s => s.Id));
        Assert.Equal([chi.Id], (await ListAsync(Admin, semester.Id, $"&classCode={classes[1].Code.ToLowerInvariant()}"))!.Items.Select(s => s.Id));
        Assert.Equal([binh.Id], (await ListAsync(Admin, semester.Id, "&search=bình"))!.Items.Select(s => s.Id));
        Assert.Equal([chi.Id], (await ListAsync(Admin, semester.Id, $"&search={chi.StudentCode.ToLowerInvariant()}"))!.Items.Select(s => s.Id));
        Assert.Empty((await ListAsync(Admin, semester.Id, "&search=100%25"))!.Items); // % là chữ thường, không phải ký tự đại diện

        var page2 = (await ListAsync(Admin, semester.Id, "&pageSize=2&page=2"))!;
        Assert.Equal((3, 2, 2, 1), (page2.TotalCount, page2.TotalPages, page2.Page, page2.Items.Count));
        Assert.Equal(chi.Id, page2.Items.Single().Id);
    }

    [Fact]
    public async Task Others_do_not_see_contact_but_admin_and_the_student_do()
    {
        if (!Enabled)
        {
            return;
        }

        var semester = await CreateSemesterAsync();
        var (spec, _) = await TwoSpecializationsAsync();
        var classCode = await _api.QueryDbAsync(db => db.ClassCodes.OrderBy(c => c.Id).FirstAsync());
        var student = await CreateStudentAsync("005", "Sinh Viên Riêng", spec, semester.Id, "EXE101", classCode);
        await _api.QueryDbAsync(async db =>
        {
            db.StudentSkills.Add(new StudentSkill { StudentId = student.Id, Skill = "MARKETING", Level = 2 });
            db.StudentSkills.Add(new StudentSkill { StudentId = student.Id, Skill = "TECHNOLOGY", Level = 5 });
            return await db.SaveChangesAsync();
        });

        Assert.Equal(string.Empty, (await ListAsync(Student, semester.Id))!.Items.Single().Email);

        var other = (await Student.GetFromJsonAsync<StudentProfileResponse>($"/api/students/{student.Id}"))!;
        Assert.Equal((string.Empty, (string?)null, false, student.FullName), (other.Email, other.Phone, other.ContactEmailVisible, other.FullName));
        Assert.Equal(["TECHNOLOGY", "MARKETING"], other.Skills.Select(s => s.Skill)); // đúng thứ tự cố định

        var admin = (await Admin.GetFromJsonAsync<StudentProfileResponse>($"/api/students/{student.Id}"))!;
        Assert.Equal((student.Email, "0901234567", true), (admin.Email, admin.Phone, admin.ContactEmailVisible));

        var self = _api.ClientWith(new Dictionary<string, object> { [ClaimNames.StudentId] = student.Id.ToString() }, Roles.Student);
        Assert.Equal("0901234567", (await self.GetFromJsonAsync<StudentProfileResponse>($"/api/students/{student.Id}"))!.Phone);

        Assert.Equal(HttpStatusCode.NotFound, (await Admin.GetAsync($"/api/students/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.CreateClient().GetAsync("/api/students")).StatusCode);
    }

    [Fact]
    public async Task List_defaults_to_current_semester_and_rejects_bad_query()
    {
        if (!Enabled)
        {
            return;
        }

        var current = await _api.QueryDbAsync(db => db.Semesters.Where(s => s.IsCurrent).Select(s => (Guid?)s.Id).SingleOrDefaultAsync());
        Assert.Equal(current is null ? HttpStatusCode.NotFound : HttpStatusCode.OK, (await Admin.GetAsync("/api/students")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin.GetAsync("/api/students?sort=email")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin.GetAsync("/api/students?courseCode=EXE301")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin.GetAsync("/api/students?pageSize=101")).StatusCode);
    }

    private Task<(int, int)> TwoSpecializationsAsync() => _api.QueryDbAsync(async db =>
    {
        var ids = await db.Specializations.Where(s => s.IsActive).OrderBy(s => s.Id).Select(s => s.Id).Take(2).ToListAsync();
        return (ids[0], ids[1]);
    });

    private async Task<Student> CreateStudentAsync(string suffix, string name, int specializationId, Guid? semesterId, string? course, ClassCode? classCode)
    {
        var code = $"ZV{_tag}{suffix}";
        var student = await _api.QueryDbAsync(async db =>
        {
            var s = new Student
            {
                Id = Guid.NewGuid(), StudentCode = code, FullName = name, Email = $"{code.ToLowerInvariant()}@example.test",
                Phone = "0901234567", SpecializationId = specializationId,
            };
            db.Students.Add(s);
            if (semesterId is { } id)
            {
                db.SemesterEnrollments.Add(new SemesterEnrollment { SemesterId = id, StudentId = s.Id, CourseCode = course!, ClassCodeId = classCode!.Id });
            }
            await db.SaveChangesAsync();
            return s;
        });
        _students.Add(student.Id);
        return student;
    }

    private async Task<SemesterResponse> CreateSemesterAsync()
    {
        var used = await _api.QueryDbAsync(db => db.Semesters.Select(s => s.Code).ToListAsync());
        var code = Enumerable.Range(0, 100).Select(n => $"FA{n:00}").First(c => !used.Contains(c));
        var response = await Admin.PostAsJsonAsync("/api/semesters", new { code, name = "Học kỳ thử sinh viên", startDate = "2033-09-05", endDate = "2033-12-30" });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var semester = (await response.Content.ReadFromJsonAsync<SemesterResponse>())!;
        _semesters.Add(semester.Id);
        return semester;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (Enabled)
        {
            await _api.QueryDbAsync(async db =>
            {
                await db.SemesterEnrollments.Where(e => _students.Contains(e.StudentId) || _semesters.Contains(e.SemesterId)).ExecuteDeleteAsync();
                await db.Students.Where(s => _students.Contains(s.Id)).ExecuteDeleteAsync(); // student_skills xóa theo ON DELETE CASCADE
                return await db.Semesters.Where(s => _semesters.Contains(s.Id)).ExecuteDeleteAsync();
            });
        }
        await _api.DisposeAsync();
    }
}
