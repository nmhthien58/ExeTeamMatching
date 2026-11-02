using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Auth;
using BuildingBlocks.Messaging;
using Identity.Repositories.Accounts;
using Identity.Services.Provisioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Identity.Tests;

/// <summary>
/// BE-64 – vai trò Mentor từ MentorRegistered (rule 5, 20). Academic phát lại sự kiện ở mọi lần sửa mentor, nên mọi trường hợp
/// đều phải idempotent theo mentorId; LecturerRegistered và MentorRegistered của cùng một người đến theo thứ tự bất kỳ.
/// </summary>
public sealed class MentorProvisioningTests : IDisposable
{
    private readonly IdentityApiFactory _factory = new();
    private readonly HttpClient _client;

    public MentorProvisioningTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task External_mentor_gets_pending_account_then_active_with_mentor_claims()
    {
        var mentorId = Guid.NewGuid();
        await HandleAsync(new MentorRegistered(mentorId, "Lê Văn Tuấn", " tuan@example.com ", null, true));

        var account = await AccountAsync("tuan@example.com");
        Assert.Equal(AccountStatuses.Pending, account.Status);
        Assert.Null(account.AllowedUntil);
        var role = Assert.Single(account.Roles);
        Assert.Equal((RoleIds.Mentor, mentorId), (role.RoleId, role.ExternalRefId));

        var jwt = await LoginAsync("tuan@example.com");
        Assert.Equal([Roles.Mentor], RolesOf(jwt));
        Assert.Equal(mentorId.ToString(), jwt.GetClaim(ClaimNames.MentorId).Value);
        Assert.Equal(AccountStatuses.Active, (await AccountAsync("tuan@example.com")).Status);
    }

