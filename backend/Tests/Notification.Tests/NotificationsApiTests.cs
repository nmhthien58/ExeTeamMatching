using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Auth;
using BuildingBlocks.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Notification.Worker.Controllers;
using Notification.Worker.Data;
using Notification.Worker.Email;
using Notification.Worker.Notifications;

namespace Notification.Tests;

public sealed class NotificationsApiTests
{
    [Fact]
    public async Task Http_api_requires_token_and_scopes_list_count_read_to_student()
    {
        using var factory = new NotificationApiFactory();
        var mine = Guid.NewGuid();
        var other = Guid.NewGuid();
        var myId = Guid.NewGuid();
        var myOtherId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Notifications.AddRange(
                Item(myId, mine), Item(myOtherId, mine), Item(otherId, other));
        });
        var client = factory.ClientFor(mine);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await factory.CreateClient().GetAsync("/api/notifications")).StatusCode);
        var listed = await client.GetFromJsonAsync<NotificationPage>("/api/notifications");
        Assert.Equal(2, listed!.Items.Count);
        Assert.All(listed.Items, item => Assert.Contains(item.Id, new[] { myId, myOtherId }));
        var count = await client.GetFromJsonAsync<UnreadCount>("/api/notifications/unread-count");
        Assert.Equal(2, count!.Count);
        var notMine = await client.PatchAsync($"/api/notifications/{otherId}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, notMine.StatusCode);
        var own = await client.PatchAsync($"/api/notifications/{myId}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, own.StatusCode);
        Assert.Equal(1, (await client.GetFromJsonAsync<UnreadCount>(
            "/api/notifications/unread-count"))!.Count);
        var readAll = await client.PatchAsync("/api/notifications/read-all", null);
        Assert.Equal(HttpStatusCode.NoContent, readAll.StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<UnreadCount>(
            "/api/notifications/unread-count"))!.Count);
        Assert.False(await factory.QueryAsync(async db =>
            (await db.Notifications.SingleAsync(x => x.Id == otherId)).IsRead));
    }

    [Fact]
    public async Task Http_api_validates_paging_and_updates_email_preferences()
    {
        using var factory = new NotificationApiFactory();
        var studentId = Guid.NewGuid();
        await factory.SeedAsync(db => db.Contacts.Add(new Contact
        {
            Id = studentId, PersonType = "STUDENT", Email = "student@example.test",
            FullName = "Sinh viên", EmailEnabled = true,
        }));
        var client = factory.ClientFor(studentId);

        var invalid = await client.GetAsync("/api/notifications?page=0&pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType?.MediaType);
        using (var body = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync()))
        {
            Assert.True(body.RootElement.TryGetProperty("errors", out var errors));
            Assert.True(errors.TryGetProperty("page", out _));
            Assert.True(errors.TryGetProperty("pageSize", out _));
        }
        Assert.True((await client.GetFromJsonAsync<EmailPreferences>(
            "/api/notifications/preferences"))!.EmailEnabled);
        var updated = await client.PutAsJsonAsync("/api/notifications/preferences",
            new EmailPreferences(false));
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        Assert.False((await client.GetFromJsonAsync<EmailPreferences>(
            "/api/notifications/preferences"))!.EmailEnabled);
    }

    private static UserNotification Item(Guid id, Guid recipientId) => new()
    {
        Id = id, RecipientType = "STUDENT", RecipientId = recipientId,
        Type = "InvitationSent", Title = "Lời mời", Content = "Nội dung",
        CreatedAt = DateTimeOffset.UtcNow,
    };
}

internal sealed class NotificationApiFactory : WebApplicationFactory<NotificationsController>
{
    private const string SigningKey = "test-only-signing-key-0123456789abcdef-xyz";
    private readonly string _databaseName = "notification-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("ConnectionStrings:NotificationDb", "Host=unused");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<NotificationDbContext>>();
            services.AddDbContext<NotificationDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
            foreach (var hosted in services.Where(d => d.ServiceType == typeof(IHostedService)
                         && (d.ImplementationType == typeof(KafkaConsumerService)
                             || d.ImplementationType == typeof(SmtpEmailWorker))).ToList())
            {
                services.Remove(hosted);
            }
        });
    }

    public async Task SeedAsync(Action<NotificationDbContext> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        action(db);
        await db.SaveChangesAsync();
    }

    public async Task<T> QueryAsync<T>(Func<NotificationDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<NotificationDbContext>());
    }

    public HttpClient ClientFor(Guid studentId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(studentId));
        return client;
    }

    private static string Token(Guid studentId) => new JsonWebTokenHandler().CreateToken(
        new SecurityTokenDescriptor
        {
            Issuer = "exe-identity", Audience = "exe-team-matching",
            Claims = new Dictionary<string, object>
            {
                [ClaimNames.Subject] = Guid.NewGuid().ToString(),
                [ClaimNames.Roles] = new[] { Roles.Student },
                [ClaimNames.StudentId] = studentId.ToString(),
            },
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256),
        });
}
