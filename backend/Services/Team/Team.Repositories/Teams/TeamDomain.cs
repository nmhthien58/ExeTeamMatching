// Sinh từ deploy/db/03_team_db.sql (BE-24). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Team.Repositories.Teams;

/// <summary>Bảng <c>team.team_domains</c>.</summary>
public class TeamDomain
{
    public Guid TeamId { get; set; }
    public int DomainId { get; set; }
    public string DomainCode { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}
