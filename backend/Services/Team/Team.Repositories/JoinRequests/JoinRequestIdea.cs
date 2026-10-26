// Sinh từ deploy/db/03_team_db.sql (BE-24). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Team.Repositories.JoinRequests;

/// <summary>Bảng <c>team.join_request_ideas</c>.</summary>
public class JoinRequestIdea
{
    public Guid JoinRequestId { get; set; }
    public short Position { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}
