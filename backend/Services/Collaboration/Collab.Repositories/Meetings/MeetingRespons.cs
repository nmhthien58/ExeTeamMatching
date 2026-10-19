// Sinh từ deploy/db/06_collab_db.sql (BE-62). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Collab.Repositories.Meetings;

/// <summary>Bảng <c>collab.meeting_responses</c>.</summary>
public class MeetingRespons
{
    public Guid SlotId { get; set; }
    public string PersonType { get; set; } = string.Empty;
    public Guid PersonId { get; set; }
    public string Answer { get; set; } = string.Empty;
    public DateTimeOffset AnsweredAt { get; set; }
}
