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

    public async Task<BaselineImportResponse> ImportAsync(
        Guid semesterId,
        Stream fileStream,
        string fileName,
        bool dryRun,
        CancellationToken ct)
    {
        var errors = new List<BaselineImportRowError>();
        var records = BaselineImportFileParser.Parse(fileStream, fileName, errors);

        if (errors.Count > 0 && records.Count == 0)
        {
            return new BaselineImportResponse(0, 0, 0, 0, errors);
        }

        // 1. Basic validation of records
        foreach (var r in records)
        {
            if (string.IsNullOrWhiteSpace(r.TeamName) || r.TeamName.Length < 2 || r.TeamName.Length > 100)
            {
                errors.Add(new BaselineImportRowError(r.RowNumber, $"Tên nhóm '{r.TeamName}' phải từ 2 đến 100 ký tự."));
            }
            if (!StudentCodeRegex.IsMatch(r.StudentCode))
            {
                errors.Add(new BaselineImportRowError(r.RowNumber, $"MSSV '{r.StudentCode}' không đúng định dạng (phải là 2 chữ cái kèm 6 chữ số)."));
            }
        }

        // 2. Duplicate student across different teams in the file
        var studentToTeams = new Dictionary<string, (int Row, string Team)>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in records)
        {
            if (studentToTeams.TryGetValue(r.StudentCode, out var existing))
            {
                if (!existing.Team.Equals(r.TeamName, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new BaselineImportRowError(r.RowNumber, $"Sinh viên '{r.StudentCode}' xuất hiện ở nhiều nhóm trong file ('{existing.Team}' và '{r.TeamName}')."));
                }
            }
            else
            {
                studentToTeams[r.StudentCode] = (r.RowNumber, r.TeamName);
            }
        }

        // 3. Group by team & validate leader and member counts
        var teamGroups = records.GroupBy(r => r.TeamName.Trim(), StringComparer.OrdinalIgnoreCase).ToList();
        var allTeamNames = teamGroups.Select(g => g.Key).Distinct().ToList();

        foreach (var group in teamGroups)
        {
            var firstRow = group.First().RowNumber;
            var distinctMembers = group.GroupBy(r => r.StudentCode, StringComparer.OrdinalIgnoreCase).ToList();

            if (distinctMembers.Count > 6)
            {
                errors.Add(new BaselineImportRowError(firstRow, $"Nhóm '{group.Key}' vượt quá 6 thành viên (có {distinctMembers.Count} người)."));
            }
            if (distinctMembers.Count < 4)
            {
                errors.Add(new BaselineImportRowError(firstRow, $"Nhóm '{group.Key}' cần ít nhất 4 thành viên (có {distinctMembers.Count} người)."));
            }

            var leaders = distinctMembers.Where(m => m.Any(r => r.IsLeader)).ToList();
            if (leaders.Count == 0)
            {
                errors.Add(new BaselineImportRowError(firstRow, $"Nhóm '{group.Key}' thiếu trưởng nhóm."));
            }
            else if (leaders.Count > 1)
            {
                var secondLeaderRow = leaders[1].First().RowNumber;
                errors.Add(new BaselineImportRowError(secondLeaderRow, $"Nhóm '{group.Key}' có nhiều hơn một trưởng nhóm."));
            }
        }

        // 4. Academic Lookup
        var allCodes = records.Select(r => r.StudentCode).Where(c => StudentCodeRegex.IsMatch(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        IReadOnlyList<StudentSnapshot> snapshots;
        try
        {
            snapshots = await academic.LookupStudentsByCodeAsync(semesterId, allCodes, ct);
        }
        catch (Exception ex)
        {
            errors.Add(new BaselineImportRowError(1, "Không thể kết nối tới Academic Service: " + ex.Message));
            snapshots = [];
        }

        var snapshotByCode = snapshots.ToDictionary(s => s.StudentCode, StringComparer.OrdinalIgnoreCase);

        foreach (var r in records.Where(r => StudentCodeRegex.IsMatch(r.StudentCode)))
        {
            if (!snapshotByCode.TryGetValue(r.StudentCode, out var snapshot))
            {
                errors.Add(new BaselineImportRowError(r.RowNumber, $"Không tìm thấy sinh viên '{r.StudentCode}' trong hệ thống."));
            }
            else if (!snapshot.IsEnrolled)
            {
                errors.Add(new BaselineImportRowError(r.RowNumber, $"Sinh viên '{r.StudentCode}' không thuộc danh sách học kỳ {semesterId}."));
            }
        }

        // 5. Existing team names in semester
        var existingTeamNames = await repository.GetExistingTeamNamesAsync(semesterId, "EXE201", allTeamNames, ct);
        foreach (var group in teamGroups)
        {
            if (existingTeamNames.Contains(group.Key.ToLowerInvariant()))
            {
                errors.Add(new BaselineImportRowError(group.First().RowNumber, $"Tên nhóm '{group.Key}' đã tồn tại trong học kỳ."));
            }
        }

        // 6. Students already in a team in this semester
        var validStudentIds = snapshots.Where(s => s.IsEnrolled).Select(s => s.Id).ToList();
        var studentsAlreadyInTeam = await repository.GetStudentsAlreadyInTeamAsync(semesterId, validStudentIds, ct);

        foreach (var r in records)
        {
            if (snapshotByCode.TryGetValue(r.StudentCode, out var snapshot) && studentsAlreadyInTeam.Contains(snapshot.Id))
            {
                errors.Add(new BaselineImportRowError(r.RowNumber, $"Sinh viên '{r.StudentCode}' đã thuộc nhóm khác trong học kỳ."));
            }
        }

        // 7. EXE101 Project linking
        var candidateTeams = await repository.GetExe101CandidateTeamsAsync(allTeamNames, ct);
        var candidatesByName = candidateTeams
            .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var resolvedExe101Teams = new Dictionary<string, Exe101CandidateTeam>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in teamGroups)
        {
            var teamName = group.Key;
            if (!candidatesByName.TryGetValue(teamName, out var candidates) || candidates.Count == 0)
            {
                continue;
            }

            if (candidates.Count == 1)
            {
                resolvedExe101Teams[teamName] = candidates[0];
                continue;
            }

            // Multiple candidates with same name across semesters: disambiguate by student code overlap
            var fileStudentCodes = group.Select(r => r.StudentCode).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var scoredCandidates = candidates.Select(c => new
            {
                Candidate = c,
                OverlapCount = c.MemberStudentCodes.Count(code => fileStudentCodes.Contains(code))
            }).ToList();

            var maxOverlap = scoredCandidates.Max(sc => sc.OverlapCount);
            if (maxOverlap > 0)
            {
                var topCandidates = scoredCandidates.Where(sc => sc.OverlapCount == maxOverlap).ToList();
                if (topCandidates.Count == 1)
                {
                    resolvedExe101Teams[teamName] = topCandidates[0].Candidate;
                }
                // If topCandidates.Count > 1: tie -> do not link, leave for Admin
            }
            // If maxOverlap == 0: no overlap -> do not link, leave for Admin
        }

        var linkedProjectCount = 0;
        var unlinkedTeamCount = 0;

        foreach (var teamName in allTeamNames)
        {
            if (resolvedExe101Teams.ContainsKey(teamName))
            {
                linkedProjectCount++;
            }
            else
            {
                unlinkedTeamCount++;
            }
        }

        var sortedErrors = errors.OrderBy(e => e.Row).ThenBy(e => e.Message).ToList();
        var studentCount = studentToTeams.Count;
        var teamCount = teamGroups.Count;

        if (dryRun || sortedErrors.Count > 0)
        {
            return new BaselineImportResponse(teamCount, studentCount, linkedProjectCount, unlinkedTeamCount, sortedErrors);
        }

        // 8. Commit to Database
        var now = clock.GetUtcNow();
        var teamsToInsert = new List<TeamEntity>();
        var membersToInsert = new List<TeamMember>();
        var baselineMembersToInsert = new List<TeamBaselineMember>();
        var outboxMessages = new List<OutboxMessage>();

        foreach (var group in teamGroups)
        {
            var teamName = group.Key;
            var teamId = Guid.NewGuid();
            resolvedExe101Teams.TryGetValue(teamName, out var exe101Team);

            var teamEntity = new TeamEntity
            {
                Id = teamId,
                SemesterId = semesterId,
                CourseCode = "EXE201",
                Name = teamName,
                ProjectId = exe101Team?.ProjectId,
                Status = "DRAFT",
                IsInherited = true,
                SourceTeamId = exe101Team?.Id,
                CreatedAt = now,
                UpdatedAt = now,
            };
            teamsToInsert.Add(teamEntity);

            var memberGroups = group.GroupBy(r => r.StudentCode, StringComparer.OrdinalIgnoreCase).ToList();
            StudentSnapshot? leaderSnapshot = null;

            foreach (var mg in memberGroups)
            {
                var code = mg.Key;
                var isLeader = mg.Any(r => r.IsLeader);
                var snap = snapshotByCode[code];

                if (isLeader)
                {
                    leaderSnapshot = snap;
                }

                membersToInsert.Add(new TeamMember
                {
                    Id = Guid.NewGuid(),
                    TeamId = teamId,
                    SemesterId = semesterId,
                    StudentId = snap.Id,
                    StudentCode = snap.StudentCode,
                    FullName = snap.FullName,
                    SpecializationId = snap.SpecializationId ?? 0,
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
                    SpecializationId = snap.SpecializationId ?? 0,
                    Role = isLeader ? MemberRoles.Leader : MemberRoles.Member,
                    ImportedAt = now,
                });
            }

            // Events
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
                    leaderSnapshot!.Id,
                    leaderSnapshot.FullName,
                    teamEntity.ProjectId,
                    IsAutoAssigned: false,
                    IsInherited: true,
                    SourceTeamId: teamEntity.SourceTeamId), EventEnvelope.JsonOptions),
                OccurredAt = now.AddTicks(outboxMessages.Count * 10L),
            });

            var memberCount = 1; // TeamCreated đã đưa leader vào read model của Collaboration và Matching.
            foreach (var member in memberGroups.Where(m => !m.Any(r => r.IsLeader)))
            {
                var snapshot = snapshotByCode[member.Key];
                outboxMessages.Add(new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    Topic = "exe.team",
                    EventType = MemberEventTypes.MemberJoined,
                    AggregateType = "Team",
                    AggregateId = teamId,
                    Payload = JsonSerializer.Serialize(new MemberJoined(
                        teamId, semesterId, snapshot.Id, snapshot.FullName,
                        snapshot.SpecializationId ?? 0, "INHERITED", ++memberCount), EventEnvelope.JsonOptions),
                    OccurredAt = now.AddTicks(outboxMessages.Count * 10L),
                });
            }

            if (exe101Team is not null && teamEntity.ProjectId is { } pId)
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
                        exe101Team.Id,
                        "IMPORT"), EventEnvelope.JsonOptions),
                    OccurredAt = now.AddTicks(outboxMessages.Count * 10L),
                });
            }
        }

        await repository.SaveImportedTeamsAsync(teamsToInsert, membersToInsert, baselineMembersToInsert, outboxMessages, ct);

        return new BaselineImportResponse(teamCount, studentCount, linkedProjectCount, unlinkedTeamCount, []);
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

        // 2. Fetch previous EXE101 teams
        var previousTeams = await repository.GetPreviousExe101TeamsAsync(semesterId, ct);

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

            var status = matchedOldMembers.Count < 4 ? "NEEDS_REVIEW" : "DRAFT";
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
                        SpecializationId = snap.SpecializationId ?? 0,
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
                        SpecializationId = snap.SpecializationId ?? 0,
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
        var needsReviewCount = inheritedSummaries.Count(t => t.Status == "NEEDS_REVIEW");

        return new AutoInheritResponse(
            InheritedTeamCount: inheritedSummaries.Count,
            InheritedStudentCount: assignedCodes.Count,
            FreeStudentCount: Math.Max(0, freeStudentsCount),
            NeedsReviewTeamCount: needsReviewCount,
            Teams: inheritedSummaries);
    }
}
