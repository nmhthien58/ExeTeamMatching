using Matching.Services.AdvisorMatching;
using Matching.Services.ReadModel;

namespace Matching.Tests;

/// <summary>BE-79 – từng bậc và tranh chấp của rule 9 (giảng viên), rule 20 (mentor).</summary>
public sealed class AdvisorMatchingPlannerTests
{
    private static readonly Guid L1 = Guid.Parse("00000000-0000-0000-0000-00000000a001");
    private static readonly Guid L2 = Guid.Parse("00000000-0000-0000-0000-00000000a002");
    private static readonly Guid L3 = Guid.Parse("00000000-0000-0000-0000-00000000a003");
    private static readonly Guid T1 = Guid.Parse("00000000-0000-0000-0000-00000000b001");
    private static readonly Guid T2 = Guid.Parse("00000000-0000-0000-0000-00000000b002");
    private static readonly Guid T3 = Guid.Parse("00000000-0000-0000-0000-00000000b003");

    private static TeamToMatch Team(Guid id, Guid[]? preferred = null, Guid[]? interested = null, int[]? domains = null, Guid? previous = null) =>
        new(id, domains ?? [], preferred ?? [], interested ?? [], previous);

    private static AdvisorOption Advisor(Guid id, int? max = null, int current = 0, int[]? domains = null, int preferred = 0) =>
        new(id, domains ?? [], current, max, preferred);

    private static Dictionary<Guid, PlannedMatch> Lecturers(TeamToMatch[] teams, params AdvisorOption[] advisors) =>
        new AdvisorMatchingPlanner().Plan(AdvisorKind.Lecturer, teams, advisors).ToDictionary(m => m.TeamId);

    private static Dictionary<Guid, PlannedMatch> Mentors(TeamToMatch[] teams, params AdvisorOption[] advisors) =>
        new AdvisorMatchingPlanner().Plan(AdvisorKind.Mentor, teams, advisors).ToDictionary(m => m.TeamId);

    [Fact]
    public void Mutual_lecturer_beats_higher_preference_who_is_not_interested()
    {
        var result = Lecturers([Team(T1, preferred: [L1, L2], interested: [L2])], Advisor(L1), Advisor(L2));

        Assert.Equal(new PlannedMatch(T1, L2, AdvisorMatchReason.Mutual, 2), result[T1]);
    }

    [Fact]
    public void Preferences_are_taken_in_rank_order()
    {
        var result = Lecturers([Team(T1, preferred: [L2, L1])], Advisor(L1), Advisor(L2));

        Assert.Equal(new PlannedMatch(T1, L2, AdvisorMatchReason.Preference, 1), result[T1]);
    }

    [Fact]
    public void Full_first_choice_moves_team_to_next_preference_with_its_real_rank()
    {
        var result = Lecturers([Team(T1, preferred: [L1, L2])], Advisor(L1, max: 3, current: 3), Advisor(L2));

        Assert.Equal(new PlannedMatch(T1, L2, AdvisorMatchReason.Preference, 2), result[T1]);
    }

    [Fact]
    public void Preference_of_inactive_lecturer_is_skipped_and_rank_is_kept()
    {
        var gone = Guid.NewGuid();

        var result = Lecturers([Team(T1, preferred: [gone, L1])], Advisor(L1));

        Assert.Equal(new PlannedMatch(T1, L1, AdvisorMatchReason.Preference, 2), result[T1]);
    }

    [Fact]
    public void Contested_lecturer_keeps_team_that_ranked_them_higher()
    {
        // T1 trùng domain nhiều hơn nhưng chỉ xếp L1 thứ 2 (L2 đã đủ): thứ hạng xét trước domain.
        var result = Lecturers(
            [Team(T1, preferred: [L2, L1], domains: [1, 2]), Team(T2, preferred: [L1])],
            Advisor(L1, max: 1, domains: [1, 2]), Advisor(L2, max: 0));

        Assert.Equal(new PlannedMatch(T2, L1, AdvisorMatchReason.Preference, 1), result[T2]);
        Assert.Null(result[T1].AdvisorId);
    }

    [Fact]
    public void Same_rank_goes_to_team_with_more_domain_overlap_and_other_team_moves_on()
    {
        var result = Lecturers(
            [Team(T1, preferred: [L1, L2], domains: [1]), Team(T2, preferred: [L1, L2], domains: [1, 2])],
            Advisor(L1, max: 1, domains: [1, 2]), Advisor(L2, max: 1));

        Assert.Equal(new PlannedMatch(T2, L1, AdvisorMatchReason.Preference, 1), result[T2]);
        Assert.Equal(new PlannedMatch(T1, L2, AdvisorMatchReason.Preference, 2), result[T1]);
    }

