using Collab.Repositories.Messages;
using Collab.Repositories.Workspaces;
using Microsoft.Extensions.Logging;

namespace Collab.Services.Workspaces;

// Payload sự kiện (docs/contracts/events.md) – chỉ lấy các trường Collaboration cần.
public sealed record TeamCreated(Guid TeamId, Guid SemesterId, string CourseCode, string Name, Guid LeaderStudentId, string LeaderName, Guid? ProjectId);
public sealed record MemberJoined(Guid TeamId, Guid StudentId, string FullName);
public sealed record MemberLeft(Guid TeamId, Guid StudentId);
public sealed record LeaderChanged(Guid TeamId, Guid OldLeaderStudentId, Guid NewLeaderStudentId);
public sealed record TeamLinkedToProject(Guid TeamId, Guid ProjectId, Guid PreviousTeamId);
public sealed record LecturerAssigned(Guid TeamId, Guid LecturerId, string LecturerName);
public sealed record MentorAssigned(Guid TeamId, Guid MentorId, string MentorName, Guid? MentorLecturerId);
public sealed record MentorUnassigned(Guid TeamId, Guid MentorId);

/// <summary>Workspace chưa tồn tại khi nhận sự kiện – consumer sẽ thử lại (TeamCreated có thể tới sau).</summary>
public sealed class WorkspaceNotReadyException(Guid teamId)
    : Exception($"Chưa có workspace của nhóm {teamId}."), BuildingBlocks.Messaging.IRetryableEventException;

/// <summary>
/// Dựng read model quyền truy cập (rule 21) từ sự kiện Team. Mọi thao tác idempotent: nhận lại cùng sự kiện không đổi kết quả.
/// </summary>
public sealed class WorkspaceMembershipService(IWorkspaceRepository workspaces, ILogger<WorkspaceMembershipService> logger)
{
    public async Task HandleAsync(TeamCreated e, CancellationToken ct)
    {
        if (await workspaces.GetByTeamIdAsync(e.TeamId, ct) is not null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            TeamId = e.TeamId,
            ProjectId = e.ProjectId,
            SemesterId = e.SemesterId,
            CourseCode = e.CourseCode,
            TeamName = e.Name,
            CreatedAt = now,
        };
        workspaces.Add(workspace,
        [
            new Channel { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, Kind = "TEAM", CreatedAt = now },
            new Channel { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, Kind = "ADVISORS", CreatedAt = now },
        ]);
        workspaces.AddMember(new WorkspaceMember
        {
            WorkspaceId = workspace.Id,
            PersonType = PersonTypes.Student,
            PersonId = e.LeaderStudentId,
            DisplayName = e.LeaderName,
            Role = WorkspaceRoles.Leader,
            JoinedAt = now,
        });
        await workspaces.SaveChangesAsync(ct);
        logger.LogInformation("Tạo workspace {WorkspaceId} cho nhóm {TeamId}", workspace.Id, e.TeamId);
    }

    public async Task HandleAsync(MemberJoined e, CancellationToken ct)
    {
        var workspace = await RequireAsync(e.TeamId, ct);
        await UpsertAsync(workspace.Id, PersonTypes.Student, e.StudentId, e.FullName, WorkspaceRoles.Member, ct, keepLeaderRole: true);
        await workspaces.SaveChangesAsync(ct);
    }

    public async Task HandleAsync(MemberLeft e, CancellationToken ct)
    {
        var workspace = await RequireAsync(e.TeamId, ct);
        var member = await workspaces.FindMemberAsync(workspace.Id, PersonTypes.Student, e.StudentId, ct);
        if (member is { LeftAt: null })
        {
            member.LeftAt = DateTimeOffset.UtcNow;
            await workspaces.SaveChangesAsync(ct);
        }
    }

