using System.Net;
using System.Text;
using Team.Services.TopicInterests;
using Team.Services.Academic;
using BuildingBlocks.Auth;
using Microsoft.Extensions.Options;

namespace Team.Tests;

public sealed class TopicInterestSettingsClientTests
{
    private sealed class AcademicIntegrationFactAttribute : FactAttribute
    {
        public AcademicIntegrationFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("EXE_ACADEMIC_SETTINGS_TEST") != "1")
                Skip = "Cần Academic local và cấu hình JWT thử nghiệm.";
        }
    }

    [AcademicIntegrationFact]
    public async Task ReadsSettingsFromRunningAcademicWithServiceToken()
    {
        var jwt = new JwtOptions
        {
            SigningKey = Environment.GetEnvironmentVariable("EXE_TEST_JWT_KEY")!,
            Issuer = Environment.GetEnvironmentVariable("EXE_TEST_JWT_ISSUER") ?? "exe-identity",
            Audience = Environment.GetEnvironmentVariable("EXE_TEST_JWT_AUDIENCE") ?? "exe-team-matching",
        };
        using var handler = new ServiceTokenHandler(new ServiceTokenProvider(Options.Create(jwt), TimeProvider.System)) { InnerHandler = new HttpClientHandler() };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5002") };
        var result = await new TopicInterestSettingsClient(http).GetAsync(CancellationToken.None);
        Assert.InRange(result.InterestExtraSlots, 0, 10);
        Assert.InRange(result.MaxInterestsUnlimited, 1, 100);
    }

    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("/api/rule-settings", request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public async Task ReadsNonDefaultSettingsFromAcademic()
    {
        using var http = new HttpClient(new Handler(HttpStatusCode.OK, "{\"interestExtraSlots\":4,\"maxInterestsUnlimited\":17}")) { BaseAddress = new Uri("http://academic") };
        var result = await new TopicInterestSettingsClient(http).GetAsync(CancellationToken.None);
        Assert.Equal(new TopicInterestSettings(4, 17), result);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "{}")]
    [InlineData(HttpStatusCode.OK, "{}")]
    [InlineData(HttpStatusCode.OK, "{\"interestExtraSlots\":11,\"maxInterestsUnlimited\":10}")]
    public async Task RejectsMissingOrUnavailableSettings(HttpStatusCode status, string body)
    {
        using var http = new HttpClient(new Handler(status, body)) { BaseAddress = new Uri("http://academic") };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new TopicInterestSettingsClient(http).GetAsync(CancellationToken.None));
    }
}
