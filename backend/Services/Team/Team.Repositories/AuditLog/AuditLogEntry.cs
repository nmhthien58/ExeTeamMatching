// Sinh từ deploy/db/03_team_db.sql (BE-24). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Team.Repositories.AuditLog;

/// <summary>Bảng <c>team.audit_logs</c> – chỉ thêm, trigger chặn sửa / xóa.</summary>
public class AuditLogEntry
{
    public long Id { get; set; }
    public Guid? TeamId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid? ActorAccountId { get; set; }
    public string ActorRole { get; set; } = string.Empty;

    /// <summary>Tên lúc thực hiện; tên nằm ở Identity nên lưu kèm. Null = hệ thống hoặc log cũ.</summary>
    public string? ActorName { get; set; }

    /// <summary>JSON (cột jsonb).</summary>
    public string? OldValues { get; set; }

    /// <summary>JSON (cột jsonb).</summary>
    public string? NewValues { get; set; }

    public string? Reason { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
