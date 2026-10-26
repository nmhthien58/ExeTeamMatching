// Sinh từ deploy/db/03_team_db.sql (BE-24). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Team.Repositories.JoinRequests;

/// <summary>Bảng <c>team.join_requests</c>. Trigger kiểm tra lúc COMMIT: 1–3 ý tưởng hoặc <see cref="HasNoIdea"/>.</summary>
public class JoinRequest
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Guid StudentId { get; set; }
    public string? Message { get; set; }
    public bool HasNoIdea { get; set; }
    public string Status { get; set; } = "PENDING";
    public Guid? ResolvedByStudentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public uint Version { get; set; }
}
