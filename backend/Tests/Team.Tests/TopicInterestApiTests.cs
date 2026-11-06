using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Auth;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Team.Services.Teams;
using Team.Services.TopicInterests;

namespace Team.Tests;

public sealed class TopicInterestApiTests
{
    private sealed class FakeSettings : ITopicInterestSettingsClient
    {
        public Task<TopicInterestSettings> GetAsync(CancellationToken ct) => Task.FromResult(new TopicInterestSettings(2, 10));
    }

    private sealed class IntegrationFactAttribute : FactAttribute
    {
        public IntegrationFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("EXE_INTEGRATION") != "1")
                Skip = "Cần EXE_INTEGRATION=1 và PostgreSQL thử nghiệm.";
        }
    }

    [IntegrationFact]
    public async Task LecturerCanOpenAndWithdraw_WithPersistedOutboxAndMemberVisibility()
    {
        await using var api = new TeamApiFactory();
        var student = api.Academic.Enroll();
        using var configured = api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITopicInterestSettingsClient>();
            services.AddSingleton<ITopicInterestSettingsClient, FakeSettings>();
        }));
        using var studentClient = configured.CreateClient();
        using var originalStudent = api.ClientFor(student.Id);
        studentClient.DefaultRequestHeaders.Authorization = originalStudent.DefaultRequestHeaders.Authorization;
        var create = await studentClient.PostAsJsonAsync("/api/teams", new TeamInput("BE73 " + Guid.NewGuid().ToString("N"), null, null, [1], null));
        create.EnsureSuccessStatusCode();
        var team = (await create.Content.ReadFromJsonAsync<TeamDetailResponse>())!;

        var lecturerId = Guid.NewGuid();
        using var lecturerClient = configured.CreateClient();
        var claims = new Dictionary<string, object>
        {
            [ClaimNames.Subject] = Guid.NewGuid().ToString(),
            [ClaimNames.Name] = "Giảng viên thử nghiệm",
            [ClaimNames.Roles] = new[] { Roles.Lecturer },
            [ClaimNames.LecturerId] = lecturerId.ToString(),
        };
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "exe-identity", Audience = "exe-team-matching", Claims = claims,
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TeamApiFactory.SigningKey)), SecurityAlgorithms.HmacSha256),
        });
        lecturerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var open = await lecturerClient.PostAsJsonAsync($"/api/teams/{team.Id}/topic-interests", new OpenTopicInterestInput("LECTURER"));
        Assert.Equal(HttpStatusCode.Created, open.StatusCode);
        var interest = (await open.Content.ReadFromJsonAsync<TopicInterestResponse>())!;
        var visible = await studentClient.GetFromJsonAsync<List<TopicInterestResponse>>($"/api/teams/{team.Id}/topic-interests");
        Assert.Equal(interest.Id, Assert.Single(visible!).Id);

        var opened = Assert.Single((await api.EventsAsync(team.Id)).Where(e => e.EventType == "TopicInterestOpened"));
        using var payload = JsonDocument.Parse(opened.Payload);
        Assert.Contains(student.Id.ToString(), payload.RootElement.GetProperty("memberStudentIds").EnumerateArray().Select(x => x.GetString()));

        var withdrawn = await lecturerClient.DeleteAsync($"/api/topic-interests/{interest.Id}");
        Assert.Equal(HttpStatusCode.NoContent, withdrawn.StatusCode);
        var after = await studentClient.GetFromJsonAsync<List<TopicInterestResponse>>($"/api/teams/{team.Id}/topic-interests");
        Assert.Empty(after!);
        var closed = Assert.Single((await api.EventsAsync(team.Id)).Where(e => e.EventType == "TopicInterestClosed"));
        using var closePayload = JsonDocument.Parse(closed.Payload);
        Assert.Equal("WITHDRAWN", closePayload.RootElement.GetProperty("reason").GetString());
    }
}
