using System.Net;
using System.Net.Http.Json;
using Academic.Repositories.Imports;
using Academic.Repositories.Students;
using Academic.Services.Rounds;
using Academic.Services.Semesters;
using Academic.Services.Students;
using BuildingBlocks.Auth;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Academic.Tests;

/// <summary>
/// Endpoint nội bộ cho Team và Job (BE-22) với academic_db và Redis thật. Bật bằng <c>EXE_INTEGRATION=1</c>;
/// container <c>academic-api</c> phải tắt (xem <see cref="SemestersRoundsApiTests"/>). Dọn học kỳ, đợt, sinh viên thử khi xong.
/// </summary>
[Collection(RoundTestsCollection.Name)]
public sealed class InternalApiTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly AcademicApiFactory _api = new();
    private readonly List<Guid> _semesters = [];
    private readonly List<Guid> _students = [];
    private HttpClient Admin => _api.ClientAs(Roles.Admin);
    private HttpClient Service => _api.ClientAs(Roles.Service);

    [Fact]
    public async Task Only_service_token_can_call_internal_endpoints()
    {
        if (!Enabled)
        {
            return;
        }

        const string url = "/internal/registration-rounds/open?courseCode=EXE101&roundType=TEAM_FORMATION";
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.CreateClient().GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Admin.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.ClientAs(Roles.Student).PostAsJsonAsync("/internal/students/lookup", new { semesterId = Guid.NewGuid(), studentIds = Array.Empty<Guid>() })).StatusCode);
    }

    [Fact]
    public async Task Team_sees_open_round_until_job_closes_it()
    {
        if (!Enabled)
        {
            return;
        }

        var (course, type) = await FreeCourseAndTypeAsync();
        var round = await OpenRoundAsync(course, type, TimeSpan.FromDays(2));
        var url = $"/internal/registration-rounds/open?courseCode={course}&roundType={type}";

        var open = await Service.GetFromJsonAsync<RoundResponse>(url);
        Assert.Equal((round.Id, "OPEN"), (open!.Id, open.Status));
        var otherType = type == "CHANGE_REQUEST" ? "TEAM_FORMATION" : "CHANGE_REQUEST";
        if (!(await OpenPairsAsync()).Contains($"{course}/{otherType}"))
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Service.GetAsync($"/internal/registration-rounds/open?courseCode={course}&roundType={otherType}")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await Service.GetAsync("/internal/registration-rounds/open?courseCode=EXE301")).StatusCode);

        var closed = await (await Service.PostAsync($"/internal/registration-rounds/{round.Id}/close", null)).Content.ReadFromJsonAsync<RoundResponse>();
        Assert.Equal("CLOSED", closed!.Status);
        Assert.Equal(HttpStatusCode.OK, (await Service.PostAsync($"/internal/registration-rounds/{round.Id}/close", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Service.GetAsync(url)).StatusCode); // cache đã xóa khi khóa
    }

    [Fact]
    public async Task Round_past_deadline_counts_as_closed_even_before_job_runs()
    {
        if (!Enabled)
        {
            return;
        }

        var (course, type) = await FreeCourseAndTypeAsync();
        var round = await OpenRoundAsync(course, type, TimeSpan.FromSeconds(3));
        var url = $"/internal/registration-rounds/open?courseCode={course}&roundType={type}";
        Assert.Equal(HttpStatusCode.OK, (await Service.GetAsync(url)).StatusCode);

        await Task.Delay(round.DeadlineAt - DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(300));
        Assert.Equal(HttpStatusCode.NotFound, (await Service.GetAsync(url)).StatusCode);
        await Service.PostAsync($"/internal/registration-rounds/{round.Id}/close", null);
    }

    [Fact]
    public async Task Lookup_tells_whether_students_are_in_the_semester()
    {
        if (!Enabled)
        {
            return;
        }

        var semester = await CreateSemesterAsync();
        var other = await CreateSemesterAsync();
        var enrolled = await CreateStudentAsync(semester.Id, "EXE201");
        var notEnrolled = await CreateStudentAsync(null, null);

        var result = await LookupAsync(semester.Id, [enrolled.Id, notEnrolled.Id, Guid.NewGuid()]);
        Assert.Equal(2, result.Count); // id không tồn tại bị bỏ
        var e = result.Single(s => s.Id == enrolled.Id);
        var classCode = await _api.QueryDbAsync(db => db.ClassCodes.OrderBy(c => c.Id).Select(c => c.Code).FirstAsync());
        Assert.Equal((enrolled.StudentCode, enrolled.FullName, enrolled.SpecializationId, classCode, "EXE201", true),
            (e.StudentCode, e.FullName, e.SpecializationId, e.ClassCode, e.CourseCode, e.IsEnrolled));
        var n = result.Single(s => s.Id == notEnrolled.Id);
        Assert.Equal(((string?)null, (string?)null, false), (n.ClassCode, n.CourseCode, n.IsEnrolled));

        Assert.False((await LookupAsync(other.Id, [enrolled.Id])).Single().IsEnrolled);
        Assert.Empty(await LookupAsync(semester.Id, []));

        var tooMany = await Service.PostAsJsonAsync("/internal/students/lookup", new { semesterId = semester.Id, studentIds = Enumerable.Range(0, 501).Select(_ => Guid.NewGuid()) });
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
    }

    [Fact]
    public async Task By_code_maps_student_codes_case_insensitively()
    {
        if (!Enabled)
        {
            return;
        }

        var semester = await CreateSemesterAsync();
        var student = await CreateStudentAsync(semester.Id, "EXE101");

        var response = await Service.PostAsJsonAsync("/internal/students/by-code",
            new { semesterId = semester.Id, studentCodes = new[] { student.StudentCode.ToLowerInvariant(), " ", "ZZ999999" } });
        var result = (await response.Content.ReadFromJsonAsync<List<StudentSnapshot>>())!;
        Assert.Equal((student.Id, "EXE101", true), (result.Single().Id, result.Single().CourseCode, result.Single().IsEnrolled));

        var noSemester = await Service.PostAsJsonAsync("/internal/students/by-code", new { studentCodes = new[] { student.StudentCode } });
        Assert.False((await noSemester.Content.ReadFromJsonAsync<List<StudentSnapshot>>())!.Single().IsEnrolled);
    }

    [Fact]
    public async Task Semester_students_lists_enrolled_students_by_course()
    {
        if (!Enabled)
        {
            return;
        }

        var semester = await CreateSemesterAsync();
        var exe101 = await CreateStudentAsync(semester.Id, "EXE101");
        var exe201 = await CreateStudentAsync(semester.Id, "EXE201");
        await CreateStudentAsync(null, null);

        var all = await Service.GetFromJsonAsync<List<StudentSnapshot>>($"/internal/students?semesterId={semester.Id}");
        Assert.Equal(new[] { exe101.StudentCode, exe201.StudentCode }.Order(), all!.Select(s => s.StudentCode));
        Assert.All(all, s => Assert.True(s.IsEnrolled && s.ClassCode is not null));

        var only201 = await Service.GetFromJsonAsync<List<StudentSnapshot>>($"/internal/students?semesterId={semester.Id}&courseCode=EXE201");
        Assert.Equal((exe201.Id, "EXE201"), (only201!.Single().Id, only201.Single().CourseCode));

        Assert.Equal(HttpStatusCode.BadRequest, (await Service.GetAsync("/internal/students")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Service.GetAsync($"/internal/students?semesterId={semester.Id}&courseCode=EXE301")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Admin.GetAsync($"/internal/students?semesterId={semester.Id}")).StatusCode);
    }

    private async Task<List<StudentSnapshot>> LookupAsync(Guid semesterId, Guid[] ids)
    {
        var response = await Service.PostAsJsonAsync("/internal/students/lookup", new { semesterId, studentIds = ids });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<List<StudentSnapshot>>())!;
    }

    private async Task<SemesterResponse> CreateSemesterAsync()
    {
        var used = await _api.QueryDbAsync(db => db.Semesters.Select(s => s.Code).ToListAsync());
        var code = Enumerable.Range(0, 100).Select(n => $"SP{n:00}").First(c => !used.Contains(c));
        var response = await Admin.PostAsJsonAsync("/api/semesters", new { code, name = "Học kỳ thử", startDate = "2031-01-05", endDate = "2031-04-30" });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var semester = (await response.Content.ReadFromJsonAsync<SemesterResponse>())!;
        _semesters.Add(semester.Id);
        return semester;
    }

    /// <summary>Tạo và mở một đợt bắt đầu từ 1 giờ trước, hạn chót sau <paramref name="untilDeadline"/>.</summary>
    private async Task<RoundResponse> OpenRoundAsync(string course, string type, TimeSpan untilDeadline)
    {
        var semester = await CreateSemesterAsync();
        var now = DateTimeOffset.UtcNow;
        var created = await Admin.PostAsJsonAsync("/api/registration-rounds",
            new { semesterId = semester.Id, courseCode = course, roundType = type, name = "Đợt thử", opensAt = now.AddHours(-1), deadlineAt = now.Add(untilDeadline) });
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
        var round = (await created.Content.ReadFromJsonAsync<RoundResponse>())!;
        var opened = await Admin.PostAsync($"/api/registration-rounds/{round.Id}/open", null);
        Assert.True(opened.IsSuccessStatusCode, await opened.Content.ReadAsStringAsync());
        return (await opened.Content.ReadFromJsonAsync<RoundResponse>())!;
    }

    private async Task<Student> CreateStudentAsync(Guid? semesterId, string? course)
    {
        var code = $"ZY{Random.Shared.Next(0, 1_000_000):D6}";
        var student = await _api.QueryDbAsync(async db =>
        {
            var s = new Student { Id = Guid.NewGuid(), StudentCode = code, FullName = "Sinh Viên Thử", Email = $"{code.ToLowerInvariant()}@example.test", SpecializationId = (await db.Specializations.FirstAsync()).Id };
            db.Students.Add(s);
            if (semesterId is { } id)
            {
                db.SemesterEnrollments.Add(new SemesterEnrollment { SemesterId = id, StudentId = s.Id, CourseCode = course!, ClassCodeId = (await db.ClassCodes.OrderBy(c => c.Id).FirstAsync()).Id });
            }
            await db.SaveChangesAsync();
            return s;
        });
        _students.Add(student.Id);
        return student;
    }

    /// <summary>Mỗi môn + loại chỉ một đợt mở một lúc: chọn cặp chưa có đợt mở trong DB dev.</summary>
    private async Task<(string Course, string Type)> FreeCourseAndTypeAsync()
    {
        var open = await OpenPairsAsync();
        var pair = new[] { "EXE101/TEAM_FORMATION", "EXE201/TEAM_FORMATION", "EXE201/CHANGE_REQUEST" }.First(p => !open.Contains(p)).Split('/');
        return (pair[0], pair[1]);
    }

    private Task<List<string>> OpenPairsAsync() =>
        _api.QueryDbAsync(db => db.RegistrationRounds.Where(r => r.Status == "OPEN").Select(r => r.CourseCode + "/" + r.RoundType).ToListAsync());

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_semesters.Count + _students.Count == 0)
        {
            await _api.DisposeAsync(); // test tích hợp tắt: không mở kết nối DB
            return;
        }
        await _api.QueryDbAsync(async db =>
        {
            await db.SemesterEnrollments.Where(e => _students.Contains(e.StudentId) || _semesters.Contains(e.SemesterId)).ExecuteDeleteAsync();
            await db.Students.Where(s => _students.Contains(s.Id)).ExecuteDeleteAsync();
            var rounds = await db.RegistrationRounds.Where(r => _semesters.Contains(r.SemesterId)).Select(r => r.Id).ToListAsync();
            await db.Set<OutboxMessage>().Where(m => rounds.Contains(m.AggregateId)).ExecuteDeleteAsync();
            await db.RegistrationRounds.Where(r => rounds.Contains(r.Id)).ExecuteDeleteAsync();
            return await db.Semesters.Where(s => _semesters.Contains(s.Id)).ExecuteDeleteAsync();
        });
        await _api.DisposeAsync();
    }
}
