using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Auth;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.Members;
using Team.Repositories.Teams;
using Team.Repositories.TopicInterests;
using Team.Services.Academic;
using Team.Services.Finalization;
using Team.Services.Teams;

namespace Team.Tests;

/// <summary>
/// Chốt nhóm khi đóng đợt, Admin chốt / mở lại, thêm / chuyển thành viên (BE-48) với team_db thật. Bật bằng <c>EXE_INTEGRATION=1</c>.
/// </summary>
public sealed class FinalizationApiTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly TeamApiFactory _api = new();

    private FakeAcademic Academic => _api.Academic;

    private HttpClient Admin => _api.ClientFor(null, Roles.Admin);

    [Fact]
    public async Task Round_close_finalizes_valid_teams_and_marks_the_rest_for_review_once()
    {
        if (!Enabled)
        {
            return;
        }

        var valid = await CreateTeamAsync("Nhóm Đủ", extraMembers: 3);
        var small = await CreateTeamAsync("Nhóm Thiếu", extraMembers: 1);
        var oneSpecialization = await CreateTeamAsync("Nhóm Một Ngành", extraMembers: 3, specializationId: 1);

        await AssertProblemAsync(await _api.ClientFor(Guid.NewGuid()).PostAsJsonAsync("/internal/teams/finalize-round", RoundRequest()), 403, null);
        var result = await FinalizeRoundAsync();
        Assert.Equal(new FinalizeRoundResult(1, 2), result);

        Assert.Equal((TeamStatuses.Finalized, true), await StatusAsync(valid.Id));
        Assert.Equal((TeamStatuses.NeedsReview, false), await StatusAsync(small.Id));
        Assert.Equal((TeamStatuses.NeedsReview, false), await StatusAsync(oneSpecialization.Id));

        var finalized = (await _api.EventsAsync(valid.Id)).Single(e => e.EventType == FinalizationEventTypes.TeamFinalized);
        Assert.Equal("SYSTEM", Payload(finalized).GetProperty("finalizedBy").GetString());
        Assert.Equal(4, Payload(finalized).GetProperty("memberStudentIds").GetArrayLength());
        var review = (await _api.EventsAsync(oneSpecialization.Id)).Single(e => e.EventType == FinalizationEventTypes.TeamNeedsReview);
        Assert.Equal(["NOT_ENOUGH_SPECIALIZATIONS"], Payload(review).GetProperty("violations").EnumerateArray().Select(v => v.GetString()));

        Assert.Equal(new FinalizeRoundResult(0, 0), await FinalizeRoundAsync());
    }

    [Fact]
    public async Task Admin_finalizes_only_valid_team_and_reopens_by_phase()
    {
        if (!Enabled)
        {
            return;
        }

        var team = await CreateTeamAsync("Nhóm Admin Chốt", extraMembers: 2);
        await AssertProblemAsync(await Admin.PostAsync($"/api/teams/{team.Id}/finalize", null), 409, "composition-invalid");
        await AssertProblemAsync(await _api.ClientFor(team.Leader!.Id).PostAsync($"/api/teams/{team.Id}/finalize", null), 403, null);

        var extra = Academic.Enroll(specializationId: 5);
        var added = await Admin.PostAsJsonAsync($"/api/teams/{team.Id}/members", new AdminAddMemberRequest(extra.Id, "Bổ sung"));
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        Assert.Equal(4, (await added.Content.ReadFromJsonAsync<TeamDetailResponse>())!.MemberCount);

        var finalized = (await (await Admin.PostAsync($"/api/teams/{team.Id}/finalize", null)).Content.ReadFromJsonAsync<TeamDetailResponse>())!;
        Assert.Equal(TeamStatuses.Finalized, finalized.Status);
        Assert.NotNull(finalized.FinalizedAt);

        // Đợt lập nhóm còn mở → về Hoạt động; đợt đã đóng → Cần xem lại.
        var reopened = (await (await Admin.PostAsync($"/api/teams/{team.Id}/reopen", null)).Content.ReadFromJsonAsync<TeamDetailResponse>())!;
        Assert.Equal((TeamStatuses.Active, (DateTimeOffset?)null), (reopened.Status, reopened.FinalizedAt));
        await Admin.PostAsync($"/api/teams/{team.Id}/finalize", null);
        Academic.OpenCourses.Remove("EXE101");
        reopened = (await (await Admin.PostAsync($"/api/teams/{team.Id}/reopen", null)).Content.ReadFromJsonAsync<TeamDetailResponse>())!;
        Assert.Equal(TeamStatuses.NeedsReview, reopened.Status);
        Assert.Equal(["TEAM_FINALIZED", "TEAM_REOPENED", "TEAM_FINALIZED", "TEAM_NEEDS_REVIEW"], await TeamActionsAsync(team.Id));
    }

    [Fact]
    public async Task Admin_add_member_checks_course_team_size_and_existing_team()
    {
        if (!Enabled)
        {
            return;
        }

        var team = await CreateTeamAsync("Nhóm Thêm Người", extraMembers: 5);
        var other = await CreateTeamAsync("Nhóm Khác", extraMembers: 0);

        await AssertProblemAsync(await Admin.PostAsJsonAsync($"/api/teams/{team.Id}/members", new AdminAddMemberRequest(Academic.Enroll().Id, null)), 409, "team-full");
        await AssertProblemAsync(await Admin.PostAsJsonAsync($"/api/teams/{other.Id}/members",
            new AdminAddMemberRequest(Academic.Enroll(course: "EXE201").Id, null)), 400, "validation");
        await AssertProblemAsync(await Admin.PostAsJsonAsync($"/api/teams/{other.Id}/members", new AdminAddMemberRequest(team.Leader!.Id, null)), 409, "already-in-team");
        await AssertProblemAsync(await Admin.PostAsJsonAsync($"/api/teams/{other.Id}/members", new AdminAddMemberRequest(Guid.NewGuid(), null)), 403, "not-in-list");

        var student = Academic.Enroll(specializationId: 4);
        Assert.Equal(HttpStatusCode.OK, (await Admin.PostAsJsonAsync($"/api/teams/{other.Id}/members", new AdminAddMemberRequest(student.Id, null))).StatusCode);
        var joined = (await _api.EventsAsync(other.Id)).Last(e => e.EventType == "MemberJoined");
        Assert.Equal(("ADMIN", 2), (Payload(joined).GetProperty("joinSource").GetString(), Payload(joined).GetProperty("memberCount").GetInt32()));
    }

    [Fact]
    public async Task Moving_leader_out_of_finalized_team_promotes_new_leader_and_needs_review()
    {
        if (!Enabled)
        {
            return;
        }

        var source = await CreateTeamAsync("Nhóm Nguồn", extraMembers: 3);
        var target = await CreateTeamAsync("Nhóm Đích", extraMembers: 1);
        await FinalizeRoundAsync();
        Assert.Equal(TeamStatuses.Finalized, (await StatusAsync(source.Id)).Status);

        var leaderId = source.Leader!.Id;
        var moved = await Admin.PostAsJsonAsync($"/api/teams/{source.Id}/members/{leaderId}/move", new MoveMemberRequest(target.Id));
        Assert.Equal(HttpStatusCode.NoContent, moved.StatusCode);

        var sourceMembers = await MembersAsync(source.Id);
        Assert.Equal(3, sourceMembers.Count);
        Assert.Single(sourceMembers, m => m.Role == MemberRoles.Leader);
        Assert.Equal(TeamStatuses.NeedsReview, (await StatusAsync(source.Id)).Status);
        var targetMembers = await MembersAsync(target.Id);
        Assert.Contains(targetMembers, m => m.StudentId == leaderId && m.Role == MemberRoles.Member && m.JoinSource == "ADMIN");

        var sourceEvents = (await _api.EventsAsync(source.Id)).Select(e => e.EventType).ToList();
        Assert.Contains("MemberLeft", sourceEvents);
        Assert.Contains("LeaderChanged", sourceEvents);
        Assert.Contains(FinalizationEventTypes.TeamNeedsReview, sourceEvents);
        Assert.Contains("MemberJoined", (await _api.EventsAsync(target.Id)).Select(e => e.EventType));

        await AssertProblemAsync(await Admin.PostAsJsonAsync($"/api/teams/{source.Id}/members/{leaderId}/move", new MoveMemberRequest(target.Id)), 404, null);
        await AssertProblemAsync(await Admin.PostAsJsonAsync($"/api/teams/{target.Id}/members/{leaderId}/move", new MoveMemberRequest(target.Id)), 400, "validation");
    }

    [Fact]
    public async Task Moving_the_last_member_disbands_the_team()
    {
        if (!Enabled)
        {
            return;
        }

        var lonely = await CreateTeamAsync("Nhóm Một Người", extraMembers: 0);
        var target = await CreateTeamAsync("Nhóm Nhận", extraMembers: 2);

        var moved = await Admin.PostAsJsonAsync($"/api/teams/{lonely.Id}/members/{lonely.Leader!.Id}/move", new MoveMemberRequest(target.Id));
        Assert.Equal(HttpStatusCode.NoContent, moved.StatusCode);
        Assert.Equal(TeamStatuses.Disbanded, (await StatusAsync(lonely.Id)).Status);
        Assert.Equal(4, (await MembersAsync(target.Id)).Count);
        Assert.Equal(TeamStatuses.Active, (await StatusAsync(target.Id)).Status);
    }

    [Fact]
    public async Task Admin_removes_leader_from_finalized_team_and_student_becomes_unmatched()
    {
        if (!Enabled)
        {
            return;
        }

        var team = await CreateTeamAsync("Nhóm Bớt Người", extraMembers: 3);
        await FinalizeRoundAsync();
        var leaderId = team.Leader!.Id;

        await AssertProblemAsync(await _api.ClientFor(leaderId).PostAsJsonAsync($"/api/teams/{team.Id}/members/{leaderId}/remove", new { }), 403, null);
        await AssertProblemAsync(await Admin.PostAsJsonAsync($"/api/teams/{team.Id}/members/{Guid.NewGuid()}/remove", new { }), 404, null);
        await AssertProblemAsync(await Admin.PostAsJsonAsync($"/api/teams/{team.Id}/members/{leaderId}/remove",
            new AdminRemoveMemberRequest(new string('x', 501))), 400, "validation");

        var removed = await Admin.PostAsJsonAsync($"/api/teams/{team.Id}/members/{leaderId}/remove", new AdminRemoveMemberRequest("Chuyển ngành"));
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        var detail = (await removed.Content.ReadFromJsonAsync<TeamDetailResponse>())!;
        Assert.Equal((3, TeamStatuses.NeedsReview), (detail.MemberCount, detail.Status));
        Assert.DoesNotContain(detail.Members, m => m.Id == leaderId);
        Assert.Single(detail.Members, m => m.Role == MemberRoles.Leader);

        var events = await _api.EventsAsync(team.Id);
        var left = events.Last(e => e.EventType == "MemberLeft");
        Assert.Equal(("ADMIN", 3), (Payload(left).GetProperty("reason").GetString(), Payload(left).GetProperty("memberCount").GetInt32()));
        Assert.Contains(events, e => e.EventType == "LeaderChanged");
        Assert.Contains(events, e => e.EventType == FinalizationEventTypes.TeamNeedsReview);
        var entry = await _api.QueryDbAsync(db => db.Set<Team.Repositories.AuditLog.AuditLogEntry>()
            .SingleAsync(a => a.TeamId == team.Id && a.Action == "MEMBER_REMOVED"));
        Assert.Equal((leaderId, "Chuyển ngành"), (entry.EntityId, entry.Reason));

        // Sinh viên bị bớt thành chưa có nhóm: Admin thêm được vào nhóm khác.
        var other = await CreateTeamAsync("Nhóm Nhận Người", extraMembers: 0);
        Assert.Equal(HttpStatusCode.OK, (await Admin.PostAsJsonAsync($"/api/teams/{other.Id}/members", new AdminAddMemberRequest(leaderId, null))).StatusCode);
    }

    [Fact]
    public async Task Admin_removal_applies_to_inherited_team_without_change_request()
    {
        if (!Enabled)
        {
            return;
        }

        // Nhóm EXE201 kế thừa: trưởng nhóm bớt người chỉ thành thay đổi chờ duyệt (rule 7), Admin thì áp dụng ngay.
        var students = new[] { Academic.Enroll(1, "EXE201"), Academic.Enroll(2, "EXE201"), Academic.Enroll(3, "EXE201"), Academic.Enroll(4, "EXE201") };
        var teamId = await TestData.CreateInheritedTeamAsync(_api, "Nhóm Kế Thừa Bớt Người", students);

        var detail = (await (await Admin.PostAsJsonAsync($"/api/teams/{teamId}/members/{students[3].Id}/remove", new AdminRemoveMemberRequest(null)))
            .Content.ReadFromJsonAsync<TeamDetailResponse>())!;

        Assert.Equal((3, false), (detail.MemberCount, detail.HasPendingChanges));
        Assert.DoesNotContain(detail.Members, m => m.Id == students[3].Id);
    }

    [Fact]
    public async Task Removing_the_last_member_without_reason_disbands_the_team()
    {
        if (!Enabled)
        {
            return;
        }

        var lonely = await CreateTeamAsync("Nhóm Bớt Hết", extraMembers: 0);

        var removed = await Admin.PostAsync($"/api/teams/{lonely.Id}/members/{lonely.Leader!.Id}/remove", null);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        var disbanded = (await removed.Content.ReadFromJsonAsync<TeamDetailResponse>())!;
        Assert.Equal((0, TeamStatuses.Disbanded), (disbanded.MemberCount, disbanded.Status));
        Assert.Equal(["TEAM_DISBANDED"], await TeamActionsAsync(lonely.Id));
        await AssertProblemAsync(await Admin.PostAsync($"/api/teams/{lonely.Id}/members/{lonely.Leader.Id}/remove", null), 404, null);
    }

    [Fact]
    public async Task Disbanding_closes_open_interests_of_both_advisor_kinds()
    {
        if (!Enabled)
        {
            return;
        }

        var lonely = await CreateTeamAsync("Nhóm Giải Tán", extraMembers: 0);
        var (lecturerId, mentorId, withdrawnBy) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await _api.QueryDbAsync(db =>
        {
            db.AddRange(
                new TopicInterest { Id = Guid.NewGuid(), TeamId = lonely.Id, AdvisorKind = "LECTURER", AdvisorId = lecturerId, AdvisorName = "GV A" },
                new TopicInterest { Id = Guid.NewGuid(), TeamId = lonely.Id, AdvisorKind = "MENTOR", AdvisorId = mentorId, AdvisorName = "Mentor M" },
                new TopicInterest
                {
                    Id = Guid.NewGuid(), TeamId = lonely.Id, AdvisorKind = "LECTURER", AdvisorId = withdrawnBy, AdvisorName = "GV B",
                    Status = "WITHDRAWN", ClosedAt = DateTimeOffset.UtcNow,
                });
            return db.SaveChangesAsync();
        });

        var removed = await Admin.PostAsync($"/api/teams/{lonely.Id}/members/{lonely.Leader!.Id}/remove", null);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);

        var interests = await _api.QueryDbAsync(db => db.Set<TopicInterest>().AsNoTracking().Where(i => i.TeamId == lonely.Id).ToListAsync());
        Assert.All(interests.Where(i => i.AdvisorId != withdrawnBy), i => Assert.Equal(("CLOSED", "TEAM_DISBANDED"), (i.Status, i.ClosedReason)));
        Assert.Equal("WITHDRAWN", interests.Single(i => i.AdvisorId == withdrawnBy).Status);

        var closed = (await _api.EventsAsync(lonely.Id)).Where(e => e.EventType == "TopicInterestClosed").Select(Payload).ToList();
        Assert.Equal(2, closed.Count);
        Assert.All(closed, p => Assert.Equal("TEAM_DISBANDED", p.GetProperty("reason").GetString()));
        Assert.Equal(new[] { "LECTURER", "MENTOR" }, closed.Select(p => p.GetProperty("advisorKind").GetString()).Order());
        Assert.Contains(closed, p => p.GetProperty("advisorId").GetGuid() == mentorId);
        Assert.All(await _api.EventsAsync(lonely.Id), e => Assert.Contains(e.Topic, new[] { "exe.team", "exe.assignment" }));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (Enabled)
        {
            await TestData.DeleteSemesterAsync(_api, Academic.Semester.Id);
        }
        await _api.DisposeAsync();
    }

    private FinalizeRoundRequest RoundRequest() => new(Guid.NewGuid(), Academic.Semester.Id, "EXE101");

    private async Task<FinalizeRoundResult> FinalizeRoundAsync()
    {
        var response = await _api.ClientFor(null, Roles.Service).PostAsJsonAsync("/internal/teams/finalize-round", RoundRequest());
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<FinalizeRoundResult>())!;
    }

    /// <summary>Nhóm do sinh viên chuyên ngành 1 tạo, thêm thành viên chuyên ngành 2, 3… (hoặc cùng <paramref name="specializationId"/>).</summary>
    private Task<TeamDetailResponse> CreateTeamAsync(string name, int extraMembers, int? specializationId = null) =>
        TestData.CreateTeamAsync(_api, name, extraMembers, specializationId);

    private Task<(string Status, bool HasFinalizedAt)> StatusAsync(Guid teamId) =>
        _api.QueryDbAsync(db => db.Set<TeamEntity>().Where(t => t.Id == teamId).Select(t => new ValueTuple<string, bool>(t.Status, t.FinalizedAt != null)).SingleAsync());

    private Task<List<TeamMember>> MembersAsync(Guid teamId) =>
        _api.QueryDbAsync(db => db.Set<TeamMember>().AsNoTracking().Where(m => m.TeamId == teamId).ToListAsync());

    private Task<List<string>> TeamActionsAsync(Guid teamId) =>
        _api.QueryDbAsync(db => db.Set<Team.Repositories.AuditLog.AuditLogEntry>()
            .Where(a => a.TeamId == teamId && a.EntityType == "Team" && a.Action != "TEAM_CREATED").OrderBy(a => a.Id).Select(a => a.Action).ToListAsync());

    private static JsonElement Payload(OutboxMessage message) => JsonDocument.Parse(message.Payload).RootElement;

    private static Task AssertProblemAsync(HttpResponseMessage response, int status, string? code) => TestData.AssertProblemAsync(response, status, code);
}
