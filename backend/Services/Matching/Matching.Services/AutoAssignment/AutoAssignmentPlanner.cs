using Matching.Services.Composition;
using Matching.Services.ReadModel;
using Matching.Services.Suggestions;

namespace Matching.Services.AutoAssignment;

public sealed record TeamFill(Guid TeamId, IReadOnlyList<Guid> StudentIds);

public sealed record NewTeamPlan(string SuggestedName, IReadOnlyList<CompositionMember> Members, Guid LeaderStudentId);

public sealed record AutoAssignmentPlan(
    IReadOnlyList<TeamFill> Fills,
    IReadOnlyList<NewTeamPlan> NewTeams,
    IReadOnlyList<Guid> UnassignedStudentIds);

/// <summary>
/// PlanAutoAssignment (BE-38, rule 14) – chỉ tính phương án, không ghi dữ liệu:
/// <list type="number">
/// <item>Lấp sinh viên chưa có nhóm vào nhóm NEEDS_REVIEW (thiếu người hoặc thiếu chuyên ngành thứ hai), nhóm gần hợp lệ nhất trước.</item>
/// <item>Chia số còn lại thành nhóm mới 4–6 người, chia đều theo chuyên ngành để mỗi nhóm có ≥ 2 chuyên ngành; đổi chéo thành viên nếu cần.</item>
/// <item>Sinh viên lẻ còn lại được thêm vào nhóm còn chỗ (vẫn hợp lệ), không được thì để Admin xử lý tay (W21).</item>
/// </list>
/// Kết quả xác định (cùng dữ liệu → cùng phương án) để Admin xem lại và chạy lại được.
/// </summary>
public sealed class AutoAssignmentPlanner(IMatchingReadModel model)
{
    public const string AutoNamePrefix = "Auto-";

    public async Task<AutoAssignmentPlan> PlanAsync(Guid semesterId, string courseCode, CompositionRules rules, CancellationToken ct)
    {
        var students = await model.GetStudentsAsync(semesterId, courseCode, ct);
        var teams = await model.GetTeamsAsync(semesterId, courseCode, ct);
        var reviewTeams = teams.Where(t => t.Status == TeamStatuses.NeedsReview).ToList();
        var membersById = (await model.GetStudentsByIdsAsync(reviewTeams.SelectMany(t => t.MemberIds), ct)).ToDictionary(s => s.Id);

        var pool = students.Where(s => s.TeamId is null).OrderBy(s => s.Id).ToList();
        var existing = reviewTeams
            .Select(t => new WorkingTeam(t.Id, t.MemberIds.Select(id => membersById.GetValueOrDefault(id)).OfType<StudentView>()))
            .ToList();

        FillReviewTeams(existing, pool, rules);
        var created = BuildNewTeams(pool, rules);
        PlaceLeftovers(pool, created, existing, rules);

        var nextNumber = NextAutoNumber(teams, courseCode);
        return new AutoAssignmentPlan(
            existing.Where(t => t.Added.Count > 0).Select(t => new TeamFill(t.TeamId!.Value, t.Added.Select(s => s.Id).ToList())).ToList(),
            created.Select((t, i) => new NewTeamPlan(
                $"{AutoNamePrefix}{courseCode}-{nextNumber + i:00}",
                t.Members.Select(s => new CompositionMember(s.Id, s.SpecializationId)).ToList(),
                ChooseLeader(t.Members))).ToList(),
            pool.Select(s => s.Id).ToList());
    }

