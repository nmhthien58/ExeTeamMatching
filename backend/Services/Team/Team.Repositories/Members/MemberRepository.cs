using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.Context;
using Team.Repositories.Invitations;
using Team.Repositories.JoinRequests;
using Team.Repositories.Teams;

namespace Team.Repositories.Members;

public interface IMemberRepository
{
    /// <summary>Thành viên của nhóm (được theo dõi), trưởng nhóm trước.</summary>
    Task<List<TeamMember>> ListAsync(Guid teamId, CancellationToken ct);

    /// <summary>Lời mời nhận được và yêu cầu đã gửi của sinh viên còn chờ trong học kỳ (được theo dõi) – để hủy khi sinh viên đã vào nhóm.</summary>
    Task<(List<Invitation> Invitations, List<JoinRequest> JoinRequests)> ListPendingOfStudentAsync(Guid semesterId, Guid studentId, CancellationToken ct);

    void Add(object entity);

    void Remove(object entity);

    /// <summary>Ghi sự kiện exe.team vào outbox (key = teamId).</summary>
    void AddEvent<TPayload>(string eventType, Guid teamId, TPayload payload);

    Task SaveChangesAsync(CancellationToken ct);
}

public sealed class MemberRepository(TeamDbContext db) : IMemberRepository
{
    public async Task<List<TeamMember>> ListAsync(Guid teamId, CancellationToken ct) =>
        (await db.Set<TeamMember>().Where(m => m.TeamId == teamId).ToListAsync(ct))
            .OrderByDescending(m => m.Role == "LEADER").ThenBy(m => m.JoinedAt).ToList();

    public async Task<(List<Invitation> Invitations, List<JoinRequest> JoinRequests)> ListPendingOfStudentAsync(
        Guid semesterId, Guid studentId, CancellationToken ct)
    {
        var semesterTeams = db.Set<TeamEntity>().Where(t => t.SemesterId == semesterId).Select(t => t.Id);
        var invitations = await db.Set<Invitation>()
            .Where(i => i.InviteeStudentId == studentId && i.Status == "PENDING" && semesterTeams.Contains(i.TeamId)).ToListAsync(ct);
        var requests = await db.Set<JoinRequest>()
            .Where(r => r.StudentId == studentId && r.Status == "PENDING" && semesterTeams.Contains(r.TeamId)).ToListAsync(ct);
        return (invitations, requests);
    }

    public void Add(object entity) => db.Add(entity);

    public void Remove(object entity) => db.Remove(entity);

    public void AddEvent<TPayload>(string eventType, Guid teamId, TPayload payload) =>
        db.AddOutboxMessage(Topics.Team, eventType, "Team", teamId, payload);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
