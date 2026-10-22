using Matching.Services.ReadModel;

namespace Matching.Services.TopicSuggestions;

public sealed record TopicQuery(Guid SemesterId, string? CourseCode, AdvisorKind Kind, Guid AdvisorId, int Limit, int Offset);

public sealed record TopicSuggestion(
    TeamView Team,
    IReadOnlyList<int> MatchedDomainIds,
    int InterestedOthersCount,
    bool WasAdvisorExe101Team,
    double Score,
    IReadOnlyList<string> Reasons);

public sealed record TopicSuggestionPage(IReadOnlyList<TopicSuggestion> Items, int Total);

/// <summary>
/// SuggestTopicsForLecturer (BE-71, rule 19, 26): nhóm chưa có cố vấn cùng loại, có domain trùng chuyên môn của cố vấn.
/// Xếp: đề tài giảng viên đã hướng dẫn ở EXE101 trước, rồi trùng nhiều domain, rồi ít người khác quan tâm
/// (rải quan tâm đều cho các đề tài). Phục vụ cả giảng viên và mentor.
/// </summary>
public sealed class TopicSuggestionService(IMatchingReadModel model)
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    public async Task<TopicSuggestionPage> SuggestAsync(TopicQuery query, CancellationToken ct)
    {
        var advisor = await model.GetAdvisorAsync(query.Kind, query.AdvisorId, ct);
        if (advisor is not { IsActive: true })
        {
            return new TopicSuggestionPage([], 0); // hồ sơ chưa đồng bộ tới Matching hoặc đã ngừng hoạt động
        }

        var advisorDomains = advisor.DomainIds.ToHashSet();
        var ranked = (await model.GetTeamsAsync(query.SemesterId, query.CourseCode, ct))
            .Where(t => t.AdvisorId(query.Kind) is null)
            .Select(t => Evaluate(t, query, advisorDomains))
            .Where(s => s.MatchedDomainIds.Count > 0 || s.WasAdvisorExe101Team)
            .OrderByDescending(s => s.WasAdvisorExe101Team)
            .ThenByDescending(s => s.MatchedDomainIds.Count)
            .ThenBy(s => s.InterestedOthersCount)
            .ThenBy(s => s.Team.Name, StringComparer.Ordinal)
            .ThenBy(s => s.Team.Id)
            .ToList();

        var limit = query.Limit <= 0 ? DefaultLimit : Math.Min(query.Limit, MaxLimit);
        var items = ranked.Skip(Math.Max(0, query.Offset)).Take(limit).ToList();
        return new TopicSuggestionPage(items, ranked.Count);
    }

    private static TopicSuggestion Evaluate(TeamView team, TopicQuery query, IReadOnlySet<int> advisorDomains)
    {
        var matched = team.DomainIds.Where(advisorDomains.Contains).ToList();
        var others = team.InterestedAdvisorIds(query.Kind).Count(id => id != query.AdvisorId);
        var wasExe101 = query.Kind == AdvisorKind.Lecturer
            && team.CourseCode == Courses.Exe201
            && team.ProjectExe101LecturerId == query.AdvisorId;

        var reasons = new List<string>();
        if (wasExe101)
        {
            reasons.Add("Bạn đã hướng dẫn đề tài này ở EXE101");
        }
        if (matched.Count > 0)
        {
            reasons.Add($"Trùng {matched.Count} domain chuyên môn của bạn");
        }
        var who = query.Kind == AdvisorKind.Mentor ? "mentor" : "giảng viên";
        reasons.Add(others == 0 ? $"Chưa có {who} nào khác quan tâm" : $"{others} {who} khác đang quan tâm");

        var overlap = team.DomainIds.Count == 0 ? 0 : (double)matched.Count / team.DomainIds.Count;
        var score = (wasExe101 ? 0.4 : 0) + 0.5 * overlap + 0.1 / (1 + others);
        return new TopicSuggestion(team, matched, others, wasExe101, Math.Round(Math.Min(score, 1), 4), reasons);
    }
}