    /// <summary>Bước 1: nhóm cần ít người nhất để hợp lệ được lấp trước, để sửa được nhiều nhóm nhất.</summary>
    private static void FillReviewTeams(List<WorkingTeam> teams, List<StudentView> pool, CompositionRules rules)
    {
        foreach (var team in teams.Where(t => t.CanBeFixed(rules))
                     .OrderBy(t => t.StudentsNeeded(rules)).ThenByDescending(t => t.Members.Count).ThenBy(t => t.TeamId))
        {
            while (!team.IsValid(rules) && team.Members.Count < rules.MaxSize && pool.Count > 0)
            {
                var needsSpecialization = team.SpecializationCount < rules.MinSpecializations;
                var candidates = needsSpecialization
                    ? pool.Where(s => s.SpecializationId > 0 && !team.HasSpecialization(s.SpecializationId)).ToList()
                    : pool;
                if (candidates.Count == 0)
                {
                    break; // không còn ai bù được chuyên ngành – để Admin sửa tay
                }

                var pick = PickFromLargestGroup(candidates, pool, team);
                pool.Remove(pick);
                team.Add(pick);
            }
        }
    }

    /// <summary>
    /// Lấy sinh viên thuộc chuyên ngành còn nhiều nhất trong pool (giữ chuyên ngành hiếm cho nhóm khác),
    /// trong nhóm đó chọn người bù kỹ năng yếu nhất của nhóm.
    /// </summary>
    private static StudentView PickFromLargestGroup(IReadOnlyList<StudentView> candidates, IReadOnlyList<StudentView> pool, WorkingTeam team)
    {
        var groupSize = pool.GroupBy(s => s.SpecializationId).ToDictionary(g => g.Key, g => g.Count());
        var profile = TeamProfile.From(team.Members);
        return candidates
            .OrderByDescending(s => groupSize[s.SpecializationId])
            .ThenBy(s => s.SpecializationId)
            .ThenByDescending(profile.SkillGain)
            .ThenBy(s => s.Id)
            .First();
    }

    /// <summary>Bước 2: số nhóm sao cho mỗi nhóm trong [min, max], gần cỡ giữa nhất; chia vòng theo chuyên ngành.</summary>
    private static List<WorkingTeam> BuildNewTeams(List<StudentView> pool, CompositionRules rules)
    {
        var count = pool.Count;
        var maxTeams = count / rules.MinSize;
        if (maxTeams == 0)
        {
            return [];
        }
        var minTeams = (count + rules.MaxSize - 1) / rules.MaxSize;
        var target = (rules.MinSize + rules.MaxSize) / 2.0;
        // Không chia vừa (ví dụ 7 người với 4–6): lập maxTeams nhóm đầy, phần dư xử lý ở bước 3.
        var teamCount = minTeams <= maxTeams ? Math.Clamp((int)Math.Round(count / target), minTeams, maxTeams) : maxTeams;

        var teams = Enumerable.Range(0, teamCount).Select(_ => new WorkingTeam(null, [])).ToList();
        var ordered = pool
            .GroupBy(s => s.SpecializationId)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .SelectMany(g => g.OrderByDescending(s => s.Skills.Values.Sum()).ThenBy(s => s.Id))
            .ToList();

        var next = 0;
        foreach (var student in ordered)
        {
            var team = Enumerable.Range(0, teamCount)
                .Select(i => teams[(next + i) % teamCount])
                .FirstOrDefault(t => t.Members.Count < rules.MaxSize);
            if (team is null)
            {
                break;
            }
            next = (teams.IndexOf(team) + 1) % teamCount;
            team.Add(student);
            pool.Remove(student);
        }

        RepairSpecializations(teams, rules);
        return teams;
    }

    /// <summary>Nhóm mới chỉ có một chuyên ngành: đổi chéo một người với nhóm khác nếu nhóm kia vẫn đủ chuyên ngành.</summary>
    private static void RepairSpecializations(List<WorkingTeam> teams, CompositionRules rules)
    {
        foreach (var team in teams.Where(t => t.SpecializationCount < rules.MinSpecializations))
        {
            var swap = (
                from other in teams
                where other != team
                from incoming in other.Members
                where !team.HasSpecialization(incoming.SpecializationId)
                from outgoing in team.Members
                where other.SpecializationCountAfterSwap(incoming, outgoing) >= rules.MinSpecializations
                select (other, incoming, outgoing)).FirstOrDefault();
            if (swap.other is null)
            {
                continue;
            }
            swap.other.Replace(swap.incoming, swap.outgoing);
            team.Replace(swap.outgoing, swap.incoming);
        }
    }

