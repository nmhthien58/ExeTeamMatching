using System.IO.Compression;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Team.Repositories.BaselineImport;
using Team.Repositories.Members;
using Team.Repositories.Teams;
using Team.Services.Academic;
using Team.Services.BaselineImport;
using Team.Services.Members;
using Team.Services.Teams;

namespace Team.Tests;

public sealed class BaselineImportTests
{
    private sealed class InMemoryBaselineRepo : IBaselineImportRepository
    {
        public HashSet<Guid> ExistingMemberStudentIds { get; } = [];
        public HashSet<string> ExistingTeamNames { get; } = [];
        public Dictionary<string, TeamEntity> Exe101Teams { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<Exe101CandidateTeam> Exe101Candidates { get; } = [];
        public List<Exe101TeamWithMembers> PreviousExe101Teams { get; } = [];

        public List<TeamEntity> SavedTeams { get; } = [];
        public List<TeamMember> SavedMembers { get; } = [];
        public List<TeamBaselineMember> SavedBaselineMembers { get; } = [];
        public List<OutboxMessage> SavedOutboxMessages { get; } = [];

        public Task<HashSet<Guid>> GetStudentsAlreadyInTeamAsync(Guid semesterId, IEnumerable<Guid> studentIds, CancellationToken ct) =>
            Task.FromResult(studentIds.Where(ExistingMemberStudentIds.Contains).ToHashSet());

        public Task<HashSet<string>> GetExistingTeamNamesAsync(Guid semesterId, string courseCode, IEnumerable<string> teamNames, CancellationToken ct) =>
            Task.FromResult(teamNames.Where(n => ExistingTeamNames.Contains(n.ToLower())).Select(n => n.ToLower()).ToHashSet());

        public Task<IReadOnlyList<Exe101CandidateTeam>> GetExe101CandidateTeamsAsync(IEnumerable<string> teamNames, CancellationToken ct)
        {
            var names = teamNames.Select(n => n.ToLower()).ToHashSet();
            var list = new List<Exe101CandidateTeam>();
            foreach (var c in Exe101Candidates)
            {
                if (names.Contains(c.Name.ToLower())) list.Add(c);
            }
            foreach (var (name, entity) in Exe101Teams)
            {
                if (names.Contains(name.ToLower()) && !list.Any(c => c.Id == entity.Id))
                {
                    list.Add(new Exe101CandidateTeam(entity.Id, entity.Name, entity.SemesterId, entity.ProjectId, []));
                }
            }
            return Task.FromResult<IReadOnlyList<Exe101CandidateTeam>>(list);
        }

        public Task<IReadOnlyList<Exe101TeamWithMembers>> GetPreviousExe101TeamsAsync(Guid currentSemesterId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Exe101TeamWithMembers>>(PreviousExe101Teams);

        public Task SaveImportedTeamsAsync(
            IReadOnlyList<TeamEntity> teams,
            IReadOnlyList<TeamMember> members,
            IReadOnlyList<TeamBaselineMember> baselineMembers,
            IReadOnlyList<OutboxMessage> outboxMessages,
            CancellationToken ct)
        {
            SavedTeams.AddRange(teams);
            SavedMembers.AddRange(members);
            SavedBaselineMembers.AddRange(baselineMembers);
            SavedOutboxMessages.AddRange(outboxMessages);
            return Task.CompletedTask;
        }
    }

    private static byte[] CreateXlsx(string[][] rows)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var sheet = zip.CreateEntry("xl/worksheets/sheet1.xml");
            using var w = new StreamWriter(sheet.Open(), Encoding.UTF8);
            w.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            w.WriteLine("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
            for (var r = 0; r < rows.Length; r++)
            {
                w.WriteLine($"<row r=\"{r + 1}\">");
                for (var c = 0; c < rows[r].Length; c++)
                {
                    var colLetter = (char)('A' + c);
                    w.WriteLine($"<c r=\"{colLetter}{r + 1}\" t=\"inlineStr\"><is><t>{rows[r][c]}</t></is></c>");
                }
                w.WriteLine("</row>");
            }
            w.WriteLine("</sheetData></worksheet>");
        }
        return ms.ToArray();
    }

