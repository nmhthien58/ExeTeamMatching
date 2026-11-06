using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Academic.Services;
using Academic.Services.Lecturers;
using BuildingBlocks.Auth;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Academic.Tests;

/// <summary>
/// API giảng viên (BE-20) với academic_db và Redis thật. Bật bằng <c>EXE_INTEGRATION=1</c>;
/// container <c>academic-api</c> phải tắt (xem <see cref="SemestersRoundsApiTests"/>). Dọn giảng viên và outbox thử khi xong.
/// </summary>
public sealed class LecturersApiTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly AcademicApiFactory _api = new();
    private readonly List<string> _emails = [];
    private HttpClient Admin => _api.ClientAs(Roles.Admin);
    private HttpClient Student => _api.ClientAs(Roles.Student);

    [Fact]
    public async Task Admin_creates_lecturer_and_publishes_lecturer_registered()
    {
        if (!Enabled)
        {
            return;
        }

        var domains = await ActiveDomainIdsAsync(2);
        var email = NewEmail();
        var response = await Admin.PostAsJsonAsync("/api/lecturers",
            new { fullName = " Trần Thị Thử ", email, phone = "090 123 4567", domainIds = new[] { domains[1], domains[0], domains[1] }, maxTeamsExe201 = 40 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var lecturer = (await response.Content.ReadFromJsonAsync<LecturerResponse>())!;
        Assert.Equal(("Trần Thị Thử", "0901234567", (int?)null, (int?)40, true),
            (lecturer.FullName, lecturer.Phone, lecturer.MaxTeamsExe101, lecturer.MaxTeamsExe201, lecturer.IsActive));
        Assert.Equal(domains.Order(), lecturer.Domains.Select(d => d.Id).Order());

        var events = await EventsAsync(lecturer.Id);
        var registered = JsonDocument.Parse(Assert.Single(events, m => m.EventType == "LecturerRegistered").Payload).RootElement;
        Assert.Equal((email, 40, 2, true), (registered.GetProperty("email").GetString(), registered.GetProperty("maxTeamsExe201").GetInt32(),
            registered.GetProperty("domainIds").GetArrayLength(), registered.GetProperty("isActive").GetBoolean()));
        Assert.All(events, m => Assert.Equal("exe.student", m.Topic));

        var duplicate = await Admin.PostAsJsonAsync("/api/lecturers", new { fullName = "Trùng", email = email.ToUpperInvariant() });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("lecturer-email-taken", await duplicate.Content.ReadAsStringAsync());

        var badDomain = await Admin.PostAsJsonAsync("/api/lecturers", new { fullName = "Sai domain", email = NewEmail(), domainIds = new[] { 999_999 } });
        Assert.Equal(HttpStatusCode.BadRequest, badDomain.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Student.PostAsJsonAsync("/api/lecturers", new { fullName = "x", email = NewEmail() })).StatusCode);
    }

    [Fact]
    public async Task Students_see_only_active_lecturers_without_phone()
    {
        if (!Enabled)
        {
            return;
        }

        var tag = Guid.NewGuid().ToString("N")[..8];
        var active = await CreateAsync($"Giảng Viên {tag} A", phone: "0901234567");
        var inactive = await CreateAsync($"Giảng Viên {tag} B", isActive: false);

        var forAdmin = await Admin.GetFromJsonAsync<PagedResponse<LecturerResponse>>($"/api/lecturers?search={tag}");
        Assert.Equal([active.Id, inactive.Id], forAdmin!.Items.Select(l => l.Id));
        Assert.Equal("0901234567", forAdmin.Items[0].Phone);

        var forStudent = await Student.GetFromJsonAsync<PagedResponse<LecturerResponse>>($"/api/lecturers?search={tag.ToUpperInvariant()}");
        var seen = forStudent!.Items.Single();
        Assert.Equal((active.Id, (string?)null, 1, 1), (seen.Id, seen.Phone, forStudent.TotalCount, forStudent.TotalPages));
        Assert.Null((await Student.GetFromJsonAsync<LecturerResponse>($"/api/lecturers/{active.Id}"))!.Phone);
        Assert.Equal(HttpStatusCode.NotFound, (await Student.GetAsync($"/api/lecturers/{inactive.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Admin.GetAsync($"/api/lecturers/{inactive.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin.GetAsync("/api/lecturers?pageSize=0")).StatusCode);
    }

    [Fact]
    public async Task Admin_update_publishes_profile_updated_and_registered_when_account_changes()
    {
        if (!Enabled)
        {
            return;
        }

        var lecturer = await CreateAsync("Giảng Viên Sửa");
        var domain = (await ActiveDomainIdsAsync(1))[0];

        var capacity = await Admin.PutAsJsonAsync($"/api/lecturers/{lecturer.Id}",
            new { fullName = lecturer.FullName, email = lecturer.Email, domainIds = new[] { domain }, maxTeamsExe101 = 8 });
        Assert.Equal(HttpStatusCode.OK, capacity.StatusCode);
        var updated = (await capacity.Content.ReadFromJsonAsync<LecturerResponse>())!;
        Assert.Equal((8, domain, true), (updated.MaxTeamsExe101!.Value, updated.Domains.Single().Id, updated.IsActive)); // isActive bỏ trống: giữ nguyên
        Assert.Equal(["LecturerProfileUpdated", "LecturerRegistered"], (await EventsAsync(lecturer.Id)).Select(m => m.EventType).Order());

        var locked = await Admin.PutAsJsonAsync($"/api/lecturers/{lecturer.Id}", new { fullName = lecturer.FullName, email = lecturer.Email, isActive = false });
        Assert.False((await locked.Content.ReadFromJsonAsync<LecturerResponse>())!.IsActive);
        Assert.Equal(domain, (await Admin.GetFromJsonAsync<LecturerResponse>($"/api/lecturers/{lecturer.Id}"))!.Domains.Single().Id); // domainIds bỏ trống: giữ nguyên
        var events = await EventsAsync(lecturer.Id);
        Assert.Equal((2, 2), (events.Count(m => m.EventType == "LecturerRegistered"), events.Count(m => m.EventType == "LecturerProfileUpdated")));

        Assert.Equal(HttpStatusCode.NotFound, (await Admin.PutAsJsonAsync($"/api/lecturers/{Guid.NewGuid()}", new { fullName = "x", email = NewEmail() })).StatusCode);
        var other = await CreateAsync("Giảng Viên Khác");
        Assert.Equal(HttpStatusCode.Conflict, (await Admin.PutAsJsonAsync($"/api/lecturers/{lecturer.Id}", new { fullName = "x", email = other.Email })).StatusCode);
    }

    [Fact]
    public async Task Lecturer_edits_own_domains_only()
    {
        if (!Enabled)
        {
            return;
        }

        var lecturer = await CreateAsync("Giảng Viên Tôi", phone: "0909999999");
        var domains = await ActiveDomainIdsAsync(3);
        var me = _api.ClientWith(new Dictionary<string, object> { [ClaimNames.LecturerId] = lecturer.Id.ToString() }, Roles.Lecturer);

        var profile = (await me.GetFromJsonAsync<LecturerResponse>("/api/lecturers/me"))!;
        Assert.Equal((lecturer.Id, "0909999999"), (profile.Id, profile.Phone));
        Assert.Equal("0909999999", (await me.GetFromJsonAsync<LecturerResponse>($"/api/lecturers/{lecturer.Id}"))!.Phone); // chính mình thấy SĐT

        var response = await me.PutAsJsonAsync("/api/lecturers/me", new { domainIds = domains, maxTeamsExe101 = 99 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<LecturerResponse>())!;
        Assert.Equal(domains.Order(), saved.Domains.Select(d => d.Id).Order());
        Assert.Null(saved.MaxTeamsExe101); // giảng viên không tự đặt số nhóm tối đa
        var updated = JsonDocument.Parse((await EventsAsync(lecturer.Id)).Single(m => m.EventType == "LecturerProfileUpdated").Payload).RootElement;
        Assert.Equal(3, updated.GetProperty("domainIds").GetArrayLength());

        var byEmail = _api.ClientWith(new Dictionary<string, object> { [ClaimNames.Email] = lecturer.Email.ToUpperInvariant() }, Roles.Lecturer);
        Assert.Equal(lecturer.Id, (await byEmail.GetFromJsonAsync<LecturerResponse>("/api/lecturers/me"))!.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.ClientWith(new Dictionary<string, object> { [ClaimNames.LecturerId] = Guid.NewGuid().ToString() }, Roles.Lecturer).GetAsync("/api/lecturers/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Student.GetAsync("/api/lecturers/me")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await me.PutAsJsonAsync("/api/lecturers/me", new { })).StatusCode);
    }

    [Fact]
    public async Task Team_reads_lecturer_through_internal_endpoint()
    {
        if (!Enabled)
        {
            return;
        }

        var lecturer = await CreateAsync("Giảng Viên Nội Bộ", phone: "0908888888", isActive: false);
        var result = await _api.ClientAs(Roles.Service).GetFromJsonAsync<LecturerResponse>($"/internal/lecturers/{lecturer.Id}");
        Assert.Equal((lecturer.Id, false, "0908888888"), (result!.Id, result.IsActive, result.Phone));
        Assert.Equal(HttpStatusCode.NotFound, (await _api.ClientAs(Roles.Service).GetAsync($"/internal/lecturers/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Admin.GetAsync($"/internal/lecturers/{lecturer.Id}")).StatusCode);
    }

    private async Task<LecturerResponse> CreateAsync(string fullName, string? phone = null, bool isActive = true)
    {
        var response = await Admin.PostAsJsonAsync("/api/lecturers", new { fullName, email = NewEmail(), phone, isActive });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<LecturerResponse>())!;
    }

    private string NewEmail()
    {
        var email = $"gv-{Guid.NewGuid():N}@example.test";
        _emails.Add(email);
        return email;
    }

    private Task<List<int>> ActiveDomainIdsAsync(int count) =>
        _api.QueryDbAsync(db => db.Domains.Where(d => d.IsActive).OrderBy(d => d.Id).Select(d => d.Id).Take(count).ToListAsync());

    private Task<List<OutboxMessage>> EventsAsync(Guid lecturerId) =>
        _api.QueryDbAsync(db => db.Set<OutboxMessage>().Where(m => m.AggregateId == lecturerId).OrderBy(m => m.OccurredAt).ToListAsync());

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (Enabled)
        {
            await _api.QueryDbAsync(async db =>
            {
                var ids = await db.Lecturers.Where(l => _emails.Contains(l.Email)).Select(l => l.Id).ToListAsync();
                await db.Set<OutboxMessage>().Where(m => ids.Contains(m.AggregateId)).ExecuteDeleteAsync();
                return await db.Lecturers.Where(l => ids.Contains(l.Id)).ExecuteDeleteAsync(); // lecturer_domains xóa theo ON DELETE CASCADE
            });
        }
        await _api.DisposeAsync();
    }
}
