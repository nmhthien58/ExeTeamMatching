using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using Notification.Worker.Data;

namespace Notification.Worker.Email;

/// <summary>Gửi lại email chưa gửi; EmailLog được ghi cùng transaction với Notification.</summary>
public sealed class SmtpEmailWorker(IServiceScopeFactory scopes, IConfiguration config,
    ILogger<SmtpEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Không đọc được hàng đợi email"); }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task SendPendingAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var pending = await db.EmailLogs.Where(x => x.Status != "SENT" && x.Attempts < 5)
            .OrderBy(x => x.CreatedAt).Take(20).ToListAsync(ct);
        foreach (var log in pending)
        {
            var notification = await db.Notifications.FirstOrDefaultAsync(x => x.Id == log.NotificationId, ct);
            if (notification?.RecipientId is not { } recipientId)
            {
                db.EmailLogs.Remove(log);
                await db.SaveChangesAsync(ct);
                continue;
            }
            var contact = await db.Contacts.FindAsync([recipientId], ct);
            if (contact is null || !contact.EmailEnabled ||
                !string.Equals(contact.Email, log.ToEmail, StringComparison.OrdinalIgnoreCase))
            {
                db.EmailLogs.Remove(log);
                await db.SaveChangesAsync(ct);
                continue;
            }
            try
            {
                await SendAsync(log, notification, ct);
                log.Status = "SENT";
                log.SentAt = DateTimeOffset.UtcNow;
                log.LastError = null;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                log.Status = "FAILED";
                log.LastError = ex.Message;
                logger.LogWarning(ex, "Gửi email {EmailLogId} thất bại lần {Attempt}", log.Id, log.Attempts + 1);
            }
            log.Attempts++;
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task SendAsync(EmailLog log, UserNotification notification, CancellationToken ct)
    {
        var host = config["Smtp:Host"] ?? "localhost";
        var port = config.GetValue("Smtp:Port", 1025);
        var from = config["Smtp:FromEmail"] ?? "no-reply@exe.local";
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(log.ToEmail));
        message.Subject = log.Subject;
        message.Body = new TextPart("plain")
        {
            Text = notification.LinkUrl is null ? notification.Content : $"{notification.Content}\n\n{notification.LinkUrl}",
        };
        using var client = new SmtpClient();
        var secure = config.GetValue("Smtp:StartTls", false)
            ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        await client.ConnectAsync(host, port, secure, ct);
        if (config["Smtp:Username"] is { Length: > 0 } username)
            await client.AuthenticateAsync(username, config["Smtp:Password"] ?? "", ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
