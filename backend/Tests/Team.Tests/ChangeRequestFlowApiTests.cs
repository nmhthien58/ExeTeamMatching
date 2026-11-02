using System.Net.Http.Json;
using BuildingBlocks.Auth;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.BaselineImport;
using Team.Repositories.Members;
using Team.Repositories.Teams;
using Team.Services.ChangeRequests;
using Team.Services.Invitations;
using Team.Services.JoinRequests;
using Team.Services.Members;
using Team.Services.Teams;

namespace Team.Tests;

// Chạy trên PostgreSQL riêng với EXE_INTEGRATION=1 và EXE_TEAM_DB.
public sealed class ChangeRequestFlowApiTests
{
    public sealed class IntegrationTheoryAttribute : TheoryAttribute
    {
        public IntegrationTheoryAttribute()
        {
            if (Environment.GetEnvironmentVariable("EXE_INTEGRATION") != "1")
                Skip = "Cần EXE_INTEGRATION=1 và PostgreSQL thử nghiệm.";
        }
    }

    [IntegrationTheory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task Pending_membership_changes_apply_only_after_admin_approval(bool approve, bool useJoinRequest)
    {
        await using var api = new TeamApiFactory();
        api.Academic.ChangeRequestRoundId = Guid.NewGuid();
        var leader = api.Academic.Enroll(course: "EXE201");
        var initial = Enumerable.Range(0, 3).Select(_ => api.Academic.Enroll(2, "EXE201")).ToList();
        var newcomer = api.Academic.Enroll(2, "EXE201");
        using var leaderClient = api.ClientFor(leader.Id);
        using var admin = api.ClientFor(null, Roles.Admin);
        var team = await Read<TeamDetailResponse>(await leaderClient.PostAsJsonAsync("/api/teams",
            new TeamInput("Nhóm kiểm thử đổi thành viên", null, null, [1], null)));
        await api.QueryDbAsync(async db =>
        {
            db.AddRange(initial.Select(s => new TeamMember
            {
                TeamId = team.Id, SemesterId = api.Academic.Semester.Id, StudentId = s.Id,
                StudentCode = s.StudentCode, FullName = s.FullName, SpecializationId = s.SpecializationId,
                JoinSource = "INHERITED",
            }));
            await db.SaveChangesAsync();
            var members = await db.Set<TeamMember>().Where(m => m.TeamId == team.Id).ToListAsync();
            db.AddRange(members.Select(m => new TeamBaselineMember
            {
                TeamId = team.Id, StudentId = m.StudentId, StudentCode = m.StudentCode,
                FullName = m.FullName, SpecializationId = m.SpecializationId, Role = m.Role,
            }));
            var entity = await db.Set<TeamEntity>().SingleAsync(t => t.Id == team.Id);
            entity.IsInherited = true;
            entity.Status = "ACTIVE";
            return await db.SaveChangesAsync();
        });

        using var newcomerClient = api.ClientFor(newcomer.Id);
        MemberChangeResult accepted;
        if (useJoinRequest)
        {
            var join = await Read<JoinRequestResponse>(await newcomerClient.PostAsJsonAsync(
                $"/api/teams/{team.Id}/join-requests", new SubmitJoinRequest(null, null, true)));
            accepted = await Read<MemberChangeResult>(await leaderClient.PostAsync($"/api/join-requests/{join.Id}/accept", null));
        }
        else
        {
            var invitation = await Read<InvitationResponse>(await leaderClient.PostAsJsonAsync(
                $"/api/teams/{team.Id}/invitations", new SendInvitationRequest(newcomer.Id, null)));
            accepted = await Read<MemberChangeResult>(await newcomerClient.PostAsync($"/api/invitations/{invitation.Id}/accept", null));
        }
        Assert.False(accepted.AppliedImmediately);
        Assert.True(accepted.Team.HasPendingChanges);

        using var leavingClient = api.ClientFor(initial[0].Id);
        var left = await Read<MemberChangeResult>(useJoinRequest
            ? await leaderClient.DeleteAsync($"/api/teams/{team.Id}/members/{initial[0].Id}")
            : await leavingClient.PostAsync($"/api/teams/{team.Id}/leave", null));
        Assert.False(left.AppliedImmediately);
        Assert.True(left.Team.HasPendingChanges);

        // Khi còn pending trước khi gửi yêu cầu, TeamMember chưa đổi.
        var pendingMembers = await MemberIds(api, team.Id);
        Assert.Equal(4, pendingMembers.Count);
        Assert.Contains(initial[0].Id, pendingMembers);
        Assert.DoesNotContain(newcomer.Id, pendingMembers);

        var pending = await Read<PendingChangesResponse>(await leaderClient.GetAsync($"/api/teams/{team.Id}/pending-changes"));
        Assert.Equal(4, pending.BaselineMembers.Count);
        Assert.Equal(2, pending.PendingItems.Count);
        var addedItem = Assert.Single(pending.PendingItems.Where(i => i.Action == "ADD"));
        Assert.Equal(newcomer.Id, addedItem.Student?.Id);
        Assert.Equal(useJoinRequest ? "JOIN_REQUEST" : "INVITATION", addedItem.Source);
        var removedItem = Assert.Single(pending.PendingItems.Where(i => i.Action == "REMOVE"));
        Assert.Equal(initial[0].Id, removedItem.Student?.Id);
        Assert.Equal(useJoinRequest ? "REMOVE" : "LEAVE", removedItem.Source);

        var submitted = await Read<ChangeRequestResponse>(await leaderClient.PostAsJsonAsync(
            $"/api/teams/{team.Id}/change-requests", new ChangeRequestInput(
                pending.PendingItems.Select(i => i.Id).ToList(), "Điều chỉnh thành viên cho học kỳ mới")));
        Assert.Equal(2, submitted.ChangedMemberCount);
        Assert.Equal(50m, submitted.ChangedPercent);
        Assert.Equal("PENDING", submitted.Status);
        var listed = await Read<PagedResponse<ChangeRequestResponse>>(await admin.GetAsync(
            $"/api/change-requests?status=PENDING&roundId={submitted.RoundId}"));
        Assert.Equal(submitted.Id, Assert.Single(listed.Items).Id);

        // Khi yêu cầu đã gửi và còn PENDING, TeamMember vẫn chưa đổi.
        var before = await MemberIds(api, team.Id);
        Assert.Equal(4, before.Count);
        Assert.Contains(initial[0].Id, before);
        Assert.DoesNotContain(newcomer.Id, before);

        var detail = await Read<ChangeRequestDetailResponse>(await admin.GetAsync($"/api/change-requests/{submitted.Id}"));
        Assert.Equal(4, detail.MembersAfterChange.Count);
        Assert.Equal(2, detail.ChangedMemberCount);
        Assert.Equal(50m, detail.ChangedPercent);
        Assert.Equal("PENDING", detail.Status);
        Assert.Contains(detail.Items, i => i.Action == "ADD" && i.Student?.Id == newcomer.Id);
        Assert.Contains(detail.Items, i => i.Action == "REMOVE" && i.Student?.Id == initial[0].Id);

        var decision = approve ? "approve" : "reject";
        var reviewed = await Read<ChangeRequestResponse>(await admin.PostAsJsonAsync(
            $"/api/change-requests/{submitted.Id}/{decision}", new ChangeRequestDecisionInput("Đã kiểm tra yêu cầu")));
        Assert.Equal(approve ? "APPROVED" : "REJECTED", reviewed.Status);
        Assert.Equal(2, reviewed.ChangedMemberCount);
        Assert.Equal(50m, reviewed.ChangedPercent);

        // Submit và Admin approve thì thành viên mới được áp dụng; reject thì giữ nguyên.
        var after = await MemberIds(api, team.Id);
        Assert.Equal(4, after.Count);
        Assert.Equal(approve, after.Contains(newcomer.Id));
        Assert.Equal(!approve, after.Contains(initial[0].Id));
        Assert.Contains(leader.Id, after);
        Assert.Contains(initial[1].Id, after);
        Assert.Contains(initial[2].Id, after);

        var events = await api.EventsAsync(submitted.Id);
        Assert.Contains(events, e => e.EventType == "ChangeRequestSubmitted");
        Assert.Contains(events, e => e.EventType == (approve ? "ChangeRequestApproved" : "ChangeRequestRejected"));
        Assert.DoesNotContain(events, e => e.EventType == (approve ? "ChangeRequestRejected" : "ChangeRequestApproved"));

        var teamEvents = await api.EventsAsync(team.Id);
        if (approve)
        {
            Assert.Contains(teamEvents, e => e.EventType == MemberEventTypes.MemberJoined);
            Assert.Contains(teamEvents, e => e.EventType == MemberEventTypes.MemberLeft);
        }
        else
        {
            Assert.DoesNotContain(teamEvents, e => e.EventType == MemberEventTypes.MemberJoined);
            Assert.DoesNotContain(teamEvents, e => e.EventType == MemberEventTypes.MemberLeft);
        }
    }

    private static Task<List<Guid>> MemberIds(TeamApiFactory api, Guid teamId) =>
        api.QueryDbAsync(db => db.Set<TeamMember>().Where(m => m.TeamId == teamId).Select(m => m.StudentId).ToListAsync());

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            return (await response.Content.ReadFromJsonAsync<T>())!;
        }
    }
}
