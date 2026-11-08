// Sinh từ deploy/db/03_team_db.sql (BE-24). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Team.Repositories.ChangeRequests;

/// <summary>Bảng <c>team.change_request_items</c>. <see cref="ChangeRequestId"/> null = đã được hai bên đồng ý, chưa đưa vào yêu cầu.</summary>
public class ChangeRequestItem
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Guid? ChangeRequestId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public Guid? StudentId { get; set; }
    public string? StudentCode { get; set; }
    public string? FullName { get; set; }
    public int MajorId { get; set; } = 1;
    public int? SpecializationId { get; set; }
    public string? NewProjectName { get; set; }
    public string? NewProjectDescription { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
