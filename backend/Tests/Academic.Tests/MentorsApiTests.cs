using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Academic.Repositories.Lecturers;
using Academic.Services;
using Academic.Services.Mentors;
using BuildingBlocks.Auth;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Academic.Tests;

/// <summary>
/// API mentor (BE-66) với academic_db và Redis thật. Bật bằng <c>EXE_INTEGRATION=1</c>;
/// container <c>academic-api</c> phải tắt (xem <see cref="SemestersRoundsApiTests"/>). Dọn mentor, giảng viên và outbox thử khi xong.
/// </summary>
public sealed class MentorsApiTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly AcademicApiFactory _api = new();
    private readonly List<string> _emails = [];
    private readonly List<Guid> _lecturers = [];
    private HttpClient Admin => _api.ClientAs(Roles.Admin);
    private HttpClient Student => _api.ClientAs(Roles.Student);

    private HttpClient MentorClient(Guid id) => _api.ClientWith(new Dictionary<string, object> { [ClaimNames.MentorId] = id.ToString() }, Roles.Mentor);

    [Fact]
    public async Task Admin_creates_mentor_and_publishes_mentor_registered()
    {
        if (!Enabled)
        {
            return;
        }

        var domains = await ActiveDomainIdsAsync(2);
        var email = NewEmail();
        var response = await Admin.PostAsJsonAsync("/api/mentors",
            new { fullName = " Lê Văn Thử ", email, organization = " Startup X ", domainIds = new[] { domains[1], domains[0], domains[1] }, maxTeams = 4 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var mentor = (await response.Content.ReadFromJsonAsync<MentorResponse>())!;
        Assert.Equal(("Lê Văn Thử", "Startup X", false, (Guid?)null, 4, (int?)null, 0, true),
            (mentor.FullName, mentor.Organization, mentor.IsInternal, mentor.LecturerId, mentor.MaxTeams, mentor.PreferredTeams, mentor.CurrentTeamCount, mentor.IsActive));
        Assert.Equal(domains.Order(), mentor.Domains.Select(d => d.Id).Order());

        var message = Assert.Single(await EventsAsync(mentor.Id));
        Assert.Equal(("exe.student", "MentorRegistered"), (message.Topic, message.EventType));
        var payload = JsonDocument.Parse(message.Payload).RootElement;
        Assert.Equal((mentor.Id.ToString(), email, "Startup X", 4, 2, true),
            (payload.GetProperty("mentorId").GetString(), payload.GetProperty("email").GetString(), payload.GetProperty("organization").GetString(),
             payload.GetProperty("maxTeams").GetInt32(), payload.GetProperty("domainIds").GetArrayLength(), payload.GetProperty("isActive").GetBoolean()));

        var duplicate = await Admin.PostAsJsonAsync("/api/mentors", new { fullName = "Trùng", email = email.ToUpperInvariant(), maxTeams = 1 });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("email-exists", await duplicate.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, (await Admin.PostAsJsonAsync("/api/mentors", new { fullName = "Thiếu tối đa", email = NewEmail() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin.PostAsJsonAsync("/api/mentors", new { fullName = "Quá tối đa", email = NewEmail(), maxTeams = 51 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin.PostAsJsonAsync("/api/mentors", new { fullName = "Sai domain", email = NewEmail(), maxTeams = 1, domainIds = new[] { 999_999 } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Student.PostAsJsonAsync("/api/mentors", new { fullName = "x", email = NewEmail(), maxTeams = 1 })).StatusCode);
    }

    [Fact]
    public async Task List_filters_mentors_for_admin_and_team_only()
    {
        if (!Enabled)
        {
            return;
        }

        var tag = Guid.NewGuid().ToString("N")[..8];
        var domains = await ActiveDomainIdsAsync(2);
        var lecturer = await CreateLecturerAsync();
        var external = await CreateAsync($"Mentor {tag} A", domainIds: [domains[0]], organization: $"Công ty {tag}");
        var inside = await CreateAsync($"Mentor {tag} B", domainIds: [domains[1]], isInternal: true, lecturerId: lecturer);

        Task<PagedResponse<MentorResponse>?> ListAsync(string query) => Admin.GetFromJsonAsync<PagedResponse<MentorResponse>>($"/api/mentors?search={tag}{query}");

        Assert.Equal([external.Id, inside.Id], (await ListAsync(""))!.Items.Select(m => m.Id));
        Assert.Equal(inside.Id, (await ListAsync("&isInternal=true"))!.Items.Single().Id);
        Assert.Equal(external.Id, (await ListAsync($"&domainId={domains[0]}"))!.Items.Single().Id);
        Assert.Equal(external.Id, (await Admin.GetFromJsonAsync<PagedResponse<MentorResponse>>($"/api/mentors?search=công ty {tag}"))!.Items.Single().Id);
        Assert.Equal(2, (await ListAsync("&hasCapacity=true"))!.TotalCount); // chưa có tác dụng, chờ Team
        var second = (await ListAsync("&pageSize=1&page=2"))!;
        Assert.Equal((2, 2, inside.Id), (second.TotalCount, second.TotalPages, second.Items.Single().Id));

        var forTeam = await _api.ClientAs(Roles.Service).GetFromJsonAsync<PagedResponse<MentorResponse>>($"/api/mentors?search={tag}");
        Assert.Equal(2, forTeam!.TotalCount);
        Assert.Equal(HttpStatusCode.Forbidden, (await Student.GetAsync("/api/mentors")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.ClientAs(Roles.Lecturer).GetAsync("/api/mentors")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.CreateClient().GetAsync("/api/mentors")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin.GetAsync("/api/mentors?pageSize=101")).StatusCode);
    }

    [Fact]
    public async Task Linking_a_lecturer_needs_internal_mentor_existing_lecturer_and_one_mentor_per_lecturer()
    {
        if (!Enabled)
        {
            return;
        }

        var lecturer = await CreateLecturerAsync();
        var notInternal = await Admin.PostAsJsonAsync("/api/mentors", new { fullName = "Ngoài trường", email = NewEmail(), maxTeams = 2, lecturerId = lecturer });
        Assert.Equal(HttpStatusCode.BadRequest, notInternal.StatusCode);
        Assert.Contains("lecturerId", await notInternal.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin.PostAsJsonAsync("/api/mentors",
            new { fullName = "Không có GV", email = NewEmail(), maxTeams = 2, isInternal = true, lecturerId = Guid.NewGuid() })).StatusCode);

        var first = await CreateAsync("Giảng viên làm mentor", isInternal: true, lecturerId: lecturer);
        Assert.Equal(lecturer, JsonDocument.Parse(Assert.Single(await EventsAsync(first.Id)).Payload).RootElement.GetProperty("lecturerId").GetGuid());

        var second = await Admin.PostAsJsonAsync("/api/mentors", new { fullName = "Trùng giảng viên", email = NewEmail(), maxTeams = 2, isInternal = true, lecturerId = lecturer });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("lecturer-has-mentor", await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Admin_updates_and_locks_mentor_and_lowered_maximum_caps_preferred()
    {
        if (!Enabled)
        {
            return;
        }

        var domains = await ActiveDomainIdsAsync(2);
        var lecturer = await CreateLecturerAsync();
        var mentor = await CreateAsync("Mentor Sửa", domainIds: [domains[0]], maxTeams: 5, isInternal: true, lecturerId: lecturer);
        Assert.Equal(HttpStatusCode.OK, (await MentorClient(mentor.Id).PutAsJsonAsync("/api/mentors/me", new { preferredTeams = 4 })).StatusCode);

        var lowered = await PutAsync(mentor.Id, new { fullName = "Mentor Đã Sửa", email = mentor.Email, maxTeams = 2 });
        Assert.Equal(("Mentor Đã Sửa", 2, (int?)2, true, lecturer, true),
            (lowered.FullName, lowered.MaxTeams, lowered.PreferredTeams, lowered.IsInternal, lowered.LecturerId!.Value, lowered.IsActive));
        Assert.Equal(domains[0], lowered.Domains.Single().Id); // bỏ trống domainIds thì giữ nguyên

        var locked = await PutAsync(mentor.Id, new { fullName = mentor.FullName, email = mentor.Email, maxTeams = 2, isActive = false, domainIds = new[] { domains[1] } });
        Assert.Equal((false, domains[1]), (locked.IsActive, locked.Domains.Single().Id));
        Assert.False(JsonDocument.Parse((await EventsAsync(mentor.Id)).Last().Payload).RootElement.GetProperty("isActive").GetBoolean());

        var external = await PutAsync(mentor.Id, new { fullName = mentor.FullName, email = mentor.Email, maxTeams = 2, isInternal = false });
        Assert.Equal((false, (Guid?)null), (external.IsInternal, external.LecturerId)); // chuyển ra ngoài trường thì gỡ liên kết giảng viên

        var other = await CreateAsync("Mentor Khác");
        var taken = await Admin.PutAsJsonAsync($"/api/mentors/{mentor.Id}", new { fullName = "x", email = other.Email, maxTeams = 2 });
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Admin.PutAsJsonAsync($"/api/mentors/{Guid.NewGuid()}", new { fullName = "x", email = NewEmail(), maxTeams = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await MentorClient(mentor.Id).PutAsJsonAsync($"/api/mentors/{mentor.Id}", new { fullName = "x", email = mentor.Email, maxTeams = 50 })).StatusCode);
    }

    [Fact]
    public async Task Mentor_edits_own_profile_within_the_maximum()
    {
        if (!Enabled)
        {
            return;
        }

        var domains = await ActiveDomainIdsAsync(2);
        var mentor = await CreateAsync("Mentor Tự Sửa", domainIds: [domains[0]], maxTeams: 3);
        var client = MentorClient(mentor.Id);
        Assert.Equal(mentor.Id, (await client.GetFromJsonAsync<MentorResponse>("/api/mentors/me"))!.Id);

        var updated = await (await client.PutAsJsonAsync("/api/mentors/me", new { preferredTeams = 3, description = " Mười năm làm sản phẩm ", domainIds = new[] { domains[1] } }))
            .Content.ReadFromJsonAsync<MentorResponse>();
        Assert.Equal((3, "Mười năm làm sản phẩm", domains[1]), (updated!.PreferredTeams, updated.Description, updated.Domains.Single().Id));
        Assert.Equal(2, (await EventsAsync(mentor.Id)).Count);
        var payload = JsonDocument.Parse((await EventsAsync(mentor.Id)).Last().Payload).RootElement;
        Assert.Equal(domains[1], payload.GetProperty("domainIds")[0].GetInt32());

        var partial = await (await client.PutAsJsonAsync("/api/mentors/me", new { preferredTeams = 2 })).Content.ReadFromJsonAsync<MentorResponse>();
        Assert.Equal((2, "Mười năm làm sản phẩm", 1), (partial!.PreferredTeams, partial.Description, partial.Domains.Count)); // bỏ trống thì giữ nguyên
        var cleared = await (await client.PutAsJsonAsync("/api/mentors/me", new { description = "" })).Content.ReadFromJsonAsync<MentorResponse>();
        Assert.Null(cleared!.Description);

        var above = await client.PutAsJsonAsync("/api/mentors/me", new { preferredTeams = 4 });
        Assert.Equal(HttpStatusCode.BadRequest, above.StatusCode);
        Assert.Contains("preferred-above-max", await above.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/mentors/me", new { preferredTeams = 0 })).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await MentorClient(Guid.NewGuid()).GetAsync("/api/mentors/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Student.GetAsync("/api/mentors/me")).StatusCode);
    }

    [Fact]
    public async Task Mentor_detail_is_for_admin_the_mentor_and_team()
    {
        if (!Enabled)
        {
            return;
        }

        var mentor = await CreateAsync("Mentor Riêng", isActive: false);
        Assert.Equal(mentor.Email, (await Admin.GetFromJsonAsync<MentorResponse>($"/api/mentors/{mentor.Id}"))!.Email);
        Assert.Equal(HttpStatusCode.OK, (await MentorClient(mentor.Id).GetAsync($"/api/mentors/{mentor.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await MentorClient(Guid.NewGuid()).GetAsync($"/api/mentors/{mentor.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Student.GetAsync($"/api/mentors/{mentor.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Admin.GetAsync($"/api/mentors/{Guid.NewGuid()}")).StatusCode);

        var snapshot = await _api.ClientAs(Roles.Service).GetFromJsonAsync<MentorResponse>($"/internal/mentors/{mentor.Id}");
        Assert.Equal((mentor.Id, false, mentor.MaxTeams), (snapshot!.Id, snapshot.IsActive, snapshot.MaxTeams)); // có cả mentor đã khóa
        Assert.Equal(HttpStatusCode.NotFound, (await _api.ClientAs(Roles.Service).GetAsync($"/internal/mentors/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Admin.GetAsync($"/internal/mentors/{mentor.Id}")).StatusCode);
    }

    private async Task<MentorResponse> CreateAsync(string name, int[]? domainIds = null, int maxTeams = 3, bool isInternal = false, Guid? lecturerId = null, string? organization = null, bool isActive = true)
    {
        var response = await Admin.PostAsJsonAsync("/api/mentors", new { fullName = name, email = NewEmail(), organization, isInternal, lecturerId, domainIds, maxTeams, isActive });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<MentorResponse>())!;
    }

    private async Task<MentorResponse> PutAsync(Guid id, object body)
    {
        var response = await Admin.PutAsJsonAsync($"/api/mentors/{id}", body);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<MentorResponse>())!;
    }

    private Task<Guid> CreateLecturerAsync() => _api.QueryDbAsync(async db =>
    {
        var lecturer = new Lecturer { Id = Guid.NewGuid(), FullName = "Giảng viên thử", Email = NewEmail() };
        db.Lecturers.Add(lecturer);
        await db.SaveChangesAsync();
        _lecturers.Add(lecturer.Id);
        return lecturer.Id;
    });

    private string NewEmail()
    {
        var email = $"mentor-thu-{Guid.NewGuid():N}@example.test";
        _emails.Add(email);
        return email;
    }

    private Task<List<int>> ActiveDomainIdsAsync(int count) =>
        _api.QueryDbAsync(db => db.Domains.Where(d => d.IsActive).OrderBy(d => d.Id).Select(d => d.Id).Take(count).ToListAsync());

    private Task<List<OutboxMessage>> EventsAsync(Guid mentorId) =>
        _api.QueryDbAsync(db => db.Set<OutboxMessage>().Where(m => m.AggregateId == mentorId).OrderBy(m => m.OccurredAt).ToListAsync());

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (Enabled)
        {
            await _api.QueryDbAsync(async db =>
            {
                var ids = await db.Mentors.Where(m => _emails.Contains(m.Email)).Select(m => m.Id).ToListAsync();
                await db.Set<OutboxMessage>().Where(m => ids.Contains(m.AggregateId)).ExecuteDeleteAsync();
                await db.Mentors.Where(m => ids.Contains(m.Id)).ExecuteDeleteAsync(); // mentor_domains xóa theo ON DELETE CASCADE
                return await db.Lecturers.Where(l => _lecturers.Contains(l.Id)).ExecuteDeleteAsync();
            });
        }
        await _api.DisposeAsync();
    }
}
