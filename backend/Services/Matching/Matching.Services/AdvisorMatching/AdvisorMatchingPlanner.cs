using Matching.Services.ReadModel;

namespace Matching.Services.AdvisorMatching;

/// <summary>Giá trị khớp enum <c>MatchReason</c> của matching.proto. <see cref="None"/> = chưa ghép được.</summary>
public enum AdvisorMatchReason
{
    None = 0,
    Mutual = 1,
    Preference = 2,
    LecturerInterest = 3,
    PreviousInstructor = 4,
    Domain = 5,
    MentorInterest = 6,
}

/// <summary><see cref="PreferredAdvisorIds"/> theo thứ tự nguyện vọng 1..n, chỉ giảng viên có.</summary>
public sealed record TeamToMatch(
    Guid TeamId,
    IReadOnlyList<int> DomainIds,
    IReadOnlyList<Guid> PreferredAdvisorIds,
    IReadOnlyList<Guid> InterestedAdvisorIds,
    Guid? PreviousInstructorId);

/// <summary><see cref="MaxTeams"/> null = không giới hạn; <see cref="PreferredTeams"/> 0 = không đặt (chỉ mentor có).</summary>
public sealed record AdvisorOption(Guid AdvisorId, IReadOnlyList<int> DomainIds, int CurrentTeamCount, int? MaxTeams, int PreferredTeams);

/// <summary><see cref="AdvisorId"/> null = không ghép được. <see cref="PreferenceRank"/> 1..n khi Mutual / Preference, còn lại 0.</summary>
public sealed record PlannedMatch(Guid TeamId, Guid? AdvisorId, AdvisorMatchReason Reason, int PreferenceRank);

/// <summary>
/// PlanAdvisorMatching (BE-79, rule 9, 20) – chỉ tính phương án từ dữ liệu trong request, không đọc read model, không ghi dữ liệu.
/// Đi lần lượt từng bậc trên toàn bộ nhóm; nhóm chưa ghép ở bậc này sang bậc sau.
/// <list type="bullet">
/// <item>Giảng viên: hai bên cùng chọn → nguyện vọng 1–3 → giảng viên quan tâm → giảng viên EXE101 cùng đề tài → trùng chuyên môn.</item>
/// <item>Mentor: quan tâm → trùng chuyên môn, lấp tới số mong muốn; còn nhóm thì lặp lại hai bậc đó tới số tối đa.</item>
/// </list>
/// Cùng dữ liệu → cùng phương án, để Ghép sớm xem trước và chốt ra một kết quả.
/// </summary>
public sealed class AdvisorMatchingPlanner
{
    public IReadOnlyList<PlannedMatch> Plan(AdvisorKind kind, IEnumerable<TeamToMatch> teams, IEnumerable<AdvisorOption> advisors)
    {
        var run = new MatchingRun(teams, advisors);
        if (kind == AdvisorKind.Mentor)
        {
            run.MatchMentors();
        }
        else
        {
            run.MatchLecturers();
        }
        return run.Results();
    }

    private sealed class MatchingRun
    {
        private readonly List<TeamState> _teams;
        private readonly Dictionary<Guid, AdvisorState> _advisors;

        public MatchingRun(IEnumerable<TeamToMatch> teams, IEnumerable<AdvisorOption> advisors)
        {
            _teams = teams.DistinctBy(t => t.TeamId).Select(t => new TeamState(t)).ToList();
            _advisors = advisors.DistinctBy(a => a.AdvisorId).ToDictionary(a => a.AdvisorId, a => new AdvisorState(a));
        }

        private IEnumerable<TeamState> OpenTeams => _teams.Where(t => t.Match is null);

        public IReadOnlyList<PlannedMatch> Results() =>
            _teams.Select(t => t.Match ?? new PlannedMatch(t.Team.TeamId, null, AdvisorMatchReason.None, 0)).ToList();

        public void MatchLecturers()
        {
            foreach (var advisor in _advisors.Values)
            {
                advisor.Limit = advisor.HardLimit;
            }

            AssignByRank(t => Preferences(t).Where(p => t.Team.InterestedAdvisorIds.Contains(p.Advisor.Id)), AdvisorMatchReason.Mutual);
            AssignByRank(Preferences, AdvisorMatchReason.Preference);
            AssignBest(Interested, (t, a) => (a.Load, -a.Overlap(t)), AdvisorMatchReason.LecturerInterest);
            AssignBest(PreviousInstructor, (t, a) => (-a.Overlap(t), a.Load), AdvisorMatchReason.PreviousInstructor);
            AssignBest(_ => _advisors.Values, (t, a) => (-a.Overlap(t), a.Load), AdvisorMatchReason.Domain);
        }

        public void MatchMentors()
        {
            foreach (var useMaximum in new[] { false, true })
            {
                foreach (var advisor in _advisors.Values)
                {
                    advisor.Limit = useMaximum ? advisor.HardLimit : advisor.SoftLimit;
                }

                AssignBest(Interested, (t, a) => (a.RelativeLoad, -a.Overlap(t)), AdvisorMatchReason.MentorInterest);
                AssignBest(_ => _advisors.Values, (t, a) => (-a.Overlap(t), a.RelativeLoad), AdvisorMatchReason.Domain);
            }
        }

