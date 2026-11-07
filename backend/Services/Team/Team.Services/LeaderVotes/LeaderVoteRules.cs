using Team.Repositories.LeaderVotes;
using Team.Repositories.Members;
using Team.Repositories.Teams;
using Team.Services.Members;

namespace Team.Services.LeaderVotes;

public static class LeaderVoteStatuses
{
    public const string Open = "OPEN";
    public const string Passed = "PASSED";
    public const string Rejected = "REJECTED";
    public const string Expired = "EXPIRED";
    public const string Cancelled = "CANCELLED";
}

public static class LeaderVoteEventTypes
{
    public const string LeaderVoteOpened = "LeaderVoteOpened";
    public const string LeaderVoteClosed = "LeaderVoteClosed";
}

// Payload exe.team (docs/contracts/events.md), key = teamId.
/// <summary><c>RecipientStudentIds</c>: mọi thành viên trừ người đề nghị.</summary>
public sealed record LeaderVoteOpened(
    Guid VoteId, Guid TeamId, Guid SemesterId, string TeamName, Guid ProposedByStudentId, string ProposerName, Guid NomineeStudentId, string NomineeName,
    DateTimeOffset ExpiresAt, IReadOnlyList<Guid> RecipientStudentIds);

/// <summary><c>Status</c>: PASSED | REJECTED | EXPIRED | CANCELLED.</summary>
public sealed record LeaderVoteClosed(Guid VoteId, Guid TeamId, Guid SemesterId, string TeamName, string Status, string? NomineeName, IReadOnlyList<Guid> MemberStudentIds);

/// <summary>Quy tắc bỏ phiếu (docs/de-xuat-bau-nhom-truong.md mục 3).</summary>
public static class LeaderVoteRules
{
    public const int MaxReasonLength = 300;

    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(48);

    /// <summary>Quá nửa số thành viên: nhóm 4 cần 3, nhóm 5 cần 3, nhóm 6 cần 4.</summary>
    public static int RequiredApprovals(int memberCount) => memberCount / 2 + 1;

    /// <summary>Kết quả sau khi đếm: PASSED khi đủ, REJECTED khi phiếu còn lại không thể đủ, null khi chưa ngã ngũ.</summary>
    public static string? Outcome(int memberCount, int approvals, int ballots)
    {
        var required = RequiredApprovals(memberCount);
        if (approvals >= required)
        {
            return LeaderVoteStatuses.Passed;
        }
        return approvals + (memberCount - ballots) < required ? LeaderVoteStatuses.Rejected : null;
    }
}

/// <summary>Đóng đề nghị và phát <see cref="LeaderVoteClosed"/>; dùng chung cho bỏ phiếu, rời nhóm và chuyển quyền. Người gọi tự lưu.</summary>
public sealed class LeaderVoteCloser(ILeaderVoteRepository votes, IMemberRepository members, TimeProvider clock)
{
    public void Close(LeaderVote vote, TeamEntity team, IReadOnlyCollection<TeamMember> current, string status)
    {
        (vote.Status, vote.ClosedAt) = (status, clock.GetUtcNow());
        var nominee = current.FirstOrDefault(m => m.StudentId == vote.NomineeStudentId);
        members.AddEvent(LeaderVoteEventTypes.LeaderVoteClosed, team.Id, new LeaderVoteClosed(
            vote.Id, team.Id, team.SemesterId, team.Name, status, nominee?.FullName, current.Select(m => m.StudentId).ToList()));
    }

    /// <summary>Hủy đề nghị đang mở của nhóm; <paramref name="onlyNominee"/> khác null thì chỉ hủy khi người đó là người được đề cử.</summary>
    public async Task CancelOpenAsync(TeamEntity team, IReadOnlyCollection<TeamMember> current, Guid? onlyNominee, CancellationToken ct)
    {
        if (await votes.GetOpenAsync(team.Id, ct) is { } vote && (onlyNominee is null || vote.NomineeStudentId == onlyNominee))
        {
            Close(vote, team, current, LeaderVoteStatuses.Cancelled);
        }
    }
}
