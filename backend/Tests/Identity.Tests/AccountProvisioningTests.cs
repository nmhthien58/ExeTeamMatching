using System.Net;
using BuildingBlocks.Messaging;
using Identity.Repositories.Accounts;
using Identity.Services.Provisioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Tests;

/// <summary>Tạo sẵn tài khoản từ StudentImported, LecturerRegistered (BE-13, rule 5).</summary>
public sealed class AccountProvisioningTests : IDisposable
{
    private readonly IdentityApiFactory _factory = new();
    private readonly HttpClient _client;

    public AccountProvisioningTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Imported_student_gets_pending_account_and_can_log_in()
    {
        var studentId = Guid.NewGuid();
        await HandleAsync(new StudentImported(studentId, "Trần Minh Anh", " sv.a@example.com ", new DateOnly(2099, 4, 30)));

        var account = await AccountAsync("sv.a@example.com");
        Assert.Equal(AccountStatuses.Pending, account.Status);
        Assert.Equal("Trần Minh Anh", account.FullName);
        Assert.Equal(new DateTimeOffset(2099, 5, 1, 0, 0, 0, TimeSpan.Zero), account.AllowedUntil);
        var role = Assert.Single(account.Roles);
        Assert.Equal(RoleIds.Student, role.RoleId);
        Assert.Equal(studentId, role.ExternalRefId);

        _factory.Google.Register("sv.a@example.com");
        Assert.Equal(HttpStatusCode.OK, (await IdentityApiFactory.LoginAsync(_client, "sv.a@example.com")).StatusCode);
        Assert.Equal(AccountStatuses.Active, (await AccountAsync("sv.a@example.com")).Status);
    }

    [Fact]
    public async Task Same_student_twice_updates_instead_of_duplicating()
    {
        var student = new StudentImported(Guid.NewGuid(), "Tên cũ", "sv.b@example.com", new DateOnly(2099, 4, 30));

        await HandleAsync(student);
        await HandleAsync(student with { FullName = "Tên mới" });

        var account = await AccountAsync("sv.b@example.com");
        Assert.Equal("Tên mới", account.FullName);
        Assert.Single(account.Roles);
        Assert.Equal(1, await _factory.WithDbAsync(db => db.UserAccounts.CountAsync()));
    }

