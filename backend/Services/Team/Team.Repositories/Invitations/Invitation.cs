// Sinh từ deploy/db/03_team_db.sql (BE-24). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Team.Repositories.Invitations;

/// <summary>Bảng <c>team.invitations</c>.</summary>
public class Invitation
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Guid InviteeStudentId { get; set; }
    public Guid InvitedByStudentId { get; set; }
    public string? Message { get; set; }
    public string Status { get; set; } = "PENDING";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public uint Version { get; set; }
}
