using System.Security.Claims;
using System.Text.Json;
using BuildingBlocks.Auth;
using BuildingBlocks.Messaging;
using Team.Repositories.AdvisorMatching;
using Team.Repositories.AuditLog;
using Team.Repositories.Teams;
using Team.Repositories.TopicInterests;
using Team.Services.Academic;
using Team.Services.AdvisorAssignments;
using Team.Services.LecturerPreferences;
using Team.Services.Matching;
using Team.Services.Teams;
using Team.Services.TopicInterests;

namespace Team.Services.AdvisorMatching;

public interface IAdvisorMatchingService
{
    /// <summary>Job gọi (trigger SCHEDULED): ghép và áp dụng ngay. Đã có lần SCHEDULED được áp dụng của môn, học kỳ thì trả lại lần đó.</summary>
    Task<AdvisorMatchingRunResponse> RunScheduledAsync(string kind, InternalAdvisorMatchingInput input, CancellationToken ct);

    /// <summary>Ghép sớm – bước 1: tính phương án cho nhóm chưa có cố vấn của học kỳ hiện hành, lưu lần chạy PREVIEW.</summary>
    Task<AdvisorMatchingRunResponse> PreviewAsync(string kind, AdvisorMatchingPreviewInput input, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Ghép sớm – bước 2: áp dụng phương án, bỏ qua nhóm đã có cố vấn trong lúc chờ.</summary>
    Task<AdvisorMatchingRunResponse> ConfirmAsync(Guid runId, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Bỏ phương án xem trước (DISCARDED).</summary>
    Task DiscardAsync(Guid runId, CancellationToken ct);

    /// <summary>Bảng W19 (LECTURER) / W28 (MENTOR).</summary>
    Task<AdvisorMatchingOverviewResponse> GetOverviewAsync(string kind, AdvisorMatchingOverviewQuery query, CancellationToken ct);

    /// <summary>Admin đổi giảng viên hướng dẫn (reason MANUAL, ghi chú bắt buộc); 409 <c>lecturer-full</c> khi giảng viên đã đủ số nhóm tối đa.</summary>
    Task<LecturerAssignmentResponse> ChangeInstructorAsync(Guid teamId, ChangeInstructorInput input, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>
    /// Admin đổi mentor (rule 20, reason MANUAL, ghi chú bắt buộc): vượt số mong muốn → cảnh báo <c>ABOVE_PREFERRED</c>;
    /// đã đủ số tối đa → 409 <c>mentor-full</c>. Mentor cũ kết thúc (ENDED), <c>MentorAssigned</c> có <c>replacedMentorId</c>.
    /// </summary>
    Task<ChangeMentorResponse> ChangeMentorAsync(Guid teamId, ChangeMentorInput input, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Admin gỡ mentor khỏi nhóm: dòng đang gán ENDED, <c>MentorUnassigned</c>. Nhóm chưa có mentor thì không làm gì.</summary>
    Task RemoveMentorAsync(Guid teamId, RemoveMentorInput? input, ClaimsPrincipal user, CancellationToken ct);
}

/// <summary>
/// Module AdvisorMatching (BE-80, rule 9, 19, 20, 21): đọc nguyện vọng, quan tâm, tải từ team_db, lấy phương án từ Matching
/// (<c>PlanAdvisorMatching</c>) rồi áp dụng trong một transaction – assignment, khóa nguyện vọng (nhóm có giảng viên thì
/// <c>preferences-locked</c>), đóng quan tâm cùng loại, <c>LecturerAssigned</c> / <c>MentorAssigned</c> và <c>AdvisorMatchingCompleted</c> qua Outbox.
/// Luồng dùng chung cho hai loại cố vấn; phần riêng nằm ở <see cref="IAdvisorSource"/>.
/// </summary>
public sealed class AdvisorMatchingService(
    IEnumerable<IAdvisorSource> sources,
    IAdvisorMatchingRepository repo,
    IMatchingClient matching,
    IAcademicClient academic,
    ITopicInterestService topicInterests,
    ITeamRepository teams,
    TeamReader reader,
    LecturerAssignmentWriter lecturerWriter,
    MentorAssignmentWriter mentorWriter,
    TimeProvider clock) : IAdvisorMatchingService
{
    public async Task<AdvisorMatchingRunResponse> RunScheduledAsync(string kind, InternalAdvisorMatchingInput input, CancellationToken ct)
    {
        var source = Source(kind);
        return await repo.InTransactionAsync(async () =>
        {
            await repo.LockAsync(source.Kind, input.SemesterId, source.LockScope(input.CourseCode), ct);
            var applied = await repo.GetLatestRunAsync(source.Kind, input.SemesterId, input.CourseCode, MatchingTriggers.Scheduled,
                MatchingRunStatuses.Applied, ct);
            if (applied is not null)
            {
                return ToResponse(applied);
            }

            var proposal = await ProposeAsync(source, input.SemesterId, input.CourseCode, ct);
            var run = NewRun(source.Kind, input.SemesterId, input.CourseCode, MatchingTriggers.Scheduled, null);
            repo.Add(run);
            await ApplyAsync(source, run, proposal.Plan, proposal.Teams, proposal.Advisors, proposal.Loads, ct);
            await repo.SaveChangesAsync(ct);
            return ToResponse(run);
        }, ct);
    }

    public async Task<AdvisorMatchingRunResponse> PreviewAsync(string kind, AdvisorMatchingPreviewInput input, ClaimsPrincipal user,
        CancellationToken ct)
    {
        var source = Source(kind);
        var semester = await academic.GetCurrentSemesterAsync(ct) ?? throw AdvisorMatchingErrors.NoCurrentSemester();
        var proposal = await ProposeAsync(source, semester.Id, input.CourseCode, ct);
        var run = NewRun(source.Kind, semester.Id, input.CourseCode, MatchingTriggers.Early, user.GetUserId());
        SetPlan(run, proposal.Plan);
        repo.Add(run);
        await repo.SaveChangesAsync(ct);
        return ToResponse(run);
    }

    public async Task<AdvisorMatchingRunResponse> ConfirmAsync(Guid runId, ClaimsPrincipal user, CancellationToken ct)
    {
        var run = await repo.GetRunAsync(runId, ct) ?? throw AdvisorMatchingErrors.RunNotFound(runId);
        var source = Source(run.AdvisorKind);
        EnsurePreview(run);
        return await repo.InTransactionAsync(async () =>
        {
            await repo.LockAsync(source.Kind, run.SemesterId, source.LockScope(run.CourseCode), ct);
            await repo.ReloadAsync(run, ct);
            EnsurePreview(run);

            var plan = ReadPlan(run);
            var planTeams = (await repo.ListTeamsAsync(plan.Select(p => p.TeamId).ToList(), ct)).ToDictionary(t => t.Id);
            var matchedMeanwhile = await repo.ListTeamIdsWithAdvisorAsync(source.Kind, planTeams.Keys.ToList(), ct);
            // Bỏ nhóm đã giải tán hoặc đã có cố vấn (Admin đổi tay, lần ghép khác) trong lúc chờ.
            var remaining = plan.Where(p => planTeams.ContainsKey(p.TeamId) && !matchedMeanwhile.Contains(p.TeamId)).ToList();
            var advisors = remaining.Any(p => p.AdvisorId is not null) ? await source.ListActiveAsync(run.CourseCode, ct) : [];
            var loads = await source.CountLoadsAsync(run.SemesterId, run.CourseCode, ct);
            await ApplyAsync(source, run, remaining, planTeams, advisors.ToDictionary(a => a.Id), loads, ct);
            await repo.SaveChangesAsync(ct);
            return ToResponse(run);
        }, ct);
    }

    public async Task DiscardAsync(Guid runId, CancellationToken ct)
    {
        var run = await repo.GetRunAsync(runId, ct) ?? throw AdvisorMatchingErrors.RunNotFound(runId);
        EnsurePreview(run);
        run.Status = MatchingRunStatuses.Discarded;
        await repo.SaveChangesAsync(ct);
    }

    public async Task<AdvisorMatchingOverviewResponse> GetOverviewAsync(string kind, AdvisorMatchingOverviewQuery query, CancellationToken ct)
    {
        var source = Source(kind);
        var courseCode = query.CourseCode ?? "EXE101";
        var semester = await academic.GetCurrentSemesterAsync(ct) ?? throw AdvisorMatchingErrors.NoCurrentSemester();
        var advisors = await source.ListActiveAsync(courseCode, ct);
        var advisorById = advisors.ToDictionary(a => a.Id);
        var loads = await source.CountLoadsAsync(semester.Id, courseCode, ct);

        var all = await repo.ListTeamsAsync(semester.Id, courseCode, ct);
        var allIds = all.Select(t => t.Id).ToList();
        var assignments = await source.ListCurrentAsync(allIds, ct);
        var preferences = source.Kind == AdvisorKinds.Lecturer ? await repo.ListLecturerPreferencesAsync(allIds, ct) : null;

        var filtered = all.Where(t => query.WithoutAdvisor != true || !assignments.ContainsKey(t.Id));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            filtered = filtered.Where(t => t.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }
        var matched = filtered.ToList();
        var page = matched.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        var pageIds = page.Select(t => t.Id).ToList();

        var catalogs = await reader.LoadCatalogsAsync(ct);
        var graphs = (await teams.ListGraphsAsync(pageIds, ct)).ToDictionary(g => g.Team.Id);
        var interests = await repo.ListInterestsAsync(source.Kind, pageIds, openOnly: false, ct);
        var previous = source.Kind == AdvisorKinds.Lecturer ? await repo.ListPreviousInstructorsAsync(page, ct) : [];

        LecturerRef Lecturer(Guid id, string snapshotName) => advisorById.TryGetValue(id, out var a)
            ? new LecturerRef(a.Id, a.FullName, a.Domains)
            : new LecturerRef(id, snapshotName, []);

        var items = page.Where(t => graphs.ContainsKey(t.Id)).Select(t =>
        {
            var assignment = assignments.GetValueOrDefault(t.Id);
            var previousId = previous.TryGetValue(t.Id, out var p) ? p.LecturerId : (Guid?)null;
            return new AdvisorMatchingRowResponse(
                TeamMapper.ToCard(graphs[t.Id], catalogs),
                assignment?.AdvisorId,
                assignment?.AdvisorName,
                assignment?.Reason,
                assignment?.PreferenceRank,
                preferences is null ? [] : preferences[t.Id].Select(x => Lecturer(x.LecturerId, x.LecturerName)).ToList(),
                interests[t.Id].Select(i => ToInterest(i, previousId)).ToList());
        }).ToList();

        var lastRun = await repo.GetLatestRunAsync(source.Kind, semester.Id, courseCode, null, null, ct);
        return new AdvisorMatchingOverviewResponse(
            await MatchingAtAsync(source, semester, courseCode, ct),
            lastRun is null ? null : ToResponse(lastRun),
            preferences is null ? 0 : all.Count(t => preferences[t.Id].Any()),
            all.Count(t => !assignments.ContainsKey(t.Id)),
            items,
            advisors.Select(a => new AdvisorLoadResponse(a.Id, a.FullName, loads.GetValueOrDefault(a.Id),
                source.Kind == AdvisorKinds.Mentor && a.PreferredTeams > 0 ? a.PreferredTeams : null, a.MaxTeams)).ToList(),
            query.Page,
            query.PageSize,
            matched.Count,
            (matched.Count + query.PageSize - 1) / query.PageSize);
    }

    public async Task<LecturerAssignmentResponse> ChangeInstructorAsync(Guid teamId, ChangeInstructorInput input, ClaimsPrincipal user,
        CancellationToken ct)
    {
        var team = await ActiveTeamAsync(teamId, ct);
        var lecturer = await academic.GetLecturerAsync(input.LecturerId, ct) ?? throw AdvisorMatchingErrors.LecturerNotFound(input.LecturerId);
        if (!lecturer.IsActive)
        {
            throw TeamErrors.Invalid("lecturerId", "Giảng viên không còn hoạt động.");
        }
        var maxTeams = LecturerAdvisorSource.MaxTeamsFor(lecturer, team.CourseCode);
        var note = input.Note.Trim();
        var actor = user.ToAuditActor(Roles.Admin);

        return await repo.InTransactionAsync(async () =>
        {
            await repo.LockAsync(AdvisorKinds.Lecturer, team.SemesterId, team.CourseCode, ct);
            var current = await repo.GetActiveLecturerAssignmentAsync(team.Id, ct);
            if (current?.LecturerId == lecturer.Id)
            {
                return ToResponse(current);
            }
            if (maxTeams is { } max && (await repo.CountLecturerLoadsAsync(team.SemesterId, team.CourseCode, ct)).GetValueOrDefault(lecturer.Id) >= max)
            {
                throw AdvisorMatchingErrors.LecturerFull(lecturer.FullName, max);
            }
            if (current is not null)
            {
                // Thu hồi trước khi thêm người mới (uq_lecturer_assignments_active).
                (current.Status, current.RevokedAt) = ("REVOKED", clock.GetUtcNow());
                await repo.SaveChangesAsync(ct);
            }

            var members = (await repo.ListMemberStudentIdsAsync([team.Id], ct))[team.Id].ToList();
            var assignment = lecturerWriter.Stage(team, lecturer.Id, lecturer.FullName, LecturerAssignmentWriter.ReasonManual, null,
                LecturerAssignmentWriter.SourceManual, null, user.GetUserId(), note, current, members, actor);
            await topicInterests.CloseInterestsOnAssignmentAsync(team.Id, AdvisorKinds.Lecturer, lecturer.Id, ct);
            await repo.SaveChangesAsync(ct);
            return ToResponse(assignment);
        }, ct);
    }

    public async Task<ChangeMentorResponse> ChangeMentorAsync(Guid teamId, ChangeMentorInput input, ClaimsPrincipal user, CancellationToken ct)
    {
        var team = await ActiveTeamAsync(teamId, ct);
        var snapshot = await academic.GetMentorAsync(input.MentorId, ct) ?? throw AdvisorMatchingErrors.MentorNotFound(input.MentorId);
        if (!snapshot.IsActive)
        {
            throw TeamErrors.Invalid("mentorId", "Mentor không còn hoạt động.");
        }
        var mentor = MentorAdvisorSource.ToCandidate(snapshot);
        var note = input.Note.Trim();
        var actor = user.ToAuditActor(Roles.Admin);

        return await repo.InTransactionAsync(async () =>
        {
            await repo.LockAsync(AdvisorKinds.Mentor, team.SemesterId, MentorAdvisorSource.AllCourses, ct);
            var current = await repo.GetActiveMentorAssignmentAsync(team.Id, ct);
            if (current?.MentorId == mentor.Id)
            {
                return new ChangeMentorResponse(ToResponse(current, snapshot), []);
            }
            var load = (await repo.CountMentorLoadsAsync(team.SemesterId, ct)).GetValueOrDefault(mentor.Id);
            if (load >= mentor.MaxTeams)
            {
                throw AdvisorMatchingErrors.MentorFull(mentor.FullName, mentor.MaxTeams!.Value);
            }
            List<MatchingWarning> warnings = mentor.PreferredTeams > 0 && load + 1 > mentor.PreferredTeams
                ? [new(MatchingWarnings.AbovePreferred,
                    $"{mentor.FullName} sẽ mentor {load + 1} nhóm, vượt số mong muốn ({mentor.PreferredTeams}); tối đa {mentor.MaxTeams}.")]
                : [];
            if (current is not null)
            {
                // Kết thúc trước khi thêm người mới (uq_mentor_assignments_active). Mentor cũ mất quyền khi Collaboration nhận MentorAssigned.
                mentorWriter.End(current, note, unassigned: false, actor);
                await repo.SaveChangesAsync(ct);
            }

            var members = (await repo.ListMemberStudentIdsAsync([team.Id], ct))[team.Id].ToList();
            var assignment = mentorWriter.Stage(team, mentor, MentorAssignmentWriter.ReasonManual, MentorAssignmentWriter.SourceManual, null,
                user.GetUserId(), note, current, members, actor);
            await topicInterests.CloseInterestsOnAssignmentAsync(team.Id, AdvisorKinds.Mentor, mentor.Id, ct);
            await repo.SaveChangesAsync(ct);
            return new ChangeMentorResponse(ToResponse(assignment, snapshot), warnings);
        }, ct);
    }

    public async Task RemoveMentorAsync(Guid teamId, RemoveMentorInput? input, ClaimsPrincipal user, CancellationToken ct)
    {
        var team = await ActiveTeamAsync(teamId, ct);
        var reason = string.IsNullOrWhiteSpace(input?.Reason) ? "Bộ môn gỡ mentor" : input.Reason.Trim();
        var actor = user.ToAuditActor(Roles.Admin);
        await repo.InTransactionAsync(async () =>
        {
            await repo.LockAsync(AdvisorKinds.Mentor, team.SemesterId, MentorAdvisorSource.AllCourses, ct);
            if (await repo.GetActiveMentorAssignmentAsync(team.Id, ct) is { } current)
            {
                mentorWriter.End(current, reason, unassigned: true, actor);
                await repo.SaveChangesAsync(ct);
            }
            return true;
        }, ct);
    }

    private async Task<TeamEntity> ActiveTeamAsync(Guid teamId, CancellationToken ct)
    {
        var team = await teams.GetAsync(teamId, ct);
        return team is null || team.Status == TeamStatuses.Disbanded ? throw TeamErrors.TeamNotFound(teamId) : team;
    }

    /// <summary>
    /// Lúc ghép theo lịch: giảng viên – hạn đợt lập nhóm đang mở của môn; mentor – đầu tuần bắt đầu mentor của học kỳ
    /// (<see cref="MentoringSchedule"/>).
    /// </summary>
    private async Task<DateTimeOffset?> MatchingAtAsync(IAdvisorSource source, SemesterDto semester, string courseCode, CancellationToken ct)
    {
        if (source.Kind == AdvisorKinds.Mentor)
        {
            return MentoringSchedule.StartsAt(semester);
        }
        var round = await academic.GetOpenRoundAsync(courseCode, RoundTypes.TeamFormation, ct);
        return round is not null && round.SemesterId == semester.Id ? round.DeadlineAt : null;
    }

    private IAdvisorSource Source(string kind)
    {
        var parsed = AdvisorMatchingKinds.Parse(kind);
        return sources.First(s => s.Kind == parsed);
    }

    private sealed record Proposal(
        List<AdvisorMatchingPlanItem> Plan, Dictionary<Guid, TeamEntity> Teams, Dictionary<Guid, AdvisorCandidate> Advisors, Dictionary<Guid, int> Loads);

    /// <summary>Phương án cho mọi nhóm chưa giải tán, chưa có cố vấn cùng loại của môn trong học kỳ (theo tên nhóm).</summary>
    private async Task<Proposal> ProposeAsync(IAdvisorSource source, Guid semesterId, string courseCode, CancellationToken ct)
    {
        var all = await repo.ListTeamsAsync(semesterId, courseCode, ct);
        var withAdvisor = await repo.ListTeamIdsWithAdvisorAsync(source.Kind, all.Select(t => t.Id).ToList(), ct);
        var open = all.Where(t => !withAdvisor.Contains(t.Id)).ToList();
        var advisors = await source.ListActiveAsync(courseCode, ct);
        var loads = await source.CountLoadsAsync(semesterId, courseCode, ct);
        var advisorById = advisors.ToDictionary(a => a.Id);

        IReadOnlyList<PlannedAdvisorMatch> matches = [];
        if (open.Count > 0 && advisors.Count > 0)
        {
            var inputs = await source.BuildTeamsAsync(open, ct);
            matches = await matching.PlanAdvisorMatchingAsync(source.Kind, courseCode, inputs,
                advisors.Select(a => new AdvisorMatchingAdvisor(a.Id, a.Domains.Select(d => d.Id).ToList(), loads.GetValueOrDefault(a.Id),
                    a.MaxTeams, a.PreferredTeams)).ToList(), ct);
        }

        var byTeam = matches.GroupBy(m => m.TeamId).ToDictionary(g => g.Key, g => g.First());
        var plan = open.Select(t =>
        {
            // Matching bỏ sót nhóm hoặc trả cố vấn ngoài danh sách → coi như chưa ghép được.
            var match = byTeam.GetValueOrDefault(t.Id);
            return match?.AdvisorId is { } id && advisorById.TryGetValue(id, out var advisor) && match.Reason is not null
                ? new AdvisorMatchingPlanItem(t.Id, t.Name, id, advisor.FullName, match.Reason, match.PreferenceRank)
                : new AdvisorMatchingPlanItem(t.Id, t.Name, null, null, null, null);
        }).ToList();
        return new Proposal(plan, open.ToDictionary(t => t.Id), advisorById, loads);
    }

    /// <summary>
    /// Áp dụng phương án trong transaction của người gọi: cố vấn đã nghỉ hoặc đã đủ số tối đa (đổi tay trong lúc chờ) thì nhóm thành chưa ghép.
    /// Ghi kết quả thực tế vào lần chạy (APPLIED) và thêm <c>AdvisorMatchingCompleted</c>; người gọi lưu lần cuối.
    /// </summary>
    private async Task ApplyAsync(IAdvisorSource source, AdvisorMatchingRun run, IReadOnlyList<AdvisorMatchingPlanItem> plan,
        IReadOnlyDictionary<Guid, TeamEntity> teamsById, IReadOnlyDictionary<Guid, AdvisorCandidate> advisors, Dictionary<Guid, int> loads,
        CancellationToken ct)
    {
        var matchedTeamIds = plan.Where(p => p.AdvisorId is not null).Select(p => p.TeamId).ToList();
        var members = await repo.ListMemberStudentIdsAsync(matchedTeamIds, ct);
        var applied = new List<AdvisorMatchingPlanItem>();
        foreach (var item in plan)
        {
            if (item.AdvisorId is not { } advisorId || !advisors.TryGetValue(advisorId, out var advisor) || item.Reason is null
                || (advisor.MaxTeams is { } max && loads.GetValueOrDefault(advisorId) >= max))
            {
                applied.Add(item with { AdvisorId = null, AdvisorName = null, Reason = null, PreferenceRank = null });
                continue;
            }

            var team = teamsById[item.TeamId];
            var done = item with { TeamName = team.Name, AdvisorName = advisor.FullName };
            source.StageAutoAssignment(team, advisor, done, run.Id, members[team.Id].ToList(), AuditActor.System);
            loads[advisorId] = loads.GetValueOrDefault(advisorId) + 1;
            await topicInterests.CloseInterestsOnAssignmentAsync(team.Id, source.Kind, advisorId, ct);
            applied.Add(done);
        }

        SetPlan(run, applied);
        (run.Status, run.AppliedAt) = (MatchingRunStatuses.Applied, clock.GetUtcNow());
        repo.AddEvent(AdvisorMatchingEventTypes.AdvisorMatchingCompleted, "AdvisorMatchingRun", run.Id, new AdvisorMatchingCompletedEvent(
            run.Id, run.AdvisorKind, run.CourseCode, run.Trigger, run.MatchedCount, run.UnmatchedCount));
    }

    private AdvisorMatchingRun NewRun(string kind, Guid semesterId, string courseCode, string trigger, Guid? requestedBy) => new()
    {
        Id = Guid.NewGuid(),
        AdvisorKind = kind,
        SemesterId = semesterId,
        CourseCode = courseCode,
        Trigger = trigger,
        Status = MatchingRunStatuses.Preview,
        RequestedByAccountId = requestedBy,
        CreatedAt = clock.GetUtcNow(),
    };

    private static void EnsurePreview(AdvisorMatchingRun run)
    {
        if (run.Status != MatchingRunStatuses.Preview)
        {
            throw AdvisorMatchingErrors.RunNotPreview(run.Status);
        }
    }

    private static void SetPlan(AdvisorMatchingRun run, IReadOnlyList<AdvisorMatchingPlanItem> plan)
    {
        var ordered = plan.OrderBy(p => p.TeamName, StringComparer.CurrentCulture).ThenBy(p => p.TeamId).ToList();
        run.Plan = JsonSerializer.Serialize(ordered, EventEnvelope.JsonOptions);
        run.MatchedCount = ordered.Count(p => p.AdvisorId is not null);
        run.UnmatchedCount = ordered.Count - run.MatchedCount;
    }

    private static List<AdvisorMatchingPlanItem> ReadPlan(AdvisorMatchingRun run) =>
        JsonSerializer.Deserialize<List<AdvisorMatchingPlanItem>>(run.Plan, EventEnvelope.JsonOptions) ?? [];

    private static AdvisorMatchingRunResponse ToResponse(AdvisorMatchingRun run) => new(
        run.Id, run.AdvisorKind, run.CourseCode, run.Trigger, run.Status, run.MatchedCount, run.UnmatchedCount, run.CreatedAt, run.AppliedAt,
        ReadPlan(run));

    private static MentorAssignmentResponse ToResponse(Team.Repositories.MentorAssignments.MentorAssignment a, MentorSnapshot mentor) =>
        new(a.Id, a.TeamId, a.MentorId, a.MentorName, mentor.Organization, mentor.IsInternal, a.Status, a.Reason, a.Source, a.AssignedAt, a.EndedAt);

    private static LecturerAssignmentResponse ToResponse(Team.Repositories.LecturerAssignments.LecturerAssignment a) =>
        new(a.Id, a.TeamId, a.LecturerId, a.LecturerName, a.Status, a.Reason, a.PreferenceRank, a.Source, a.AssignedAt, a.Note);

    private static TopicInterestResponse ToInterest(TopicInterest i, Guid? previousInstructorId) =>
        new(i.Id, i.TeamId, i.AdvisorKind, i.AdvisorId, i.AdvisorName, previousInstructorId == i.AdvisorId, i.Status, i.ClosedReason, i.CreatedAt);
}