    [Fact]
    public async Task Valid_xlsx_dry_run_returns_counts_and_does_not_save_to_db()
    {
        var repo = new InMemoryBaselineRepo();
        var fakeAcademic = new FakeAcademic();
        var s1 = fakeAcademic.EnrollWithCode("SE160001", "Sinh viên 1");
        var s2 = fakeAcademic.EnrollWithCode("SE160002", "Sinh viên 2");
        var s3 = fakeAcademic.EnrollWithCode("SE160003", "Sinh viên 3");
        var s4 = fakeAcademic.EnrollWithCode("SE160004", "Sinh viên 4");

        var xlsxBytes = CreateXlsx([
            ["Tên nhóm", "Dự án", "Leader", "MSSV thành viên"],
            ["Nhóm Rồng Vàng", "EduPlatform", "SE160001", "SE160002, SE160003, SE160004"]
        ]);

        var service = new BaselineImportService(repo, fakeAcademic, TimeProvider.System);
        using var stream = new MemoryStream(xlsxBytes);

        var result = await service.ImportAsync(fakeAcademic.Semester.Id, stream, "test.xlsx", dryRun: true, CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.Equal(1, result.TeamCount);
        Assert.Equal(4, result.StudentCount);
        Assert.Equal(0, result.LinkedProjectCount);
        Assert.Equal(1, result.UnlinkedTeamCount);
        Assert.Empty(repo.SavedTeams);
    }

    [Fact]
    public async Task Valid_import_commits_teams_members_baseline_and_outbox_events()
    {
        var repo = new InMemoryBaselineRepo();
        var fakeAcademic = new FakeAcademic();
        var s1 = fakeAcademic.EnrollWithCode("SE160001", "Sinh viên 1");
        var s2 = fakeAcademic.EnrollWithCode("SE160002", "Sinh viên 2");
        var s3 = fakeAcademic.EnrollWithCode("SE160003", "Sinh viên 3");
        var s4 = fakeAcademic.EnrollWithCode("SE160004", "Sinh viên 4");

        // Seed EXE101 team with same name
        var exe101ProjectId = Guid.NewGuid();
        var exe101TeamId = Guid.NewGuid();
        repo.Exe101Teams["Nhóm Rồng Vàng"] = new TeamEntity
        {
            Id = exe101TeamId,
            Name = "Nhóm Rồng Vàng",
            CourseCode = "EXE101",
            ProjectId = exe101ProjectId
        };

        var csv = "Tên nhóm,Dự án,Leader,MSSV thành viên\nNhóm Rồng Vàng,EduPlatform,SE160001,\"SE160002, SE160003, SE160004\"";
        var service = new BaselineImportService(repo, fakeAcademic, TimeProvider.System);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await service.ImportAsync(fakeAcademic.Semester.Id, stream, "test.csv", dryRun: false, CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.Equal(1, result.TeamCount);
        Assert.Equal(4, result.StudentCount);
        Assert.Equal(1, result.LinkedProjectCount);
        Assert.Equal(0, result.UnlinkedTeamCount);

        Assert.Single(repo.SavedTeams);
        var team = repo.SavedTeams.Single();
        Assert.Equal("Nhóm Rồng Vàng", team.Name);
        Assert.Equal("EXE201", team.CourseCode);
        Assert.True(team.IsInherited);
        Assert.Equal(exe101ProjectId, team.ProjectId);
        Assert.Equal(exe101TeamId, team.SourceTeamId);

        Assert.Equal(4, repo.SavedMembers.Count);
        var leaderMember = repo.SavedMembers.Single(m => m.Role == "LEADER");
        Assert.Equal(s1.Id, leaderMember.StudentId);
        Assert.Equal("INHERITED", leaderMember.JoinSource);

        Assert.Equal(4, repo.SavedBaselineMembers.Count);
        var leaderBaseline = repo.SavedBaselineMembers.Single(m => m.Role == "LEADER");
        Assert.Equal(s1.Id, leaderBaseline.StudentId);

        Assert.Equal(5, repo.SavedOutboxMessages.Count);
        Assert.Equal(TeamEventTypes.TeamCreated, repo.SavedOutboxMessages[0].EventType);
        using (var teamCreatedPayload = JsonDocument.Parse(repo.SavedOutboxMessages[0].Payload))
        {
            Assert.True(teamCreatedPayload.RootElement.TryGetProperty("teamId", out _));
            Assert.False(teamCreatedPayload.RootElement.TryGetProperty("TeamId", out _));
        }
        var joined = repo.SavedOutboxMessages.Where(m => m.EventType == MemberEventTypes.MemberJoined)
            .Select(m => JsonSerializer.Deserialize<MemberJoined>(m.Payload, EventEnvelope.JsonOptions)!).ToList();
        Assert.Equal(3, joined.Count);
        Assert.Equal(new[] { s2.Id, s3.Id, s4.Id }, joined.Select(e => e.StudentId));
        Assert.Equal(new[] { 2, 3, 4 }, joined.Select(e => e.MemberCount));
        Assert.All(joined, e => Assert.Equal("INHERITED", e.JoinSource));
        Assert.Equal(TeamEventTypes.TeamLinkedToProject, repo.SavedOutboxMessages[^1].EventType);
        Assert.True(repo.SavedOutboxMessages.Zip(repo.SavedOutboxMessages.Skip(1))
            .All(pair => pair.First.OccurredAt < pair.Second.OccurredAt));
    }

    [Fact]
    public async Task Duplicate_EXE101_team_names_across_semesters_picks_highest_student_overlap()
    {
        var repo = new InMemoryBaselineRepo();
        var fakeAcademic = new FakeAcademic();
        var s1 = fakeAcademic.EnrollWithCode("SE160001", "Sinh viên 1");
        var s2 = fakeAcademic.EnrollWithCode("SE160002", "Sinh viên 2");
        var s3 = fakeAcademic.EnrollWithCode("SE160003", "Sinh viên 3");
        var s4 = fakeAcademic.EnrollWithCode("SE160004", "Sinh viên 4");

        var teamName = "Nhóm 1";
        var sem1Id = Guid.NewGuid();
        var sem2Id = Guid.NewGuid();
        var candidateAId = Guid.NewGuid();
        var candidateBId = Guid.NewGuid();
        var projectAId = Guid.NewGuid();
        var projectBId = Guid.NewGuid();

        // Candidate A (Semester 1): 3 matching students
        repo.Exe101Candidates.Add(new Exe101CandidateTeam(
            candidateAId, teamName, sem1Id, projectAId, ["SE160001", "SE160002", "SE160003"]));

        // Candidate B (Semester 2): 1 matching student
        repo.Exe101Candidates.Add(new Exe101CandidateTeam(
            candidateBId, teamName, sem2Id, projectBId, ["SE160001", "SE160098", "SE160099"]));

        var csv = "Tên nhóm,Dự án,Leader,MSSV thành viên\nNhóm 1,EduPlatform,SE160001,\"SE160002, SE160003, SE160004\"";
        var service = new BaselineImportService(repo, fakeAcademic, TimeProvider.System);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await service.ImportAsync(fakeAcademic.Semester.Id, stream, "test.csv", dryRun: false, CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.Equal(1, result.TeamCount);
        Assert.Equal(1, result.LinkedProjectCount);
        Assert.Equal(0, result.UnlinkedTeamCount);

        var savedTeam = repo.SavedTeams.Single();
        Assert.Equal(projectAId, savedTeam.ProjectId);
        Assert.Equal(candidateAId, savedTeam.SourceTeamId);
    }

    [Fact]
    public async Task Duplicate_EXE101_team_names_with_zero_overlap_leaves_unlinked()
    {
        var repo = new InMemoryBaselineRepo();
        var fakeAcademic = new FakeAcademic();
        fakeAcademic.EnrollWithCode("SE160001", "Sinh viên 1");
        fakeAcademic.EnrollWithCode("SE160002", "Sinh viên 2");
        fakeAcademic.EnrollWithCode("SE160003", "Sinh viên 3");
        fakeAcademic.EnrollWithCode("SE160004", "Sinh viên 4");

        var teamName = "Nhóm 1";
        var sem1Id = Guid.NewGuid();
        var sem2Id = Guid.NewGuid();

        // Candidates with 0 overlapping student codes
        repo.Exe101Candidates.Add(new Exe101CandidateTeam(
            Guid.NewGuid(), teamName, sem1Id, Guid.NewGuid(), ["SE160091", "SE160092", "SE160093"]));
        repo.Exe101Candidates.Add(new Exe101CandidateTeam(
            Guid.NewGuid(), teamName, sem2Id, Guid.NewGuid(), ["SE160094", "SE160095", "SE160096"]));

        var csv = "Tên nhóm,Dự án,Leader,MSSV thành viên\nNhóm 1,EduPlatform,SE160001,\"SE160002, SE160003, SE160004\"";
        var service = new BaselineImportService(repo, fakeAcademic, TimeProvider.System);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await service.ImportAsync(fakeAcademic.Semester.Id, stream, "test.csv", dryRun: false, CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.Equal(1, result.TeamCount);
        Assert.Equal(0, result.LinkedProjectCount);
        Assert.Equal(1, result.UnlinkedTeamCount);

        var savedTeam = repo.SavedTeams.Single();
        Assert.Null(savedTeam.ProjectId);
        Assert.Null(savedTeam.SourceTeamId);
    }

    [Fact]
    public async Task Duplicate_EXE101_team_names_with_tie_overlap_leaves_unlinked()
    {
        var repo = new InMemoryBaselineRepo();
        var fakeAcademic = new FakeAcademic();
        fakeAcademic.EnrollWithCode("SE160001", "Sinh viên 1");
        fakeAcademic.EnrollWithCode("SE160002", "Sinh viên 2");
        fakeAcademic.EnrollWithCode("SE160003", "Sinh viên 3");
        fakeAcademic.EnrollWithCode("SE160004", "Sinh viên 4");

        var teamName = "Nhóm 1";
        var sem1Id = Guid.NewGuid();
        var sem2Id = Guid.NewGuid();

        // Candidate A: 2 matching students (SE160001, SE160002)
        repo.Exe101Candidates.Add(new Exe101CandidateTeam(
            Guid.NewGuid(), teamName, sem1Id, Guid.NewGuid(), ["SE160001", "SE160002", "SE160093"]));

        // Candidate B: 2 matching students (SE160001, SE160003) - TIE
        repo.Exe101Candidates.Add(new Exe101CandidateTeam(
            Guid.NewGuid(), teamName, sem2Id, Guid.NewGuid(), ["SE160001", "SE160003", "SE160096"]));

        var csv = "Tên nhóm,Dự án,Leader,MSSV thành viên\nNhóm 1,EduPlatform,SE160001,\"SE160002, SE160003, SE160004\"";
        var service = new BaselineImportService(repo, fakeAcademic, TimeProvider.System);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await service.ImportAsync(fakeAcademic.Semester.Id, stream, "test.csv", dryRun: false, CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.Equal(1, result.TeamCount);
        Assert.Equal(0, result.LinkedProjectCount);
        Assert.Equal(1, result.UnlinkedTeamCount);

        var savedTeam = repo.SavedTeams.Single();
        Assert.Null(savedTeam.ProjectId);
        Assert.Null(savedTeam.SourceTeamId);
    }

    [Fact]
    public async Task Invalid_student_code_reports_row_error()
    {
        var repo = new InMemoryBaselineRepo();
        var fakeAcademic = new FakeAcademic();
        var csv = "Tên nhóm,Dự án,Leader,MSSV thành viên\nNhóm Rồng Vàng,EduPlatform,INVALID_CODE,SE160002";

        var service = new BaselineImportService(repo, fakeAcademic, TimeProvider.System);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await service.ImportAsync(fakeAcademic.Semester.Id, stream, "test.csv", dryRun: true, CancellationToken.None);

        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Row == 2 && e.Message.Contains("đúng định dạng"));
    }

    [Fact]
    public async Task Student_not_found_or_not_enrolled_reports_error()
    {
        var repo = new InMemoryBaselineRepo();
        var fakeAcademic = new FakeAcademic();
        // SE160001 enrolled, SE160002 not enrolled, SE160003 not in system
        fakeAcademic.EnrollWithCode("SE160001", "Sinh viên 1", enrolled: true);
        fakeAcademic.EnrollWithCode("SE160002", "Sinh viên 2", enrolled: false);

        var csv = "Tên nhóm,Dự án,Leader,MSSV thành viên\nNhóm 1,DA1,SE160001,\"SE160002, SE160003\"";
        var service = new BaselineImportService(repo, fakeAcademic, TimeProvider.System);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await service.ImportAsync(fakeAcademic.Semester.Id, stream, "test.csv", dryRun: true, CancellationToken.None);

        Assert.Contains(result.Errors, e => e.Row == 2 && e.Message.Contains("SE160002") && e.Message.Contains("không thuộc danh sách học kỳ"));
        Assert.Contains(result.Errors, e => e.Row == 2 && e.Message.Contains("SE160003") && e.Message.Contains("Không tìm thấy"));
    }

    [Fact]
    public async Task Duplicate_student_in_different_teams_reports_error()
    {
        var repo = new InMemoryBaselineRepo();
        var fakeAcademic = new FakeAcademic();
        fakeAcademic.EnrollWithCode("SE160001", "Sinh viên 1");
        fakeAcademic.EnrollWithCode("SE160002", "Sinh viên 2");

        var csv = "Tên nhóm,Dự án,Leader,MSSV thành viên\nNhóm A,DA1,SE160001,SE160002\nNhóm B,DA2,SE160001,SE160002";
        var service = new BaselineImportService(repo, fakeAcademic, TimeProvider.System);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await service.ImportAsync(fakeAcademic.Semester.Id, stream, "test.csv", dryRun: true, CancellationToken.None);

        Assert.Contains(result.Errors, e => e.Message.Contains("xuất hiện ở nhiều nhóm"));
    }

    [Fact]
    public async Task Student_already_in_team_in_semester_reports_error()
    {
        var repo = new InMemoryBaselineRepo();
        var fakeAcademic = new FakeAcademic();
        var s1 = fakeAcademic.EnrollWithCode("SE160001", "Sinh viên 1");
        fakeAcademic.EnrollWithCode("SE160002", "Sinh viên 2");
        repo.ExistingMemberStudentIds.Add(s1.Id);

        var csv = "Tên nhóm,Dự án,Leader,MSSV thành viên\nNhóm A,DA1,SE160001,SE160002";
        var service = new BaselineImportService(repo, fakeAcademic, TimeProvider.System);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await service.ImportAsync(fakeAcademic.Semester.Id, stream, "test.csv", dryRun: true, CancellationToken.None);

        Assert.Contains(result.Errors, e => e.Message.Contains("đã thuộc nhóm khác"));
    }

    [Fact]
    public async Task Team_name_already_exists_reports_error()
    {
        var repo = new InMemoryBaselineRepo();
        var fakeAcademic = new FakeAcademic();
        fakeAcademic.EnrollWithCode("SE160001", "Sinh viên 1");
        fakeAcademic.EnrollWithCode("SE160002", "Sinh viên 2");
        repo.ExistingTeamNames.Add("nhóm a");

        var csv = "Tên nhóm,Dự án,Leader,MSSV thành viên\nNhóm A,DA1,SE160001,SE160002";
        var service = new BaselineImportService(repo, fakeAcademic, TimeProvider.System);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await service.ImportAsync(fakeAcademic.Semester.Id, stream, "test.csv", dryRun: true, CancellationToken.None);

        Assert.Contains(result.Errors, e => e.Message.Contains("Tên nhóm 'Nhóm A' đã tồn tại"));
    }

    [Fact]
    public async Task Team_without_leader_or_with_multiple_leaders_reports_error()
    {
        var repo = new InMemoryBaselineRepo();
        var fakeAcademic = new FakeAcademic();
        fakeAcademic.EnrollWithCode("SE160001", "Sinh viên 1");
        fakeAcademic.EnrollWithCode("SE160002", "Sinh viên 2");

        // Format: Tên nhóm, Dự án, Vai trò (Leader flag), MSSV
        // No leader
        var csvNoLeader = "Tên nhóm,Dự án,Leader,MSSV\nNhóm A,DA1,,SE160001\nNhóm A,DA1,,SE160002";
        var service = new BaselineImportService(repo, fakeAcademic, TimeProvider.System);
        using var stream1 = new MemoryStream(Encoding.UTF8.GetBytes(csvNoLeader));
        var result1 = await service.ImportAsync(fakeAcademic.Semester.Id, stream1, "test.csv", dryRun: true, CancellationToken.None);
        Assert.Contains(result1.Errors, e => e.Message.Contains("thiếu trưởng nhóm"));

        // Multiple leaders
        var csvMultiLeader = "Tên nhóm,Dự án,Leader,MSSV\nNhóm A,DA1,Leader,SE160001\nNhóm A,DA1,Leader,SE160002";
        using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes(csvMultiLeader));
        var result2 = await service.ImportAsync(fakeAcademic.Semester.Id, stream2, "test.csv", dryRun: true, CancellationToken.None);
        Assert.Contains(result2.Errors, e => e.Message.Contains("nhiều hơn một trưởng nhóm"));
    }

    [Fact]
    public async Task Team_exceeding_6_members_reports_error()
    {
        var repo = new InMemoryBaselineRepo();
        var fakeAcademic = new FakeAcademic();
        for (var i = 1; i <= 7; i++)
        {
            fakeAcademic.EnrollWithCode($"SE16000{i}", $"Sinh viên {i}");
        }

        var csv = "Tên nhóm,Dự án,Leader,MSSV\n" +
                  string.Join("\n", Enumerable.Range(1, 7).Select(i => $"Nhóm A,DA1,{(i == 1 ? "Leader" : "")},SE16000{i}"));

        var service = new BaselineImportService(repo, fakeAcademic, TimeProvider.System);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await service.ImportAsync(fakeAcademic.Semester.Id, stream, "test.csv", dryRun: true, CancellationToken.None);

        Assert.Contains(result.Errors, e => e.Message.Contains("vượt quá 6 thành viên"));
    }

    [Fact]
    public async Task Team_with_fewer_than_4_members_is_not_imported()
    {
        var repo = new InMemoryBaselineRepo();
        var academic = new FakeAcademic();
        for (var i = 1; i <= 3; i++)
        {
            academic.EnrollWithCode($"SE16000{i}", $"Sinh viên {i}");
        }

        var csv = "Tên nhóm,Dự án,Leader,MSSV thành viên\nNhóm A,DA1,SE160001,SE160002 SE160003";
        var service = new BaselineImportService(repo, academic, TimeProvider.System);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await service.ImportAsync(academic.Semester.Id, stream, "test.csv", dryRun: false, CancellationToken.None);

        Assert.Contains(result.Errors, e => e.Message.Contains("ít nhất 4 thành viên"));
        Assert.Empty(repo.SavedTeams);
    }

    [Fact]
    public async Task Student_calling_baseline_import_api_gets_403_forbidden()
    {
        var api = new TeamApiFactory();
        var client = api.ClientFor(Guid.NewGuid()); // Student role

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(Guid.NewGuid().ToString()), "semesterId");
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("header\nrow")), "file", "test.csv");

        var response = await client.PostAsync("/api/teams/baseline-import", content);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void Real_SU26_EXE101_Group_List_xlsx_can_be_parsed_successfully()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? docPath = null;
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "SU26_EXE101 _ Group List.xlsx");
            if (File.Exists(candidate))
            {
                docPath = candidate;
                break;
            }
            dir = dir.Parent;
        }

