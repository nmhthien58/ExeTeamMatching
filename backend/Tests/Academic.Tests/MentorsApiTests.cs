using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Academic.Repositories.Lecturers;
using Academic.Repositories.Students;
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
    private readonly List<Guid> _students = [];
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

        var keepsLecturerEmail = await Admin.PutAsJsonAsync($"/api/mentors/{mentor.Id}", new { fullName = mentor.FullName, email = mentor.Email, maxTeams = 2, isInternal = false });
        Assert.Equal(HttpStatusCode.Conflict, keepsLecturerEmail.StatusCode); // ra ngoài trường phải có email riêng
        var ownEmail = NewEmail();
        var external = await PutAsync(mentor.Id, new { fullName = mentor.FullName, email = ownEmail, maxTeams = 2, isInternal = false });
        Assert.Equal((false, (Guid?)null, ownEmail), (external.IsInternal, external.LecturerId, external.Email)); // chuyển ra ngoài trường thì gỡ liên kết giảng viên

        var other = await CreateAsync("Mentor Khác");
        var taken = await Admin.PutAsJsonAsync($"/api/mentors/{mentor.Id}", new { fullName = "x", email = other.Email, maxTeams = 2 });
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Admin.PutAsJsonAsync($"/api/mentors/{mentor.Id}", new { fullName = "Giữ email", email = ownEmail, maxTeams = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Admin.PutAsJsonAsync($"/api/mentors/{Guid.NewGuid()}", new { fullName = "x", email = NewEmail(), maxTeams = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await MentorClient(mentor.Id).PutAsJsonAsync($"/api/mentors/{mentor.Id}", new { fullName = "x", email = mentor.Email, maxTeams = 50 })).StatusCode);
    }

    [Fact]
    public async Task Email_must_not_belong_to_a_lecturer_or_student()
    {
        if (!Enabled)
        {
            return;
        }

        var lecturerEmail = await LecturerEmailAsync(await CreateLecturerAsync());
        var studentEmail = await CreateStudentAsync();
        foreach (var email in new[] { lecturerEmail, studentEmail, studentEmail.ToUpperInvariant() })
        {
            var response = await Admin.PostAsJsonAsync("/api/mentors", new { fullName = "Trùng người khác", email, maxTeams = 2 });
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Contains("email-exists", await response.Content.ReadAsStringAsync());
        }

        var mentor = await CreateAsync("Mentor Ngoài");
        var update = await Admin.PutAsJsonAsync($"/api/mentors/{mentor.Id}", new { fullName = mentor.FullName, email = studentEmail, maxTeams = 2 });
        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
    }

    [Fact]
    public async Task Lecturer_mentor_uses_the_lecturer_email_and_follows_its_changes()
    {
        if (!Enabled)
        {
            return;
        }

        var lecturer = await CreateLecturerAsync();
        var lecturerEmail = await LecturerEmailAsync(lecturer);
        var mentor = await CreateAsync("Giảng viên kiêm mentor", isInternal: true, lecturerId: lecturer); // gửi email khác vẫn dùng email giảng viên
        Assert.Equal(lecturerEmail, mentor.Email);
        Assert.Equal(lecturerEmail, JsonDocument.Parse(Assert.Single(await EventsAsync(mentor.Id)).Payload).RootElement.GetProperty("email").GetString());

        var kept = await PutAsync(mentor.Id, new { fullName = "Đổi tên", email = NewEmail(), maxTeams = 3 });
        Assert.Equal((lecturerEmail, "Đổi tên"), (kept.Email, kept.FullName));

        var newEmail = NewEmail();
        var changed = await Admin.PutAsJsonAsync($"/api/lecturers/{lecturer}", new { fullName = "Giảng viên thử", email = newEmail });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(newEmail, (await Admin.GetFromJsonAsync<MentorResponse>($"/api/mentors/{mentor.Id}"))!.Email);
        Assert.Equal(newEmail, JsonDocument.Parse((await EventsAsync(mentor.Id)).Last().Payload).RootElement.GetProperty("email").GetString());

        var taken = await CreateAsync("Mentor Khác");
        var collision = await Admin.PutAsJsonAsync($"/api/lecturers/{lecturer}", new { fullName = "Giảng viên thử", email = taken.Email });
        Assert.Equal(HttpStatusCode.Conflict, collision.StatusCode);
        Assert.Equal(newEmail, await LecturerEmailAsync(lecturer)); // cả hai giữ nguyên
    }

    [Fact]
    public async Task Lecturer_email_must_not_belong_to_another_mentor()
    {
        if (!Enabled)
        {
            return;
        }

        var mentor = await CreateAsync("Mentor Ngoài");
        var created = await Admin.PostAsJsonAsync("/api/lecturers", new { fullName = "Trùng mentor", email = mentor.Email.ToUpperInvariant() });
        Assert.Equal(HttpStatusCode.Conflict, created.StatusCode);
        Assert.Contains("email-exists", await created.Content.ReadAsStringAsync());

        var lecturer = await CreateLecturerAsync();
        var updated = await Admin.PutAsJsonAsync($"/api/lecturers/{lecturer}", new { fullName = "Trùng mentor", email = mentor.Email });
        Assert.Equal(HttpStatusCode.Conflict, updated.StatusCode);
        Assert.Contains("email-exists", await updated.Content.ReadAsStringAsync());

        var linked = await CreateAsync("Giảng viên kiêm mentor", isInternal: true, lecturerId: lecturer);
        var sameEmail = await Admin.PutAsJsonAsync($"/api/lecturers/{lecturer}", new { fullName = "Đổi tên", email = linked.Email });
        Assert.Equal(HttpStatusCode.OK, sameEmail.StatusCode); // mentor của chính giảng viên không tính là trùng
    }

    [Fact]
    public async Task Locking_a_lecturer_locks_the_linked_mentor_but_unlocking_does_not()
    {
        if (!Enabled)
        {
            return;
        }

        var lecturer = await CreateLecturerAsync();
        var email = await LecturerEmailAsync(lecturer);
        var mentor = await CreateAsync("Giảng viên kiêm mentor", isInternal: true, lecturerId: lecturer);
        var other = await CreateAsync("Mentor Khác");

        Task<HttpResponseMessage> LecturerActiveAsync(bool isActive) =>
            Admin.PutAsJsonAsync($"/api/lecturers/{lecturer}", new { fullName = "Giảng viên thử", email, isActive });
        Task<MentorResponse?> MentorAsync() => Admin.GetFromJsonAsync<MentorResponse>($"/api/mentors/{mentor.Id}");

        Assert.Equal(HttpStatusCode.OK, (await LecturerActiveAsync(false)).StatusCode);
        Assert.False((await MentorAsync())!.IsActive);
        var events = await EventsAsync(mentor.Id);
        Assert.Equal(2, events.Count);
        Assert.Equal(("MentorRegistered", false), (events[^1].EventType, JsonDocument.Parse(events[^1].Payload).RootElement.GetProperty("isActive").GetBoolean()));
        Assert.True((await Admin.GetFromJsonAsync<MentorResponse>($"/api/mentors/{other.Id}"))!.IsActive);

        Assert.Equal(HttpStatusCode.OK, (await LecturerActiveAsync(false)).StatusCode); // đã khóa: không phát lại
        Assert.Equal(2, (await EventsAsync(mentor.Id)).Count);

        Assert.Equal(HttpStatusCode.OK, (await LecturerActiveAsync(true)).StatusCode);
        Assert.False((await MentorAsync())!.IsActive); // Bộ môn tự mở
        Assert.Equal(2, (await EventsAsync(mentor.Id)).Count);

        var reopened = await PutAsync(mentor.Id, new { fullName = mentor.FullName, maxTeams = 3, isActive = true });
        Assert.True(reopened.IsActive);
    }

    [Fact]
    public async Task Mentor_of_a_locked_lecturer_cannot_be_reopened_but_other_edits_work()
    {
        if (!Enabled)
        {
            return;
        }

        var lecturer = await CreateLecturerAsync();
        var mentor = await CreateAsync("Giảng viên kiêm mentor", isInternal: true, lecturerId: lecturer);
        var email = await LecturerEmailAsync(lecturer);
        Assert.Equal(HttpStatusCode.OK,
            (await Admin.PutAsJsonAsync($"/api/lecturers/{lecturer}", new { fullName = "Giảng viên thử", email, isActive = false })).StatusCode);
        var eventsBefore = (await EventsAsync(mentor.Id)).Count;

        // Mở khóa mentor khi giảng viên còn khóa → 409, không lưu, không phát sự kiện.
        var reopen = await Admin.PutAsJsonAsync($"/api/mentors/{mentor.Id}", new { fullName = "Tên mới", maxTeams = 5, isActive = true });
        Assert.Equal(HttpStatusCode.Conflict, reopen.StatusCode);
        Assert.Contains("lecturer-locked", await reopen.Content.ReadAsStringAsync());
        var unchanged = (await Admin.GetFromJsonAsync<MentorResponse>($"/api/mentors/{mentor.Id}"))!;
        Assert.Equal(("Giảng viên kiêm mentor", 3, false), (unchanged.FullName, unchanged.MaxTeams, unchanged.IsActive));
        Assert.Equal(eventsBefore, (await EventsAsync(mentor.Id)).Count);

        // Sửa khác vẫn được: giữ khóa rõ ràng hoặc bỏ trống isActive.
        var keptLocked = await PutAsync(mentor.Id, new { fullName = "Tên mới", maxTeams = 5, isActive = false });
        Assert.Equal(("Tên mới", 5, false), (keptLocked.FullName, keptLocked.MaxTeams, keptLocked.IsActive));
        var noFlag = await PutAsync(mentor.Id, new { fullName = "Tên mới nữa", maxTeams = 4 });
        Assert.Equal(("Tên mới nữa", 4, false), (noFlag.FullName, noFlag.MaxTeams, noFlag.IsActive));

        // Mở giảng viên rồi mới mở được mentor.
        Assert.Equal(HttpStatusCode.OK,
            (await Admin.PutAsJsonAsync($"/api/lecturers/{lecturer}", new { fullName = "Giảng viên thử", email, isActive = true })).StatusCode);
        Assert.True((await PutAsync(mentor.Id, new { fullName = "Tên mới nữa", maxTeams = 4, isActive = true })).IsActive);
    }

    [Fact]
    public async Task Locked_mentor_of_a_lecturer_stays_locked_when_the_lecturer_changes_email()
    {
        if (!Enabled)
        {
            return;
        }

        var lecturer = await CreateLecturerAsync();
        var mentor = await CreateAsync("Giảng viên kiêm mentor", isInternal: true, lecturerId: lecturer, isActive: false);
        var newEmail = NewEmail();
        Assert.Equal(HttpStatusCode.OK, (await Admin.PutAsJsonAsync($"/api/lecturers/{lecturer}", new { fullName = "Giảng viên thử", email = newEmail })).StatusCode);

        var after = (await Admin.GetFromJsonAsync<MentorResponse>($"/api/mentors/{mentor.Id}"))!;
        Assert.Equal((newEmail, false), (after.Email, after.IsActive));
    }

    [Fact]
    public async Task Linked_mentor_needs_no_email_but_others_do()
    {
        if (!Enabled)
        {
            return;
        }

        var lecturer = await CreateLecturerAsync();
        var email = await LecturerEmailAsync(lecturer);
        var created = await Admin.PostAsJsonAsync("/api/mentors", new { fullName = "Không gửi email", maxTeams = 2, isInternal = true, lecturerId = lecturer });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var mentor = (await created.Content.ReadFromJsonAsync<MentorResponse>())!;
        Assert.Equal(email, mentor.Email);

        var kept = await PutAsync(mentor.Id, new { fullName = "Sửa, không gửi lecturerId", maxTeams = 3, email = "khong-phai-email" }); // liên kết cũ, email bị bỏ qua
        Assert.Equal((email, lecturer), (kept.Email, kept.LecturerId!.Value));

        var noEmail = await Admin.PostAsJsonAsync("/api/mentors", new { fullName = "Ngoài trường", maxTeams = 2 });
        Assert.Equal(HttpStatusCode.BadRequest, noEmail.StatusCode);
        Assert.Contains("email", await noEmail.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin.PostAsJsonAsync("/api/mentors", new { fullName = "Ngoài trường", email = "khong-phai-email", maxTeams = 2 })).StatusCode);

        var unlink = await Admin.PutAsJsonAsync($"/api/mentors/{mentor.Id}", new { fullName = "Ra ngoài trường", maxTeams = 3, isInternal = false });
        Assert.Equal(HttpStatusCode.BadRequest, unlink.StatusCode); // thôi gắn giảng viên phải có email riêng

        var external = await CreateAsync("Mentor Ngoài");
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin.PutAsJsonAsync($"/api/mentors/{external.Id}", new { fullName = "Thiếu email", maxTeams = 3 })).StatusCode);
    }

    [Fact]
    public async Task Locked_lecturer_cannot_be_linked_until_unlocked()
    {
        if (!Enabled)
        {
            return;
        }

        var lecturer = await CreateLecturerAsync();
        await SetLecturerActiveAsync(lecturer, false);
        var body = new { fullName = "Gắn giảng viên khóa", email = NewEmail(), maxTeams = 2, isInternal = true, lecturerId = lecturer };
        var locked = await Admin.PostAsJsonAsync("/api/mentors", body);
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        Assert.Contains("lecturer-locked", await locked.Content.ReadAsStringAsync());

        var mentor = await CreateAsync("Mentor Trong Trường", isInternal: true);
        var relink = await Admin.PutAsJsonAsync($"/api/mentors/{mentor.Id}", new { fullName = mentor.FullName, email = mentor.Email, maxTeams = 2, lecturerId = lecturer });
        Assert.Contains("lecturer-locked", await relink.Content.ReadAsStringAsync());

        await SetLecturerActiveAsync(lecturer, true);
        Assert.Equal(lecturer, (await PutAsync(mentor.Id, new { fullName = mentor.FullName, email = mentor.Email, maxTeams = 2, lecturerId = lecturer })).LecturerId);

        await SetLecturerActiveAsync(lecturer, false); // khóa sau khi đã gắn: mentor vẫn sửa được
        Assert.Equal(lecturer, (await PutAsync(mentor.Id, new { fullName = "Vẫn sửa được", email = mentor.Email, maxTeams = 3 })).LecturerId);
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

    private Task<string> LecturerEmailAsync(Guid id) => _api.QueryDbAsync(db => db.Lecturers.Where(l => l.Id == id).Select(l => l.Email).SingleAsync());

    private Task<int> SetLecturerActiveAsync(Guid id, bool isActive) =>
        _api.QueryDbAsync(db => db.Lecturers.Where(l => l.Id == id).ExecuteUpdateAsync(s => s.SetProperty(l => l.IsActive, isActive)));

    private Task<string> CreateStudentAsync() => _api.QueryDbAsync(async db =>
    {
        var email = NewEmail();
        var id = Guid.NewGuid();
        var specialization = await db.Specializations.Where(s => s.IsActive).OrderBy(s => s.Id).Select(s => s.Id).FirstAsync();
        db.Students.Add(new Student { Id = id, StudentCode = $"ZM{Random.Shared.Next(0, 1_000_000):D6}", FullName = "Sinh viên thử", Email = email, SpecializationId = specialization });
        await db.SaveChangesAsync();
        _students.Add(id);
        return email;
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
                await db.Set<OutboxMessage>().Where(m => ids.Contains(m.AggregateId) || _lecturers.Contains(m.AggregateId)).ExecuteDeleteAsync();
                await db.Mentors.Where(m => ids.Contains(m.Id)).ExecuteDeleteAsync(); // mentor_domains xóa theo ON DELETE CASCADE
                await db.Students.Where(s => _students.Contains(s.Id)).ExecuteDeleteAsync();
                return await db.Lecturers.Where(l => _lecturers.Contains(l.Id)).ExecuteDeleteAsync();
            });
        }
        await _api.DisposeAsync();
    }
}
