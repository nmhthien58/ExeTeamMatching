using Microsoft.EntityFrameworkCore;
using Team.Repositories.Context;

namespace Team.Repositories.LeaderVotes;

public interface ILeaderVoteRepository
{
    /// <summary>Đề nghị đang mở của nhóm (được theo dõi); null nếu không có.</summary>
    Task<LeaderVote?> GetOpenAsync(Guid teamId, CancellationToken ct);

    /// <summary>Phiếu của đề nghị (được theo dõi).</summary>
    Task<List<LeaderVoteBallot>> ListBallotsAsync(Guid voteId, CancellationToken ct);

    void Add(object entity);
}

public sealed class LeaderVoteRepository(TeamDbContext db) : ILeaderVoteRepository
{
    public Task<LeaderVote?> GetOpenAsync(Guid teamId, CancellationToken ct) =>
        db.Set<LeaderVote>().SingleOrDefaultAsync(v => v.TeamId == teamId && v.Status == "OPEN", ct);

    public Task<List<LeaderVoteBallot>> ListBallotsAsync(Guid voteId, CancellationToken ct) =>
        db.Set<LeaderVoteBallot>().Where(b => b.VoteId == voteId).ToListAsync(ct);

    public void Add(object entity) => db.Add(entity);
}
