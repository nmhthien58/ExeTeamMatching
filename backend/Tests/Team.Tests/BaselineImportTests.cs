using System.IO.Compression;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Team.Repositories.BaselineImport;
using Team.Repositories.Members;
using Team.Repositories.Projects;
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
        public List<Project> SavedProjects { get; } = [];
        public List<ProjectVersion> SavedProjectVersions { get; } = [];
        public List<TeamDomain> SavedTeamDomains { get; } = [];

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

        public Task<IReadOnlyList<Exe101TeamWithMembers>> GetExe101TeamsBySemesterAsync(Guid semesterId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Exe101TeamWithMembers>>(PreviousExe101Teams.Where(t => t.SemesterId == semesterId).ToList());

        public Task SaveImportedTeamsAsync(
            IReadOnlyList<TeamEntity> teams,
            IReadOnlyList<TeamMember> members,
            IReadOnlyList<TeamBaselineMember> baselineMembers,
            IReadOnlyList<OutboxMessage> outboxMessages,
            IReadOnlyList<Project>? projects = null,
            IReadOnlyList<ProjectVersion>? projectVersions = null,
            IReadOnlyList<TeamDomain>? teamDomains = null,
            CancellationToken ct = default)
        {
            SavedTeams.AddRange(teams);
            SavedMembers.AddRange(members);
            SavedBaselineMembers.AddRange(baselineMembers);
            SavedOutboxMessages.AddRange(outboxMessages);
            if (projects is not null) SavedProjects.AddRange(projects);
            if (projectVersions is not null) SavedProjectVersions.AddRange(projectVersions);
            if (teamDomains is not null) SavedTeamDomains.AddRange(teamDomains);
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
        Assert.NotNull(savedTeam.ProjectId); // BE-92: Tạo đề tài mới từ file
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
        Assert.NotNull(savedTeam.ProjectId); // BE-92: Tạo đề tài mới từ file
        Assert.Null(savedTeam.SourceTeamId);
    }

    private static async Task<BaselineImportResponse> ImportCsvAsync(InMemoryBaselineRepo repo, FakeAcademic academic, string csv, bool dryRun = false)
    {
        var service = new BaselineImportService(repo, academic, TimeProvider.System);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return await service.ImportAsync(academic.Semester.Id, stream, "test.csv", dryRun, CancellationToken.None);
    }

    [Fact]
    public async Task Invalid_student_code_is_warning_and_team_keeps_valid_members()
    {
        var repo = new InMemoryBaselineRepo();
        var academic = new FakeAcademic();
        academic.EnrollWithCode("SE160002", "Sinh viên 2");

        var result = await ImportCsvAsync(repo, academic, "Tên nhóm,Dự án,Leader,MSSV thành viên\nNhóm Rồng Vàng,EduPlatform,INVALID_CODE,SE160002");

        Assert.Empty(result.Errors);
        Assert.Contains(result.Warnings!, w => w.Message.Contains("đúng định dạng"));
        var team = Assert.Single(repo.SavedTeams);
        Assert.Equal(TeamStatuses.Draft, team.Status);
        Assert.Equal("SE160002", repo.SavedMembers.Single(m => m.Role == "LEADER").StudentCode);
    }

    [Fact]
    public async Task Old_members_not_in_new_semester_are_listed_as_missing_not_errors()
    {
        var repo = new InMemoryBaselineRepo();
        var academic = new FakeAcademic();
        // SE160001 học EXE201 kỳ này; SE160002 không ghi danh; SE160003 không có trong hệ thống; SE160004 ghi danh EXE101.
        academic.EnrollWithCode("SE160001", "Sinh viên 1");
        academic.EnrollWithCode("SE160002", "Sinh viên 2", enrolled: false);
        academic.EnrollWithCode("SE160004", "Sinh viên 4", course: "EXE101");

        var result = await ImportCsvAsync(repo, academic, "Tên nhóm,Dự án,Leader,MSSV thành viên\nNhóm 1,DA1,SE160001,\"SE160002, SE160003, SE160004\"");

        Assert.Empty(result.Errors);
        var summary = Assert.Single(result.Teams!);
        Assert.Equal(1, summary.MemberCount);
        Assert.Equal(new[] { "SE160002", "SE160003", "SE160004" }, summary.MissingOldMemberStudentCodes);
        Assert.Equal(1, result.NeedsReviewTeamCount);
        Assert.Single(repo.SavedTeams);
    }

    [Fact]
    public async Task Team_with_no_member_in_new_semester_is_skipped()
    {
        var repo = new InMemoryBaselineRepo();
        var academic = new FakeAcademic();
        academic.EnrollWithCode("SE160001", "Sinh viên 1");

        var result = await ImportCsvAsync(repo, academic,
            "Tên nhóm,Dự án,Leader,MSSV thành viên\nNhóm A,DA1,SE160001,SE160002\nNhóm B,DA2,SE170001,SE170002 SE170003 SE170004");

        Assert.Empty(result.Errors);
        Assert.Equal(1, result.TeamCount);
        Assert.Equal(1, result.SkippedTeamCount);
        Assert.Equal("Nhóm A", Assert.Single(repo.SavedTeams).Name);
    }

    [Fact]
    public async Task Duplicate_student_in_different_teams_stays_in_first_team()
    {
        var repo = new InMemoryBaselineRepo();
        var academic = new FakeAcademic();
        academic.EnrollWithCode("SE160001", "Sinh viên 1");
        academic.EnrollWithCode("SE160002", "Sinh viên 2");
        academic.EnrollWithCode("SE160003", "Sinh viên 3");

        var result = await ImportCsvAsync(repo, academic,
            "Tên nhóm,Dự án,Leader,MSSV thành viên\nNhóm A,DA1,SE160001,SE160002\nNhóm B,DA2,SE160003,SE160002");

        Assert.Contains(result.Warnings!, w => w.Message.Contains("đã thuộc nhóm khác trong file"));
        Assert.Equal(2, repo.SavedTeams.Count);
        Assert.Equal(3, repo.SavedMembers.Count);
    }

    [Fact]
    public async Task Student_already_in_team_in_semester_is_not_inherited_again()
    {
        var repo = new InMemoryBaselineRepo();
        var academic = new FakeAcademic();
        var s1 = academic.EnrollWithCode("SE160001", "Sinh viên 1");
        academic.EnrollWithCode("SE160002", "Sinh viên 2");
        repo.ExistingMemberStudentIds.Add(s1.Id);

        var result = await ImportCsvAsync(repo, academic, "Tên nhóm,Dự án,Leader,MSSV thành viên\nNhóm A,DA1,SE160001,SE160002");

        var summary = Assert.Single(result.Teams!);
        Assert.Equal("SE160002", summary.LeaderStudentCode);
        Assert.Contains("SE160001", summary.MissingOldMemberStudentCodes);
    }

    [Fact]
    public async Task Existing_team_name_gets_exe201_suffix()
    {
        var repo = new InMemoryBaselineRepo();
        var academic = new FakeAcademic();
        academic.EnrollWithCode("SE160001", "Sinh viên 1");
        repo.ExistingTeamNames.Add("nhóm a");

        await ImportCsvAsync(repo, academic, "Tên nhóm,Dự án,Leader,MSSV thành viên\nNhóm A,DA1,SE160001,SE160002");

        Assert.Equal("Nhóm A (EXE201)", Assert.Single(repo.SavedTeams).Name);
    }

    [Fact]
    public async Task Old_leader_absent_promotes_first_remaining_member()
    {
        var repo = new InMemoryBaselineRepo();
        var academic = new FakeAcademic();
        var s2 = academic.EnrollWithCode("SE160002", "Sinh viên 2");
        academic.EnrollWithCode("SE160003", "Sinh viên 3");

        await ImportCsvAsync(repo, academic, "Tên nhóm,Dự án,Leader,MSSV thành viên\nNhóm A,DA1,SE160001,SE160002 SE160003");

        Assert.Equal(s2.Id, repo.SavedMembers.Single(m => m.Role == "LEADER").StudentId);
    }

    [Fact]
    public async Task Team_reaching_min_size_is_active_and_smaller_team_is_draft()
    {
        var repo = new InMemoryBaselineRepo();
        var academic = new FakeAcademic();
        for (var i = 1; i <= 6; i++) academic.EnrollWithCode($"SE16000{i}", $"Sinh viên {i}");

        await ImportCsvAsync(repo, academic,
            "Tên nhóm,Dự án,Leader,MSSV thành viên\nNhóm A,DA1,SE160001,SE160002 SE160003 SE160004\nNhóm B,DA2,SE160005,SE160006");

        Assert.Equal(TeamStatuses.Active, repo.SavedTeams.Single(t => t.Name == "Nhóm A").Status);
        Assert.Equal(TeamStatuses.Draft, repo.SavedTeams.Single(t => t.Name == "Nhóm B").Status);
    }

    [Fact]
    public async Task Real_SU26_group_list_reconciles_with_few_FA26_students()
    {
        // File thật có SĐT sinh viên nên không commit; máy không có file thì bỏ qua.
        var docPath = FindDoc("SU26_EXE101 _ Group List.xlsx");
        if (docPath is null) return;
        var repo = new InMemoryBaselineRepo();
        var academic = new FakeAcademic();
        // Danh sách lớp FA26 EXE201 của nhóm demo: chỉ 6 sinh viên.
        academic.EnrollWithCode("SE172145", "Nguyễn Mai Hào Thiên");
        academic.EnrollWithCode("SE190507", "Ngô Chí Nam");
        academic.EnrollWithCode("SE193559", "Đoàn Nguyễn Trung Nguyên");
        academic.EnrollWithCode("SE193445", "Ngô Tấn Thành");
        academic.EnrollWithCode("SE193416", "Nguyễn Ngọc Bảo Cường");

        var service = new BaselineImportService(repo, academic, TimeProvider.System);
        await using var stream = File.OpenRead(docPath);
        var result = await service.ImportAsync(academic.Semester.Id, stream, Path.GetFileName(docPath), dryRun: false, CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.TeamCount);
        Assert.Equal(5, result.StudentCount);
        var dailyMate = result.Teams!.Single(t => t.TeamName == "DailyMate");
        Assert.Equal("SE172145", dailyMate.LeaderStudentCode);
        Assert.Equal(2, dailyMate.MemberCount);
        Assert.Equal("Daily Mate", dailyMate.ProjectName);
        Assert.NotNull(dailyMate.DomainCodes);
        Assert.Contains("D01", dailyMate.DomainCodes);
        Assert.Contains("D04", dailyMate.DomainCodes);

        var dailyMateTeam = repo.SavedTeams.Single(t => t.Name == "DailyMate");
        Assert.NotNull(dailyMateTeam.ProjectId);
        var dailyMateProject = repo.SavedProjects.Single(p => p.Id == dailyMateTeam.ProjectId);
        Assert.Equal("Daily Mate", dailyMateProject.Name);
        Assert.Equal("EXE101", dailyMateProject.OriginCourseCode);

        var dailyMateDomains = repo.SavedTeamDomains.Where(d => d.TeamId == dailyMateTeam.Id).ToList();
        Assert.Equal(2, dailyMateDomains.Count);
        Assert.Equal("D01", dailyMateDomains.Single(d => d.IsPrimary).DomainCode);
        Assert.Equal("D04", dailyMateDomains.Single(d => !d.IsPrimary).DomainCode);

        var sleepy = result.Teams!.Single(t => t.TeamName == "Sleepy Heads Studio");
        Assert.Equal("SE193559", sleepy.LeaderStudentCode);
        Assert.Equal(3, sleepy.MemberCount);
        Assert.All(repo.SavedTeams, t => Assert.Equal(TeamStatuses.Draft, t.Status));
    }

    [Fact]
    public async Task Baseline_import_without_exe101_creates_project_project_version_team_domain_and_project_created_event()
    {
        var repo = new InMemoryBaselineRepo();
        var academic = new FakeAcademic();
        var s1 = academic.EnrollWithCode("SE160001", "Sinh viên 1");
        var s2 = academic.EnrollWithCode("SE160002", "Sinh viên 2");
        var s3 = academic.EnrollWithCode("SE160003", "Sinh viên 3");
        var s4 = academic.EnrollWithCode("SE160004", "Sinh viên 4");

        var csv = "Tên nhóm,Tên dự án,Mô tả ý tưởng,Domain,Leader,MSSV thành viên\n" +
                  "Nhóm Phoenix,Phoenix App,Nền tảng hỗ trợ AI,\"AI & Data Innovation, Software / SaaS / Platform\",SE160001,\"SE160002, SE160003, SE160004\"";

        var service = new BaselineImportService(repo, academic, TimeProvider.System);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await service.ImportAsync(academic.Semester.Id, stream, "test.csv", dryRun: false, CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.Equal(1, result.TeamCount);
        var summary = result.Teams!.Single();
        Assert.Equal("Phoenix App", summary.ProjectName);
        Assert.NotNull(summary.DomainCodes);
        Assert.Equal(["D01", "D04"], summary.DomainCodes);

        // Kiểm tra Project
        Assert.Single(repo.SavedProjects);
        var project = repo.SavedProjects.Single();
        Assert.Equal("Phoenix App", project.Name);
        Assert.Equal("Nền tảng hỗ trợ AI", project.Description);
        Assert.Equal(academic.Semester.Id, project.OriginSemesterId);
        Assert.Equal("EXE101", project.OriginCourseCode);
        Assert.Equal((short)1, project.CurrentVersion);
        Assert.Equal("ACTIVE", project.Status);

        // Kiểm tra ProjectVersion
        Assert.Single(repo.SavedProjectVersions);
        var version = repo.SavedProjectVersions.Single();
        Assert.Equal(project.Id, version.ProjectId);
        Assert.Equal((short)1, version.Version);
        Assert.Equal("Phoenix App", version.Name);
        Assert.Equal("CREATED", version.ChangeType);
        Assert.Equal("EXE201", version.CourseCode);
        Assert.Null(version.ChangedByAccountId);

        // Kiểm tra Team và gắn ProjectId
        Assert.Single(repo.SavedTeams);
        var team = repo.SavedTeams.Single();
        Assert.Equal(project.Id, team.ProjectId);

        // Kiểm tra TeamDomains
        Assert.Equal(2, repo.SavedTeamDomains.Count);
        var primary = repo.SavedTeamDomains.Single(d => d.IsPrimary);
        Assert.Equal("D01", primary.DomainCode);
        Assert.Equal(1, primary.DomainId);
        var secondary = repo.SavedTeamDomains.Single(d => !d.IsPrimary);
        Assert.Equal("D04", secondary.DomainCode);
        Assert.Equal(4, secondary.DomainId);

        // Kiểm tra Outbox events: TeamCreated -> ProjectCreated -> MemberJoined
        Assert.True(repo.SavedOutboxMessages.Count >= 5);
        Assert.Equal(TeamEventTypes.TeamCreated, repo.SavedOutboxMessages[0].EventType);
        Assert.Equal(TeamEventTypes.ProjectCreated, repo.SavedOutboxMessages[1].EventType);

        var projectCreated = JsonSerializer.Deserialize<ProjectCreated>(repo.SavedOutboxMessages[1].Payload, EventEnvelope.JsonOptions);
        Assert.NotNull(projectCreated);
        Assert.Equal(project.Id, projectCreated.ProjectId);
        Assert.Equal(team.Id, projectCreated.TeamId);
        Assert.Equal("Phoenix App", projectCreated.Name);
        Assert.Equal([1, 4], projectCreated.DomainIds);
        Assert.Equal("EXE201", projectCreated.CourseCode);

        for (var i = 2; i < 5; i++)
        {
            Assert.Equal(MemberEventTypes.MemberJoined, repo.SavedOutboxMessages[i].EventType);
        }
    }

    [Fact]
    public async Task Baseline_import_unmatched_domain_generates_warning_and_does_not_block()
    {
        var repo = new InMemoryBaselineRepo();
        var academic = new FakeAcademic();
        academic.EnrollWithCode("SE160001", "Sinh viên 1");
        academic.EnrollWithCode("SE160002", "Sinh viên 2");
        academic.EnrollWithCode("SE160003", "Sinh viên 3");
        academic.EnrollWithCode("SE160004", "Sinh viên 4");

        var csv = "Tên nhóm,Tên dự án,Domain,Leader,MSSV thành viên\n" +
                  "Nhóm Test,Test App,\"AI & Data Innovation, Không contact được nhóm, Lĩnh vực giả 999\",SE160001,\"SE160002, SE160003, SE160004\"";

        var service = new BaselineImportService(repo, academic, TimeProvider.System);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await service.ImportAsync(academic.Semester.Id, stream, "test.csv", dryRun: false, CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.NotNull(result.Warnings);
        Assert.Contains(result.Warnings, w => w.Message.Contains("Không contact được nhóm"));
        Assert.Contains(result.Warnings, w => w.Message.Contains("Lĩnh vực giả 999"));

        // Vẫn tạo nhóm, đề tài và domain hợp lệ (D01)
        Assert.Single(repo.SavedTeams);
        Assert.Single(repo.SavedProjects);
        var domains = repo.SavedTeamDomains;
        Assert.Single(domains);
        Assert.Equal("D01", domains.Single().DomainCode);
        Assert.True(domains.Single().IsPrimary);
    }

    [Fact]
    public async Task Baseline_import_with_existing_exe101_team_does_not_create_new_project()
    {
        var repo = new InMemoryBaselineRepo();
        var academic = new FakeAcademic();
        academic.EnrollWithCode("SE160001", "Sinh viên 1");
        academic.EnrollWithCode("SE160002", "Sinh viên 2");
        academic.EnrollWithCode("SE160003", "Sinh viên 3");
        academic.EnrollWithCode("SE160004", "Sinh viên 4");

        var existingProjectId = Guid.NewGuid();
        var existingTeamId = Guid.NewGuid();
        repo.Exe101Teams["Nhóm Rồng Vàng"] = new TeamEntity
        {
            Id = existingTeamId,
            Name = "Nhóm Rồng Vàng",
            CourseCode = "EXE101",
            ProjectId = existingProjectId
        };

        var csv = "Tên nhóm,Tên dự án,Domain,Leader,MSSV thành viên\n" +
                  "Nhóm Rồng Vàng,New Title,AI & Data Innovation,SE160001,\"SE160002, SE160003, SE160004\"";

        var service = new BaselineImportService(repo, academic, TimeProvider.System);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await service.ImportAsync(academic.Semester.Id, stream, "test.csv", dryRun: false, CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.Empty(repo.SavedProjects); // Không tạo project mới
        Assert.Empty(repo.SavedProjectVersions);

        var team = repo.SavedTeams.Single();
        Assert.Equal(existingProjectId, team.ProjectId);
        Assert.Equal(existingTeamId, team.SourceTeamId);

        // Outbox phát TeamLinkedToProject, không phát ProjectCreated
        Assert.Contains(repo.SavedOutboxMessages, m => m.EventType == TeamEventTypes.TeamLinkedToProject);
        Assert.DoesNotContain(repo.SavedOutboxMessages, m => m.EventType == TeamEventTypes.ProjectCreated);
    }

    private static string? FindDoc(string fileName)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "docs", fileName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
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
    public async Task AutoInherit_when_all_members_enrolled_creates_active_team_and_links_project()
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
            fakeAcademic.PrevSemester.Id,
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
        Assert.Equal(TeamStatuses.Active, team.Status);
        Assert.Equal(prevProjectId, team.ProjectId);

        var leaderMember = repo.SavedMembers.Single(m => m.TeamId == team.Id && m.Role == MemberRoles.Leader);
        Assert.Equal(s1.Id, leaderMember.StudentId);
        Assert.Equal(4, repo.SavedMembers.Count);
        Assert.Equal(4, repo.SavedBaselineMembers.Count);
    }

    [Fact]
    public async Task AutoInherit_when_fewer_than_4_members_enrolled_creates_draft_team()
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
            fakeAcademic.PrevSemester.Id,
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
        Assert.Equal(TeamStatuses.Draft, team.Status);
        Assert.Equal(2, repo.SavedMembers.Count);
        Assert.Single(response.Teams);
        Assert.Equal(TeamStatuses.Draft, response.Teams[0].Status);
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
            fakeAcademic.PrevSemester.Id,
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
            prevTeamId, "Nhóm 1", fakeAcademic.PrevSemester.Id, null, null,
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
            prevTeamId, "Nhóm Cũ", fakeAcademic.PrevSemester.Id, null, null,
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

    [Fact]
    public async Task AutoInherit_picks_chronologically_previous_semester_even_if_multiple_semesters_exist_out_of_order()
    {
        var repo = new InMemoryBaselineRepo();
        var fakeAcademic = new FakeAcademic();

        var semOldest = new SemesterDto(Guid.NewGuid(), "SP26", "Spring 2026", new DateOnly(2026, 1, 15));
        var semImmediatePrev = new SemesterDto(Guid.NewGuid(), "SU26", "Summer 2026", new DateOnly(2026, 5, 10));
        var semCurrent = new SemesterDto(Guid.NewGuid(), "FA26", "Fall 2026", new DateOnly(2026, 9, 1));
        var semFuture = new SemesterDto(Guid.NewGuid(), "SP27", "Spring 2027", new DateOnly(2027, 1, 15));

        // Insert in random/reversed order to test sorting
        fakeAcademic.Semester = semCurrent;
        fakeAcademic.Semesters.AddRange([semFuture, semOldest, semCurrent, semImmediatePrev]);

        var s1 = fakeAcademic.EnrollWithCode("SE160001", "Leader 1");
        var s2 = fakeAcademic.EnrollWithCode("SE160002", "Member 2");
        var s3 = fakeAcademic.EnrollWithCode("SE160003", "Member 3");
        var s4 = fakeAcademic.EnrollWithCode("SE160004", "Member 4");

        // Team in oldest semester
        var teamOldId = Guid.NewGuid();
        repo.PreviousExe101Teams.Add(new Exe101TeamWithMembers(
            teamOldId, "Oldest Team", semOldest.Id, null, null,
            [
                new OldTeamMemberInfo(teamOldId, s1.Id, "SE160001", "Leader 1", 1, "Leader")
            ]
        ));

        // Team in immediately previous semester (SU26)
        var teamImmediateId = Guid.NewGuid();
        repo.PreviousExe101Teams.Add(new Exe101TeamWithMembers(
            teamImmediateId, "Immediate Prev Team", semImmediatePrev.Id, null, null,
            [
                new OldTeamMemberInfo(teamImmediateId, s1.Id, "SE160001", "Leader 1", 1, MemberRoles.Leader),
                new OldTeamMemberInfo(teamImmediateId, s2.Id, "SE160002", "Member 2", 1, MemberRoles.Member),
                new OldTeamMemberInfo(teamImmediateId, s3.Id, "SE160003", "Member 3", 1, MemberRoles.Member),
                new OldTeamMemberInfo(teamImmediateId, s4.Id, "SE160004", "Member 4", 1, MemberRoles.Member)
            ]
        ));

        var service = new BaselineImportService(repo, fakeAcademic, TimeProvider.System);
        var response = await service.AutoInheritAsync(new AutoInheritRequest(semCurrent.Id, DryRun: false), CancellationToken.None);

        Assert.Equal(1, response.InheritedTeamCount);
        Assert.Single(repo.SavedTeams);
        Assert.Equal("Immediate Prev Team", repo.SavedTeams.Single().Name);
    }
}
