using Microsoft.EntityFrameworkCore;
using Notification.Worker.Data;

namespace Notification.Worker.Push;

public sealed class FcmPushDispatcher(
    NotificationDbContext db,
    IFcmSender sender,
    ILogger<FcmPushDispatcher> logger)
{
    public async Task SendAsync(IEnumerable<UserNotification> notifications, CancellationToken ct)
    {
        var invalidTokens = new List<DeviceToken>();
        foreach (var notification in notifications.Where(x => x.RecipientId.HasValue))
        {
            try
            {
                var userIds = await RecipientIdsAsync(notification.RecipientId!.Value, ct);
                var tokens = await db.DeviceTokens.Where(x => userIds.Contains(x.UserId)).ToListAsync(ct);
                foreach (var device in tokens)
                {
                    var data = new Dictionary<string, string>
                    {
                        ["type"] = notification.Type,
                        ["id"] = notification.Id.ToString(),
                    };
                    if (!string.IsNullOrWhiteSpace(notification.LinkUrl)) data["linkUrl"] = notification.LinkUrl;
                    var result = await sender.SendAsync(new FcmPushMessage(
                        device.Token, notification.Title, notification.Content, data), ct);
                    if (result == FcmSendResult.InvalidToken) invalidTokens.Add(device);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Không gửi được push cho notification {NotificationId}", notification.Id);
            }
        }

        if (invalidTokens.Count == 0) return;
        db.DeviceTokens.RemoveRange(invalidTokens.DistinctBy(x => x.Id));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không xóa được device token FCM không còn hợp lệ.");
        }
    }

    private async Task<Guid[]> RecipientIdsAsync(Guid recipientId, CancellationToken ct)
    {
        var contact = await db.Contacts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == recipientId, ct);
        if (contact is null) return [recipientId];
        // Giảng viên kiêm mentor có hai external id nhưng dùng cùng một thiết bị/tài khoản.
        return await db.Contacts.AsNoTracking().Where(x => x.Email == contact.Email)
            .Select(x => x.Id).ToArrayAsync(ct);
    }
}