        private IEnumerable<(AdvisorState Advisor, int Rank)> Preferences(TeamState team) =>
            team.Team.PreferredAdvisorIds
                .Select((id, index) => (Advisor: _advisors.GetValueOrDefault(id), Rank: index + 1))
                .Where(p => p.Advisor is not null)
                .DistinctBy(p => p.Advisor)!;

        private IEnumerable<AdvisorState> Interested(TeamState team) =>
            team.Team.InterestedAdvisorIds.Distinct().Select(id => _advisors.GetValueOrDefault(id)).OfType<AdvisorState>();

        private IEnumerable<AdvisorState> PreviousInstructor(TeamState team) =>
            team.Team.PreviousInstructorId is { } id && _advisors.TryGetValue(id, out var advisor) ? [advisor] : [];

        /// <summary>
        /// Bậc có thứ tự nguyện vọng. Mỗi nhóm hỏi lần lượt ứng viên của mình; cố vấn vượt số nhóm thì giữ nhóm xếp mình cao hơn, rồi
        /// nhóm trùng domain nhiều hơn, nhóm bị bỏ hỏi ứng viên kế tiếp. Kết quả không phụ thuộc thứ tự duyệt nhóm.
        /// </summary>
        private void AssignByRank(Func<TeamState, IEnumerable<(AdvisorState Advisor, int Rank)>> candidatesOf, AdvisorMatchReason reason)
        {
            var candidates = OpenTeams.ToDictionary(t => t, t => new Queue<(AdvisorState Advisor, int Rank)>(candidatesOf(t)));
            var waiting = new Queue<TeamState>(candidates.Keys);
            var held = new Dictionary<AdvisorState, List<(TeamState Team, int Rank)>>();

            while (waiting.TryDequeue(out var team))
            {
                if (!candidates[team].TryDequeue(out var candidate))
                {
                    continue;
                }

                var (advisor, rank) = candidate;
                if (!held.TryGetValue(advisor, out var teams))
                {
                    held[advisor] = teams = [];
                }
                teams.Add((team, rank));
                if (teams.Count > advisor.FreeSlots)
                {
                    var dropped = teams.OrderByDescending(t => t.Rank).ThenBy(t => advisor.Overlap(t.Team)).ThenByDescending(t => t.Team.Team.TeamId).First();
                    teams.Remove(dropped);
                    waiting.Enqueue(dropped.Team);
                }
            }

            foreach (var (advisor, teams) in held)
            {
                foreach (var (team, rank) in teams)
                {
                    Assign(team, advisor, reason, rank);
                }
            }
        }

        /// <summary>Bậc không có thứ tự: chọn từng cặp tốt nhất theo <paramref name="priority"/> (nhỏ hơn trước); số nhóm đang có cập nhật sau mỗi cặp.</summary>
        private void AssignBest(
            Func<TeamState, IEnumerable<AdvisorState>> candidatesOf,
            Func<TeamState, AdvisorState, (double First, double Second)> priority,
            AdvisorMatchReason reason)
        {
            while (true)
            {
                var pairs = OpenTeams.SelectMany(t => candidatesOf(t).Where(a => a.FreeSlots > 0).Select(a => (Team: t, Advisor: a))).ToList();
                if (pairs.Count == 0)
                {
                    return;
                }

                var best = pairs.MinBy(p => (priority(p.Team, p.Advisor), p.Team.Team.TeamId, p.Advisor.Id));
                Assign(best.Team, best.Advisor, reason, 0);
            }
        }

        private static void Assign(TeamState team, AdvisorState advisor, AdvisorMatchReason reason, int rank)
        {
            team.Match = new PlannedMatch(team.Team.TeamId, advisor.Id, reason, rank);
            advisor.Load++;
        }
    }

    private sealed class TeamState(TeamToMatch team)
    {
        public TeamToMatch Team { get; } = team;
        public PlannedMatch? Match { get; set; }
    }

    private sealed class AdvisorState(AdvisorOption advisor)
    {
        private readonly HashSet<int> _domains = [.. advisor.DomainIds];

        public Guid Id { get; } = advisor.AdvisorId;
        public int Load { get; set; } = advisor.CurrentTeamCount;
        public int Limit { get; set; }

        public int HardLimit { get; } = advisor.MaxTeams ?? int.MaxValue;

        /// <summary>Số mong muốn không vượt tối đa; không đặt thì lấy tối đa.</summary>
        public int SoftLimit { get; } = advisor.PreferredTeams > 0 ? Math.Min(advisor.PreferredTeams, advisor.MaxTeams ?? int.MaxValue) : advisor.MaxTeams ?? int.MaxValue;

        public int FreeSlots => Limit - Load;

        /// <summary>Tải so với số mong muốn (rule 20).</summary>
        public double RelativeLoad => (double)Load / Math.Max(SoftLimit, 1);

        public int Overlap(TeamState team) => team.Team.DomainIds.Distinct().Count(_domains.Contains);
    }
}
