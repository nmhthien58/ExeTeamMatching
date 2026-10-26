// Sinh từ deploy/db/03_team_db.sql (BE-24). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Team.Repositories.Posts;

/// <summary>Bảng <c>team.seeking_post_domains</c>.</summary>
public class SeekingPostDomain
{
    public Guid SeekingPostId { get; set; }
    public int DomainId { get; set; }
    public string DomainCode { get; set; } = string.Empty;
}
