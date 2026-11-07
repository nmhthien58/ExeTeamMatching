namespace Team.Repositories.LeaderVotes;

/// <summary>Bảng <c>team.leader_votes</c> – đề nghị đổi nhóm trưởng (BE-88).</summary>
public class LeaderVote
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Guid ProposedByStudentId { get; set; }
    public Guid NomineeStudentId { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = "OPEN";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
}

/// <summary>Bảng <c>team.leader_vote_ballots</c> – mỗi thành viên một phiếu, đổi được khi đề nghị còn mở.</summary>
public class LeaderVoteBallot
{
    public Guid VoteId { get; set; }
    public Guid StudentId { get; set; }
    public bool Approve { get; set; }
    public DateTimeOffset VotedAt { get; set; }
}
