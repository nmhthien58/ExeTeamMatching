// Sinh từ deploy/db/03_team_db.sql (BE-24). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Team.Repositories.Teams;

/// <summary>Bảng <c>team.teams</c>. Tên có hậu tố Entity vì <c>Team</c> trùng namespace gốc.</summary>
public class TeamEntity
{
    public Guid Id { get; set; }
    public Guid SemesterId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? ProjectId { get; set; }
    public string Status { get; set; } = "DRAFT";

    /// <summary>Trigger <c>trg_team_members_count</c> duy trì; EF không ghi cột này.</summary>
    public short MemberCount { get; set; }

    public bool IsInherited { get; set; }
    public Guid? SourceTeamId { get; set; }
    public bool IsAutoAssigned { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Cột hệ thống <c>xmin</c>, dùng làm concurrency token.</summary>
    public uint Version { get; set; }
}
