using System.Net;
using System.Net.Http.Json;
using Academic.Services.Caching;
using Academic.Services.Rounds;
using Academic.Services.Semesters;
using BuildingBlocks.Auth;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academic.Tests;

/// <summary>
/// Giai đoạn môn (BE-67) với academic_db và Redis thật. Bật bằng <c>EXE_INTEGRATION=1</c>;
/// container <c>academic-api</c> phải tắt (xem <see cref="SemestersRoundsApiTests"/>).
/// Tạo đợt lập nhóm thử trong học kỳ hiện hành cho môn chưa có đợt nào, xóa đợt và outbox khi xong.
/// </summary>
[Collection(RoundTestsCollection.Name)]
public sealed class CoursePhaseApiTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly AcademicApiFactory _api = new();
    private readonly List<Guid> _rounds = [];
    private HttpClient Admin => _api.ClientAs(Roles.Admin);

    [Fact]
    public async Task Phase_follows_team_formation_round_and_cache_is_refreshed()
    {
        if (!Enabled)
        {
            return;
        }

        var current = await _api.QueryDbAsync(db => db.Semesters.Where(s => s.IsCurrent).Select(s => (Guid?)s.Id).SingleOrDefaultAsync());
        if (current is not { } semesterId)
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Student().GetAsync("/api/semesters/current/phase")).StatusCode);
            return;
        }
        var used = await _api.QueryDbAsync(db => db.RegistrationRounds
            .Where(r => r.SemesterId == semesterId && r.RoundType == "TEAM_FORMATION").Select(r => r.CourseCode).ToListAsync());
        var course = new[] { "EXE101", "EXE201" }.FirstOrDefault(c => !used.Contains(c));
        var otherOpen = await _api.QueryDbAsync(db => db.RegistrationRounds.AnyAsync(r => r.Status == "OPEN" && r.CourseCode == course && r.RoundType == "TEAM_FORMATION"));
        if (course is null || otherOpen)
        {
            return; // học kỳ hiện hành đã có đợt lập nhóm của cả hai môn – không đụng dữ liệu thật
        }

        Assert.Equal(("TEAM_FORMATION", (DateTimeOffset?)null), await PhaseAsync(course)); // chưa có đợt (đã nạp cache)

        var now = DateTimeOffset.UtcNow;
        var created = await Admin.PostAsJsonAsync("/api/registration-rounds",
            new { semesterId, courseCode = course, roundType = "TEAM_FORMATION", name = "Đợt thử giai đoạn", opensAt = now.AddHours(-1), deadlineAt = now.AddDays(1) });
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
        var round = (await created.Content.ReadFromJsonAsync<RoundResponse>())!;
        _rounds.Add(round.Id);
        Assert.True((await Admin.PostAsync($"/api/registration-rounds/{round.Id}/open", null)).IsSuccessStatusCode);
        Assert.Equal(("TEAM_FORMATION", (DateTimeOffset?)round.DeadlineAt), await PhaseAsync(course)); // cache đã xóa khi tạo / mở đợt

        Assert.True((await Admin.PostAsync($"/api/registration-rounds/{round.Id}/close", null)).IsSuccessStatusCode);
        Assert.Equal("SEMESTER_RUNNING", (await PhaseAsync(course)).Phase); // rule 18: đợt lập nhóm đã đóng
    }

    private HttpClient Student() => _api.ClientAs(Roles.Student);

    private async Task<(string Phase, DateTimeOffset? Deadline)> PhaseAsync(string course)
    {
        var phases = (await Student().GetFromJsonAsync<List<CoursePhaseResponse>>("/api/semesters/current/phase"))!;
        Assert.Equal(["EXE101", "EXE201"], phases.Select(p => p.CourseCode));
        var phase = phases.Single(p => p.CourseCode == course);
        return (phase.Phase, phase.TeamFormationDeadlineAt);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (Enabled && _rounds.Count > 0)
        {
            await _api.QueryDbAsync(async db =>
            {
                await db.Set<OutboxMessage>().Where(m => _rounds.Contains(m.AggregateId)).ExecuteDeleteAsync();
                return await db.RegistrationRounds.Where(r => _rounds.Contains(r.Id)).ExecuteDeleteAsync();
            });
            // Xóa thẳng trong DB, không qua RoundService, nên tự xóa cache đợt.
            var cache = _api.Services.GetRequiredService<AcademicCache>();
            await cache.InvalidateAsync(AcademicCacheKeys.CurrentPhaseSource, CancellationToken.None);
            await cache.InvalidateAsync(AcademicCacheKeys.OpenRounds, CancellationToken.None);
        }
        await _api.DisposeAsync();
    }
}