    /// <summary>Bước 3: người lẻ vào nhóm còn chỗ – ưu tiên nhóm mới, nhóm mà chuyên ngành của họ còn mới.</summary>
    private static void PlaceLeftovers(List<StudentView> pool, List<WorkingTeam> created, List<WorkingTeam> existing, CompositionRules rules)
    {
        foreach (var student in pool.ToList())
        {
            var target = created.Concat(existing.Where(t => t.IsValid(rules)))
                .Where(t => t.Members.Count < rules.MaxSize)
                .OrderBy(t => t.HasSpecialization(student.SpecializationId))
                .ThenBy(t => t.Members.Count)
                .FirstOrDefault();
            if (target is null)
            {
                continue;
            }
            target.Add(student);
            pool.Remove(student);
        }
    }

    /// <summary>Trưởng nhóm đề xuất: kỹ năng Quản lý cao nhất, rồi tổng kỹ năng, rồi id.</summary>
    private static Guid ChooseLeader(IEnumerable<StudentView> members) => members
        .OrderByDescending(s => s.SkillLevel(Skill.Management))
        .ThenByDescending(s => s.Skills.Values.Sum())
        .ThenBy(s => s.Id)
        .First().Id;

    /// <summary>Đánh số tiếp theo sau các nhóm Auto-… đã có, để chạy lại không trùng tên.</summary>
    private static int NextAutoNumber(IEnumerable<TeamView> teams, string courseCode)
    {
        var prefix = $"{AutoNamePrefix}{courseCode}-";
        var used = teams
            .Select(t => t.Name)
            .Where(n => n is not null && n.StartsWith(prefix, StringComparison.Ordinal))
            .Select(n => int.TryParse(n![prefix.Length..], out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max();
        return used + 1;
    }

    private sealed class WorkingTeam(Guid? teamId, IEnumerable<StudentView> members)
    {
        public Guid? TeamId { get; } = teamId;
        public List<StudentView> Members { get; } = members.ToList();
        public List<StudentView> Added { get; } = [];

        public int SpecializationCount => CompositionValidator.CountSpecializations(Members.Select(m => m.SpecializationId));

        public bool HasSpecialization(int specializationId) => Members.Any(m => m.SpecializationId == specializationId);

        public bool IsValid(CompositionRules rules) => CompositionValidator.IsValid(Members.Select(m => m.SpecializationId), rules);

        /// <summary>Số người tối thiểu cần thêm để hợp lệ (đủ sĩ số và đủ chuyên ngành).</summary>
        public int StudentsNeeded(CompositionRules rules) =>
            Math.Max(Math.Max(0, rules.MinSize - Members.Count), Math.Max(0, rules.MinSpecializations - SpecializationCount));

        /// <summary>Thêm người mà không vượt sĩ số tối đa thì mới sửa được.</summary>
        public bool CanBeFixed(CompositionRules rules) => Members.Count + StudentsNeeded(rules) <= rules.MaxSize;

        public int SpecializationCountAfterSwap(StudentView leaving, StudentView joining) =>
            CompositionValidator.CountSpecializations(Members.Where(m => m != leaving).Append(joining).Select(m => m.SpecializationId));

        /// <summary>Thêm người mới vào nhóm (thành viên sẵn có truyền qua constructor, không nằm trong <see cref="Added"/>).</summary>
        public void Add(StudentView student)
        {
            Members.Add(student);
            Added.Add(student);
        }

        public void Replace(StudentView leaving, StudentView joining)
        {
            Members[Members.IndexOf(leaving)] = joining;
            if (Added.IndexOf(leaving) is >= 0 and var index)
            {
                Added[index] = joining;
            }
        }
    }
}
