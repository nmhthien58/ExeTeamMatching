using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using BuildingBlocks.Auth;
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

public sealed class AnnouncementsApiTests
{
    private sealed class AnnouncementApiFactory : WebApplicationFactory<NotificationsController>
    {
        private const string SigningKey = "test-only-signing-key-0123456789abcdef-xyz";
        private readonly string _databaseName = "announcement-" + Guid.NewGuid();

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
                             && (d.ImplementationType == typeof(SmtpEmailWorker))).ToList())
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

        public HttpClient ClientForRole(string role, Guid? personId = null)
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(role, personId));
            return client;
        }

        private static string Token(string role, Guid? personId)
        {
            var claims = new Dictionary<string, object>
            {
                [ClaimNames.Subject] = Guid.NewGuid().ToString(),
                [ClaimNames.Roles] = new[] { role },
            };
            if (personId is { } pid)
            {
                if (role == Roles.Student) claims[ClaimNames.StudentId] = pid.ToString();
                else if (role == Roles.Lecturer) claims[ClaimNames.LecturerId] = pid.ToString();
                else if (role == Roles.Mentor) claims[ClaimNames.MentorId] = pid.ToString();
            }
            return new JsonWebTokenHandler().CreateToken(
                new SecurityTokenDescriptor
                {
                    Issuer = "exe-identity",
                    Audience = "exe-team-matching",
                    Claims = claims,
                    Expires = DateTime.UtcNow.AddMinutes(10),
                    SigningCredentials = new SigningCredentials(
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                        SecurityAlgorithms.HmacSha256),
                });
        }
    }

    [Fact]
    public async Task Admin_role_is_required_for_announcements()
    {
        using var factory = new AnnouncementApiFactory();
        var studentClient = factory.ClientForRole(Roles.Student);
        var lecturerClient = factory.ClientForRole(Roles.Lecturer);
        var anonClient = factory.CreateClient();

        var body = new AnnouncementRequest("ALL_STUDENTS", "Thông báo", "Nội dung");

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.PostAsJsonAsync("/api/announcements", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await studentClient.PostAsJsonAsync("/api/announcements", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await lecturerClient.PostAsJsonAsync("/api/announcements", body)).StatusCode);

        var adminClient = factory.ClientForRole(Roles.Admin);
        var response = await adminClient.PostAsJsonAsync("/api/announcements", body);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_audience_or_empty_title_returns_400_problem()
    {
        using var factory = new AnnouncementApiFactory();
        var adminClient = factory.ClientForRole(Roles.Admin);

        var invalidAudience = new AnnouncementRequest("INVALID_AUDIENCE", "Tiêu đề", "Nội dung");
        var res1 = await adminClient.PostAsJsonAsync("/api/announcements", invalidAudience);
        Assert.Equal(HttpStatusCode.BadRequest, res1.StatusCode);

        var emptyTitle = new AnnouncementRequest("ALL_STUDENTS", "", "Nội dung");
        var res2 = await adminClient.PostAsJsonAsync("/api/announcements", emptyTitle);
        Assert.Equal(HttpStatusCode.BadRequest, res2.StatusCode);

        var emptyContent = new AnnouncementRequest("ALL_STUDENTS", "Tiêu đề", "");
        var res3 = await adminClient.PostAsJsonAsync("/api/announcements", emptyContent);
        Assert.Equal(HttpStatusCode.BadRequest, res3.StatusCode);

        var invalidCourse = new AnnouncementRequest("ALL_STUDENTS", "Tiêu đề", "Nội dung", CourseCode: "INVALID");
        var res4 = await adminClient.PostAsJsonAsync("/api/announcements", invalidCourse);
        Assert.Equal(HttpStatusCode.BadRequest, res4.StatusCode);
    }

    [Fact]
    public async Task Announcement_creates_notifications_and_respects_email_preferences()
    {
        using var factory = new AnnouncementApiFactory();
        var s1Id = Guid.NewGuid();
        var s2Id = Guid.NewGuid();
        var l1Id = Guid.NewGuid();

        await factory.SeedAsync(db =>
        {
            db.Contacts.AddRange(
                new Contact { Id = s1Id, PersonType = "STUDENT", Email = "s1@fpt.edu.vn", FullName = "SV 1", EmailEnabled = true },
                new Contact { Id = s2Id, PersonType = "STUDENT", Email = "s2@fpt.edu.vn", FullName = "SV 2", EmailEnabled = false },
                new Contact { Id = l1Id, PersonType = "LECTURER", Email = "l1@fpt.edu.vn", FullName = "GV 1", EmailEnabled = true }
            );
        });

        var adminClient = factory.ClientForRole(Roles.Admin);
        var request = new AnnouncementRequest("ALL_STUDENTS", "Nhắc hạn nộp đề tài", "Hạn chót ngày mai", SendEmail: true);

        var response = await adminClient.PostAsJsonAsync("/api/announcements", request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var resData = await response.Content.ReadFromJsonAsync<AnnouncementResponse>();
        Assert.Equal(2, resData!.RecipientCount);

        // Verify in DB
        await factory.QueryAsync(async db =>
        {
            var notifs = await db.Notifications.Where(n => n.Type == "Announcement").ToListAsync();
            Assert.Equal(2, notifs.Count);
            Assert.Contains(notifs, n => n.RecipientId == s1Id);
            Assert.Contains(notifs, n => n.RecipientId == s2Id);
            Assert.DoesNotContain(notifs, n => n.RecipientId == l1Id);

            var emails = await db.EmailLogs.Where(e => e.TemplateCode == "Announcement").ToListAsync();
            // s1 has emailEnabled = true, s2 has emailEnabled = false -> only 1 email log
            Assert.Single(emails);
            Assert.Equal("s1@fpt.edu.vn", emails.Single().ToEmail);
            Assert.Equal("PENDING", emails.Single().Status);
            return 0;
        });
    }

    [Fact]
    public async Task Announcement_with_send_email_false_creates_no_email_logs()
    {
        using var factory = new AnnouncementApiFactory();
        var s1Id = Guid.NewGuid();

        await factory.SeedAsync(db =>
        {
            db.Contacts.Add(new Contact { Id = s1Id, PersonType = "STUDENT", Email = "s1@fpt.edu.vn", FullName = "SV 1", EmailEnabled = true });
        });

        var adminClient = factory.ClientForRole(Roles.Admin);
        var request = new AnnouncementRequest("ALL_STUDENTS", "Thông báo", "Nội dung", SendEmail: false);

        var response = await adminClient.PostAsJsonAsync("/api/announcements", request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        await factory.QueryAsync(async db =>
        {
            var emails = await db.EmailLogs.Where(e => e.TemplateCode == "Announcement").ToListAsync();
            Assert.Empty(emails);
            return 0;
        });
    }

    [Fact]
    public async Task Announcement_empty_recipients_returns_zero_count()
    {
        using var factory = new AnnouncementApiFactory();
        var adminClient = factory.ClientForRole(Roles.Admin);

        // Empty database (no mentors)
        var request = new AnnouncementRequest("ALL_MENTORS", "Thông báo", "Nội dung");
        var response = await adminClient.PostAsJsonAsync("/api/announcements", request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var resData = await response.Content.ReadFromJsonAsync<AnnouncementResponse>();
        Assert.Equal(0, resData!.RecipientCount);

        // STUDENTS_WITHOUT_TEAM audience (no internal API yet) returns 0 count safely
        var req2 = new AnnouncementRequest("STUDENTS_WITHOUT_TEAM", "Thông báo", "Nội dung");
        var res2 = await adminClient.PostAsJsonAsync("/api/announcements", req2);
        Assert.Equal(HttpStatusCode.Accepted, res2.StatusCode);
        var resData2 = await res2.Content.ReadFromJsonAsync<AnnouncementResponse>();
        Assert.Equal(0, resData2!.RecipientCount);
    }
}