    public async Task HandleAsync(LeaderChanged e, CancellationToken ct)
    {
        var workspace = await RequireAsync(e.TeamId, ct);
        var oldLeader = await workspaces.FindMemberAsync(workspace.Id, PersonTypes.Student, e.OldLeaderStudentId, ct);
        var newLeader = await workspaces.FindMemberAsync(workspace.Id, PersonTypes.Student, e.NewLeaderStudentId, ct)
            ?? throw new WorkspaceNotReadyException(e.TeamId); // MemberJoined của trưởng nhóm mới chưa tới
        if (oldLeader is not null)
        {
            oldLeader.Role = WorkspaceRoles.Member;
        }
        newLeader.Role = WorkspaceRoles.Leader;
        await workspaces.SaveChangesAsync(ct);
    }

    public async Task HandleAsync(TeamLinkedToProject e, CancellationToken ct)
    {
        var workspace = await RequireAsync(e.TeamId, ct);
        var previous = await workspaces.GetByTeamIdAsync(e.PreviousTeamId, ct);
        workspace.ProjectId = e.ProjectId;
        workspace.PreviousWorkspaceId = previous?.Id;
        await workspaces.SaveChangesAsync(ct);
    }

    public async Task HandleAsync(LecturerAssigned e, CancellationToken ct)
    {
        var workspace = await RequireAsync(e.TeamId, ct);
        await ReplaceAdvisorAsync(workspace.Id, WorkspaceRoles.Instructor, PersonTypes.Lecturer, e.LecturerId, e.LecturerName, ct);
        await workspaces.SaveChangesAsync(ct);
    }

    public async Task HandleAsync(MentorAssigned e, CancellationToken ct)
    {
        var workspace = await RequireAsync(e.TeamId, ct);
        // Giảng viên làm mentor dùng chung tài khoản giảng viên (rule 20) nên lưu theo lecturer_id.
        var (type, id) = e.MentorLecturerId is { } lecturerId
            ? (PersonTypes.Lecturer, lecturerId)
            : (PersonTypes.Mentor, e.MentorId);
        await ReplaceAdvisorAsync(workspace.Id, WorkspaceRoles.Mentor, type, id, e.MentorName, ct);
        await workspaces.SaveChangesAsync(ct);
    }

    public async Task HandleAsync(MentorUnassigned e, CancellationToken ct)
    {
        var workspace = await RequireAsync(e.TeamId, ct);
        var current = await workspaces.FindActiveByRoleAsync(workspace.Id, WorkspaceRoles.Mentor, ct);
        if (current is not null)
        {
            current.LeftAt = DateTimeOffset.UtcNow;
            await workspaces.SaveChangesAsync(ct);
        }
    }

    private async Task<Workspace> RequireAsync(Guid teamId, CancellationToken ct) =>
        await workspaces.GetByTeamIdAsync(teamId, ct) ?? throw new WorkspaceNotReadyException(teamId);

    /// <summary>Mỗi workspace một giảng viên hướng dẫn và một mentor: người cũ rời, người mới vào (lịch sử giữ lại).</summary>
    private async Task ReplaceAdvisorAsync(Guid workspaceId, string role, string personType, Guid personId, string name, CancellationToken ct)
    {
        var current = await workspaces.FindActiveByRoleAsync(workspaceId, role, ct);
        if (current is not null && current.PersonType == personType && current.PersonId == personId)
        {
            return;
        }
        if (current is not null)
        {
            current.LeftAt = DateTimeOffset.UtcNow;
        }
        await UpsertAsync(workspaceId, personType, personId, name, role, ct, keepLeaderRole: false);
    }

    private async Task UpsertAsync(Guid workspaceId, string personType, Guid personId, string name, string role, CancellationToken ct, bool keepLeaderRole)
    {
        var member = await workspaces.FindMemberAsync(workspaceId, personType, personId, ct);
        if (member is null)
        {
            workspaces.AddMember(new WorkspaceMember
            {
                WorkspaceId = workspaceId,
                PersonType = personType,
                PersonId = personId,
                DisplayName = name,
                Role = role,
                JoinedAt = DateTimeOffset.UtcNow,
            });
            return;
        }

        member.DisplayName = name;
        if (!(keepLeaderRole && member.Role == WorkspaceRoles.Leader && member.LeftAt is null))
        {
            member.Role = role;
        }
        if (member.LeftAt is not null)
        {
            member.LeftAt = null;
            member.JoinedAt = DateTimeOffset.UtcNow;
        }
    }
}
