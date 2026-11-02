using System.Net;
using System.Net.Http.Json;
using System.Text;
using BuildingBlocks.Auth;
using Microsoft.Extensions.Caching.Memory;
using Team.Services.Academic;
using Team.Services.AdvisorMatching;

namespace Team.Tests;

/// <summary>Phần của BE-80 không cần PostgreSQL: loại cố vấn, phân trang Academic (giảng viên, mentor), tuần bắt đầu mentor, validate, quyền.</summary>
public sealed class AdvisorMatchingRulesTests : IAsyncLifetime
{
    private readonly TeamApiFactory _api = new();

    [Theory]
    [InlineData("LECTURER", "LECTURER")]
    [InlineData("lecturer", "LECTURER")]
    [InlineData("lecturers", "LECTURER")]
    [InlineData("MENTOR", "MENTOR")]
    [InlineData("mentors", "MENTOR")]
    public void Kind_accepts_contract_values_and_project_context_plural(string kind, string expected) =>
        Assert.Equal(expected, AdvisorMatchingKinds.Parse(kind));

    [Theory]
    [InlineData("")]
    [InlineData("STUDENT")]
    [InlineData(null)]
    public void Unknown_kind_is_a_validation_error(string? kind) =>
        Assert.Equal(400, Assert.Throws<BuildingBlocks.Errors.RequestValidationException>(() => AdvisorMatchingKinds.Parse(kind)).Status);

    [Fact]
    public void Advisor_is_blocked_by_lecturer_identity_for_both_kinds()
    {
        var teamId = Guid.NewGuid();
        var lecturerId = Guid.NewGuid();
        var blocked = new Dictionary<Guid, Guid> { [teamId] = lecturerId };
        var lecturer = LecturerAdvisorSource.ToCandidate(new LecturerSnapshot(lecturerId, "GV An", "an@example.edu", [], null, null, true), "EXE101");
        var internalMentor = new AdvisorCandidate(Guid.NewGuid(), "Mentor An", [], 3, 0, LecturerId: lecturerId);
        var externalMentor = new AdvisorCandidate(Guid.NewGuid(), "Mentor Ngoài", [], 3, 0);

        Assert.True(lecturer.IsBlockedAt(teamId, blocked));
        Assert.True(internalMentor.IsBlockedAt(teamId, blocked));
        Assert.False(externalMentor.IsBlockedAt(teamId, blocked));
        Assert.False(lecturer.IsBlockedAt(Guid.NewGuid(), blocked));
    }

