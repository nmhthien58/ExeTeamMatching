using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Notification.Worker.Data;
using Notification.Worker.Email;

namespace Notification.Tests;

public sealed class SmtpEmailWorkerTests
{
    [Fact]
    public async Task Disabled_email_is_removed_from_queue_before_smtp()
    {
        using var services = Services();
        var notificationId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        await SeedAsync(services, notificationId, recipientId, emailEnabled: false);
        using var worker = Worker(services);

        await worker.StartAsync(default);
        try
        {
            await WaitUntilAsync(services, async db => !await db.EmailLogs.AnyAsync());
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            Assert.Single(await db.Notifications.ToListAsync());
        }
        finally { await worker.StopAsync(default); }
    }

    [Fact]
    public async Task Smtp_failure_records_attempt_and_keeps_email_for_retry()
    {
        using var services = Services();
        var notificationId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        await SeedAsync(services, notificationId, recipientId, emailEnabled: true);
        using var worker = Worker(services);

        await worker.StartAsync(default);
        try
        {
            await WaitUntilAsync(services, db => db.EmailLogs.AnyAsync(x => x.Attempts == 1));
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var log = await db.EmailLogs.SingleAsync();
            Assert.Equal("FAILED", log.Status);
            Assert.NotEmpty(log.LastError!);
            Assert.Null(log.SentAt);
        }
        finally { await worker.StopAsync(default); }
    }

    private static ServiceProvider Services()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<NotificationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        return services.BuildServiceProvider();
    }

    private static SmtpEmailWorker Worker(ServiceProvider services)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "127.0.0.1",
            ["Smtp:Port"] = "1",
            ["Smtp:FromEmail"] = "no-reply@example.test",
        }).Build();
        return new SmtpEmailWorker(services.GetRequiredService<IServiceScopeFactory>(), config,
            NullLogger<SmtpEmailWorker>.Instance);
    }

    private static async Task SeedAsync(ServiceProvider services, Guid notificationId, Guid recipientId,
        bool emailEnabled)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        db.Contacts.Add(new Contact
        {
            Id = recipientId, PersonType = "STUDENT", Email = "student@example.test",
            FullName = "Sinh viên", EmailEnabled = emailEnabled,
        });
        db.Notifications.Add(new UserNotification
        {
            Id = notificationId, RecipientType = "STUDENT", RecipientId = recipientId,
            Type = "InvitationSent", Title = "Lời mời", Content = "Bạn có lời mời",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        db.EmailLogs.Add(new EmailLog
        {
            Id = Guid.NewGuid(), NotificationId = notificationId, SourceEventId = Guid.NewGuid(),
            ToEmail = "student@example.test", Subject = "Lời mời", TemplateCode = "InvitationSent",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static async Task WaitUntilAsync(ServiceProvider services,
        Func<NotificationDbContext, Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!timeout.IsCancellationRequested)
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            if (await condition(db)) return;
            await Task.Delay(25, timeout.Token);
        }
        throw new TimeoutException("SMTP worker did not process the queued email");
    }
}
