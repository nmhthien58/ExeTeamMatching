// Sinh từ deploy/db/03_team_db.sql (BE-24). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Team.Repositories.TopicInterests;

/// <summary>Bảng <c>team.topic_interests</c> – quan tâm của giảng viên / mentor (rule 19).</summary>
public class TopicInterest
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public string AdvisorKind { get; set; } = string.Empty;
    public Guid AdvisorId { get; set; }
    public string AdvisorName { get; set; } = string.Empty;
    public string Status { get; set; } = "OPEN";
    public string? ClosedReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public uint Version { get; set; }
}
