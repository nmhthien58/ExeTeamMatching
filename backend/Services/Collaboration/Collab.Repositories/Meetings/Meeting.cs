// Sinh từ deploy/db/06_collab_db.sql (BE-62). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Collab.Repositories.Meetings;

/// <summary>Bảng <c>collab.meetings</c>.</summary>
public class Meeting
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Agenda { get; set; }
    public string? Location { get; set; }
    public string OrganizerType { get; set; } = string.Empty;
    public Guid OrganizerId { get; set; }
    public string OrganizerName { get; set; } = string.Empty;
    public string OrganizerRole { get; set; } = string.Empty;
    public bool IsMentorSession { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? ConfirmedSlotId { get; set; }
    public string? CancelReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? ScheduleBookingId { get; set; }
    public short? WeekNo { get; set; }
}
