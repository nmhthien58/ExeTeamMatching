// Sinh từ deploy/db/03_team_db.sql (BE-24). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Team.Repositories.Posts;

/// <summary>Bảng <c>team.seeking_posts</c>.</summary>
public class SeekingPost
{
    public Guid Id { get; set; }
    public Guid SemesterId { get; set; }
    public Guid StudentId { get; set; }
    public int SpecializationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "OPEN";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public uint Version { get; set; }
}
