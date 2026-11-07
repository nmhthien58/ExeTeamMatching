using System.Security.Claims;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using Microsoft.Extensions.DependencyInjection;
using Team.Repositories.AuditLog;
using Team.Repositories.LeaderVotes;
using Team.Repositories.Members;
using Team.Repositories.Teams;
using Team.Services.Academic;
using Team.Services.Members;
using Team.Services.Teams;

namespace Team.Services.LeaderVotes;

public sealed record OpenLeaderVoteRequest(Guid NomineeStudentId, string? Reason);

public sealed record CastBallotRequest(bool Approve);

public sealed record LeaderVotePerson(Guid StudentId, string FullName);

/// <summary>Schema <c>LeaderVote</c>. <see cref="MyBallot"/> null = chưa bỏ phiếu; <see cref="ApproveCount"/> chỉ tính thành viên hiện tại.</summary>
public sealed record LeaderVoteResponse(
    Guid Id, Guid TeamId, string Status, LeaderVotePerson? Nominee, LeaderVotePerson? ProposedBy, string? Reason, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt,
    int ApproveCount, int RejectCount, int RequiredApprovals, int MemberCount, bool? MyBallot, bool CanWithdraw);

public interface ILeaderVoteService
{
    /// <summary>Đề nghị đang mở của nhóm; null nếu không có.</summary>
    Task<LeaderVoteResponse?> GetCurrentAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct);

    Task<LeaderVoteResponse> OpenAsync(Guid teamId, OpenLeaderVoteRequest request, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Bỏ hoặc đổi phiếu; trả đề nghị sau khi đếm, có thể đã PASSED hoặc REJECTED.</summary>
    Task<LeaderVoteResponse> CastBallotAsync(Guid teamId, Guid voteId, CastBallotRequest request, ClaimsPrincipal user, CancellationToken ct);

    Task WithdrawAsync(Guid teamId, Guid voteId, ClaimsPrincipal user, CancellationToken ct);
}

