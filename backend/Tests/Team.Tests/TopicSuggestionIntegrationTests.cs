using ExeTeamMatching.Protos.Matching.V1;
using Grpc.Net.Client;
using Team.Services.TopicInterests;

namespace Team.Tests;

// Chạy với Matching HTTP/2 tại 127.0.0.1:15004 và Redis thử nghiệm riêng tại 16379,
// Kafka:ConsumersEnabled=false. Trước khi chạy, nạp Redis bằng redis-cli:
// HSET matching:advisor:lecturer:a0000000-0000-4000-8000-000000000002 domainIds "[1,2]" active 1
// HSET matching:team:a0000000-0000-4000-8000-000000000003 semesterId a0000000-0000-4000-8000-000000000001 courseCode EXE201 name "Team One" status OPEN domainIds "[1,2]"
// HSET matching:team:a0000000-0000-4000-8000-000000000004 semesterId a0000000-0000-4000-8000-000000000001 courseCode EXE201 name "Team Two" status OPEN domainIds "[1]"
// SADD matching:teams:a0000000-0000-4000-8000-000000000001:EXE201 a0000000-0000-4000-8000-000000000003 a0000000-0000-4000-8000-000000000004
// Chỉ nạp vào Redis thử nghiệm; đặt EXE_MATCHING_INTEGRATION=1 để chạy test.
public sealed class TopicSuggestionIntegrationTests
{
    private sealed class MatchingIntegrationFactAttribute : FactAttribute
    {
        public MatchingIntegrationFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("EXE_MATCHING_INTEGRATION") != "1")
                Skip = "Cần Matching và Redis thử nghiệm riêng.";
        }
    }

    [MatchingIntegrationFact]
    public async Task CallsMatchingGrpcAndPreservesRankingAndPagination()
    {
        using var channel = GrpcChannel.ForAddress("http://127.0.0.1:15004");
        var client = new TopicSuggestionClient(new MatchingService.MatchingServiceClient(channel));
        var semester = Guid.Parse("a0000000-0000-4000-8000-000000000001");
        var advisor = Guid.Parse("a0000000-0000-4000-8000-000000000002");
        var first = await client.SuggestAsync(semester, "EXE201", "LECTURER", advisor, 1, 1, CancellationToken.None);
        var second = await client.SuggestAsync(semester, "EXE201", "LECTURER", advisor, 2, 1, CancellationToken.None);

        Assert.Equal(Guid.Parse("a0000000-0000-4000-8000-000000000003"), Assert.Single(first).TeamId);
        Assert.Equal(new[] { 1, 2 }, first[0].MatchedDomainIds);
        Assert.Equal(Guid.Parse("a0000000-0000-4000-8000-000000000004"), Assert.Single(second).TeamId);
        Assert.Equal(new[] { 1 }, second[0].MatchedDomainIds);
    }
}