    [Fact]
    public void Rejected_team_can_push_out_a_lower_ranked_team_held_by_next_lecturer()
    {
        // T3 giữ L2 ở nguyện vọng 3. T1 bị L1 bỏ, chuyển sang L2 ở nguyện vọng 2 nên đẩy T3 ra.
        var result = Lecturers(
            [Team(T1, preferred: [L1, L2], domains: [1]), Team(T2, preferred: [L1], domains: [1, 2]), Team(T3, preferred: [L3, Guid.NewGuid(), L2])],
            Advisor(L1, max: 1, domains: [1, 2]), Advisor(L2, max: 1), Advisor(L3, max: 0));

        Assert.Equal(L1, result[T2].AdvisorId);
        Assert.Equal(new PlannedMatch(T1, L2, AdvisorMatchReason.Preference, 2), result[T1]);
        Assert.Null(result[T3].AdvisorId);
    }

    [Fact]
    public void Earlier_tier_is_served_before_later_tier_across_all_teams()
    {
        // T1 đứng trước nhưng chỉ có bậc quan tâm; T2 có nguyện vọng nên giữ L1 trước.
        var result = Lecturers([Team(T1, interested: [L1]), Team(T2, preferred: [L1])], Advisor(L1, max: 1));

        Assert.Equal(L1, result[T2].AdvisorId);
        Assert.Null(result[T1].AdvisorId);
    }

    [Fact]
    public void Interested_lecturer_with_fewest_teams_wins()
    {
        var result = Lecturers([Team(T1, interested: [L1, L2])], Advisor(L1, current: 3), Advisor(L2, current: 1));

        Assert.Equal(new PlannedMatch(T1, L2, AdvisorMatchReason.LecturerInterest, 0), result[T1]);
    }

    [Fact]
    public void Interest_counts_teams_matched_earlier_in_the_same_run()
    {
        var result = Lecturers(
            [Team(T1, interested: [L1, L2]), Team(T2, interested: [L1, L2])],
            Advisor(L1), Advisor(L2));

        Assert.NotEqual(result[T1].AdvisorId, result[T2].AdvisorId);
    }

    [Fact]
    public void Previous_instructor_comes_before_domain_match()
    {
        var result = Lecturers([Team(T1, domains: [1], previous: L1)], Advisor(L1), Advisor(L2, domains: [1]));

        Assert.Equal(new PlannedMatch(T1, L1, AdvisorMatchReason.PreviousInstructor, 0), result[T1]);
    }

    [Fact]
    public void Full_previous_instructor_falls_back_to_domain()
    {
        var result = Lecturers([Team(T1, domains: [1], previous: L1)], Advisor(L1, max: 1, current: 1), Advisor(L2, domains: [1]));

        Assert.Equal(new PlannedMatch(T1, L2, AdvisorMatchReason.Domain, 0), result[T1]);
    }

    [Fact]
    public void Domain_tier_prefers_overlap_then_fewest_teams()
    {
        var byOverlap = Lecturers([Team(T1, domains: [1, 2])], Advisor(L1, domains: [1]), Advisor(L2, current: 5, domains: [1, 2]));
        var byLoad = Lecturers([Team(T1, domains: [1])], Advisor(L1, current: 2, domains: [1]), Advisor(L2, current: 1, domains: [1]));

        Assert.Equal(L2, byOverlap[T1].AdvisorId);
        Assert.Equal(L2, byLoad[T1].AdvisorId);
    }

    [Fact]
    public void Domain_tier_spreads_teams_across_lecturers()
    {
        var result = Lecturers(
            [Team(T1, domains: [1]), Team(T2, domains: [1]), Team(T3, domains: [1])],
            Advisor(L1, domains: [1]), Advisor(L2, domains: [1]));

        var counts = result.Values.GroupBy(m => m.AdvisorId).Select(g => g.Count()).Order();
        Assert.Equal([1, 2], counts);
    }

    [Fact]
    public void Team_without_any_domain_overlap_still_gets_a_lecturer()
    {
        var result = Lecturers([Team(T1, domains: [9])], Advisor(L1, domains: [1]));

        Assert.Equal(new PlannedMatch(T1, L1, AdvisorMatchReason.Domain, 0), result[T1]);
    }

