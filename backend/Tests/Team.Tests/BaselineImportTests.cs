using System.IO.Compression;
using System.Text;
using System.Text.Json;
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

        public List<TeamEntity> SavedTeams { get; } = [];
        public List<TeamMember> SavedMembers { get; } = [];
        public List<TeamBaselineMember> SavedBaselineMembers { get; } = [];
        public List<OutboxMessage> SavedOutboxMessages { get; } = [];

        public Task<HashSet<Guid>> GetStudentsAlreadyInTeamAsync(Guid semesterId, IEnumerable<Guid> studentIds, CancellationToken ct) =>
            Task.FromResult(studentIds.Where(ExistingMemberStudentIds.Contains).ToHashSet());

        public Task<HashSet<string>> GetExistingTeamNamesAsync(Guid semesterId, string courseCode, IEnumerable<string> teamNames, CancellationToken ct) =>
            Task.FromResult(teamNames.Where(n => ExistingTeamNames.Contains(n.ToLower())).Select(n => n.ToLower()).ToHashSet());

        public Task<Dictionary<string, TeamEntity>> GetExe101TeamsByNamesAsync(IEnumerable<string> teamNames, CancellationToken ct)
        {
            var res = new Dictionary<string, TeamEntity>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in teamNames)
            {
                if (Exe101Teams.TryGetValue(name, out var t)) res[name] = t;
            }
            return Task.FromResult(res);
        }

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
        var joined = repo.SavedOutboxMessages.Where(m => m.EventType == MemberEventTypes.MemberJoined)
            .Select(m => JsonSerializer.Deserialize<MemberJoined>(m.Payload)!).ToList();
        Assert.Equal(3, joined.Count);
        Assert.Equal(new[] { s2.Id, s3.Id, s4.Id }, joined.Select(e => e.StudentId));
        Assert.Equal(new[] { 2, 3, 4 }, joined.Select(e => e.MemberCount));
        Assert.All(joined, e => Assert.Equal("INHERITED", e.JoinSource));
        Assert.Equal(TeamEventTypes.TeamLinkedToProject, repo.SavedOutboxMessages[^1].EventType);
        Assert.True(repo.SavedOutboxMessages.Zip(repo.SavedOutboxMessages.Skip(1))
            .All(pair => pair.First.OccurredAt < pair.Second.OccurredAt));
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
}
