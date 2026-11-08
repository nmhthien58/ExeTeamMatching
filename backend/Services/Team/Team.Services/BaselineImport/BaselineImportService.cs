using System.Text.Json;
using System.Text.RegularExpressions;
using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Team.Repositories.BaselineImport;
using Team.Repositories.Members;
using Team.Repositories.Teams;
using Team.Services.Academic;
using Team.Services.Members;
using Team.Services.Teams;

namespace Team.Services.BaselineImport;

public sealed class BaselineImportService(
    IBaselineImportRepository repository,
    IAcademicClient academic,
    TimeProvider clock) : IBaselineImportService
{
    private static readonly Regex StudentCodeRegex = new(@"^[A-Z]{2,3}[0-9]{6}$", RegexOptions.Compiled);

    /// <summary>
    /// Import file mốc nhóm kỳ trước (Group List EXE101 của FAP) – BE-92. Bộ môn mới dùng hệ thống nên chưa có nhóm cũ trong DB:
    /// file là danh sách nhóm kỳ trước, đối chiếu với sinh viên EXE201 của học kỳ đang import. Mỗi nhóm cũ giữ lại thành viên
    /// đã ghi danh EXE201 và chưa có nhóm; còn ít nhất 1 người thì tạo nhóm kế thừa (đủ sĩ số tối thiểu: ACTIVE, thiếu: DRAFT để
    /// nhận thêm người). Sinh viên không có trong học kỳ chỉ là cảnh báo; chỉ lỗi đọc file mới chặn.
    /// </summary>
    public async Task<BaselineImportResponse> ImportAsync(
        Guid semesterId,
        Stream fileStream,
        string fileName,
        bool dryRun,
        CancellationToken ct)
    {
        var errors = new List<BaselineImportRowError>();
        var records = BaselineImportFileParser.Parse(fileStream, fileName, errors);
        if (records.Count == 0)
        {
            if (errors.Count == 0) errors.Add(new BaselineImportRowError(1, "Không đọc được nhóm nào trong file."));
            return new BaselineImportResponse(0, 0, 0, 0, errors);
        }

        // Lỗi từng dòng của file (định dạng, dòng không thuộc nhóm) chỉ là cảnh báo: các nhóm còn lại vẫn đối chiếu được.
        var warnings = new List<BaselineImportRowError>(errors);
        errors.Clear();

        IReadOnlyList<StudentSnapshot> enrolled;
        try
        {
            enrolled = await academic.ListSemesterStudentsAsync(semesterId, "EXE201", ct);
        }
        catch (Exception ex)
        {
            return new BaselineImportResponse(0, 0, 0, 0, [new BaselineImportRowError(1, "Không thể kết nối tới Academic Service: " + ex.Message)]);
        }

        var enrolledByCode = enrolled
            .Where(s => s.IsEnrolled)
            .GroupBy(s => s.StudentCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var alreadyInTeam = await repository.GetStudentsAlreadyInTeamAsync(semesterId, enrolledByCode.Values.Select(s => s.Id), ct);

        var teamGroups = records
            .Where(r => !string.IsNullOrWhiteSpace(r.TeamName))
            .GroupBy(r => r.TeamName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToList();
        var allTeamNames = teamGroups.Select(g => g.Key).ToList();
        var resolvedExe101Teams = await ResolveExe101TeamsAsync(teamGroups, ct);
        var existingNames = await repository.GetExistingTeamNamesAsync(
            semesterId, "EXE201", allTeamNames.Concat(allTeamNames.Select(n => $"{n} (EXE201)")), ct);

        var assignedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedNames = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        var summaries = new List<InheritedTeamSummary>();
        var batch = new InheritBatch();
        var now = clock.GetUtcNow();
        var skippedTeamCount = 0;
        var linkedProjectCount = 0;

        foreach (var group in teamGroups)
        {
            var oldMembers = group
                .GroupBy(r => r.StudentCode, StringComparer.OrdinalIgnoreCase)
                .Select(g => (Code: g.Key, IsLeader: g.Any(r => r.IsLeader), Row: g.First().RowNumber))
                .ToList();

            var matched = new List<(StudentSnapshot Student, bool WasLeader)>();
            var missing = new List<string>();
            foreach (var m in oldMembers)
            {
                if (!StudentCodeRegex.IsMatch(m.Code))
                {
                    warnings.Add(new BaselineImportRowError(m.Row, $"MSSV '{m.Code}' không đúng định dạng, bỏ qua."));
                    continue;
                }
                if (assignedCodes.Contains(m.Code))
                {
                    warnings.Add(new BaselineImportRowError(m.Row, $"Sinh viên '{m.Code}' đã thuộc nhóm khác trong file, bỏ qua ở nhóm '{group.Key}'."));
                    continue;
                }
                if (!enrolledByCode.TryGetValue(m.Code, out var student) || alreadyInTeam.Contains(student.Id))
                {
                    missing.Add(m.Code);
                    continue;
                }
                matched.Add((student, m.IsLeader));
            }

            if (matched.Count == 0)
            {
                skippedTeamCount++;
                continue;
            }

            foreach (var (student, _) in matched) assignedCodes.Add(student.StudentCode);
            var leader = matched.FirstOrDefault(m => m.WasLeader).Student ?? matched[0].Student;

            var teamName = group.Key;
            if (usedNames.Contains(teamName)) teamName = $"{teamName} (EXE201)";
            if (usedNames.Contains(teamName))
            {
                warnings.Add(new BaselineImportRowError(group.First().RowNumber, $"Tên nhóm '{group.Key}' đã tồn tại trong học kỳ, bỏ qua nhóm."));
                foreach (var (student, _) in matched) assignedCodes.Remove(student.StudentCode);
                skippedTeamCount++;
                continue;
            }
            usedNames.Add(teamName);

            resolvedExe101Teams.TryGetValue(group.Key, out var exe101Team);
            if (exe101Team?.ProjectId is not null) linkedProjectCount++;
            var status = matched.Count >= TeamRules.MinSize ? TeamStatuses.Active : TeamStatuses.Draft;
            var teamId = dryRun ? (Guid?)null : Guid.NewGuid();

            summaries.Add(new InheritedTeamSummary(
                TeamId: teamId,
                TeamName: teamName,
                OldTeamId: exe101Team?.Id,
                ProjectId: exe101Team?.ProjectId,
                ProjectName: group.Select(r => r.ProjectName).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)),
                LeaderStudentCode: leader.StudentCode,
                LeaderName: leader.FullName,
                MemberCount: matched.Count,
                Members: matched.Select(m => new InheritedTeamMemberSummary(
                    m.Student.Id, m.Student.StudentCode, m.Student.FullName, m.Student.SpecializationId, m.Student.Id == leader.Id)).ToList(),
                MissingOldMemberStudentCodes: missing,
                Status: status));

            if (teamId is { } id)
            {
                AddInheritedTeam(batch, id, semesterId, teamName, status, exe101Team?.ProjectId, exe101Team?.Id,
                    leader, matched.Select(m => m.Student).ToList(), exe101Team is null ? null : "IMPORT", now);
            }
        }

        if (!dryRun && batch.Teams.Count > 0)
        {
            await repository.SaveImportedTeamsAsync(batch.Teams, batch.Members, batch.BaselineMembers, batch.Outbox, ct);
        }

        return new BaselineImportResponse(
            TeamCount: summaries.Count,
            StudentCount: assignedCodes.Count,
            LinkedProjectCount: linkedProjectCount,
            UnlinkedTeamCount: summaries.Count - linkedProjectCount,
            Errors: [],
            NeedsReviewTeamCount: summaries.Count(t => t.Status == TeamStatuses.Draft),
            SkippedTeamCount: skippedTeamCount,
            FreeStudentCount: enrolledByCode.Values.Count(s => !alreadyInTeam.Contains(s.Id) && !assignedCodes.Contains(s.StudentCode)),
            Teams: summaries,
            Warnings: warnings.OrderBy(w => w.Row).ToList());
    }

    /// <summary>Gắn nhóm trong file với nhóm EXE101 trong hệ thống (cùng tên; trùng tên nhiều kỳ thì chọn nhóm trùng nhiều MSSV nhất).</summary>
    private async Task<Dictionary<string, Exe101CandidateTeam>> ResolveExe101TeamsAsync(
        IReadOnlyList<IGrouping<string, ParsedBaselineRecord>> teamGroups,
        CancellationToken ct)
    {
        var candidates = (await repository.GetExe101CandidateTeamsAsync(teamGroups.Select(g => g.Key), ct))
            .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var resolved = new Dictionary<string, Exe101CandidateTeam>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in teamGroups)
        {
            if (!candidates.TryGetValue(group.Key, out var list) || list.Count == 0) continue;
            if (list.Count == 1)
            {
                resolved[group.Key] = list[0];
                continue;
            }
            var codes = group.Select(r => r.StudentCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var scored = list.Select(c => (Candidate: c, Overlap: c.MemberStudentCodes.Count(codes.Contains))).ToList();
            var max = scored.Max(s => s.Overlap);
            var top = scored.Where(s => s.Overlap == max).ToList();
            // Không trùng ai hoặc hòa điểm: để Bộ môn gắn tay (W29).
            if (max > 0 && top.Count == 1) resolved[group.Key] = top[0].Candidate;
        }
        return resolved;
    }

    private sealed class InheritBatch
    {
        public List<TeamEntity> Teams { get; } = [];
        public List<TeamMember> Members { get; } = [];
        public List<TeamBaselineMember> BaselineMembers { get; } = [];
        public List<OutboxMessage> Outbox { get; } = [];
    }

    /// <summary>Nhóm kế thừa + thành viên + mốc + sự kiện TeamCreated, MemberJoined, TeamLinkedToProject.</summary>
    private static void AddInheritedTeam(
        InheritBatch batch, Guid teamId, Guid semesterId, string teamName, string status, Guid? projectId, Guid? sourceTeamId,
        StudentSnapshot leader, IReadOnlyList<StudentSnapshot> members, string? linkedBy, DateTimeOffset now)
    {
        batch.Teams.Add(new TeamEntity
        {
            Id = teamId,
            SemesterId = semesterId,
            CourseCode = "EXE201",
            Name = teamName,
            ProjectId = projectId,
            Status = status,
            IsInherited = true,
            SourceTeamId = sourceTeamId,
            CreatedAt = now,
            UpdatedAt = now,
        });

        foreach (var s in members)
        {
            var role = s.Id == leader.Id ? MemberRoles.Leader : MemberRoles.Member;
            batch.Members.Add(new TeamMember
            {
                Id = Guid.NewGuid(), TeamId = teamId, SemesterId = semesterId, StudentId = s.Id, StudentCode = s.StudentCode,
                FullName = s.FullName, MajorId = s.MajorId, SpecializationId = s.SpecializationId, Role = role,
                JoinSource = "INHERITED", JoinedAt = now,
            });
            batch.BaselineMembers.Add(new TeamBaselineMember
            {
                TeamId = teamId, StudentId = s.Id, StudentCode = s.StudentCode, FullName = s.FullName, MajorId = s.MajorId,
                SpecializationId = s.SpecializationId, Role = role, ImportedAt = now,
            });
        }

        void AddEvent(string type, object payload) => batch.Outbox.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Topic = "exe.team",
            EventType = type,
            AggregateType = "Team",
            AggregateId = teamId,
            Payload = JsonSerializer.Serialize(payload, EventEnvelope.JsonOptions),
            OccurredAt = now.AddTicks(batch.Outbox.Count * 10L),
        });

        AddEvent(TeamEventTypes.TeamCreated, new TeamCreated(teamId, semesterId, "EXE201", teamName, leader.Id, leader.FullName,
            projectId, IsAutoAssigned: false, IsInherited: true, SourceTeamId: sourceTeamId));
        var memberCount = 1; // TeamCreated đã đưa trưởng nhóm vào read model của Collaboration và Matching.
        foreach (var s in members.Where(m => m.Id != leader.Id))
        {
            AddEvent(MemberEventTypes.MemberJoined, new MemberJoined(teamId, semesterId, s.Id, s.FullName, s.SpecializationId ?? 0, "INHERITED", ++memberCount));
        }
        if (projectId is { } pId && sourceTeamId is { } source && linkedBy is not null)
        {
            AddEvent(TeamEventTypes.TeamLinkedToProject, new TeamLinkedToProject(teamId, pId, source, linkedBy));
        }
    }

    public async Task<AutoInheritResponse> AutoInheritAsync(AutoInheritRequest request, CancellationToken ct)
    {
        var semesterId = request.SemesterId;

        // 1. Fetch EXE201 students for this semester
        IReadOnlyList<StudentSnapshot> enrolledStudents;
        if (request.StudentCodes is { Count: > 0 } codes)
        {
            var distinctCodes = codes.Where(c => StudentCodeRegex.IsMatch(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            enrolledStudents = await academic.LookupStudentsByCodeAsync(semesterId, distinctCodes, ct);
        }
        else
        {
            enrolledStudents = await academic.ListSemesterStudentsAsync(semesterId, "EXE201", ct);
        }

        var enrolledByCode = enrolledStudents
            .Where(s => s.IsEnrolled)
            .ToDictionary(s => s.StudentCode, StringComparer.OrdinalIgnoreCase);

        // Check which students are already in a team in this semester
        var alreadyInTeamIds = await repository.GetStudentsAlreadyInTeamAsync(semesterId, enrolledByCode.Values.Select(s => s.Id), ct);
        var availableStudentsByCode = enrolledByCode
            .Where(kvp => !alreadyInTeamIds.Contains(kvp.Value.Id))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

        // 2. Fetch previous EXE101 teams (xác định kỳ liền trước theo StartDate từ Academic)
        var semesters = await academic.ListSemestersAsync(ct);
        var currentSemester = semesters.FirstOrDefault(s => s.Id == semesterId);
        var previousSemester = semesters
            .Where(s => s.Id != semesterId && s.StartDate.HasValue && (currentSemester?.StartDate == null || s.StartDate < currentSemester.StartDate))
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefault();

        var previousTeams = previousSemester != null
            ? await repository.GetExe101TeamsBySemesterAsync(previousSemester.Id, ct)
            : [];

        // 3. Match members
        var inheritedSummaries = new List<InheritedTeamSummary>();
        var assignedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var teamsToInsert = new List<TeamEntity>();
        var membersToInsert = new List<TeamMember>();
        var baselineMembersToInsert = new List<TeamBaselineMember>();
        var outboxMessages = new List<OutboxMessage>();
        var now = clock.GetUtcNow();

        // Get existing team names in current semester to avoid collisions
        var existingNames = await repository.GetExistingTeamNamesAsync(semesterId, "EXE201", previousTeams.Select(t => t.Name), ct);

        foreach (var oldTeam in previousTeams)
        {
            var matchedOldMembers = oldTeam.Members
                .Where(m => availableStudentsByCode.ContainsKey(m.StudentCode) && !assignedCodes.Contains(m.StudentCode))
                .ToList();

            if (matchedOldMembers.Count == 0)
            {
                continue; // 0 students enrolled in EXE201 -> do not create team
            }

            var missingOldMembers = oldTeam.Members
                .Where(m => !availableStudentsByCode.ContainsKey(m.StudentCode) || assignedCodes.Contains(m.StudentCode))
                .Select(m => m.StudentCode)
                .ToList();

            var leaderMember = matchedOldMembers.FirstOrDefault(m => m.Role == "LEADER") ?? matchedOldMembers[0];

            foreach (var m in matchedOldMembers)
            {
                assignedCodes.Add(m.StudentCode);
            }

            var teamName = oldTeam.Name;
            if (existingNames.Contains(teamName.ToLowerInvariant()))
            {
                teamName = $"{teamName} (EXE201)";
            }

            // NEEDS_REVIEW chặn mọi thay đổi thành viên (RoundGuard): nhóm thiếu người để DRAFT để còn nhận thêm.
            var status = matchedOldMembers.Count >= TeamRules.MinSize ? TeamStatuses.Active : TeamStatuses.Draft;
            var teamId = Guid.NewGuid();

            var teamMembersSummary = new List<InheritedTeamMemberSummary>();
            foreach (var m in matchedOldMembers)
            {
                var snap = availableStudentsByCode[m.StudentCode];
                var isLeader = m.StudentCode.Equals(leaderMember.StudentCode, StringComparison.OrdinalIgnoreCase);
                teamMembersSummary.Add(new InheritedTeamMemberSummary(snap.Id, snap.StudentCode, snap.FullName, snap.SpecializationId, isLeader));
            }

            var summary = new InheritedTeamSummary(
                TeamId: request.DryRun ? null : teamId,
                TeamName: teamName,
                OldTeamId: oldTeam.Id,
                ProjectId: oldTeam.ProjectId,
                ProjectName: oldTeam.ProjectName,
                LeaderStudentCode: leaderMember.StudentCode,
                LeaderName: leaderMember.FullName,
                MemberCount: matchedOldMembers.Count,
                Members: teamMembersSummary,
                MissingOldMemberStudentCodes: missingOldMembers,
                Status: status);

            inheritedSummaries.Add(summary);

            if (!request.DryRun)
            {
                var teamEntity = new TeamEntity
                {
                    Id = teamId,
                    SemesterId = semesterId,
                    CourseCode = "EXE201",
                    Name = teamName,
                    ProjectId = oldTeam.ProjectId,
                    Status = status,
                    IsInherited = true,
                    SourceTeamId = oldTeam.Id,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                teamsToInsert.Add(teamEntity);

                var leaderSnap = availableStudentsByCode[leaderMember.StudentCode];

                foreach (var m in matchedOldMembers)
                {
                    var snap = availableStudentsByCode[m.StudentCode];
                    var isLeader = m.StudentCode.Equals(leaderMember.StudentCode, StringComparison.OrdinalIgnoreCase);

                    membersToInsert.Add(new TeamMember
                    {
                        Id = Guid.NewGuid(),
                        TeamId = teamId,
                        SemesterId = semesterId,
                        StudentId = snap.Id,
                        StudentCode = snap.StudentCode,
                        FullName = snap.FullName,
                        MajorId = snap.MajorId,
                        SpecializationId = snap.SpecializationId,
                        Role = isLeader ? MemberRoles.Leader : MemberRoles.Member,
                        JoinSource = "INHERITED",
                        JoinedAt = now,
                    });

                    baselineMembersToInsert.Add(new TeamBaselineMember
                    {
                        TeamId = teamId,
                        StudentId = snap.Id,
                        StudentCode = snap.StudentCode,
                        FullName = snap.FullName,
                        MajorId = snap.MajorId,
                        SpecializationId = snap.SpecializationId,
                        Role = isLeader ? MemberRoles.Leader : MemberRoles.Member,
                        ImportedAt = now,
                    });
                }

                outboxMessages.Add(new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    Topic = "exe.team",
                    EventType = TeamEventTypes.TeamCreated,
                    AggregateType = "Team",
                    AggregateId = teamId,
                    Payload = JsonSerializer.Serialize(new TeamCreated(
                        teamId,
                        semesterId,
                        "EXE201",
                        teamName,
                        leaderSnap.Id,
                        leaderSnap.FullName,
                        teamEntity.ProjectId,
                        IsAutoAssigned: false,
                        IsInherited: true,
                        SourceTeamId: teamEntity.SourceTeamId), EventEnvelope.JsonOptions),
                    OccurredAt = now.AddTicks(outboxMessages.Count * 10L),
                });

                var memberCount = 1;
                foreach (var m in matchedOldMembers.Where(m => !m.StudentCode.Equals(leaderMember.StudentCode, StringComparison.OrdinalIgnoreCase)))
                {
                    var snap = availableStudentsByCode[m.StudentCode];
                    outboxMessages.Add(new OutboxMessage
                    {
                        Id = Guid.NewGuid(),
                        Topic = "exe.team",
                        EventType = MemberEventTypes.MemberJoined,
                        AggregateType = "Team",
                        AggregateId = teamId,
                        Payload = JsonSerializer.Serialize(new MemberJoined(
                            teamId, semesterId, snap.Id, snap.FullName,
                            snap.SpecializationId ?? 0, "INHERITED", ++memberCount), EventEnvelope.JsonOptions),
                        OccurredAt = now.AddTicks(outboxMessages.Count * 10L),
                    });
                }

                if (teamEntity.ProjectId is { } pId)
                {
                    outboxMessages.Add(new OutboxMessage
                    {
                        Id = Guid.NewGuid(),
                        Topic = "exe.team",
                        EventType = TeamEventTypes.TeamLinkedToProject,
                        AggregateType = "Team",
                        AggregateId = teamId,
                        Payload = JsonSerializer.Serialize(new TeamLinkedToProject(
                            teamId,
                            pId,
                            oldTeam.Id,
                            "INHERITED"), EventEnvelope.JsonOptions),
                        OccurredAt = now.AddTicks(outboxMessages.Count * 10L),
                    });
                }
            }
        }

        if (!request.DryRun && teamsToInsert.Count > 0)
        {
            await repository.SaveImportedTeamsAsync(teamsToInsert, membersToInsert, baselineMembersToInsert, outboxMessages, ct);
        }

        var freeStudentsCount = enrolledByCode.Count - assignedCodes.Count;
        var needsReviewCount = inheritedSummaries.Count(t => t.Status == TeamStatuses.Draft);

        return new AutoInheritResponse(
            InheritedTeamCount: inheritedSummaries.Count,
            InheritedStudentCount: assignedCodes.Count,
            FreeStudentCount: Math.Max(0, freeStudentsCount),
            NeedsReviewTeamCount: needsReviewCount,
            Teams: inheritedSummaries);
    }
}