    [Fact]
    public async Task Replayed_event_updates_the_same_account()
    {
        var mentor = new MentorRegistered(Guid.NewGuid(), "Tên cũ", "m.a@example.com", null, true);

        await HandleAsync(mentor);
        await HandleAsync(mentor with { FullName = "Tên mới" });
        await HandleAsync(mentor with { FullName = "Tên mới" });

        var account = await AccountAsync("m.a@example.com");
        Assert.Equal("Tên mới", account.FullName);
        Assert.Single(account.Roles);
        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public async Task External_mentor_changing_email_moves_the_account()
    {
        var mentor = new MentorRegistered(Guid.NewGuid(), "Mentor", "m.cu@example.com", null, true);
        await HandleAsync(mentor);
        await LoginAsync("m.cu@example.com");

        await HandleAsync(mentor with { Email = "m.moi@example.com" });

        var account = await AccountAsync("m.moi@example.com");
        Assert.Null(account.GoogleSubject);
        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public async Task Locking_external_mentor_locks_account_and_unlocking_restores_it()
    {
        var mentor = new MentorRegistered(Guid.NewGuid(), "Mentor", "m.b@example.com", null, true);
        await HandleAsync(mentor);
        await LoginAsync("m.b@example.com");

        await HandleAsync(mentor with { IsActive = false });
        Assert.Equal(AccountStatuses.Locked, (await AccountAsync("m.b@example.com")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await IdentityApiFactory.LoginAsync(_client, "m.b@example.com")).StatusCode);

        await HandleAsync(mentor);
        var account = await AccountAsync("m.b@example.com");
        Assert.Equal(AccountStatuses.Active, account.Status);
        Assert.Equal(RoleIds.Mentor, Assert.Single(account.Roles).RoleId);
    }

    [Fact]
    public async Task Mentor_created_locked_gets_locked_account()
    {
        await HandleAsync(new MentorRegistered(Guid.NewGuid(), "Mentor", "m.c@example.com", null, false));

        Assert.Equal(AccountStatuses.Locked, (await AccountAsync("m.c@example.com")).Status);
    }

    [Fact]
    public async Task Lecturer_mentor_shares_one_account_with_both_roles_and_ids()
    {
        var lecturerId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();
        await HandleAsync(new LecturerRegistered(lecturerId, "Cô Hà", "gv.ha@example.com", true));
        await LoginAsync("gv.ha@example.com");

        await HandleAsync(new MentorRegistered(mentorId, "Tên khác ở hồ sơ mentor", "gv.ha@example.com", lecturerId, true));

        var account = await AccountAsync("gv.ha@example.com");
        Assert.Equal("Cô Hà", account.FullName);
        Assert.Equal(AccountStatuses.Active, account.Status);
        Assert.Equal(1, await CountAsync());

        var jwt = await LoginAsync("gv.ha@example.com");
        Assert.Equal([Roles.Lecturer, Roles.Mentor], RolesOf(jwt));
        Assert.Equal(lecturerId.ToString(), jwt.GetClaim(ClaimNames.LecturerId).Value);
        Assert.Equal(mentorId.ToString(), jwt.GetClaim(ClaimNames.MentorId).Value);
    }

    [Fact]
    public async Task Mentor_event_before_lecturer_event_still_gives_one_account()
    {
        var lecturerId = Guid.NewGuid();
        await HandleAsync(new MentorRegistered(Guid.NewGuid(), "Cô Lan", "gv.lan@example.com", lecturerId, true));
        await HandleAsync(new LecturerRegistered(lecturerId, "Cô Lan", "gv.lan@example.com", true));

        var account = await AccountAsync("gv.lan@example.com");
        Assert.Equal([RoleIds.Lecturer, RoleIds.Mentor], account.Roles.Select(r => r.RoleId).Order());
        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public async Task Locking_lecturer_mentor_only_drops_mentor_role_and_unlocking_restores_it()
    {
        var lecturerId = Guid.NewGuid();
        var mentor = new MentorRegistered(Guid.NewGuid(), "Thầy Minh", "gv.minh@example.com", lecturerId, true);
        await HandleAsync(new LecturerRegistered(lecturerId, "Thầy Minh", "gv.minh@example.com", true));
        await HandleAsync(mentor);
        await LoginAsync("gv.minh@example.com");

        await HandleAsync(mentor with { IsActive = false });
        await HandleAsync(mentor with { IsActive = false });

        var account = await AccountAsync("gv.minh@example.com");
        Assert.Equal(AccountStatuses.Active, account.Status);
        Assert.Equal(RoleIds.Lecturer, Assert.Single(account.Roles).RoleId);
        Assert.Equal([Roles.Lecturer], RolesOf(await LoginAsync("gv.minh@example.com")));

        await HandleAsync(mentor);
        Assert.Equal([RoleIds.Lecturer, RoleIds.Mentor], (await AccountAsync("gv.minh@example.com")).Roles.Select(r => r.RoleId).Order());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Lecturer_email_change_in_any_order_keeps_one_account(bool lecturerEventFirst)
    {
        var lecturerId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();
        await HandleAsync(new LecturerRegistered(lecturerId, "Cô Thu", "gv.thu@example.com", true));
        await HandleAsync(new MentorRegistered(mentorId, "Cô Thu", "gv.thu@example.com", lecturerId, true));

        var lecturer = new LecturerRegistered(lecturerId, "Cô Thu", "thu.moi@example.com", true);
        var mentor = new MentorRegistered(mentorId, "Cô Thu", "thu.moi@example.com", lecturerId, true);
        if (lecturerEventFirst)
        {
            await HandleAsync(lecturer);
            await HandleAsync(mentor);
        }
        else
        {
            await HandleAsync(mentor);
            await HandleAsync(lecturer);
        }

        Assert.Equal(1, await CountAsync());
        var account = await AccountAsync("thu.moi@example.com");
        Assert.Equal([(RoleIds.Lecturer, lecturerId), (RoleIds.Mentor, mentorId)],
            account.Roles.OrderBy(r => r.RoleId).Select(r => (r.RoleId, r.ExternalRefId!.Value)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Locking_and_unlocking_lecturer_and_mentor_in_any_order_ends_the_same(bool lecturerEventFirst)
    {
        var lecturerId = Guid.NewGuid();
        var lecturer = new LecturerRegistered(lecturerId, "Thầy Nam", "gv.nam@example.com", true);
        var mentor = new MentorRegistered(Guid.NewGuid(), "Thầy Nam", "gv.nam@example.com", lecturerId, true);
        await HandleAsync(lecturer);
        await HandleAsync(mentor);
        await LoginAsync("gv.nam@example.com");

        // Bộ môn khóa giảng viên: Academic khóa luôn mentor gắn với họ, hai sự kiện đến theo thứ tự bất kỳ.
        await InOrderAsync(lecturerEventFirst, lecturer with { IsActive = false }, mentor with { IsActive = false });
        Assert.Equal(AccountStatuses.Locked, (await AccountAsync("gv.nam@example.com")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await IdentityApiFactory.LoginAsync(_client, "gv.nam@example.com")).StatusCode);

        await InOrderAsync(lecturerEventFirst, lecturer, mentor);
        var account = await AccountAsync("gv.nam@example.com");
        Assert.Equal(AccountStatuses.Active, account.Status);
        Assert.Equal([RoleIds.Lecturer, RoleIds.Mentor], account.Roles.Select(r => r.RoleId).Order());
        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public async Task Linking_external_mentor_to_lecturer_moves_role_to_lecturer_account()
    {
        var lecturerId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();
        await HandleAsync(new LecturerRegistered(lecturerId, "Cô Mai", "gv.mai@example.com", true));
        await HandleAsync(new MentorRegistered(mentorId, "Cô Mai", "mai.rieng@example.com", null, true));

        await HandleAsync(new MentorRegistered(mentorId, "Cô Mai", "gv.mai@example.com", lecturerId, true));

        Assert.Equal([RoleIds.Lecturer, RoleIds.Mentor], (await AccountAsync("gv.mai@example.com")).Roles.Select(r => r.RoleId).Order());
        var old = await AccountAsync("mai.rieng@example.com");
        Assert.Empty(old.Roles);
        Assert.Equal(AccountStatuses.Locked, old.Status);
    }

    [Fact]
    public async Task Unlinking_mentor_from_lecturer_gives_mentor_its_own_account()
    {
        var lecturerId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();
        await HandleAsync(new LecturerRegistered(lecturerId, "Thầy Phúc", "gv.phuc@example.com", true));
        await HandleAsync(new MentorRegistered(mentorId, "Thầy Phúc", "gv.phuc@example.com", lecturerId, true));

        await HandleAsync(new MentorRegistered(mentorId, "Thầy Phúc", "phuc.mentor@example.com", null, true));

        var lecturer = await AccountAsync("gv.phuc@example.com");
        Assert.Equal(RoleIds.Lecturer, Assert.Single(lecturer.Roles).RoleId);
        var mentor = await AccountAsync("phuc.mentor@example.com");
        Assert.Equal((RoleIds.Mentor, mentorId), (Assert.Single(mentor.Roles).RoleId, mentor.Roles[0].ExternalRefId!.Value));
    }

    [Fact]
    public async Task Handler_reads_the_payload_academic_publishes()
    {
        var mentorId = Guid.NewGuid();
        var envelope = EventEnvelope.Create(nameof(MentorRegistered), new
        {
            mentorId, fullName = "Lê Văn Tuấn", email = "m.d@example.com", organization = "Startup X", isInternal = false,
            lecturerId = (Guid?)null, domainIds = new[] { 4, 7 }, maxTeams = 4, isActive = true,
        }, "academic", null);

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ProvisioningEventHandler<MentorRegistered>>().HandleAsync(envelope, CancellationToken.None);

        Assert.Equal(mentorId, Assert.Single((await AccountAsync("m.d@example.com")).Roles).ExternalRefId);
    }

    private async Task InOrderAsync(bool lecturerFirst, LecturerRegistered lecturer, MentorRegistered mentor)
    {
        if (lecturerFirst)
        {
            await HandleAsync(lecturer);
            await HandleAsync(mentor);
        }
        else
        {
            await HandleAsync(mentor);
            await HandleAsync(lecturer);
        }
    }

    private async Task<JsonWebToken> LoginAsync(string email)
    {
        _factory.Google.Register(email);
        var response = await IdentityApiFactory.LoginAsync(_client, email);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new JsonWebToken(body.GetProperty("accessToken").GetString());
    }

    private static string[] RolesOf(JsonWebToken jwt) => jwt.Claims.Where(c => c.Type == ClaimNames.Roles).Select(c => c.Value).ToArray();

    private async Task HandleAsync(LecturerRegistered e)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AccountProvisioningService>().HandleAsync(e, CancellationToken.None);
    }

    private async Task HandleAsync(MentorRegistered e)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AccountProvisioningService>().HandleAsync(e, CancellationToken.None);
    }

    private Task<int> CountAsync() => _factory.WithDbAsync(db => db.UserAccounts.CountAsync());

    private Task<UserAccount> AccountAsync(string email) =>
        _factory.WithDbAsync(db => db.UserAccounts.Include(x => x.Roles).AsNoTracking().SingleAsync(x => x.Email == email));
}
