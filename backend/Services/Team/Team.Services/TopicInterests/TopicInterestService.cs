using System.Security.Claims;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using BuildingBlocks.Messaging;
using Team.Repositories.AuditLog;
using Team.Repositories.Context;
using Team.Repositories.Teams;
using Team.Repositories.TopicInterests;
using Team.Services.Academic;
using Team.Services.Teams;

namespace Team.Services.TopicInterests;

public sealed class TopicInterestService(
    ITopicInterestRepository repo,
    ITeamRepository teams,
    IAcademicClient academic,
    TeamReader reader,
    AuditLogWriter audit,
    TimeProvider clock) : ITopicInterestService
{
    private const int DefaultUnlimitedLimit = 10;
    private const int ExtraSlots = 2;
    private const int DefaultMentorMax = 5;

    public async Task<TopicRecommendationsResponse> GetRecommendationsAsync(
        string advisorKind,
        string? courseCode,
        int page,
        int pageSize,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        advisorKind = NormalizeAdvisorKind(advisorKind);
        var (advisorId, _) = ResolveAdvisorIdentity(advisorKind, user);

        var semester = await academic.GetCurrentSemesterAsync(ct) ?? throw TeamErrors.RoundClosed();
        var targetCourse = courseCode ?? "EXE101";

        var (interestOpen, matchingAt) = await ResolvePhaseAsync(advisorKind, targetCourse, semester, ct);
        var (limit, remainingCapacity) = await CalculateLimitAsync(advisorKind, advisorId, targetCourse, ct);
        var openCount = await repo.CountOpenByAdvisorAsync(advisorKind, advisorId, ct);

        var advisorDomainIds = await GetAdvisorDomainIdsAsync(advisorKind, advisorId, ct);
        var catalogs = await reader.LoadCatalogsAsync(ct);

        // Lấy danh sách nhóm của môn trong học kỳ chưa có cố vấn loại này
        var allTeamIds = await repo.ListTeamIdsInSemesterAsync(semester.Id, courseCode, ct);
        var graphs = await teams.ListGraphsAsync(allTeamIds, ct);

        var candidates = new List<(TeamGraph Graph, bool WasEXE101, List<CatalogRef> MatchedDomains, int OtherInterests, int? PrefRank, TopicInterest? MyInterest)>();

        foreach (var graph in graphs)
        {
            if (graph.Team.Status == TeamStatuses.Disbanded) continue;
            var hasAdvisor = await repo.HasActiveAssignmentAsync(graph.Team.Id, advisorKind, ct);
            if (hasAdvisor) continue;

            var wasEXE101 = advisorKind == AdvisorKinds.Lecturer && await repo.WasLecturerEXE101InstructorAsync(graph.Team.Id, advisorId, ct);
            var matchedDomains = graph.Domains
                .Where(d => advisorDomainIds.Contains(d.DomainId))
                .Select(d => new CatalogRef(d.DomainId, d.DomainCode, catalogs.Domains.GetValueOrDefault(d.DomainId)?.Name))
                .ToList();
            var otherCount = await repo.CountOpenByTeamAsync(graph.Team.Id, advisorKind, advisorId, ct);
            var prefRank = advisorKind == AdvisorKinds.Lecturer
                ? await repo.GetLecturerPreferenceRankAsync(graph.Team.Id, advisorId, ct)
                : null;
            var myInterest = await repo.GetLatestInterestAsync(graph.Team.Id, advisorKind, advisorId, ct);

            candidates.Add((graph, wasEXE101, matchedDomains, otherCount, prefRank, myInterest));
        }

        // Sắp xếp: Đề tài EXE101 trước, rồi theo số domain trùng, rồi theo ít người quan tâm hơn
        var sorted = candidates
            .OrderByDescending(c => c.WasEXE101)
            .ThenByDescending(c => c.MatchedDomains.Count)
            .ThenBy(c => c.OtherInterests)
            .ToList();

        var paged = sorted
            .Skip(Math.Max(0, (page - 1) * pageSize))
            .Take(pageSize > 0 ? pageSize : 20)
            .ToList();

        var items = paged.Select(c => new TopicRecommendationResponse(
            TeamMapper.ToCard(c.Graph, catalogs),
            c.MatchedDomains,
            c.OtherInterests,
            c.PrefRank,
            c.MyInterest is not null ? ToResponse(c.MyInterest, c.WasEXE101) : null,
            c.WasEXE101,
            false
        )).ToList();

        return new TopicRecommendationsResponse(
            openCount,
            limit,
            remainingCapacity,
            interestOpen,
            matchingAt,
            items);
    }

    public async Task<IReadOnlyList<TopicInterestResponse>> GetMyInterestsAsync(
        string advisorKind,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        advisorKind = NormalizeAdvisorKind(advisorKind);
        var (advisorId, _) = ResolveAdvisorIdentity(advisorKind, user);

        var list = await repo.ListByAdvisorAsync(advisorKind, advisorId, ct);
        var responses = new List<TopicInterestResponse>();
        foreach (var item in list)
        {
            var wasPrevious = advisorKind == AdvisorKinds.Lecturer
                && await repo.WasLecturerEXE101InstructorAsync(item.TeamId, advisorId, ct);
            responses.Add(ToResponse(item, wasPrevious));
        }

        return responses;
    }

    public async Task<IReadOnlyList<TopicInterestResponse>> GetTeamInterestsAsync(
        Guid teamId,
        string? advisorKind,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var graph = await teams.GetGraphAsync(teamId, ct) ?? throw TeamErrors.TeamNotFound(teamId);
        var list = await repo.ListByTeamIdAsync(teamId, advisorKind, ct);

        var isAdmin = user.IsInRole(Roles.Admin);
        var studentId = user.GetStudentId();
        var isMember = studentId is not null && graph.Members.Any(m => m.StudentId == studentId);
        var lecturerId = user.GetLecturerId();
        var mentorId = user.GetMentorId();

        var responses = new List<TopicInterestResponse>();
        foreach (var item in list)
        {
            // Sinh viên chỉ thấy quan tâm của giảng viên (rule 19), không thấy của mentor (rule 20)
            if (isMember && !isAdmin && item.AdvisorKind == AdvisorKinds.Mentor)
            {
                continue;
            }

            // Người ngoài nhóm không có quyền xem trừ Admin hoặc chính cố vấn đó
            var isSelf = (item.AdvisorKind == AdvisorKinds.Lecturer && lecturerId == item.AdvisorId)
                || (item.AdvisorKind == AdvisorKinds.Mentor && mentorId == item.AdvisorId);

            if (!isAdmin && !isMember && !isSelf)
            {
                continue;
            }

            var wasPrevious = item.AdvisorKind == AdvisorKinds.Lecturer
                && await repo.WasLecturerEXE101InstructorAsync(teamId, item.AdvisorId, ct);

            responses.Add(ToResponse(item, wasPrevious));
        }

        return responses;
    }

    public async Task<TopicInterestResponse> OpenInterestAsync(
        Guid teamId,
        OpenTopicInterestInput input,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var advisorKind = NormalizeAdvisorKind(input.AdvisorKind);
        var (advisorId, advisorName) = ResolveAdvisorIdentity(advisorKind, user);

        var graph = await teams.GetGraphAsync(teamId, ct) ?? throw TeamErrors.TeamNotFound(teamId);
        if (graph.Team.Status == TeamStatuses.Disbanded)
        {
            throw TeamErrors.TeamNotFound(teamId);
        }

        if (await repo.HasActiveAssignmentAsync(teamId, advisorKind, ct))
        {
            throw new ConflictException("team-has-advisor", "Nhóm đã có cố vấn",
                $"Nhóm đã được ghép {(advisorKind == AdvisorKinds.Lecturer ? "giảng viên hướng dẫn" : "mentor")}.");
        }

        var semester = await academic.GetCurrentSemesterAsync(ct);
        var (interestOpen, _) = await ResolvePhaseAsync(advisorKind, graph.Team.CourseCode, semester, ct);
        if (!interestOpen)
        {
            var round = await academic.GetOpenRoundAsync(graph.Team.CourseCode, RoundTypes.TeamFormation, ct);
            var isFormationOpen = round is not null && (semester is null || round.SemesterId == semester.Id);
            string detail = $"Giai đoạn bày tỏ quan tâm của {(advisorKind == AdvisorKinds.Lecturer ? "giảng viên" : "mentor")} đã kết thúc hoặc chưa bắt đầu.";
            if (advisorKind == AdvisorKinds.Mentor)
            {
                if (isFormationOpen)
                {
                    detail = "Đợt lập nhóm còn mở, mentor chỉ được mở quan tâm đề tài sau khi đợt lập nhóm đã chốt.";
                }
                else if (semester?.StartDate is { } startDate && semester.MentoringStartWeek is { } startWeek)
                {
                    var mentoringStartsAt = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays((startWeek - 1) * 7);
                    if (clock.GetUtcNow() >= mentoringStartsAt)
                    {
                        detail = $"Đã qua tuần bắt đầu hướng dẫn của mentor (tuần {startWeek}).";
                    }
                }
                else
                {
                    detail = "Học kỳ chưa cấu hình ngày bắt đầu hoặc tuần bắt đầu hướng dẫn của mentor.";
                }
            }
            throw new ConflictException("interest-closed", "Hết thời gian bày tỏ quan tâm", detail);
        }

        var (limit, _) = await CalculateLimitAsync(advisorKind, advisorId, graph.Team.CourseCode, ct);
        var currentOpen = await repo.CountOpenByAdvisorAsync(advisorKind, advisorId, ct);

        // Kiểm tra xem đã có quan tâm OPEN chưa
        var existingOpen = await repo.GetOpenInterestAsync(teamId, advisorKind, advisorId, ct);
        var wasPrevious = advisorKind == AdvisorKinds.Lecturer
            && await repo.WasLecturerEXE101InstructorAsync(teamId, advisorId, ct);

        if (existingOpen is not null)
        {
            return ToResponse(existingOpen, wasPrevious);
        }

        if (currentOpen >= limit)
        {
            throw new ConflictException("interest-limit", "Đạt giới hạn quan tâm",
                $"{(advisorKind == AdvisorKinds.Lecturer ? "Giảng viên" : "Mentor")} chỉ có thể mở tối đa {limit} quan tâm cùng lúc.");
        }

        // Nếu từng WITHDRAWN thì mở lại
        var existingAny = await repo.GetLatestInterestAsync(teamId, advisorKind, advisorId, ct);
        TopicInterest interest;
        if (existingAny is not null && existingAny.Status == TopicInterestStatuses.Withdrawn)
        {
            existingAny.Status = TopicInterestStatuses.Open;
            existingAny.ClosedAt = null;
            existingAny.ClosedReason = null;
            existingAny.CreatedAt = clock.GetUtcNow();
            existingAny.AdvisorName = advisorName;
            interest = existingAny;
        }
        else
        {
            interest = new TopicInterest
            {
                Id = Guid.NewGuid(),
                TeamId = teamId,
                AdvisorKind = advisorKind,
                AdvisorId = advisorId,
                AdvisorName = advisorName,
                Status = TopicInterestStatuses.Open,
                CreatedAt = clock.GetUtcNow(),
            };
            repo.Add(interest);
        }

        var memberStudentIds = graph.Members.Select(m => m.StudentId).ToList();

        audit.Add(teamId, "TopicInterest", interest.Id, "TOPIC_INTEREST_OPENED", user.ToAuditActor(),
            newValues: new { advisorKind, advisorId, advisorName, wasPreviousInstructor = wasPrevious });

        repo.AddOutboxMessage(
            Topics.Assignment,
            "TopicInterestOpened",
            "Team",
            teamId,
            new
            {
                interestId = interest.Id,
                teamId,
                projectId = graph.Team.ProjectId,
                advisorKind,
                advisorId,
                advisorName,
                wasPreviousInstructor = wasPrevious,
                memberStudentIds,
            });

        await repo.SaveChangesAsync(ct);
        return ToResponse(interest, wasPrevious);
    }

    public async Task WithdrawInterestAsync(Guid id, ClaimsPrincipal user, CancellationToken ct)
    {
        var interest = await repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("quan tâm đề tài", id);

        var isAdmin = user.IsInRole(Roles.Admin);
        var (advisorId, _) = TryResolveAdvisorIdentity(interest.AdvisorKind, user);
        if (!isAdmin && advisorId != interest.AdvisorId)
        {
            throw new ForbiddenException(detail: "Bạn không có quyền rút quan tâm này.");
        }

        if (interest.Status != TopicInterestStatuses.Open)
        {
            return;
        }

        if (await repo.HasActiveAssignmentAsync(interest.TeamId, interest.AdvisorKind, ct))
        {
            throw new ConflictException("preferences-locked", "Không thể rút quan tâm", "Nhóm đã được ghép cố vấn.");
        }

        interest.Status = TopicInterestStatuses.Withdrawn;
        interest.ClosedAt = clock.GetUtcNow();
        interest.ClosedReason = null;

        audit.Add(interest.TeamId, "TopicInterest", interest.Id, "TOPIC_INTEREST_WITHDRAWN", user.ToAuditActor());

        repo.AddOutboxMessage(
            Topics.Assignment,
            "TopicInterestClosed",
            "Team",
            interest.TeamId,
            new
            {
                interestId = interest.Id,
                teamId = interest.TeamId,
                advisorKind = interest.AdvisorKind,
                advisorId = interest.AdvisorId,
                reason = TopicInterestStatuses.Withdrawn,
            });

        await repo.SaveChangesAsync(ct);
    }

    public async Task CloseInterestsOnAssignmentAsync(
        Guid teamId,
        string advisorKind,
        Guid matchedAdvisorId,
        CancellationToken ct)
    {
        var openInterests = await repo.ListOpenByTeamIdAsync(teamId, advisorKind, ct);
        if (openInterests.Count == 0) return;

        var now = clock.GetUtcNow();
        foreach (var interest in openInterests)
        {
            var reason = interest.AdvisorId == matchedAdvisorId
                ? ClosedReasons.ThisAdvisorMatched
                : ClosedReasons.OtherAdvisorMatched;

            interest.Status = TopicInterestStatuses.Closed;
            interest.ClosedAt = now;
            interest.ClosedReason = reason;

            repo.AddOutboxMessage(
                Topics.Assignment,
                "TopicInterestClosed",
                "Team",
                teamId,
                new
                {
                    interestId = interest.Id,
                    teamId,
                    advisorKind,
                    advisorId = interest.AdvisorId,
                    reason,
                });
        }

        await repo.SaveChangesAsync(ct);
    }

    private static string NormalizeAdvisorKind(string advisorKind) =>
        advisorKind.Equals(AdvisorKinds.Mentor, StringComparison.OrdinalIgnoreCase)
            ? AdvisorKinds.Mentor
            : AdvisorKinds.Lecturer;

    private static (Guid AdvisorId, string AdvisorName) ResolveAdvisorIdentity(string advisorKind, ClaimsPrincipal user)
    {
        if (advisorKind == AdvisorKinds.Lecturer)
        {
            var lecturerId = user.GetLecturerId() ?? throw new ForbiddenException(detail: "Chỉ giảng viên mới có quyền thực hiện thao tác này.");
            var name = user.FindFirstValue(ClaimNames.Name) ?? "Giảng viên";
            return (lecturerId, name);
        }
        else
        {
            var mentorId = user.GetMentorId() ?? throw new ForbiddenException(detail: "Chỉ mentor mới có quyền thực hiện thao tác này.");
            var name = user.FindFirstValue(ClaimNames.Name) ?? "Mentor";
            return (mentorId, name);
        }
    }

    private static (Guid? AdvisorId, string? AdvisorName) TryResolveAdvisorIdentity(string advisorKind, ClaimsPrincipal user)
    {
        if (advisorKind == AdvisorKinds.Lecturer)
        {
            var lecturerId = user.GetLecturerId();
            return (lecturerId, user.FindFirstValue(ClaimNames.Name));
        }
        else
        {
            var mentorId = user.GetMentorId();
            return (mentorId, user.FindFirstValue(ClaimNames.Name));
        }
    }

    private async Task<(bool Open, DateTimeOffset? MatchingAt)> ResolvePhaseAsync(
        string advisorKind,
        string courseCode,
        SemesterDto? semester,
        CancellationToken ct)
    {
        var round = await academic.GetOpenRoundAsync(courseCode, RoundTypes.TeamFormation, ct);
        var isFormationOpen = round is not null && (semester is null || round.SemesterId == semester.Id);

        if (advisorKind == AdvisorKinds.Lecturer)
        {
            return (isFormationOpen, round?.DeadlineAt);
        }

        // Mentor: Mở từ khi đợt lập nhóm đóng cho đến khi bắt đầu tuần mentor (rule 19)
        if (isFormationOpen) return (false, round?.DeadlineAt);
        if (semester?.StartDate is { } startDate && semester.MentoringStartWeek is { } startWeek)
        {
            var mentoringStartsAt = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays((startWeek - 1) * 7);
            return (clock.GetUtcNow() < mentoringStartsAt, mentoringStartsAt);
        }
        // Khi thiếu dữ liệu cấu hình học kỳ (StartDate hoặc MentoringStartWeek), không mặc định mở mà đóng cổng quan tâm của mentor để tránh nhận quan tâm ngoài kỳ.
        return (false, null);
    }

    private async Task<(int Limit, int? RemainingCapacity)> CalculateLimitAsync(
        string advisorKind,
        Guid advisorId,
        string courseCode,
        CancellationToken ct)
    {
        var currentAssigned = await repo.CountActiveAssignmentsByAdvisorAsync(advisorKind, advisorId, ct);

        if (advisorKind == AdvisorKinds.Lecturer)
        {
            var snapshot = await academic.GetLecturerAsync(advisorId, ct);
            var maxTeams = courseCode == "EXE201" ? snapshot?.MaxTeamsExe201 : snapshot?.MaxTeamsExe101;

            if (maxTeams is null)
            {
                return (DefaultUnlimitedLimit, null);
            }

            var remaining = Math.Max(0, maxTeams.Value - currentAssigned);
            return (remaining + ExtraSlots, remaining);
        }
        else
        {
            // Rule 19: Giới hạn quan tâm mentor = sức chứa tối đa + 2.
            // Tích hợp IAcademicClient.GetMentorAsync (BE-66 của Nam). Khi Academic chưa có endpoint (404), fallback DefaultMentorMax = 5.
            var snapshot = await academic.GetMentorAsync(advisorId, ct);
            var maxTeams = snapshot?.PreferredTeams ?? snapshot?.MaxTeams ?? DefaultMentorMax;
            var remaining = Math.Max(0, maxTeams - currentAssigned);
            return (remaining + ExtraSlots, remaining);
        }
    }

    private async Task<HashSet<int>> GetAdvisorDomainIdsAsync(string advisorKind, Guid advisorId, CancellationToken ct)
    {
        if (advisorKind == AdvisorKinds.Lecturer)
        {
            var snapshot = await academic.GetLecturerAsync(advisorId, ct);
            if (snapshot?.Domains is { } domains)
            {
                return domains.Select(d => d.Id).ToHashSet();
            }
        }
        else if (advisorKind == AdvisorKinds.Mentor)
        {
            var snapshot = await academic.GetMentorAsync(advisorId, ct);
            if (snapshot?.Domains is { } domains)
            {
                return domains.Select(d => d.Id).ToHashSet();
            }
        }
        return [];
    }

    private static TopicInterestResponse ToResponse(TopicInterest interest, bool wasPrevious) =>
        new(
            interest.Id,
            interest.TeamId,
            interest.AdvisorKind,
            interest.AdvisorId,
            interest.AdvisorName,
            wasPrevious,
            interest.Status,
            interest.ClosedReason,
            interest.CreatedAt);
}
