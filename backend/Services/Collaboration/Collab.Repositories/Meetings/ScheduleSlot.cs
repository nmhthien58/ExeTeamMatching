// Sinh từ deploy/db/06_collab_db.sql (BE-62). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Collab.Repositories.Meetings;

/// <summary>Bảng <c>collab.schedule_slots</c>.</summary>
public class ScheduleSlot
{
    public Guid Id { get; set; }
    public Guid ScheduleId { get; set; }
    public Guid WindowId { get; set; }
    public short DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}
