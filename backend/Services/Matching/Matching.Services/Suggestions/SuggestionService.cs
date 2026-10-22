using BuildingBlocks.Errors;
using Matching.Services.Composition;
using Matching.Services.ReadModel;

namespace Matching.Services.Suggestions;

public sealed record MemberSuggestion(StudentView Student, double Score, IReadOnlyList<string> Reasons);

public sealed record MemberSuggestionResult(
    IReadOnlyList<int> MissingSpecializationIds,
    IReadOnlyList<Skill> WeakSkills,
    IReadOnlyList<MemberSuggestion> Suggestions);

public sealed record TeamSuggestion(TeamView Team, IReadOnlyList<int> MissingSpecializationIds, double Score, IReadOnlyList<string> Reasons);

/// <summary>
/// SuggestMembers và SuggestTeams (BE-36, BE-71). Chỉ gợi ý sinh viên chưa có nhóm và nhóm chưa chốt còn chỗ,
/// cùng học kỳ và môn. Điểm 0..1 chỉ để sắp xếp; lý do là câu tiếng Việt hiện cho người dùng.
/// </summary>
public sealed class SuggestionService(IMatchingReadModel model)
{
    public const int DefaultLimit = 10;
    public const int MaxLimit = 50;

    // Trọng số: chuyên ngành còn thiếu quan trọng nhất (rule 2), sau đó bù kỹ năng yếu (rule 16).
    private const double MissingSpecializationWeight = 0.5;
    private const double NewSpecializationWeight = 0.15;
    private const double SkillWeight = 0.4;
    private const double SeekingBonus = 0.05;
    private const double ProfileBonus = 0.05;
    private const double UnderMinSizeBonus = 0.1;

    public async Task<MemberSuggestionResult> SuggestMembersAsync(Guid teamId, int limit, CancellationToken ct)
    {
        var rules = CompositionRules.Default;
        var team = await model.GetTeamAsync(teamId, ct) ?? throw new NotFoundException("nhóm", teamId);
        var members = await model.GetStudentsByIdsAsync(team.MemberIds, ct);
        var profile = TeamProfile.From(members);

        var candidates = (await model.GetStudentsAsync(team.SemesterId, team.CourseCode, ct))
            .Where(s => s.TeamId is null && !team.MemberIds.Contains(s.Id))
            .ToList();

        var missing = profile.NeedsSpecialization(rules)
            ? candidates.Where(c => c.SpecializationId > 0 && !profile.Specializations.Contains(c.SpecializationId))
                .GroupBy(c => c.SpecializationId)
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
                .Select(g => g.Key).ToList()
            : [];

        // Nhóm đã đủ người hoặc đã chốt: không gợi ý thêm (S18 hiện "Nhóm đã đủ 6").
        if (profile.MemberCount >= rules.MaxSize || team.Status == TeamStatuses.Finalized)
        {
            return new MemberSuggestionResult(missing, profile.WeakSkills, []);
        }

        var suggestions = candidates
            .Select(c => ScoreMember(c, profile, rules))
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Student.FullName, StringComparer.Ordinal)
            .ThenBy(s => s.Student.Id)
            .Take(NormalizeLimit(limit))
            .ToList();
        return new MemberSuggestionResult(missing, profile.WeakSkills, suggestions);
    }

    public async Task<IReadOnlyList<TeamSuggestion>> SuggestTeamsAsync(Guid studentId, int limit, CancellationToken ct)
    {
        var rules = CompositionRules.Default;
        var student = await model.GetStudentAsync(studentId, ct) ?? throw new NotFoundException("sinh viên", studentId);
        if (student.TeamId is not null)
        {
            return [];
        }

        var teams = (await model.GetTeamsAsync(student.SemesterId, student.CourseCode, ct))
            .Where(t => t.Status == TeamStatuses.Open && t.MemberIds.Count > 0 && t.MemberIds.Count < rules.MaxSize)
            .ToList();
        var memberIds = teams.SelectMany(t => t.MemberIds).ToHashSet();
        var membersById = (await model.GetStudentsByIdsAsync(memberIds, ct)).ToDictionary(s => s.Id);

        return teams
            .Select(t => ScoreTeam(student, t, TeamProfile.From(t.MemberIds
                .Select(id => membersById.GetValueOrDefault(id)).OfType<StudentView>().ToList()), rules))
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Team.Name, StringComparer.Ordinal)
            .ThenBy(s => s.Team.Id)
            .Take(NormalizeLimit(limit))
            .ToList();
    }

    private static MemberSuggestion ScoreMember(StudentView candidate, TeamProfile team, CompositionRules rules)
    {
        var reasons = new List<string>();
        var score = SpecializationScore(candidate.SpecializationId, team, rules, reasons, forTeam: false);
        score += SkillScore(candidate, team, reasons);
        if (candidate.IsSeekingTeam)
        {
            score += SeekingBonus;
            reasons.Add("Đang tìm nhóm");
        }
        return new MemberSuggestion(candidate, Math.Round(Math.Clamp(score, 0, 1), 4), reasons);
    }

    private static TeamSuggestion ScoreTeam(StudentView student, TeamView team, TeamProfile profile, CompositionRules rules)
    {
        var reasons = new List<string>();
        var score = SpecializationScore(student.SpecializationId, profile, rules, reasons, forTeam: true);
        score += SkillScore(student, profile, reasons);
        if (profile.MemberCount < rules.MinSize)
        {
            score += UnderMinSizeBonus;
            reasons.Add($"Nhóm còn thiếu {rules.MinSize - profile.MemberCount} người");
        }

        IReadOnlyList<int> missing = profile.NeedsSpecialization(rules)
            && student.SpecializationId > 0 && !profile.Specializations.Contains(student.SpecializationId)
            ? [student.SpecializationId]
            : [];
        return new TeamSuggestion(team, missing, Math.Round(Math.Clamp(score, 0, 1), 4), reasons);
    }

    private static double SpecializationScore(int specializationId, TeamProfile team, CompositionRules rules, List<string> reasons, bool forTeam)
    {
        if (specializationId <= 0 || team.Specializations.Contains(specializationId))
        {
            return 0;
        }
        if (team.NeedsSpecialization(rules))
        {
            reasons.Add(forTeam ? "Nhóm đang cần chuyên ngành của bạn" : "Bù chuyên ngành nhóm đang thiếu");
            return MissingSpecializationWeight;
        }
        reasons.Add(forTeam ? "Bạn thêm chuyên ngành mới cho nhóm" : "Thêm chuyên ngành mới cho nhóm");
        return NewSpecializationWeight;
    }

    private static double SkillScore(StudentView candidate, TeamProfile team, List<string> reasons)
    {
        if (candidate.Skills.Count == 0)
        {
            return 0; // chưa đánh giá kỹ năng (rule 16)
        }
        foreach (var (skill, level) in team.StrongAt(candidate).Take(2))
        {
            reasons.Add($"Mạnh {SkillCodes.DisplayName(skill)} ({level}/5) – kỹ năng nhóm đang yếu");
        }
        return ProfileBonus + SkillWeight * team.SkillGain(candidate);
    }

    private static int NormalizeLimit(int limit) => limit <= 0 ? DefaultLimit : Math.Min(limit, MaxLimit);
}
