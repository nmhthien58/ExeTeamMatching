using Jobs.Host.Clients;

namespace Jobs.Host.Jobs;

/// <summary>
/// SlotBookingCloseJob (BE-82, rule 25): định kỳ gọi Collaboration đóng các lịch họp hằng tuần đã tới hạn chọn slot – xếp nhóm chưa chọn
/// vào slot trống và sinh họp hằng tuần. Hạn nằm ở từng lịch nên chạy theo chu kỳ thay vì hẹn giờ từng lịch; gọi lại không tạo trùng.
/// </summary>
public sealed class SlotBookingCloseJob(ICollabClient collab, ILogger<SlotBookingCloseJob> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var result = await collab.CloseSlotBookingAsync(ct);
        if (result.TeamsWithoutSlot > 0)
        {
            logger.LogWarning("Còn {Teams} nhóm chưa có slot họp hằng tuần: giảng viên cần thêm khung giờ", result.TeamsWithoutSlot);
        }
        logger.LogInformation("Đóng chọn slot: xếp {AutoBooked} nhóm, tạo {Meetings} họp", result.AutoBooked, result.MeetingsCreated);
    }
}