        Assert.NotNull(docPath);
        Assert.True(File.Exists(docPath), $"File {docPath} must exist for real test");

        using var fileStream = File.OpenRead(docPath);
        var errors = new List<BaselineImportRowError>();
        var records = BaselineImportFileParser.Parse(fileStream, "SU26_EXE101 _ Group List.xlsx", errors);

        Assert.Empty(errors);
        Assert.NotEmpty(records);
        var teams = records.GroupBy(r => r.TeamName).ToList();
        Assert.True(teams.Count >= 10, $"Expected at least 10 teams, got {teams.Count}");

        var teamsWithoutLeader = teams.Where(t => !t.Any(m => m.IsLeader)).Select(t => t.Key).ToList();
        Assert.True(teamsWithoutLeader.Count == 0, $"Teams without leader: {string.Join("; ", teamsWithoutLeader)}");
    }

    [Fact]
    public void Real_FA26_EXE201_Group_List_xlsx_can_be_parsed_successfully()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? docPath = null;
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "FA26_EXE201 _ Group List.xlsx");
            if (File.Exists(candidate))
            {
                docPath = candidate;
                break;
            }
            dir = dir.Parent;
        }

        Assert.NotNull(docPath);
        Assert.True(File.Exists(docPath), $"File {docPath} must exist for real test");

        using var fileStream = File.OpenRead(docPath);
        var errors = new List<BaselineImportRowError>();
        var records = BaselineImportFileParser.Parse(fileStream, "FA26_EXE201 _ Group List.xlsx", errors);

        Assert.Empty(errors);
        Assert.NotEmpty(records);
        var teams = records.GroupBy(r => r.TeamName).ToList();
        Assert.True(teams.Count >= 10, $"Expected at least 10 teams, got {teams.Count}");

        var teamsWithoutLeader = teams.Where(t => !t.Any(m => m.IsLeader)).Select(t => t.Key).ToList();
        Assert.True(teamsWithoutLeader.Count == 0, $"Teams without leader: {string.Join("; ", teamsWithoutLeader)}");
    }

    [Fact]
    public async Task AutoInherit_when_all_members_enrolled_creates_draft_team_and_links_project()
    {
        var repo = new InMemoryBaselineRepo();
        var fakeAcademic = new FakeAcademic();
        var s1 = fakeAcademic.EnrollWithCode("SE160001", "Leader Name");
        var s2 = fakeAcademic.EnrollWithCode("SE160002", "Member 2");
        var s3 = fakeAcademic.EnrollWithCode("SE160003", "Member 3");
        var s4 = fakeAcademic.EnrollWithCode("SE160004", "Member 4");

        var prevProjectId = Guid.NewGuid();
        var prevTeamId = Guid.NewGuid();
        repo.PreviousExe101Teams.Add(new Exe101TeamWithMembers(
            prevTeamId,
            "Nhóm Rồng Vàng",
            fakeAcademic.Semester.Id,
            prevProjectId,
            "EduPlatform",
            [
                new OldTeamMemberInfo(prevTeamId, s1.Id, "SE160001", "Leader Name", 1, MemberRoles.Leader),
                new OldTeamMemberInfo(prevTeamId, s2.Id, "SE160002", "Member 2", 1, MemberRoles.Member),
                new OldTeamMemberInfo(prevTeamId, s3.Id, "SE160003", "Member 3", 1, MemberRoles.Member),
                new OldTeamMemberInfo(prevTeamId, s4.Id, "SE160004", "Member 4", 1, MemberRoles.Member)
            ]
        ));

        var service = new BaselineImportService(repo, fakeAcademic, TimeProvider.System);
        var response = await service.AutoInheritAsync(new AutoInheritRequest(fakeAcademic.Semester.Id, DryRun: false), CancellationToken.None);

        Assert.Equal(1, response.InheritedTeamCount);
        Assert.Equal(4, response.InheritedStudentCount);
        Assert.Equal(0, response.FreeStudentCount);
        Assert.Equal(0, response.NeedsReviewTeamCount);

        Assert.Single(repo.SavedTeams);
        var team = repo.SavedTeams.Single();
        Assert.Equal("Nhóm Rồng Vàng", team.Name);
        Assert.Equal("DRAFT", team.Status);
        Assert.Equal(prevProjectId, team.ProjectId);

        var leaderMember = repo.SavedMembers.Single(m => m.TeamId == team.Id && m.Role == MemberRoles.Leader);
        Assert.Equal(s1.Id, leaderMember.StudentId);
        Assert.Equal(4, repo.SavedMembers.Count);
        Assert.Equal(4, repo.SavedBaselineMembers.Count);
    }

    [Fact]
    public async Task AutoInherit_when_fewer_than_4_members_enrolled_creates_needs_review_team()
    {
        var repo = new InMemoryBaselineRepo();
        var fakeAcademic = new FakeAcademic();
        var s1 = fakeAcademic.EnrollWithCode("SE160001", "Leader Name");
        var s2 = fakeAcademic.EnrollWithCode("SE160002", "Member 2");
        // s3 and s4 NOT enrolled in current semester

        var prevTeamId = Guid.NewGuid();
        repo.PreviousExe101Teams.Add(new Exe101TeamWithMembers(
            prevTeamId,
            "Nhóm Rồng Vàng",
            fakeAcademic.Semester.Id,
            null,
            null,
            [
                new OldTeamMemberInfo(prevTeamId, s1.Id, "SE160001", "Leader Name", 1, MemberRoles.Leader),
                new OldTeamMemberInfo(prevTeamId, s2.Id, "SE160002", "Member 2", 1, MemberRoles.Member),
                new OldTeamMemberInfo(prevTeamId, Guid.NewGuid(), "SE160003", "Member 3", 1, MemberRoles.Member),
                new OldTeamMemberInfo(prevTeamId, Guid.NewGuid(), "SE160004", "Member 4", 1, MemberRoles.Member)
            ]
        ));

        var service = new BaselineImportService(repo, fakeAcademic, TimeProvider.System);
        var response = await service.AutoInheritAsync(new AutoInheritRequest(fakeAcademic.Semester.Id, DryRun: false), CancellationToken.None);

        Assert.Equal(1, response.InheritedTeamCount);
        Assert.Equal(2, response.InheritedStudentCount);
        Assert.Equal(1, response.NeedsReviewTeamCount);

        Assert.Single(repo.SavedTeams);
        var team = repo.SavedTeams.Single();
        Assert.Equal("NEEDS_REVIEW", team.Status);
        Assert.Equal(2, repo.SavedMembers.Count);
        Assert.Single(response.Teams);
        Assert.Equal("NEEDS_REVIEW", response.Teams[0].Status);
        Assert.Equal(2, response.Teams[0].MissingOldMemberStudentCodes.Count);
    }

    [Fact]
    public async Task AutoInherit_when_old_leader_absent_promotes_first_member_to_leader()
    {
        var repo = new InMemoryBaselineRepo();
        var fakeAcademic = new FakeAcademic();
        // s1 (old leader) not enrolled
        var s2 = fakeAcademic.EnrollWithCode("SE160002", "Member 2");
        var s3 = fakeAcademic.EnrollWithCode("SE160003", "Member 3");
        var s4 = fakeAcademic.EnrollWithCode("SE160004", "Member 4");
        var s5 = fakeAcademic.EnrollWithCode("SE160005", "Member 5");

        var prevTeamId = Guid.NewGuid();
        repo.PreviousExe101Teams.Add(new Exe101TeamWithMembers(
            prevTeamId,
            "Nhóm Alpha",
            fakeAcademic.Semester.Id,
            null,
            null,
            [
                new OldTeamMemberInfo(prevTeamId, Guid.NewGuid(), "SE160001", "Old Leader", 1, MemberRoles.Leader),
                new OldTeamMemberInfo(prevTeamId, s2.Id, "SE160002", "Member 2", 1, MemberRoles.Member),
                new OldTeamMemberInfo(prevTeamId, s3.Id, "SE160003", "Member 3", 1, MemberRoles.Member),
                new OldTeamMemberInfo(prevTeamId, s4.Id, "SE160004", "Member 4", 1, MemberRoles.Member),
                new OldTeamMemberInfo(prevTeamId, s5.Id, "SE160005", "Member 5", 1, MemberRoles.Member)
            ]
        ));

        var service = new BaselineImportService(repo, fakeAcademic, TimeProvider.System);
        var response = await service.AutoInheritAsync(new AutoInheritRequest(fakeAcademic.Semester.Id, DryRun: false), CancellationToken.None);

        var team = repo.SavedTeams.Single();
        var leaderMember = repo.SavedMembers.Single(m => m.TeamId == team.Id && m.Role == MemberRoles.Leader);
        Assert.Equal(s2.Id, leaderMember.StudentId);
        Assert.Equal("SE160002", response.Teams[0].LeaderStudentCode);
    }

    [Fact]
    public async Task AutoInherit_dry_run_does_not_commit_to_db()
    {
        var repo = new InMemoryBaselineRepo();
        var fakeAcademic = new FakeAcademic();
        var s1 = fakeAcademic.EnrollWithCode("SE160001", "Leader");
        var s2 = fakeAcademic.EnrollWithCode("SE160002", "M2");
        var s3 = fakeAcademic.EnrollWithCode("SE160003", "M3");
        var s4 = fakeAcademic.EnrollWithCode("SE160004", "M4");

        var prevTeamId = Guid.NewGuid();
        repo.PreviousExe101Teams.Add(new Exe101TeamWithMembers(
            prevTeamId, "Nhóm 1", fakeAcademic.Semester.Id, null, null,
            [
                new OldTeamMemberInfo(prevTeamId, s1.Id, "SE160001", "Leader", 1, "Leader"),
                new OldTeamMemberInfo(prevTeamId, s2.Id, "SE160002", "M2", 1, "Member"),
                new OldTeamMemberInfo(prevTeamId, s3.Id, "SE160003", "M3", 1, "Member"),
                new OldTeamMemberInfo(prevTeamId, s4.Id, "SE160004", "M4", 1, "Member")
            ]
        ));

        var service = new BaselineImportService(repo, fakeAcademic, TimeProvider.System);
        var response = await service.AutoInheritAsync(new AutoInheritRequest(fakeAcademic.Semester.Id, DryRun: true), CancellationToken.None);

        Assert.Equal(1, response.InheritedTeamCount);
        Assert.Empty(repo.SavedTeams);
        Assert.Empty(repo.SavedMembers);
        Assert.Empty(repo.SavedBaselineMembers);
    }

    [Fact]
    public async Task AutoInherit_when_zero_members_enrolled_does_not_create_team()
    {
        var repo = new InMemoryBaselineRepo();
        var fakeAcademic = new FakeAcademic();
        // None of the members enrolled

        var prevTeamId = Guid.NewGuid();
        repo.PreviousExe101Teams.Add(new Exe101TeamWithMembers(
            prevTeamId, "Nhóm Cũ", fakeAcademic.Semester.Id, null, null,
            [
                new OldTeamMemberInfo(prevTeamId, Guid.NewGuid(), "SE160001", "M1", 1, "Leader"),
                new OldTeamMemberInfo(prevTeamId, Guid.NewGuid(), "SE160002", "M2", 1, "Member")
            ]
        ));

        var service = new BaselineImportService(repo, fakeAcademic, TimeProvider.System);
        var response = await service.AutoInheritAsync(new AutoInheritRequest(fakeAcademic.Semester.Id, DryRun: false), CancellationToken.None);

        Assert.Equal(0, response.InheritedTeamCount);
        Assert.Empty(repo.SavedTeams);
    }
}
