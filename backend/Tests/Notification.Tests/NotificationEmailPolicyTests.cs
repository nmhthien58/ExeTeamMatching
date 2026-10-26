using BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Notification.Worker.Data;
using Notification.Worker.Events;

namespace Notification.Tests;

public sealed class NotificationEmailPolicyTests
{
    [Theory]
    [InlineData("InvitationAccepted", "leaderStudentId")]
    [InlineData("InvitationDeclined", "leaderStudentId")]
    [InlineData("InvitationExpired", "leaderStudentId")]
    [InlineData("JoinRequestSubmitted", "leaderStudentId")]
    [InlineData("JoinRequestDeclined", "studentId")]
    [InlineData("TopicInterestClosed", "advisorId")]
    public async Task Actionable_result_emails_recipient_only_when_enabled(string eventType, string recipientField)
    {
        foreach (var enabled in new[] { true, false })
        {
            await using var db = new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var recipient = Guid.NewGuid();
            db.Contacts.Add(new Contact
            {
                Id = recipient, PersonType = recipientField == "advisorId" ? "LECTURER" : "STUDENT",
                Email = "recipient@example.test", FullName = "Người nhận", EmailEnabled = enabled,
            });
            await db.SaveChangesAsync();
            var payload = new Dictionary<string, object>
            {
                [recipientField] = recipient, ["advisorKind"] = "LECTURER",
                ["reason"] = "OTHER_ADVISOR_MATCHED",
            };
            var envelope = EventEnvelope.Create(eventType, payload, "team", null);

            await new NotificationEventHandler(db).HandleAsync(envelope, default);
            await new NotificationProcessedEventStore(db).MarkProcessedAsync(envelope, default);

            var notification = Assert.Single(db.Notifications);
            Assert.Equal(recipient, notification.RecipientId);
            if (enabled)
            {
                var email = Assert.Single(db.EmailLogs);
                Assert.Equal(notification.Id, email.NotificationId);
                Assert.Equal("recipient@example.test", email.ToEmail);
            }
            else Assert.Empty(db.EmailLogs);
        }
    }
}