/// <summary>
/// Module LeaderVotes (BE-88): thành viên đề nghị đổi nhóm trưởng, nhóm bỏ phiếu. Nhóm luôn có đúng một trưởng nhóm trong suốt quá trình (rule 1).
/// Hết hạn được đóng khi có người đọc hoặc bỏ phiếu, nên thông báo "Hết hạn" đến khi có lần đọc đầu tiên sau hạn.
/// </summary>
public sealed class LeaderVoteService(
    ILeaderVoteRepository votes, ITeamRepository teams, IMemberRepository members, LeaderVoteCloser closer, RoundGuard rounds, AuditLogWriter audit,
    TimeProvider clock) : ILeaderVoteService
{
    private static readonly AuditActor VoteActor = new(null, "System", "Bỏ phiếu");

    public async Task<LeaderVoteResponse?> GetCurrentAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct)
    {
        var (team, current, me) = await LoadAsync(teamId, user, ct);
        var vote = await ResolveOpenAsync(team, current, ct);
        return vote is null ? null : await ToResponseAsync(vote, current, me, ct);
    }

    public async Task<LeaderVoteResponse> OpenAsync(Guid teamId, OpenLeaderVoteRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var (team, current, me) = await LoadAsync(teamId, user, ct);
        if (team.Status == TeamStatuses.Finalized || team.IsInherited || team.CourseCode == "EXE201")
        {
            throw NotAllowed("Nhóm đã chốt hoặc nhóm EXE201 không đổi nhóm trưởng bằng bỏ phiếu.");
        }
        if (current.Count < 2)
        {
            throw NotAllowed("Nhóm cần ít nhất 2 thành viên để bỏ phiếu.");
        }
        if (me.Role == MemberRoles.Leader)
        {
            throw TeamErrors.Invalid("nomineeStudentId", "Trưởng nhóm chuyển quyền trực tiếp, không cần bỏ phiếu.");
        }
        var nominee = current.FirstOrDefault(m => m.StudentId == request.NomineeStudentId) ?? throw TeamErrors.MemberNotFound(request.NomineeStudentId);
        if (nominee.Role == MemberRoles.Leader)
        {
            throw TeamErrors.Invalid("nomineeStudentId", "Người này đang là trưởng nhóm.");
        }
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason?.Length > LeaderVoteRules.MaxReasonLength)
        {
            throw TeamErrors.Invalid("reason", $"Lý do tối đa {LeaderVoteRules.MaxReasonLength} ký tự.");
        }
        await rounds.RequireMembershipRoundAsync(team, ct);
        if (await ResolveOpenAsync(team, current, ct) is not null)
        {
            throw new ConflictException("leader-vote-open", "Nhóm đang có đề nghị", "Nhóm đang có một đề nghị đổi nhóm trưởng chưa kết thúc.");
        }

        var now = clock.GetUtcNow();
        var vote = new LeaderVote
        {
            Id = Guid.NewGuid(), TeamId = team.Id, ProposedByStudentId = me.StudentId, NomineeStudentId = nominee.StudentId, Reason = reason,
            CreatedAt = now, ExpiresAt = now + LeaderVoteRules.Lifetime,
        };
        votes.Add(vote);
        votes.Add(new LeaderVoteBallot { VoteId = vote.Id, StudentId = me.StudentId, Approve = true, VotedAt = now });
        audit.Add(team.Id, "LeaderVote", vote.Id, "LEADER_VOTE_OPENED", user.ToAuditActor(), newValues: new { nominee = nominee.StudentCode, reason });
        members.AddEvent(LeaderVoteEventTypes.LeaderVoteOpened, team.Id, new LeaderVoteOpened(
            vote.Id, team.Id, team.SemesterId, team.Name, me.StudentId, me.FullName, nominee.StudentId, nominee.FullName, vote.ExpiresAt,
            current.Where(m => m.StudentId != me.StudentId).Select(m => m.StudentId).ToList()));
        await members.SaveChangesAsync(ct);
        return await ToResponseAsync(vote, current, me, ct);
    }

    public async Task<LeaderVoteResponse> CastBallotAsync(Guid teamId, Guid voteId, CastBallotRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var (team, current, me) = await LoadAsync(teamId, user, ct);
        await rounds.RequireMembershipRoundAsync(team, ct);
        var vote = await RequireOpenAsync(team, current, voteId, ct);

        var ballots = await votes.ListBallotsAsync(vote.Id, ct);
        var now = clock.GetUtcNow();
        if (ballots.FirstOrDefault(b => b.StudentId == me.StudentId) is { } mine)
        {
            (mine.Approve, mine.VotedAt) = (request.Approve, now);
        }
        else
        {
            var ballot = new LeaderVoteBallot { VoteId = vote.Id, StudentId = me.StudentId, Approve = request.Approve, VotedAt = now };
            votes.Add(ballot);
            ballots.Add(ballot);
        }

        var counted = Counted(ballots, current);
        switch (LeaderVoteRules.Outcome(current.Count, counted.Count(b => b.Approve), counted.Count))
        {
            case LeaderVoteStatuses.Passed:
                ChangeLeader(team, current, vote);
                closer.Close(vote, team, current, LeaderVoteStatuses.Passed);
                break;
            case LeaderVoteStatuses.Rejected:
                closer.Close(vote, team, current, LeaderVoteStatuses.Rejected);
                break;
        }
        await members.SaveChangesAsync(ct);
        return Map(vote, counted, current, me);
    }

    public async Task WithdrawAsync(Guid teamId, Guid voteId, ClaimsPrincipal user, CancellationToken ct)
    {
        var (team, current, me) = await LoadAsync(teamId, user, ct);
        var vote = await RequireOpenAsync(team, current, voteId, ct);
        if (vote.ProposedByStudentId != me.StudentId)
        {
            throw new ForbiddenException(title: "Chỉ người đề nghị mới rút được");
        }
        closer.Close(vote, team, current, LeaderVoteStatuses.Cancelled);
        await members.SaveChangesAsync(ct);
    }

    private void ChangeLeader(TeamEntity team, List<TeamMember> current, LeaderVote vote)
    {
        var leader = current.Single(m => m.Role == MemberRoles.Leader);
        var nominee = current.Single(m => m.StudentId == vote.NomineeStudentId);
        (leader.Role, nominee.Role) = (MemberRoles.Member, MemberRoles.Leader);
        audit.Add(team.Id, "TeamMember", nominee.StudentId, "LEADER_CHANGED", VoteActor, new { leader = leader.StudentCode }, new { leader = nominee.StudentCode });
        members.AddEvent(MemberEventTypes.LeaderChanged, team.Id, new LeaderChanged(team.Id, leader.StudentId, nominee.StudentId));
    }

    /// <summary>Đề nghị còn hiệu lực; đóng đề nghị đã hết hạn, nhóm đã chốt, người được đề cử không còn trong nhóm hoặc đã thành trưởng nhóm.</summary>
    private async Task<LeaderVote?> ResolveOpenAsync(TeamEntity team, List<TeamMember> current, CancellationToken ct)
    {
        if (await votes.GetOpenAsync(team.Id, ct) is not { } vote)
        {
            return null;
        }
        var nominee = current.FirstOrDefault(m => m.StudentId == vote.NomineeStudentId);
        var closeAs = clock.GetUtcNow() >= vote.ExpiresAt ? LeaderVoteStatuses.Expired
            : team.Status == TeamStatuses.Finalized || nominee is null || nominee.Role == MemberRoles.Leader ? LeaderVoteStatuses.Cancelled
            : null;
        if (closeAs is null)
        {
            return vote;
        }
        closer.Close(vote, team, current, closeAs);
        await members.SaveChangesAsync(ct);
        return null;
    }

    private async Task<LeaderVote> RequireOpenAsync(TeamEntity team, List<TeamMember> current, Guid voteId, CancellationToken ct)
    {
        var vote = await ResolveOpenAsync(team, current, ct);
        return vote?.Id == voteId ? vote : throw new ConflictException("leader-vote-closed", "Đề nghị đã kết thúc", "Đề nghị này đã kết thúc, tải lại để xem kết quả.");
    }

    private async Task<(TeamEntity Team, List<TeamMember> Members, TeamMember Me)> LoadAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct)
    {
        var studentId = user.GetStudentId() ?? throw TeamErrors.StudentsOnly();
        var team = await teams.GetAsync(teamId, ct);
        if (team is null || team.Status == TeamStatuses.Disbanded)
        {
            throw TeamErrors.TeamNotFound(teamId);
        }
        var current = await members.ListAsync(teamId, ct);
        var me = current.FirstOrDefault(m => m.StudentId == studentId) ?? throw TeamErrors.NotMember();
        return (team, current, me);
    }

    private async Task<LeaderVoteResponse> ToResponseAsync(LeaderVote vote, List<TeamMember> current, TeamMember me, CancellationToken ct) =>
        Map(vote, Counted(await votes.ListBallotsAsync(vote.Id, ct), current), current, me);

    /// <summary>Phiếu của người đã rời nhóm không được tính.</summary>
    private static List<LeaderVoteBallot> Counted(IEnumerable<LeaderVoteBallot> ballots, List<TeamMember> current) =>
        ballots.Where(b => current.Any(m => m.StudentId == b.StudentId)).ToList();

    private static LeaderVoteResponse Map(LeaderVote vote, List<LeaderVoteBallot> counted, List<TeamMember> current, TeamMember me)
    {
        LeaderVotePerson? Person(Guid id) => current.FirstOrDefault(m => m.StudentId == id) is { } m ? new(m.StudentId, m.FullName) : null;
        return new(vote.Id, vote.TeamId, vote.Status, Person(vote.NomineeStudentId), Person(vote.ProposedByStudentId), vote.Reason, vote.CreatedAt, vote.ExpiresAt,
            counted.Count(b => b.Approve), counted.Count(b => !b.Approve), LeaderVoteRules.RequiredApprovals(current.Count), current.Count,
            counted.FirstOrDefault(b => b.StudentId == me.StudentId)?.Approve, vote.Status == LeaderVoteStatuses.Open && vote.ProposedByStudentId == me.StudentId);
    }

    private static ConflictException NotAllowed(string detail) => new("leader-vote-not-allowed", "Không mở được đề nghị", detail);
}

public static class LeaderVotesModule
{
    /// <summary>Module LeaderVotes (BE-88): bỏ phiếu đổi nhóm trưởng.</summary>
    public static IServiceCollection AddLeaderVotesModule(this IServiceCollection services)
    {
        services.AddScoped<ILeaderVoteRepository, LeaderVoteRepository>();
        services.AddScoped<LeaderVoteCloser>();
        services.AddScoped<ILeaderVoteService, LeaderVoteService>();
        return services;
    }
}
