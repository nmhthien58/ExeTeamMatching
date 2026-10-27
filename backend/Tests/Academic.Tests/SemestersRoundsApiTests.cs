using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Academic.Services.Rounds;
using Academic.Services.Semesters;
using BuildingBlocks.Auth;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Academic.Tests;

/// <summary>
/// API học kỳ và đợt đăng ký (BE-16) với academic_db và Redis thật. Bật bằng <c>EXE_INTEGRATION=1</c>.
/// Mỗi test tạo học kỳ thử có mã chưa dùng, xóa học kỳ, đợt và outbox của nó khi xong; học kỳ hiện hành được trả lại như cũ.
/// Container <c>academic-api</c> phải tắt trong lúc chạy, để Outbox dispatcher của nó không phát sự kiện thử lên Kafka.
/// </summary>
public sealed class SemestersRoundsApiTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly AcademicApiFactory _api = new();
    private readonly List<Guid> _semesters = [];
    private HttpClient Admin => _api.ClientAs(Roles.Admin);

    [Fact]
    public async Task Admin_creates_and_updates_semester_with_defaults()
    {
        if (!Enabled)
        {
            return;
        }

        var semester = await CreateSemesterAsync();
        Assert.Equal((false, 6, 3, 2, 15), (semester.IsCurrent, semester.MentoringStartWeek, semester.ExpectedMentorSessions,
            semester.SlotBookingDeadlineWeek, semester.MeetingSlotMinutes));
        Assert.Null(semester.CheckpointTargetWeeks);

        var response = await Admin.PutAsJsonAsync($"/api/semesters/{semester.Id}", new
        {
            code = semester.Code, name = "Học kỳ đã sửa", startDate = semester.StartDate, endDate = semester.EndDate,
            mentoringStartWeek = 7, checkpointTargetWeeks = new[] { 3, 6, 9, 12 },
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<SemesterResponse>())!;
        Assert.Equal(("Học kỳ đã sửa", 7, 3), (updated.Name, updated.MentoringStartWeek, updated.ExpectedMentorSessions));
        Assert.Equal([3, 6, 9, 12], updated.CheckpointTargetWeeks!);

        var list = await Admin.GetFromJsonAsync<List<SemesterResponse>>("/api/semesters");
        Assert.Contains(list!, s => s.Id == semester.Id);

        var duplicate = await Admin.PostAsJsonAsync("/api/semesters", new { code = semester.Code, name = "Trùng", startDate = "2030-01-01", endDate = "2030-04-01" });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        var student = await _api.ClientAs(Roles.Student).PostAsJsonAsync("/api/semesters", new { code = "SP99", name = "x", startDate = "2030-01-01", endDate = "2030-04-01" });
        Assert.Equal(HttpStatusCode.Forbidden, student.StatusCode);
    }

    [Fact]
    public async Task Making_semester_current_moves_the_flag_and_refreshes_cache()
    {
        if (!Enabled)
        {
            return;
        }

        var original = await _api.ClientAs(Roles.Student).GetFromJsonAsync<SemesterResponse>("/api/semesters/current"); // nạp cache
        try
        {
            var semester = await CreateSemesterAsync(isCurrent: true);

            var current = await _api.ClientAs(Roles.Student).GetFromJsonAsync<SemesterResponse>("/api/semesters/current");
            Assert.Equal(semester.Id, current!.Id);
            Assert.Equal(1, await _api.QueryDbAsync(db => db.Semesters.CountAsync(s => s.IsCurrent)));
        }
        finally
        {
            if (original is not null)
            {
                var restored = await Admin.PutAsJsonAsync($"/api/semesters/{original.Id}", new
                {
                    code = original.Code, name = original.Name, startDate = original.StartDate, endDate = original.EndDate, isCurrent = true,
                    checkpointTargetWeeks = original.CheckpointTargetWeeks,
                });
                Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
            }
        }
    }

    [Fact]
    public async Task Round_goes_scheduled_open_closed_and_publishes_events()
    {
        if (!Enabled)
        {
            return;
        }

        var semester = await CreateSemesterAsync();
        var (course, type) = await FreeCourseAndTypeAsync();
        var opensAt = DateTimeOffset.UtcNow.AddHours(-1);
        var input = new { semesterId = semester.Id, courseCode = course, roundType = type, name = "Đợt thử", opensAt, deadlineAt = opensAt.AddDays(3) };

        var created = await Admin.PostAsJsonAsync("/api/registration-rounds", input);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var round = await ReadRoundAsync(created);
        Assert.Equal(("SCHEDULED", (DateTimeOffset?)null), (round.Status, round.ClosedAt));
        Assert.Equal(HttpStatusCode.Conflict, (await Admin.PostAsync($"/api/registration-rounds/{round.Id}/close", null)).StatusCode);

        var overlap = await Admin.PostAsJsonAsync("/api/registration-rounds", input with { name = "Đợt chồng", opensAt = opensAt.AddDays(1), deadlineAt = opensAt.AddDays(5) });
        Assert.Equal("round-overlap", await ProblemCodeAsync(overlap));

        await Admin.GetFromJsonAsync<List<RoundResponse>>($"/api/registration-rounds/active?courseCode={course}"); // nạp cache
        var opened = await ReadRoundAsync(await Admin.PostAsync($"/api/registration-rounds/{round.Id}/open", null));
        Assert.Equal("OPEN", opened.Status);
        var active = await _api.ClientAs(Roles.Student).GetFromJsonAsync<List<RoundResponse>>($"/api/registration-rounds/active?courseCode={course}");
        Assert.Contains(active!, r => r.Id == round.Id);

        var moveDeadline = await Admin.PutAsJsonAsync($"/api/registration-rounds/{round.Id}", input with { deadlineAt = opensAt.AddDays(4) });
        Assert.Equal("round-open-locked", await ProblemCodeAsync(moveDeadline));
        var rename = await Admin.PutAsJsonAsync($"/api/registration-rounds/{round.Id}", input with { name = "Đợt thử (đổi tên)" });
        Assert.Equal("Đợt thử (đổi tên)", (await ReadRoundAsync(rename)).Name);

        var closed = await ReadRoundAsync(await Admin.PostAsync($"/api/registration-rounds/{round.Id}/close", null));
        Assert.Equal("CLOSED", closed.Status);
        Assert.NotNull(closed.ClosedAt);
        var again = await Admin.PostAsync($"/api/registration-rounds/{round.Id}/close", null);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode); // Job thử lại an toàn
        Assert.Equal("round-closed", await ProblemCodeAsync(await Admin.PostAsync($"/api/registration-rounds/{round.Id}/open", null)));
        Assert.DoesNotContain((await Admin.GetFromJsonAsync<List<RoundResponse>>("/api/registration-rounds/active"))!, r => r.Id == round.Id);

        var events = await _api.QueryDbAsync(db => db.Set<OutboxMessage>().Where(m => m.AggregateId == round.Id).OrderBy(m => m.OccurredAt).ToListAsync());
        Assert.Equal(["RoundOpened", "RoundClosed"], events.Select(e => e.EventType));
        Assert.All(events, e => Assert.Equal("exe.registration", e.Topic));
        var payload = JsonDocument.Parse(events[0].Payload).RootElement;
        Assert.Equal((round.Id, semester.Id, course), (payload.GetProperty("roundId").GetGuid(), payload.GetProperty("semesterId").GetGuid(), payload.GetProperty("courseCode").GetString()));
        Assert.True(payload.TryGetProperty("deadlineAt", out _));
    }

    [Fact]
    public async Task Round_input_is_validated_against_semester_and_rules()
    {
        if (!Enabled)
        {
            return;
        }

        var semester = await CreateSemesterAsync();
        var now = DateTimeOffset.UtcNow;
        var changeIn101 = await Admin.PostAsJsonAsync("/api/registration-rounds",
            new { semesterId = semester.Id, courseCode = "EXE101", roundType = "CHANGE_REQUEST", name = "x", opensAt = now, deadlineAt = now.AddDays(1) });
        Assert.Equal(HttpStatusCode.BadRequest, changeIn101.StatusCode);

        var noSemester = await Admin.PostAsJsonAsync("/api/registration-rounds",
            new { semesterId = Guid.NewGuid(), courseCode = "EXE101", roundType = "TEAM_FORMATION", name = "x", opensAt = now, deadlineAt = now.AddDays(1) });
        Assert.Contains("semesterId", await noSemester.Content.ReadAsStringAsync());

        var past = await Admin.PostAsJsonAsync("/api/registration-rounds",
            new { semesterId = semester.Id, courseCode = "EXE101", roundType = "TEAM_FORMATION", name = "Đã qua", opensAt = now.AddDays(-3), deadlineAt = now.AddDays(-1) });
        var round = await ReadRoundAsync(past);
        var open = await Admin.PostAsync($"/api/registration-rounds/{round.Id}/open", null);
        Assert.Equal(HttpStatusCode.BadRequest, open.StatusCode);
        Assert.Contains("deadlineAt", await open.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, (await Admin.PostAsync($"/api/registration-rounds/{Guid.NewGuid()}/open", null)).StatusCode);
    }

    private async Task<SemesterResponse> CreateSemesterAsync(bool isCurrent = false)
    {
        var used = await _api.QueryDbAsync(db => db.Semesters.Select(s => s.Code).ToListAsync());
        var code = Enumerable.Range(0, 100).Select(n => $"SU{n:00}").First(c => !used.Contains(c));
        var response = await Admin.PostAsJsonAsync("/api/semesters", new { code, name = "Học kỳ thử", startDate = "2030-05-01", endDate = "2030-08-31", isCurrent });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var semester = (await response.Content.ReadFromJsonAsync<SemesterResponse>())!;
        _semesters.Add(semester.Id);
        return semester;
    }

    /// <summary>Mỗi môn + loại chỉ một đợt mở một lúc: chọn cặp chưa có đợt mở trong DB dev.</summary>
    private async Task<(string Course, string Type)> FreeCourseAndTypeAsync()
    {
        var open = await _api.QueryDbAsync(db => db.RegistrationRounds.Where(r => r.Status == "OPEN").Select(r => r.CourseCode + "/" + r.RoundType).ToListAsync());
        var pair = new[] { "EXE101/TEAM_FORMATION", "EXE201/TEAM_FORMATION", "EXE201/CHANGE_REQUEST" }.First(p => !open.Contains(p)).Split('/');
        return (pair[0], pair[1]);
    }

    private static async Task<RoundResponse> ReadRoundAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<RoundResponse>())!;
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        var type = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("type").GetString();
        return type?.Split('/').Last();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_semesters.Count > 0)
        {
            await _api.QueryDbAsync(async db =>
            {
                var rounds = await db.RegistrationRounds.Where(r => _semesters.Contains(r.SemesterId)).Select(r => r.Id).ToListAsync();
                await db.Set<OutboxMessage>().Where(m => rounds.Contains(m.AggregateId)).ExecuteDeleteAsync();
                await db.RegistrationRounds.Where(r => rounds.Contains(r.Id)).ExecuteDeleteAsync();
                return await db.Semesters.Where(s => _semesters.Contains(s.Id)).ExecuteDeleteAsync();
            });
        }
        await _api.DisposeAsync();
    }
}
