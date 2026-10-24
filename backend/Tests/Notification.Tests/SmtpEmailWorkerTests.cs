using System.Net;
using System.Net.Sockets;
using System.Text;
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
    public async Task Smtp_success_marks_email_sent()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var smtp = ServeOneEmailAsync(listener, timeout.Token);
        using var services = Services();
        await SeedAsync(services, Guid.NewGuid(), Guid.NewGuid(), emailEnabled: true);
        using var worker = Worker(services, ((IPEndPoint)listener.LocalEndpoint).Port);

        await worker.StartAsync(default);
        try
        {
            await WaitUntilAsync(services, db => db.EmailLogs.AnyAsync(x => x.Status == "SENT"));
            await smtp;
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var log = await db.EmailLogs.SingleAsync();
            Assert.Equal(1, log.Attempts);
            Assert.NotNull(log.SentAt);
            Assert.Null(log.LastError);
        }
        finally { await worker.StopAsync(default); }
    }

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
    public async Task Changed_email_address_is_removed_from_queue_before_smtp()
    {
        using var services = Services();
        await SeedAsync(services, Guid.NewGuid(), Guid.NewGuid(), emailEnabled: true);
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            (await db.Contacts.SingleAsync()).Email = "new@example.test";
            await db.SaveChangesAsync();
        }
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

    private static SmtpEmailWorker Worker(ServiceProvider services, int port = 1)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "127.0.0.1",
            ["Smtp:Port"] = port.ToString(),
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

    private static async Task ServeOneEmailAsync(TcpListener listener, CancellationToken ct)
    {
        using var client = await listener.AcceptTcpClientAsync(ct);
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true)
        {
            NewLine = "\r\n", AutoFlush = true,
        };
        await writer.WriteLineAsync("220 localhost SMTP ready");
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (line.StartsWith("EHLO ", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("250-localhost");
                await writer.WriteLineAsync("250 OK");
            }
            else if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("354 End data with <CRLF>.<CRLF>");
                while (await reader.ReadLineAsync(ct) is { } data && data != ".") { }
                await writer.WriteLineAsync("250 Message accepted");
            }
            else if (line.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("221 Bye");
                return;
            }
            else await writer.WriteLineAsync("250 OK");
        }
    }
}