    [Fact]
    public async Task Unknown_kind_answers_400() =>
        await TestData.AssertProblemAsync(await _api.ClientFor(null, Roles.Admin).PostAsJsonAsync("/api/advisor-matching/TA/preview",
            new { courseCode = "EXE101" }), 400, "validation");

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Lecturer)]
    [InlineData(Roles.Student)]
    public async Task Internal_run_needs_service_token(string role) =>
        await TestData.AssertProblemAsync(await _api.ClientFor(null, role).PostAsJsonAsync("/internal/advisor-matching/LECTURER",
            new { semesterId = Guid.NewGuid(), courseCode = "EXE101" }), 403, null);

    [Theory]
    [InlineData("POST", "/api/advisor-matching/LECTURER/preview")]
    [InlineData("POST", "/api/advisor-matching/runs/00000000-0000-0000-0000-000000000001/confirm")]
    [InlineData("DELETE", "/api/advisor-matching/runs/00000000-0000-0000-0000-000000000001")]
    [InlineData("GET", "/api/advisor-matching/LECTURER/overview")]
    [InlineData("PUT", "/api/teams/00000000-0000-0000-0000-000000000001/instructor")]
    [InlineData("GET", "/api/advisor-matching/MENTOR/overview")]
    [InlineData("POST", "/api/advisor-matching/MENTOR/preview")]
    [InlineData("PUT", "/api/teams/00000000-0000-0000-0000-000000000001/mentor-assignment")]
    [InlineData("DELETE", "/api/teams/00000000-0000-0000-0000-000000000001/mentor-assignment")]
    public async Task Admin_endpoints_reject_other_roles(string method, string path)
    {
        foreach (var role in new[] { Roles.Student, Roles.Lecturer, Roles.Mentor, Roles.Service })
        {
            var request = new HttpRequestMessage(new HttpMethod(method), path)
            {
                Content = method is "POST" or "PUT" ? JsonContent.Create(new { courseCode = "EXE101", lecturerId = Guid.NewGuid(), mentorId = Guid.NewGuid(), note = "x" }) : null,
            };
            await TestData.AssertProblemAsync(await _api.ClientFor(Guid.NewGuid(), role).SendAsync(request), 403, null);
        }
        var anonymous = await _api.CreateClient().SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Internal_run_validates_body()
    {
        var job = _api.ClientFor(null, Roles.Service);
        await TestData.AssertProblemAsync(await job.PostAsJsonAsync("/internal/advisor-matching/LECTURER", new { semesterId = Guid.Empty, courseCode = "EXE101" }), 400, "validation");
        await TestData.AssertProblemAsync(await job.PostAsJsonAsync("/internal/advisor-matching/LECTURER", new { semesterId = Guid.NewGuid(), courseCode = "EXE301" }), 400, "validation");
    }

    [Fact]
    public void Instructor_change_needs_a_note_of_at_most_500_characters()
    {
        var validator = new ChangeInstructorInputValidator();
        Assert.True(validator.Validate(new ChangeInstructorInput(Guid.NewGuid(), "Bộ môn phân công")).IsValid);
        Assert.False(validator.Validate(new ChangeInstructorInput(Guid.NewGuid(), " ")).IsValid);
        Assert.False(validator.Validate(new ChangeInstructorInput(Guid.NewGuid(), new string('x', 501))).IsValid);
        Assert.False(validator.Validate(new ChangeInstructorInput(Guid.Empty, "ok")).IsValid);
    }

    [Fact]
    public void Max_teams_follows_the_course()
    {
        var lecturer = new LecturerSnapshot(Guid.NewGuid(), "GV", "gv@example.edu", [], 3, null, true);
        Assert.Equal(3, LecturerAdvisorSource.MaxTeamsFor(lecturer, "EXE101"));
        Assert.Null(LecturerAdvisorSource.MaxTeamsFor(lecturer, "EXE201"));
    }

    [Fact]
    public async Task Academic_lecturer_list_is_read_page_by_page_with_the_largest_allowed_page_size()
    {
        var handler = new PagedLecturersHandler(totalPages: 3);
        var client = new AcademicClient(new HttpClient(handler) { BaseAddress = new Uri("http://academic") }, new MemoryCache(new MemoryCacheOptions()));

        var lecturers = await client.ListActiveLecturersAsync(CancellationToken.None);

        Assert.Equal(new[] { 1, 2, 3 }, handler.Pages);
        Assert.All(handler.Queries, q => Assert.Contains("pageSize=100", q));
        // Mỗi trang có một giảng viên đã khóa – bị bỏ.
        Assert.Equal(3, lecturers.Count);
        Assert.All(lecturers, l => Assert.True(l.IsActive));
        Assert.Equal(new int?[] { 5, 5, 5 }, lecturers.Select(l => l.MaxTeamsExe101));
    }

    [Fact]
    public void Mentor_change_needs_a_note_and_removal_reason_is_optional()
    {
        var validator = new ChangeMentorInputValidator();
        Assert.True(validator.Validate(new ChangeMentorInput(Guid.NewGuid(), "Nhóm xin đổi")).IsValid);
        Assert.False(validator.Validate(new ChangeMentorInput(Guid.NewGuid(), "")).IsValid);
        Assert.False(validator.Validate(new ChangeMentorInput(Guid.NewGuid(), new string('x', 501))).IsValid);
        Assert.False(validator.Validate(new ChangeMentorInput(Guid.Empty, "ok")).IsValid);
        Assert.True(new RemoveMentorInputValidator().Validate(new RemoveMentorInput(null)).IsValid);
        Assert.False(new RemoveMentorInputValidator().Validate(new RemoveMentorInput(new string('x', 501))).IsValid);
    }

    [Fact]
    public async Task Academic_mentor_list_is_read_page_by_page_and_keeps_locked_mentors_for_the_caller_to_filter()
    {
        var handler = new PagedMentorsHandler(totalPages: 2);
        var client = new AcademicClient(new HttpClient(handler) { BaseAddress = new Uri("http://academic") }, new MemoryCache(new MemoryCacheOptions()));

        var mentors = await client.ListMentorsAsync(CancellationToken.None);

        Assert.Equal(new[] { 1, 2 }, handler.Pages);
        Assert.All(handler.Queries, q => Assert.Contains("pageSize=100", q));
        Assert.Equal(4, mentors.Count);
        var active = mentors.First(m => m.IsActive);
        Assert.Equal((4, (int?)2, "Startup X", true), (active.MaxTeams, active.PreferredTeams, active.Organization, active.IsInternal));
        Assert.NotNull(active.LecturerId);
        Assert.Equal(7, active.Domains.Single().Id);

        // Nguồn ghép mentor chỉ giữ mentor đang hoạt động; preferredTeams null → 0 (không đặt).
        var source = new MentorAdvisorSource(new FakeAcademicWith(mentors), null!, null!);
        var candidates = await source.ListActiveAsync("EXE101", CancellationToken.None);
        Assert.Equal(2, candidates.Count);
        Assert.All(candidates, c => Assert.Equal((4, 2), (c.MaxTeams, c.PreferredTeams)));
        Assert.Equal(0, MentorAdvisorSource.ToCandidate(mentors.First(m => !m.IsActive)).PreferredTeams);
    }

    [Theory]
    [InlineData(6, "2026-10-12T00:00:00+07:00")]
    [InlineData(null, "2026-10-12T00:00:00+07:00")]
    [InlineData(1, "2026-09-07T00:00:00+07:00")]
    public void Mentoring_starts_at_the_beginning_of_the_start_week_vietnam_time(int? week, string expected) =>
        Assert.Equal(DateTimeOffset.Parse(expected),
            MentoringSchedule.StartsAt(new SemesterDto(Guid.NewGuid(), "FA26", "Fall 2026", new DateOnly(2026, 9, 7), week)));

    [Fact]
    public void Mentoring_start_is_unknown_without_semester_start_date() =>
        Assert.Null(MentoringSchedule.StartsAt(new SemesterDto(Guid.NewGuid(), "FA26", "Fall 2026")));

    [Fact]
    public async Task Academic_outage_while_listing_lecturers_surfaces_as_http_error()
    {
        var client = new AcademicClient(new HttpClient(new FailingHandler()) { BaseAddress = new Uri("http://academic") },
            new MemoryCache(new MemoryCacheOptions()));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.ListActiveLecturersAsync(CancellationToken.None));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _api.DisposeAsync();

    private sealed class PagedLecturersHandler(int totalPages) : HttpMessageHandler
    {
        public List<int> Pages { get; } = [];

        public List<string> Queries { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("/api/lecturers", request.RequestUri!.AbsolutePath);
            var query = request.RequestUri.Query;
            Queries.Add(query);
            var page = int.Parse(System.Web.HttpUtility.ParseQueryString(query)["page"]!);
            Pages.Add(page);
            var json = $$"""
                {"items":[
                  {"id":"{{Guid.NewGuid()}}","fullName":"GV {{page}}","email":"a{{page}}@example.edu","domains":[{"id":1,"code":"D01","name":"AI"}],"maxTeamsExe101":5,"maxTeamsExe201":null,"isActive":true},
                  {"id":"{{Guid.NewGuid()}}","fullName":"GV khóa {{page}}","email":"b{{page}}@example.edu","domains":[],"maxTeamsExe101":null,"maxTeamsExe201":null,"isActive":false}
                ],"page":{{page}},"pageSize":100,"totalCount":{{totalPages * 2}},"totalPages":{{totalPages}}}
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class PagedMentorsHandler(int totalPages) : HttpMessageHandler
    {
        public List<int> Pages { get; } = [];

        public List<string> Queries { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("/api/mentors", request.RequestUri!.AbsolutePath);
            var query = request.RequestUri.Query;
            Queries.Add(query);
            var page = int.Parse(System.Web.HttpUtility.ParseQueryString(query)["page"]!);
            Pages.Add(page);
            // Schema Mentor của academic.yaml; currentTeamCount không đọc (Team tự đếm).
            var json = $$"""
                {"items":[
                  {"id":"{{Guid.NewGuid()}}","fullName":"Mentor {{page}}","email":"m{{page}}@example.com","organization":"Startup X","isInternal":true,
                   "lecturerId":"{{Guid.NewGuid()}}","description":null,"domains":[{"id":7,"code":"D07","name":"Fintech"}],"maxTeams":4,"preferredTeams":2,
                   "currentTeamCount":0,"isActive":true},
                  {"id":"{{Guid.NewGuid()}}","fullName":"Mentor khóa {{page}}","email":"k{{page}}@example.com","organization":null,"isInternal":false,
                   "lecturerId":null,"description":null,"domains":[],"maxTeams":3,"preferredTeams":null,"currentTeamCount":0,"isActive":false}
                ],"page":{{page}},"pageSize":100,"totalCount":{{totalPages * 2}},"totalPages":{{totalPages}}}
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class FakeAcademicWith(IReadOnlyList<MentorSnapshot> mentors) : IAcademicClient
    {
        public Task<IReadOnlyList<MentorSnapshot>> ListMentorsAsync(CancellationToken ct) => Task.FromResult(mentors);
        public Task<SemesterDto?> GetCurrentSemesterAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<RoundDto?> GetOpenRoundAsync(string courseCode, string roundType, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<StudentSnapshot>> LookupStudentsAsync(Guid semesterId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<StudentSnapshot>> LookupStudentsByCodeAsync(Guid? semesterId, IReadOnlyCollection<string> studentCodes, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<CatalogItem>> GetDomainsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<CatalogItem>> GetSpecializationsAsync(CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }
}
