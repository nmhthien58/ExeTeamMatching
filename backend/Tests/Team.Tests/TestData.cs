using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.ChangeRequests;
using Team.Repositories.Invitations;
using Team.Repositories.JoinRequests;
using Team.Repositories.Members;
using Team.Repositories.Teams;
using Team.Services.Teams;

namespace Team.Tests;

/// <summary>Dữ liệu và kiểm tra dùng chung cho test tích hợp của Finalization, AutoAssign, Statistics.</summary>
public static class TestData
{
    /// <summary>
    /// Nhóm EXE101 do sinh viên chuyên ngành 1 tạo qua API, thêm thẳng vào DB <paramref name="extraMembers"/> thành viên
    /// chuyên ngành 2, 3… (hoặc cùng <paramref name="specializationId"/>); trạng thái theo sĩ số như rule 15.
    /// </summary>
    public static async Task<TeamDetailResponse> CreateTeamAsync(TeamApiFactory api, string name, int extraMembers, int? specializationId = null)
    {
        var leader = api.Academic.Enroll(specializationId: 1);
        var response = await api.ClientFor(leader.Id).PostAsJsonAsync("/api/teams", new TeamInput(name, null, null, [1], null));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var team = (await response.Content.ReadFromJsonAsync<TeamDetailResponse>())!;
        if (extraMembers == 0)
        {
            return team;
        }

        var students = Enumerable.Range(0, extraMembers).Select(i => api.Academic.Enroll(specializationId ?? i + 2)).ToList();
        await api.QueryDbAsync(db =>
        {
            db.AddRange(students.Select(s => new TeamMember
            {
                TeamId = team.Id, SemesterId = api.Academic.Semester.Id, StudentId = s.Id, StudentCode = s.StudentCode, FullName = s.FullName,
                SpecializationId = s.SpecializationId, JoinSource = "INVITATION",
            }));
            return db.SaveChangesAsync();
        });
        await api.QueryDbAsync(async db =>
        {
            var entity = await db.Set<TeamEntity>().SingleAsync(t => t.Id == team.Id);
            entity.Status = TeamRules.StatusAfterMemberChange(entity.Status, entity.MemberCount);
            return await db.SaveChangesAsync();
        });
        return team;
    }

    /// <summary>Xóa nhóm (cascade thành viên, lời mời…) và sự kiện outbox của học kỳ giả, nhóm, lời mời, yêu cầu.</summary>
    public static Task DeleteSemesterAsync(TeamApiFactory api, Guid semesterId) => api.QueryDbAsync(async db =>
    {
        var teamIds = await db.Set<TeamEntity>().Where(t => t.SemesterId == semesterId).Select(t => t.Id).ToListAsync();
        var keys = teamIds.Append(semesterId)
            .Concat(await db.Set<Invitation>().Where(i => teamIds.Contains(i.TeamId)).Select(i => i.Id).ToListAsync())
            .Concat(await db.Set<JoinRequest>().Where(r => teamIds.Contains(r.TeamId)).Select(r => r.Id).ToListAsync())
            .Concat(await db.Set<ChangeRequest>().Where(r => teamIds.Contains(r.TeamId)).Select(r => r.Id).ToListAsync())
            .ToList();
        // change_requests chặn xóa nhóm (ON DELETE RESTRICT); mục thay đổi xóa theo cascade.
        await db.Set<ChangeRequest>().Where(r => teamIds.Contains(r.TeamId)).ExecuteDeleteAsync();
        await db.Set<TeamEntity>().Where(t => teamIds.Contains(t.Id)).ExecuteDeleteAsync();
        return await db.Set<OutboxMessage>().Where(m => keys.Contains(m.AggregateId)).ExecuteDeleteAsync();
    });

    /// <summary>Kiểm tra mã HTTP và (nếu có) mã lỗi ProblemDetails <c>…/errors/&lt;code&gt;</c>.</summary>
    public static async Task AssertProblemAsync(HttpResponseMessage response, int status, string? code)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == status, $"{(int)response.StatusCode}: {body}");
        if (code is not null)
        {
            using var problem = JsonDocument.Parse(body);
            Assert.EndsWith("/" + code, problem.RootElement.GetProperty("type").GetString());
        }
    }
}
