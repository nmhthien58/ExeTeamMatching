using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Notification.Worker.Data;
using Notification.Worker.Push;

namespace Notification.Tests;

public sealed class FcmPushTests
{
    [Fact]
    public async Task Dispatcher_sends_type_and_id_to_every_device()
    {
        await using var db = Database();
        var recipientId = Guid.NewGuid();
        db.DeviceTokens.AddRange(Token(recipientId, "device-1"), Token(recipientId, "device-2"));
        await db.SaveChangesAsync();
        var sender = new FakeFcmSender();
        var notification = Notification(recipientId);

        await new FcmPushDispatcher(db, sender, NullLogger<FcmPushDispatcher>.Instance)
            .SendAsync([notification], default);

        Assert.Equal(2, sender.Messages.Count);
        Assert.All(sender.Messages, message =>
        {
            Assert.Equal(notification.Type, message.Data["type"]);
            Assert.Equal(notification.Id.ToString(), message.Data["id"]);
            Assert.Equal(notification.LinkUrl, message.Data["linkUrl"]);
        });
    }

    [Fact]
    public async Task Dispatcher_deletes_invalid_token_but_keeps_notification_flow_alive()
    {
        await using var db = Database();
        var recipientId = Guid.NewGuid();
        db.DeviceTokens.Add(Token(recipientId, "invalid"));
        await db.SaveChangesAsync();
        var sender = new FakeFcmSender(FcmSendResult.InvalidToken);

        await new FcmPushDispatcher(db, sender, NullLogger<FcmPushDispatcher>.Instance)
            .SendAsync([Notification(recipientId)], default);

        Assert.Empty(await db.DeviceTokens.ToListAsync());
    }

    [Fact]
    public async Task Dispatcher_sender_failure_does_not_delete_token_or_throw()
    {
        await using var db = Database();
        var recipientId = Guid.NewGuid();
        db.DeviceTokens.Add(Token(recipientId, "retry-later"));
        await db.SaveChangesAsync();
        var sender = new FakeFcmSender(FcmSendResult.Failed);

        await new FcmPushDispatcher(db, sender, NullLogger<FcmPushDispatcher>.Instance)
            .SendAsync([Notification(recipientId)], default);

        Assert.Single(await db.DeviceTokens.ToListAsync());
    }

    [Fact]
    public async Task Dispatcher_finds_same_device_for_lecturer_mentor_account_by_contact_email()
    {
        await using var db = Database();
        var lecturerId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();
        db.Contacts.AddRange(
            new Contact { Id = lecturerId, PersonType = "LECTURER", Email = "advisor@example.test", FullName = "Cố vấn" },
            new Contact { Id = mentorId, PersonType = "MENTOR", Email = "advisor@example.test", FullName = "Cố vấn" });
        db.DeviceTokens.Add(Token(lecturerId, "shared-device"));
        await db.SaveChangesAsync();
        var sender = new FakeFcmSender();

        await new FcmPushDispatcher(db, sender, NullLogger<FcmPushDispatcher>.Instance)
            .SendAsync([Notification(mentorId)], default);

        Assert.Single(sender.Messages);
        Assert.Equal("shared-device", sender.Messages[0].Token);
    }

    private static NotificationDbContext Database() => new(new DbContextOptionsBuilder<NotificationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static DeviceToken Token(Guid userId, string token) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Token = token,
        Platform = "android",
        CreatedAt = DateTimeOffset.UtcNow,
        LastSeenAt = DateTimeOffset.UtcNow,
    };

    private static UserNotification Notification(Guid recipientId) => new()
    {
        Id = Guid.NewGuid(),
        RecipientType = "STUDENT",
        RecipientId = recipientId,
        Type = "ChangeRequestApproved",
        Title = "Đã duyệt",
        Content = "Yêu cầu đã được duyệt.",
        LinkUrl = "app://change-requests",
        SourceEventId = Guid.NewGuid(),
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private sealed class FakeFcmSender(FcmSendResult result = FcmSendResult.Sent) : IFcmSender
    {
        public List<FcmPushMessage> Messages { get; } = [];

        public Task<FcmSendResult> SendAsync(FcmPushMessage message, CancellationToken ct)
        {
            Messages.Add(message);
            return Task.FromResult(result);
        }
    }
}
