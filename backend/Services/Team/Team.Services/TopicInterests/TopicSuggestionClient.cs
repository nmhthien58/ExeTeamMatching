using ExeTeamMatching.Protos.Matching.V1;
using Grpc.Core;
using Team.Services.Matching;

namespace Team.Services.TopicInterests;

public sealed record TopicSuggestionItem(Guid TeamId, IReadOnlyList<int> MatchedDomainIds, int OtherInterestedCount, bool WasPreviousInstructor);

public interface ITopicSuggestionClient
{
    Task<IReadOnlyList<TopicSuggestionItem>> SuggestAsync(Guid semesterId, string? courseCode, string advisorKind, Guid advisorId, int page, int pageSize, CancellationToken ct);
}

public sealed class TopicSuggestionClient(MatchingService.MatchingServiceClient grpc) : ITopicSuggestionClient
{
    public async Task<IReadOnlyList<TopicSuggestionItem>> SuggestAsync(Guid semesterId, string? courseCode, string advisorKind, Guid advisorId, int page, int pageSize, CancellationToken ct)
    {
        var size = Math.Clamp(pageSize, 1, 100);
        var offset = (int)Math.Min((long)(Math.Max(page, 1) - 1) * size, int.MaxValue);
        try
        {
            var response = await grpc.SuggestTopicsForLecturerAsync(new SuggestTopicsForLecturerRequest
            {
                SemesterId = semesterId.ToString(), LecturerId = advisorId.ToString(),
                Course = courseCode == "EXE101" ? Course.Exe101 : courseCode == "EXE201" ? Course.Exe201 : Course.Unspecified,
                AdvisorKind = advisorKind == AdvisorKinds.Mentor ? AdvisorKind.Mentor : AdvisorKind.Lecturer,
                Limit = size, Offset = offset,
            }, new CallOptions(deadline: DateTime.UtcNow.AddSeconds(5), cancellationToken: ct));
            return response.Suggestions.Select(s => new TopicSuggestionItem(Guid.Parse(s.TeamId), s.MatchedDomainIds.ToList(), s.InterestedLecturerCount, s.WasLecturerExe101Team)).ToList();
        }
        catch (RpcException ex) when (ex.StatusCode != StatusCode.Cancelled || !ct.IsCancellationRequested)
        {
            throw MatchingErrors.Unavailable();
        }
    }
}