    [Fact]
    public async Task Reimporting_older_semester_keeps_later_deadline()
    {
        var studentId = Guid.NewGuid();
        await HandleAsync(new StudentImported(studentId, "SV", "sv.c@example.com", new DateOnly(2099, 12, 31)));
        await HandleAsync(new StudentImported(studentId, "SV", "sv.c@example.com", new DateOnly(2099, 4, 30)));

        Assert.Equal(new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero), (await AccountAsync("sv.c@example.com")).AllowedUntil);
    }

    [Fact]
    public async Task Changed_email_moves_account_and_releases_google_link()
    {
        var lecturerId = Guid.NewGuid();
        await _factory.SeedAccountAsync("gv.cu@example.com", AccountStatuses.Active, (RoleIds.Lecturer, lecturerId));
        await _factory.WithDbAsync(async db =>
        {
            (await db.UserAccounts.SingleAsync()).GoogleSubject = "sub-cu";
            return await db.SaveChangesAsync();
        });

        await HandleAsync(new LecturerRegistered(lecturerId, "GV", "gv.moi@example.com", true));

        var account = await AccountAsync("gv.moi@example.com");
        Assert.Null(account.GoogleSubject);
        Assert.Equal(AccountStatuses.Active, account.Status);
        Assert.Equal(1, await _factory.WithDbAsync(db => db.UserAccounts.CountAsync()));
    }

    [Fact]
    public async Task Changed_email_already_taken_fails_so_event_reaches_dead_letter()
    {
        var lecturerId = Guid.NewGuid();
        await _factory.SeedAccountAsync("gv.a@example.com", AccountStatuses.Active, (RoleIds.Lecturer, lecturerId));
        await _factory.SeedAccountAsync("gv.b@example.com", AccountStatuses.Active, (RoleIds.Lecturer, Guid.NewGuid()));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            HandleAsync(new LecturerRegistered(lecturerId, "GV", "gv.b@example.com", true)));
    }

    [Fact]
    public async Task Student_who_becomes_lecturer_has_two_roles_and_no_deadline()
    {
        var studentId = Guid.NewGuid();
        await HandleAsync(new StudentImported(studentId, "NCS", "gv.c@example.com", new DateOnly(2099, 4, 30)));

        await HandleAsync(new LecturerRegistered(Guid.NewGuid(), "NCS", "gv.c@example.com", true));
        await HandleAsync(new StudentImported(studentId, "NCS", "gv.c@example.com", new DateOnly(2099, 8, 30)));

        var account = await AccountAsync("gv.c@example.com");
        Assert.Equal([RoleIds.Student, RoleIds.Lecturer], account.Roles.Select(r => r.RoleId).Order());
        Assert.Null(account.AllowedUntil);
    }

    [Fact]
    public async Task Inactive_lecturer_is_locked_and_reactivation_restores_status()
    {
        var lecturerId = Guid.NewGuid();
        await HandleAsync(new LecturerRegistered(lecturerId, "GV", "gv.d@example.com", false));
        Assert.Equal(AccountStatuses.Locked, (await AccountAsync("gv.d@example.com")).Status);

        await HandleAsync(new LecturerRegistered(lecturerId, "GV", "gv.d@example.com", true));
        Assert.Equal(AccountStatuses.Pending, (await AccountAsync("gv.d@example.com")).Status);

        _factory.Google.Register("gv.d@example.com");
        await IdentityApiFactory.LoginAsync(_client, "gv.d@example.com");
        await HandleAsync(new LecturerRegistered(lecturerId, "GV", "gv.d@example.com", false));
        await HandleAsync(new LecturerRegistered(lecturerId, "GV", "gv.d@example.com", true));
        Assert.Equal(AccountStatuses.Active, (await AccountAsync("gv.d@example.com")).Status);
    }

    [Fact]
    public async Task Inactive_lecturer_who_is_also_mentor_keeps_account_open()
    {
        var lecturerId = Guid.NewGuid();
        await _factory.SeedAccountAsync("gv.e@example.com", AccountStatuses.Active, (RoleIds.Lecturer, lecturerId), (RoleIds.Mentor, Guid.NewGuid()));

        await HandleAsync(new LecturerRegistered(lecturerId, "GV", "gv.e@example.com", false));

        Assert.Equal(AccountStatuses.Active, (await AccountAsync("gv.e@example.com")).Status);
    }

    [Fact]
    public async Task Handler_reads_the_payload_academic_publishes()
    {
        var studentId = Guid.NewGuid();
        var envelope = EventEnvelope.Create(nameof(StudentImported), new
        {
            studentId, studentCode = "DE180001", fullName = "SV", email = "sv.e@example.com", phone = (string?)null, specializationId = 1,
            semesterId = Guid.NewGuid(), semesterEndDate = new DateOnly(2099, 4, 30), courseCode = "EXE101", classCode = "SE1",
        }, "academic", null);

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ProvisioningEventHandler<StudentImported>>().HandleAsync(envelope, CancellationToken.None);

        Assert.Equal(studentId, Assert.Single((await AccountAsync("sv.e@example.com")).Roles).ExternalRefId);
    }

    [Fact]
    public void Consumer_listens_to_both_events_on_student_topic()
    {
        var registrations = _factory.Services.GetServices<HandlerRegistration>().ToList();

        Assert.Equal([nameof(LecturerRegistered), nameof(StudentImported)], registrations.Select(r => r.EventType).Order());
        Assert.All(registrations, r => Assert.Equal(Topics.Student, r.Topic));
    }

    private async Task HandleAsync(StudentImported e)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AccountProvisioningService>().HandleAsync(e, CancellationToken.None);
    }

    private async Task HandleAsync(LecturerRegistered e)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AccountProvisioningService>().HandleAsync(e, CancellationToken.None);
    }

    private Task<UserAccount> AccountAsync(string email) =>
        _factory.WithDbAsync(db => db.UserAccounts.Include(x => x.Roles).AsNoTracking().SingleAsync(x => x.Email == email));
}