    [Fact]
    public void Every_lecturer_full_leaves_team_unmatched_and_maximum_is_respected()
    {
        var result = Lecturers([Team(T1), Team(T2), Team(T3)], Advisor(L1, max: 1), Advisor(L2, max: 2, current: 1));

        Assert.Equal(2, result.Values.Count(m => m.AdvisorId is not null));
        Assert.Equal(new PlannedMatch(T3, null, AdvisorMatchReason.None, 0), result[T3]);
    }

    [Fact]
    public void Lecturer_without_maximum_is_never_full()
    {
        var result = Lecturers([Team(T1), Team(T2)], Advisor(L1, current: 1000));

        Assert.All(result.Values, m => Assert.Equal(L1, m.AdvisorId));
    }

    [Fact]
    public void Plan_is_deterministic_whatever_the_input_order()
    {
        TeamToMatch[] teams = [Team(T1, preferred: [L1], domains: [1]), Team(T2, preferred: [L1], domains: [1]), Team(T3, interested: [L2], domains: [1])];
        AdvisorOption[] advisors = [Advisor(L1, max: 1, domains: [1]), Advisor(L2, max: 2, domains: [1])];

        var forward = new AdvisorMatchingPlanner().Plan(AdvisorKind.Lecturer, teams, advisors).OrderBy(m => m.TeamId);
        var backward = new AdvisorMatchingPlanner().Plan(AdvisorKind.Lecturer, teams.Reverse(), advisors.Reverse()).OrderBy(m => m.TeamId);

        Assert.Equal(forward, backward);
    }

    [Fact]
    public void Mentor_interest_comes_before_domain_and_preferences_are_ignored()
    {
        var result = Mentors([Team(T1, preferred: [L2], interested: [L1], domains: [1])], Advisor(L1, max: 3), Advisor(L2, max: 3, domains: [1]));

        Assert.Equal(new PlannedMatch(T1, L1, AdvisorMatchReason.MentorInterest, 0), result[T1]);
    }

    [Fact]
    public void Mentor_with_lowest_load_relative_to_preferred_goes_first()
    {
        var result = Mentors(
            [Team(T1, interested: [L1, L2])],
            Advisor(L1, max: 5, current: 1, preferred: 2), Advisor(L2, max: 5, current: 2, preferred: 5));

        Assert.Equal(L2, result[T1].AdvisorId);
    }

    [Fact]
    public void Mentors_fill_up_to_preferred_number_before_going_above_it()
    {
        // L1 được quan tâm nhưng đã đủ số mong muốn: nhóm sang mentor còn dưới mong muốn thay vì vượt mức.
        var result = Mentors([Team(T1, interested: [L1], domains: [1])], Advisor(L1, max: 3, current: 1, preferred: 1), Advisor(L2, max: 3, preferred: 2, domains: [1]));

        Assert.Equal(new PlannedMatch(T1, L2, AdvisorMatchReason.Domain, 0), result[T1]);
    }

    [Fact]
    public void Mentor_goes_above_preferred_only_when_team_would_stay_without_mentor()
    {
        var result = Mentors(
            [Team(T1, interested: [L1], domains: [1]), Team(T2, domains: [1])],
            Advisor(L1, max: 2, current: 1, preferred: 1, domains: [1]), Advisor(L2, max: 1, preferred: 1, domains: [1]));

        Assert.Equal(new PlannedMatch(T1, L2, AdvisorMatchReason.Domain, 0), result[T1]);
        Assert.Equal(new PlannedMatch(T2, L1, AdvisorMatchReason.Domain, 0), result[T2]);
    }

    [Fact]
    public void Interested_mentor_above_preferred_keeps_interest_reason_and_maximum_blocks()
    {
        var result = Mentors(
            [Team(T1, interested: [L1]), Team(T2, interested: [L1])],
            Advisor(L1, max: 2, current: 1, preferred: 1));

        Assert.Equal(new PlannedMatch(T1, L1, AdvisorMatchReason.MentorInterest, 0), result[T1]);
        Assert.Null(result[T2].AdvisorId);
    }

    [Fact]
    public void Mentor_without_preferred_number_uses_maximum()
    {
        var result = Mentors([Team(T1), Team(T2)], Advisor(L1, max: 2));

        Assert.All(result.Values, m => Assert.Equal(L1, m.AdvisorId));
    }
}
