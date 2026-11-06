using System.Net;
using System.Net.Http.Json;
using Academic.Services;
using Academic.Services.Mentors;
using BuildingBlocks.Auth;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Academic.Tests;

/// <summary>
/// Số nhóm đang mentor lấy từ Team (BE-66): <c>currentTeamCount</c>, lọc <c>hasCapacity</c>, chặn <c>max-below-current</c>,
/// xử lý khi Team không trả lời. Team thay bằng <see cref="FakeTeamClient"/>; academic_db và Redis thật (<c>EXE_INTEGRATION=1</c>).
/// Chạy tuần tự với lớp đổi học kỳ hiện hành (<see cref="SemestersRoundsApiTests"/>) vì test kiểm tra semesterId gửi cho Team.
/// </summary>
[Collection(RoundTestsCollection.Name)]
public sealed class MentorTeamCountsApiTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly AcademicApiFactory _api = new();
    private readonly List<string> _emails = [];
    private HttpClient Admin => _api.ClientAs(Roles.Admin);

    [Fact]
    public async Task Current_team_count_comes_from_team_for_current_semester()
    {
        if (!Enabled)
        {
            return;
        }

        var semester = await CurrentSemesterAsync();
        var mentor = await CreateAsync("Mentor đếm nhóm", maxTeams: 4);
        _api.Team.Counts[mentor.Id] = 3;
        _api.Team.Calls.Clear();

        var detail = (await Admin.GetFromJsonAsync<MentorResponse>($"/api/mentors/{mentor.Id}"))!;
        var me = (await _api.ClientWith(new Dictionary<string, object> { [ClaimNames.MentorId] = mentor.Id.ToString() }, Roles.Mentor)
            .GetFromJsonAsync<MentorResponse>("/api/mentors/me"))!;
        var internalView = (await _api.ClientAs(Roles.Service).GetFromJsonAsync<MentorResponse>($"/internal/mentors/{mentor.Id}"))!;
        var list = (await Admin.GetFromJsonAsync<PagedResponse<MentorResponse>>($"/api/mentors?search={Uri.EscapeDataString(mentor.Email)}"))!;

        Assert.Equal((3, 3, 3, 3), (detail.CurrentTeamCount, me.CurrentTeamCount, internalView.CurrentTeamCount, Assert.Single(list.Items).CurrentTeamCount));
        Assert.NotEmpty(_api.Team.Calls);
        Assert.All(_api.Team.Calls, call => Assert.Equal(semester, call.SemesterId));
        // Danh sách hỏi Team một lần cho cả trang, không N+1.
        Assert.Equal(4, _api.Team.Calls.Count);
    }

    [Fact]
    public async Task Has_capacity_filters_all_mentors_before_paging()
    {
        if (!Enabled)
        {
            return;
        }

        var tag = Guid.NewGuid().ToString("N")[..8];
        var full = await CreateAsync($"Mentor {tag} A", maxTeams: 2);
        var free = await CreateAsync($"Mentor {tag} B", maxTeams: 2);
        var almost = await CreateAsync($"Mentor {tag} C", maxTeams: 2);
        var over = await CreateAsync($"Mentor {tag} D", maxTeams: 1);
        _api.Team.Counts[full.Id] = 2;
        _api.Team.Counts[almost.Id] = 1;
        _api.Team.Counts[over.Id] = 3; // Bộ môn gán tay vượt tối đa

        Task<PagedResponse<MentorResponse>?> ListAsync(string query) =>
            Admin.GetFromJsonAsync<PagedResponse<MentorResponse>>($"/api/mentors?search={tag}{query}");

        // Trang 1 của danh sách gốc (A) đã đủ nhóm: lọc trước khi phân trang thì trang 1 vẫn có mentor còn chỗ.
        var page1 = (await ListAsync("&hasCapacity=true&pageSize=1&page=1"))!;
        var page2 = (await ListAsync("&hasCapacity=true&pageSize=1&page=2"))!;
        var page3 = (await ListAsync("&hasCapacity=true&pageSize=1&page=3"))!;
        Assert.Equal((2, 2), (page1.TotalCount, page1.TotalPages));
        Assert.Equal(free.Id, Assert.Single(page1.Items).Id);
        Assert.Equal((almost.Id, 1), (Assert.Single(page2.Items).Id, page2.Items[0].CurrentTeamCount));
        Assert.Empty(page3.Items);

        var fullOnes = (await ListAsync("&hasCapacity=false"))!;
        Assert.Equal([full.Id, over.Id], fullOnes.Items.Select(m => m.Id));
        Assert.Equal(4, (await ListAsync(""))!.TotalCount);

        // Team không trả lời: bỏ qua bộ lọc như trước, số nhóm 0, màn hình vẫn có dữ liệu.
        _api.Team.Unavailable = true;
        var fallback = await Admin.GetAsync($"/api/mentors?search={tag}&hasCapacity=true");
        Assert.Equal(HttpStatusCode.OK, fallback.StatusCode);
        var items = (await fallback.Content.ReadFromJsonAsync<PagedResponse<MentorResponse>>())!;
        Assert.Equal(4, items.TotalCount);
        Assert.All(items.Items, m => Assert.Equal(0, m.CurrentTeamCount));
        Assert.Equal(HttpStatusCode.OK, (await Admin.GetAsync($"/api/mentors/{full.Id}")).StatusCode);
    }

    [Fact]
    public async Task Lowering_max_below_current_team_count_is_rejected()
    {
        if (!Enabled)
        {
            return;
        }

        var mentor = await CreateAsync("Mentor hạ tối đa", maxTeams: 5);
        _api.Team.Counts[mentor.Id] = 3;
        var eventsBefore = await EventCountAsync(mentor.Id);

        var below = await Admin.PutAsJsonAsync($"/api/mentors/{mentor.Id}", new { fullName = mentor.FullName, email = mentor.Email, maxTeams = 2 });
        Assert.Equal(HttpStatusCode.Conflict, below.StatusCode);
        Assert.Contains("max-below-current", await below.Content.ReadAsStringAsync());
        Assert.Equal(eventsBefore, await EventCountAsync(mentor.Id));

        var equal = await Admin.PutAsJsonAsync($"/api/mentors/{mentor.Id}", new { fullName = mentor.FullName, email = mentor.Email, maxTeams = 3 });
        Assert.Equal(HttpStatusCode.OK, equal.StatusCode);
        var saved = (await equal.Content.ReadFromJsonAsync<MentorResponse>())!;
        Assert.Equal((3, 3), (saved.MaxTeams, saved.CurrentTeamCount));

        // Team không trả lời: đổi tối đa thì 503 chứ không bỏ qua kiểm tra; sửa khác vẫn được.
        _api.Team.Unavailable = true;
        var unavailable = await Admin.PutAsJsonAsync($"/api/mentors/{mentor.Id}", new { fullName = mentor.FullName, email = mentor.Email, maxTeams = 4 });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Contains("team-unavailable", await unavailable.Content.ReadAsStringAsync());
        var renamed = await Admin.PutAsJsonAsync($"/api/mentors/{mentor.Id}", new { fullName = "Tên mới", email = mentor.Email, maxTeams = 3 });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var afterRename = (await renamed.Content.ReadFromJsonAsync<MentorResponse>())!;
        Assert.Equal(("Tên mới", 3, 0), (afterRename.FullName, afterRename.MaxTeams, afterRename.CurrentTeamCount));
        Assert.Equal(3, (await _api.QueryDbAsync(db => db.Mentors.SingleAsync(m => m.Id == mentor.Id))).MaxTeams);
    }

    private Task<Guid> CurrentSemesterAsync() =>
        _api.QueryDbAsync(db => db.Semesters.Where(s => s.IsCurrent).Select(s => s.Id).SingleAsync());

    private async Task<MentorResponse> CreateAsync(string name, int maxTeams)
    {
        var email = $"mentor-dem-{Guid.NewGuid():N}@example.test";
        _emails.Add(email);
        var response = await Admin.PostAsJsonAsync("/api/mentors", new { fullName = name, email, maxTeams });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<MentorResponse>())!;
    }

    private Task<int> EventCountAsync(Guid mentorId) =>
        _api.QueryDbAsync(db => db.Set<OutboxMessage>().CountAsync(m => m.AggregateId == mentorId));

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (Enabled)
        {
            await _api.QueryDbAsync(async db =>
            {
                var ids = await db.Mentors.Where(m => _emails.Contains(m.Email)).Select(m => m.Id).ToListAsync();
                await db.Set<OutboxMessage>().Where(m => ids.Contains(m.AggregateId)).ExecuteDeleteAsync();
                return await db.Mentors.Where(m => ids.Contains(m.Id)).ExecuteDeleteAsync();
            });
        }
        await _api.DisposeAsync();
    }
}
