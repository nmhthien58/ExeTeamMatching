namespace Jobs.Host.Configuration;

/// <summary>Cấu hình Job Service (section <c>Jobs</c>).</summary>
public sealed class JobsOptions
{
    public const string SectionName = "Jobs";

    /// <summary>Múi giờ của lịch chạy (08:00 hằng ngày theo giờ Việt Nam).</summary>
    public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";

    /// <summary>DeadlineReminderJob và TaskDueReminderJob – mặc định 08:00 hằng ngày.</summary>
    public string DailyCron { get; set; } = "0 8 * * *";

    /// <summary>InvitationExpiryJob – mặc định đầu mỗi giờ.</summary>
    public string HourlyCron { get; set; } = "0 * * * *";

    /// <summary>MeetingReminderJob – mặc định mỗi 15 phút.</summary>
    public string MeetingReminderCron { get; set; } = "*/15 * * * *";

    /// <summary>SlotBookingCloseJob – mặc định mỗi 15 phút.</summary>
    public string SlotBookingCloseCron { get; set; } = "*/15 * * * *";

    /// <summary>Quy tắc 1, 2 truyền cho PlanAutoAssignment (0 = mặc định của Matching).</summary>
    public int MinTeamSize { get; set; } = 4;
    public int MaxTeamSize { get; set; } = 6;
    public int MinSpecializations { get; set; } = 2;

    /// <summary>Địa chỉ nội bộ các service (trong Docker là tên container).</summary>
    public string AcademicUrl { get; set; } = "http://localhost:5002";
    public string TeamUrl { get; set; } = "http://localhost:5003";
    public string MatchingUrl { get; set; } = "http://localhost:5004";
    public string CollabUrl { get; set; } = "http://localhost:5007";

    public TimeZoneInfo ResolveTimeZone() =>
        TimeZoneInfo.TryFindSystemTimeZoneById(TimeZone, out var zone) ? zone : TimeZoneInfo.Utc;
}
