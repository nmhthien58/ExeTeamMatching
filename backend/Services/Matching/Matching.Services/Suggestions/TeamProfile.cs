using Matching.Services.Composition;
using Matching.Services.ReadModel;

namespace Matching.Services.Suggestions;

/// <summary>
/// Hồ sơ năng lực của nhóm (PROJECT_CONTEXT §5.5): mức kỹ năng của nhóm = mức cao nhất trong các thành viên;
/// kỹ năng yếu = mức cao nhất &lt;= <see cref="WeakSkillMaxLevel"/>.
/// </summary>
public sealed class TeamProfile
{
    public const int WeakSkillMaxLevel = 2;

    private TeamProfile(int memberCount, IReadOnlySet<int> specializations, IReadOnlyDictionary<Skill, int> maxLevels)
    {
        MemberCount = memberCount;
        Specializations = specializations;
        MaxLevels = maxLevels;
        WeakSkills = maxLevels.Where(s => s.Value <= WeakSkillMaxLevel)
            .OrderBy(s => s.Value).ThenBy(s => s.Key)
            .Select(s => s.Key).ToList();
    }

    public int MemberCount { get; }
    public IReadOnlySet<int> Specializations { get; }
    public IReadOnlyDictionary<Skill, int> MaxLevels { get; }

    /// <summary>Kỹ năng yếu, yếu nhất trước.</summary>
    public IReadOnlyList<Skill> WeakSkills { get; }

    public static TeamProfile From(IReadOnlyCollection<StudentView> members) => new(
        members.Count,
        members.Select(m => m.SpecializationId).Where(id => id > 0).ToHashSet(),
        Enum.GetValues<Skill>().ToDictionary(s => s, s => members.Count == 0 ? 0 : members.Max(m => m.SkillLevel(s))));

    public bool NeedsSpecialization(CompositionRules rules) => Specializations.Count < rules.MinSpecializations;

    /// <summary>
    /// Mức bù kỹ năng yếu của nhóm, 0..1: tổng phần vượt mức của nhóm ở từng kỹ năng yếu, chia cho mức tối đa có thể.
    /// Nhóm không có kỹ năng yếu thì tính trên cả 5 kỹ năng.
    /// </summary>
    public double SkillGain(StudentView candidate)
    {
        var skills = WeakSkills.Count > 0 ? WeakSkills : Enum.GetValues<Skill>();
        var gain = skills.Sum(s => Math.Max(0, candidate.SkillLevel(s) - MaxLevels[s]));
        return (double)gain / (4 * skills.Count);
    }

    /// <summary>Kỹ năng yếu của nhóm mà ứng viên mạnh (>= 4/5), mạnh nhất trước.</summary>
    public IEnumerable<(Skill Skill, int Level)> StrongAt(StudentView candidate) => WeakSkills
        .Select(s => (Skill: s, Level: candidate.SkillLevel(s)))
        .Where(x => x.Level >= 4)
        .OrderByDescending(x => x.Level);
}
