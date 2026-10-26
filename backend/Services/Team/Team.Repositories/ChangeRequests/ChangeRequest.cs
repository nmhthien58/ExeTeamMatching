// Sinh từ deploy/db/03_team_db.sql (BE-24). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Team.Repositories.ChangeRequests;

/// <summary>Bảng <c>team.change_requests</c> – chỉ EXE201 (rule 7).</summary>
public class ChangeRequest
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public string CourseCode { get; set; } = "EXE201";
    public Guid RoundId { get; set; }
    public Guid RequestedByStudentId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public short ChangedMemberCount { get; set; }
    public decimal ChangedPercent { get; set; }
    public string Status { get; set; } = "PENDING";
    public Guid? ReviewedByAccountId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public uint Version { get; set; }
}
