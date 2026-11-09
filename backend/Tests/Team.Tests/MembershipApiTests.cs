using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Auth;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.ChangeRequests;
using Team.Repositories.Invitations;
using Team.Repositories.JoinRequests;
using Team.Repositories.Members;
using Team.Repositories.Teams;
using Team.Services.Academic;
using Team.Services.Invitations;
using Team.Services.JoinRequests;
using Team.Services.Members;
using Team.Services.Teams;

namespace Team.Tests;

/// <summary>
/// Lời mời, yêu cầu tham gia, thành viên (BE-28, BE-29, BE-30) với team_db thật. Bật bằng <c>EXE_INTEGRATION=1</c>.
/// </summary>
public sealed class MembershipApiTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly TeamApiFactory _api = new();

    private FakeAcademic Academic => _api.Academic;

    [Fact]
    public async Task Invitation_accepted_joins_team_and_cancels_other_pending_requests()
    {
        if (!Enabled)
        {
            return;
        }

        var leader = Academic.Enroll();
        var team = await CreateTeamAsync(leader, "Nhóm Mời");
        var other = await CreateTeamAsync(Academic.Enroll(), "Nhóm Khác");
        var invitee = Academic.Enroll(specializationId: 2);
        var joinOther = await SubmitAsync(invitee, other.Id, hasNoIdea: true);

        var sent = await _api.ClientFor(leader.Id).PostAsJsonAsync($"/api/teams/{team.Id}/invitations", new SendInvitationRequest(invitee.Id, "Vào nhóm nhé"));
        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        var invitation = (await sent.Content.ReadFromJsonAsync<InvitationResponse>())!;
        Assert.Equal(("PENDING", invitee.StudentCode, "Nhóm Mời"), (invitation.Status, invitation.Invitee.StudentCode, invitation.Team!.Name));
        await AssertProblemAsync(await _api.ClientFor(leader.Id).PostAsJsonAsync($"/api/teams/{team.Id}/invitations",
            new SendInvitationRequest(invitee.Id, null)), 409, "pending-exists");

        var mine = await _api.ClientFor(invitee.Id).GetFromJsonAsync<List<InvitationResponse>>("/api/invitations/me?status=PENDING");
        Assert.Equal([invitation.Id], mine!.Select(i => i.Id));

        var accepted = await _api.ClientFor(invitee.Id).PostAsync($"/api/invitations/{invitation.Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var result = (await accepted.Content.ReadFromJsonAsync<MemberChangeResult>())!;
        Assert.Equal((true, 2, "MEMBER"), (result.AppliedImmediately, result.Team!.MemberCount, result.Team.MyRelation));

        Assert.Equal("CANCELLED", await _api.QueryDbAsync(db => db.Set<JoinRequest>().Where(r => r.Id == joinOther.Id).Select(r => r.Status).SingleAsync()));
        Assert.Contains(await EventTypesAsync(team.Id), e => e == "MemberJoined");
        Assert.Equal(["InvitationSent", "InvitationAccepted"], await EventTypesAsync(invitation.Id));
        await AssertProblemAsync(await _api.ClientFor(invitee.Id).PostAsync($"/api/invitations/{invitation.Id}/decline", null), 409, "request-not-pending");
    }

    [Fact]
    public async Task Join_request_needs_ideas_and_fourth_member_makes_team_active()
    {
        if (!Enabled)
        {
            return;
        }

        var leader = Academic.Enroll(specializationId: 1);
        var team = await CreateTeamAsync(leader, "Nhóm Ý Tưởng");
        await AddMembersAsync(team.Id, 2, specializationId: 1);
        var student = Academic.Enroll(specializationId: 3);

        await AssertProblemAsync(await _api.ClientFor(student.Id).PostAsJsonAsync($"/api/teams/{team.Id}/join-requests",
            new SubmitJoinRequest(null, [], HasNoIdea: false)), 400, "ideas-required");
        await AssertProblemAsync(await _api.ClientFor(student.Id).PostAsJsonAsync($"/api/teams/{team.Id}/join-requests",
            new SubmitJoinRequest(null, [new IdeaDto("App", null)], HasNoIdea: true)), 400, "ideas-required");

        var request = await SubmitAsync(student, team.Id, ideas: [new IdeaDto("Học nhóm AI", "Gợi ý bạn học"), new IdeaDto("Chợ đồ cũ", null)]);
        var forTeam = await _api.ClientFor(leader.Id).GetFromJsonAsync<List<JoinRequestResponse>>($"/api/teams/{team.Id}/join-requests?status=PENDING");
        Assert.Equal(["Học nhóm AI", "Chợ đồ cũ"], forTeam!.Single().Ideas.Select(i => i.Title));
        await AssertProblemAsync(await _api.ClientFor(student.Id).GetAsync($"/api/teams/{team.Id}/join-requests"), 409, "not-leader");

        var accepted = await _api.ClientFor(leader.Id).PostAsync($"/api/join-requests/{request.Id}/accept", null);
        var result = (await accepted.Content.ReadFromJsonAsync<MemberChangeResult>())!;
        Assert.Equal((4, "ACTIVE", true), (result.Team!.MemberCount, result.Team.Status, result.Team.Requirement.IsValid));
        Assert.Equal(["JoinRequestSubmitted", "JoinRequestAccepted"], await EventTypesAsync(request.Id));
    }

    [Fact]
    public async Task Full_team_and_student_with_team_are_refused()
    {
        if (!Enabled)
        {
            return;
        }

        var leader = Academic.Enroll();
        var team = await CreateTeamAsync(leader, "Nhóm Đầy");
        await AddMembersAsync(team.Id, 5);
        var outsider = Academic.Enroll();
        await AssertProblemAsync(await _api.ClientFor(leader.Id).PostAsJsonAsync($"/api/teams/{team.Id}/invitations",
            new SendInvitationRequest(outsider.Id, null)), 409, "team-full");
        await AssertProblemAsync(await _api.ClientFor(outsider.Id).PostAsJsonAsync($"/api/teams/{team.Id}/join-requests",
            new SubmitJoinRequest(null, null, HasNoIdea: true)), 409, "team-full");

        var other = await CreateTeamAsync(Academic.Enroll(), "Nhóm Còn Chỗ");
        await AssertProblemAsync(await _api.ClientFor(leader.Id).PostAsJsonAsync($"/api/teams/{other.Id}/join-requests",
            new SubmitJoinRequest(null, null, HasNoIdea: true)), 409, "already-in-team");
    }

    [Fact]
    public async Task Leader_transfers_then_leaves_and_removes_member()
    {
        if (!Enabled)
        {
            return;
        }

        var leader = Academic.Enroll();
        var team = await CreateTeamAsync(leader, "Nhóm Đổi Trưởng");
        var members = await AddMembersAsync(team.Id, 3);
        Assert.Equal("ACTIVE", await StatusAsync(team.Id));

        await AssertProblemAsync(await _api.ClientFor(leader.Id).PostAsync($"/api/teams/{team.Id}/leave", null), 409, "leader-must-transfer");
        await AssertProblemAsync(await _api.ClientFor(members[0].Id).PostAsJsonAsync($"/api/teams/{team.Id}/transfer-leader",
            new TransferLeaderRequest(members[1].Id)), 409, "not-leader");

        var transferred = await _api.ClientFor(leader.Id).PostAsJsonAsync($"/api/teams/{team.Id}/transfer-leader", new TransferLeaderRequest(members[0].Id));
        Assert.True(transferred.IsSuccessStatusCode, await transferred.Content.ReadAsStringAsync());
        Assert.Equal(members[0].Id, (await transferred.Content.ReadFromJsonAsync<MemberChangeResult>())!.Team!.Leader!.Id);

        var left = (await (await _api.ClientFor(leader.Id).PostAsync($"/api/teams/{team.Id}/leave", null)).Content.ReadFromJsonAsync<MemberChangeResult>())!;
        Assert.Equal((3, "DRAFT", "NONE"), (left.Team!.MemberCount, left.Team.Status, left.Team.MyRelation));

        var removed = await _api.ClientFor(members[0].Id).DeleteAsync($"/api/teams/{team.Id}/members/{members[1].Id}");
        Assert.Equal(2, (await removed.Content.ReadFromJsonAsync<MemberChangeResult>())!.Team!.MemberCount);

        var events = await _api.EventsAsync(team.Id);
        Assert.Equal(["LeaderChanged"], events.Where(e => e.EventType == "LeaderChanged").Select(e => e.EventType));
        Assert.Equal(["LEAVE", "REMOVED"], events.Where(e => e.EventType == "MemberLeft").Select(e => Payload(e, "reason")));
    }

    [Fact]
    public async Task Sole_leader_leaves_and_team_is_disbanded()
    {
        if (!Enabled)
        {
            return;
        }

        var leader = Academic.Enroll();
        var team = await CreateTeamAsync(leader, "Nhóm Một Người");

        var response = await _api.ClientFor(leader.Id).PostAsync($"/api/teams/{team.Id}/leave", null);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var left = (await response.Content.ReadFromJsonAsync<MemberChangeResult>())!;
        Assert.Equal((true, (TeamDetailResponse?)null), (left.AppliedImmediately, left.Team));

        Assert.Equal("DISBANDED", await StatusAsync(team.Id));
        var events = await _api.EventsAsync(team.Id);
        var memberLeft = Assert.Single(events, e => e.EventType == "MemberLeft");
        Assert.Equal("LEAVE", Payload(memberLeft, "reason"));
        Assert.Equal(0, JsonDocument.Parse(memberLeft.Payload).RootElement.GetProperty("memberCount").GetInt32());
        Assert.Equal(404, (int)(await _api.ClientFor(leader.Id).PostAsync($"/api/teams/{team.Id}/leave", null)).StatusCode);
    }

    [Fact]
    public async Task Inherited_team_turns_changes_into_pending_items()
    {
        if (!Enabled)
        {
            return;
        }

        var leader = Academic.Enroll(course: "EXE201");
        var team = await CreateTeamAsync(leader, "Nhóm Kế Thừa");
        var members = await AddMembersAsync(team.Id, 1, course: "EXE201");
        await _api.QueryDbAsync(db => db.Set<TeamEntity>().Where(t => t.Id == team.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.IsInherited, true)));
        var invitee = Academic.Enroll(course: "EXE201");

        var invitation = (await (await _api.ClientFor(leader.Id).PostAsJsonAsync($"/api/teams/{team.Id}/invitations",
            new SendInvitationRequest(invitee.Id, null))).Content.ReadFromJsonAsync<InvitationResponse>())!;
        var accepted = (await (await _api.ClientFor(invitee.Id).PostAsync($"/api/invitations/{invitation.Id}/accept", null))
            .Content.ReadFromJsonAsync<MemberChangeResult>())!;
        Assert.Equal((false, 2, true), (accepted.AppliedImmediately, accepted.Team!.MemberCount, accepted.Team.HasPendingChanges));

        var left = (await (await _api.ClientFor(members[0].Id).PostAsync($"/api/teams/{team.Id}/leave", null)).Content.ReadFromJsonAsync<MemberChangeResult>())!;
        Assert.False(left.AppliedImmediately);

        var items = await _api.QueryDbAsync(db => db.Set<ChangeRequestItem>().Where(i => i.TeamId == team.Id).OrderBy(i => i.Action).ToListAsync());
        Assert.Equal([("ADD", "INVITATION", invitee.Id), ("REMOVE", "LEAVE", members[0].Id)], items.Select(i => (i.Action, i.Source, i.StudentId!.Value)));
        Assert.DoesNotContain(await EventTypesAsync(team.Id), e => e is "MemberJoined" or "MemberLeft");
    }

    [Fact]
    public async Task Job_expires_overdue_invitations_and_requests()
    {
        if (!Enabled)
        {
            return;
        }

        var leader = Academic.Enroll();
        var team = await CreateTeamAsync(leader, "Nhóm Hết Hạn");
        var invitee = Academic.Enroll();
        var invitation = (await (await _api.ClientFor(leader.Id).PostAsJsonAsync($"/api/teams/{team.Id}/invitations",
            new SendInvitationRequest(invitee.Id, null))).Content.ReadFromJsonAsync<InvitationResponse>())!;
        var request = await SubmitAsync(Academic.Enroll(), team.Id, hasNoIdea: true);
        await _api.QueryDbAsync(async db =>
        {
            var past = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.Set<Invitation>().Where(i => i.Id == invitation.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.CreatedAt, past.AddHours(-72)).SetProperty(i => i.ExpiresAt, past));
            return await db.Set<JoinRequest>().Where(r => r.Id == request.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.CreatedAt, past.AddHours(-72)).SetProperty(r => r.ExpiresAt, past));
        });

        Assert.Equal("EXPIRED", (await _api.ClientFor(invitee.Id).GetFromJsonAsync<List<InvitationResponse>>("/api/invitations/me"))!.Single().Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.ClientFor(leader.Id).PostAsync("/internal/invitations/expire-overdue", null)).StatusCode);

        var expired = await (await _api.ClientFor(null, Roles.Service).PostAsync("/internal/invitations/expire-overdue", null))
            .Content.ReadFromJsonAsync<ExpireResult>();
        Assert.True(expired!.Invitations >= 1 && expired.JoinRequests >= 1);
        Assert.Contains("InvitationExpired", await EventTypesAsync(invitation.Id));
        await AssertProblemAsync(await _api.ClientFor(invitee.Id).PostAsync($"/api/invitations/{invitation.Id}/accept", null), 409, "request-not-pending");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (Enabled)
        {
            await _api.QueryDbAsync(async db =>
            {
                var teamIds = await db.Set<TeamEntity>().Where(t => t.SemesterId == Academic.Semester.Id).Select(t => t.Id).ToListAsync();
                var keys = teamIds
                    .Concat(await db.Set<Invitation>().Where(i => teamIds.Contains(i.TeamId)).Select(i => i.Id).ToListAsync())
                    .Concat(await db.Set<JoinRequest>().Where(r => teamIds.Contains(r.TeamId)).Select(r => r.Id).ToListAsync())
                    .ToList();
                await db.Set<TeamEntity>().Where(t => teamIds.Contains(t.Id)).ExecuteDeleteAsync();
                return await db.Set<OutboxMessage>().Where(m => keys.Contains(m.AggregateId)).ExecuteDeleteAsync();
            });
        }
        await _api.DisposeAsync();
    }

    private async Task<TeamDetailResponse> CreateTeamAsync(StudentSnapshot leader, string name)
    {
        var response = await _api.ClientFor(leader.Id).PostAsJsonAsync("/api/teams", new TeamInput(name, null, null, [1], null));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<TeamDetailResponse>())!;
    }

    private async Task<JoinRequestResponse> SubmitAsync(StudentSnapshot student, Guid teamId, IReadOnlyList<IdeaDto>? ideas = null, bool hasNoIdea = false)
    {
        var response = await _api.ClientFor(student.Id).PostAsJsonAsync($"/api/teams/{teamId}/join-requests", new SubmitJoinRequest(null, ideas, hasNoIdea));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JoinRequestResponse>())!;
    }

    /// <summary>Thêm thẳng vào DB, các chuyên ngành khác nhau trừ khi chỉ định.</summary>
    private async Task<List<StudentSnapshot>> AddMembersAsync(Guid teamId, int count, int? specializationId = null, string course = "EXE101")
    {
        var students = Enumerable.Range(0, count).Select(i => Academic.Enroll(specializationId ?? i + 2, course)).ToList();
        await _api.QueryDbAsync(db =>
        {
            db.AddRange(students.Select(s => new TeamMember
            {
                TeamId = teamId, SemesterId = Academic.Semester.Id, StudentId = s.Id, StudentCode = s.StudentCode, FullName = s.FullName,
                SpecializationId = s.SpecializationId ?? 0, JoinSource = "INVITATION",
            }));
            return db.SaveChangesAsync();
        });
        // Trạng thái theo sĩ số như khi vào qua API (rule 15).
        await _api.QueryDbAsync(async db =>
        {
            var team = await db.Set<TeamEntity>().SingleAsync(t => t.Id == teamId);
            team.Status = TeamRules.StatusAfterMemberChange(team.Status, team.MemberCount);
            return await db.SaveChangesAsync();
        });
        return students;
    }

    private Task<string> StatusAsync(Guid teamId) => _api.QueryDbAsync(db => db.Set<TeamEntity>().Where(t => t.Id == teamId).Select(t => t.Status).SingleAsync());

    private async Task<List<string>> EventTypesAsync(Guid aggregateId) => (await _api.EventsAsync(aggregateId)).Select(e => e.EventType).ToList();

    private static string? Payload(OutboxMessage message, string property) =>
        JsonDocument.Parse(message.Payload).RootElement.GetProperty(property).GetString();

    private static async Task AssertProblemAsync(HttpResponseMessage response, int status, string code)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == status, $"{(int)response.StatusCode}: {body}");
        using var problem = JsonDocument.Parse(body);
        Assert.EndsWith("/" + code, problem.RootElement.GetProperty("type").GetString());
    }
}
