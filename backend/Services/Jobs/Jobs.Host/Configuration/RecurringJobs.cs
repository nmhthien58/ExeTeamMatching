using Hangfire;
using Hangfire.Dashboard;
using Jobs.Host.Jobs;

namespace Jobs.Host.Configuration;

/// <summary>Lịch chạy định kỳ (PROJECT_CONTEXT §5.6); id cố định nên khởi động lại không tạo trùng.</summary>
public static class RecurringJobs
{
    public static void Register(IRecurringJobManager manager, JobsOptions options)
    {
        var schedule = new RecurringJobOptions { TimeZone = options.ResolveTimeZone() };

        manager.AddOrUpdate<DeadlineReminderJob>("deadline-reminder", j => j.RunAsync(CancellationToken.None), options.DailyCron, schedule);
        manager.AddOrUpdate<InvitationExpiryJob>("invitation-expiry", j => j.RunAsync(CancellationToken.None), options.HourlyCron, schedule);
        manager.AddOrUpdate<MeetingReminderJob>("meeting-reminder", j => j.RunAsync(CancellationToken.None), options.MeetingReminderCron, schedule);
        manager.AddOrUpdate<TaskDueReminderJob>("task-due-reminder", j => j.RunAsync(CancellationToken.None), options.DailyCron, schedule);
    }
}

/// <summary>Dashboard /hangfire: Development mở cho mọi máy (demo trong Docker), môi trường khác chỉ truy cập từ máy chủ.</summary>
public sealed class DashboardAccessFilter(bool allowRemote) : IDashboardAuthorizationFilter
{
    private static readonly LocalRequestsOnlyAuthorizationFilter LocalOnly = new();

    public bool Authorize(DashboardContext context) => allowRemote || LocalOnly.Authorize(context);
}
