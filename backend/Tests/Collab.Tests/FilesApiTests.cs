using System.Net;
using System.Net.Http.Json;
using System.Text;
using BuildingBlocks.Errors;
using Collab.Services.Files;

namespace Collab.Tests;

/// <summary>BE-65 – file dự án (rule 24); BE-77 – kho EXE101 chỉ đọc (rule 26).</summary>
public sealed class FilesApiTests : IClassFixture<CollabApiFactory>
{
    private readonly CollabApiFactory _factory;
    private readonly TestPerson _leader = TestPerson.Leader();
    private readonly TestPerson _member = TestPerson.Member();
    private readonly TestPerson _instructor = TestPerson.Instructor();
    private Guid _workspace;

    public FilesApiTests(CollabApiFactory factory) => _factory = factory;

    private async Task<Guid> WorkspaceAsync(Guid? previous = null) =>
        _workspace = await _factory.SeedWorkspaceAsync("Nhóm Files", "EXE201", previous, _leader, _member, _instructor);

    private static MultipartFormDataContent Upload(string fileName, string content = "nội dung", Guid? folderId = null)
    {
        var form = new MultipartFormDataContent { { new ByteArrayContent(Encoding.UTF8.GetBytes(content)), "file", fileName } };
        if (folderId is { } id)
        {
            form.Add(new StringContent(id.ToString()), "folderId");
        }
        return form;
    }

    [Fact]
    public async Task Same_name_upload_creates_new_version_and_keeps_history()
    {
        await WorkspaceAsync();
        var client = _factory.ClientFor(_member);

        var first = await client.PostAsync($"/api/workspaces/{_workspace}/files", Upload("Bao-cao.pdf", "v1"));
        var second = await client.PostAsync($"/api/workspaces/{_workspace}/files", Upload("bao-cao.PDF", "version 2"));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var upload = await CollabApiFactory.ReadAsync<UploadFileResponse>(second);
        Assert.True(upload.CreatedNewVersion);
        Assert.Equal(2, upload.File.CurrentVersion);
        Assert.Equal("application/pdf", upload.File.ContentType);

        var detail = await CollabApiFactory.ReadAsync<ProjectFileDetailResponse>(await client.GetAsync($"/api/files/{upload.File.Id}"));
        Assert.Equal([2, 1], detail.Versions.Select(v => v.Version));
        Assert.Equal(2, _factory.Storage.Objects.Keys.Count(k => k.Contains(upload.File.Id.ToString())));

        var url = await CollabApiFactory.ReadAsync<DownloadUrlResponse>(await client.GetAsync($"/api/files/{upload.File.Id}/download-url?version=1"));
        Assert.EndsWith($"/{upload.File.Id}/1?signed=1", url.Url);
    }

    [Fact]
    public async Task Wrong_type_is_415_and_advisor_cannot_upload_but_can_comment()
    {
        await WorkspaceAsync();
        var member = _factory.ClientFor(_member);
        var instructor = _factory.ClientFor(_instructor);

        var exe = await member.PostAsync($"/api/workspaces/{_workspace}/files", Upload("virus.exe"));
        var byInstructor = await instructor.PostAsync($"/api/workspaces/{_workspace}/files", Upload("slide.pptx"));
        var file = await CollabApiFactory.ReadAsync<UploadFileResponse>(
            await member.PostAsync($"/api/workspaces/{_workspace}/files", Upload("slide.pptx")));
        var comment = await instructor.PostAsJsonAsync($"/api/files/{file.File.Id}/comments", new { body = "Slide 3 thiếu số liệu", version = 1 });

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, exe.StatusCode);
        Assert.Equal("file-type-not-allowed", await CollabApiFactory.ErrorCodeAsync(exe));
        Assert.Equal(HttpStatusCode.Forbidden, byInstructor.StatusCode);
        Assert.Equal(HttpStatusCode.Created, comment.StatusCode);
        var saved = await CollabApiFactory.ReadAsync<CommentResponse>(comment);
        Assert.Equal("INSTRUCTOR", saved.Author.Role);
        Assert.Equal(1, saved.Version);
    }

    [Fact]
    public async Task Admin_is_read_only()
    {
        await WorkspaceAsync();
        var admin = _factory.ClientFor(TestPerson.Admin());

        var list = await admin.GetAsync($"/api/workspaces/{_workspace}/files");
        var upload = await admin.PostAsync($"/api/workspaces/{_workspace}/files", Upload("a.pdf"));

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.False((await CollabApiFactory.ReadAsync<FolderContentResponse>(list)).CanUpload);
        Assert.Equal("workspace-read-only", await CollabApiFactory.ErrorCodeAsync(upload));
    }

    [Fact]
    public async Task Only_uploader_or_leader_deletes()
    {
        await WorkspaceAsync();
        var file = await CollabApiFactory.ReadAsync<UploadFileResponse>(
            await _factory.ClientFor(_leader).PostAsync($"/api/workspaces/{_workspace}/files", Upload("ke-hoach.xlsx")));
        var mine = await CollabApiFactory.ReadAsync<UploadFileResponse>(
            await _factory.ClientFor(_member).PostAsync($"/api/workspaces/{_workspace}/files", Upload("ghi-chu.docx")));

        var memberDeletesLeaderFile = await _factory.ClientFor(_member).DeleteAsync($"/api/files/{file.File.Id}");
        var leaderDeletesMemberFile = await _factory.ClientFor(_leader).DeleteAsync($"/api/files/{mine.File.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, memberDeletesLeaderFile.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, leaderDeletesMemberFile.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.ClientFor(_member).GetAsync($"/api/files/{mine.File.Id}")).StatusCode);
    }

    [Fact]
    public async Task Folders_hold_files_and_duplicate_name_conflicts()
    {
        await WorkspaceAsync();
        var client = _factory.ClientFor(_member);

        var folder = await CollabApiFactory.ReadAsync<FolderResponse>(
            await client.PostAsJsonAsync($"/api/workspaces/{_workspace}/folders", new { name = "Báo cáo" }));
        var duplicate = await client.PostAsJsonAsync($"/api/workspaces/{_workspace}/folders", new { name = "báo cáo" });
        await client.PostAsync($"/api/workspaces/{_workspace}/files", Upload("tuan-1.pdf", folderId: folder.Id));

        var root = await CollabApiFactory.ReadAsync<FolderContentResponse>(await client.GetAsync($"/api/workspaces/{_workspace}/files"));
        var inside = await CollabApiFactory.ReadAsync<FolderContentResponse>(
            await client.GetAsync($"/api/workspaces/{_workspace}/files?folderId={folder.Id}"));

        Assert.Equal("folder-exists", await CollabApiFactory.ErrorCodeAsync(duplicate));
        Assert.Equal(1, Assert.Single(root.Folders).ItemCount);
        Assert.Equal(["tuan-1.pdf"], inside.Files.Select(f => f.Name));
        Assert.Equal([folder.Id], inside.Breadcrumb.Select(b => b.Id));
    }

    [Fact]
    public async Task Exe201_team_reads_exe101_archive_but_cannot_write_it()
    {
        var oldLeader = TestPerson.Leader("Trưởng nhóm EXE101");
        var exe101 = await _factory.SeedWorkspaceAsync("Nhóm EXE101", "EXE101", null, oldLeader);
        var oldFile = await CollabApiFactory.ReadAsync<UploadFileResponse>(
            await _factory.ClientFor(oldLeader).PostAsync($"/api/workspaces/{exe101}/files", Upload("pitch.pdf")));
        await WorkspaceAsync(previous: exe101);
        var client = _factory.ClientFor(_member);

        var root = await CollabApiFactory.ReadAsync<FolderContentResponse>(await client.GetAsync($"/api/workspaces/{_workspace}/files"));
        var archive = await CollabApiFactory.ReadAsync<FolderContentResponse>(
            await client.GetAsync($"/api/workspaces/{_workspace}/files?folderId={exe101}"));
        var detail = await client.GetAsync($"/api/files/{oldFile.File.Id}");
        var upload = await client.PostAsync($"/api/workspaces/{_workspace}/files", Upload("moi.pdf", folderId: exe101));
        var comment = await client.PostAsJsonAsync($"/api/files/{oldFile.File.Id}/comments", new { body = "Xem lại" });

        var pinned = root.Folders[0];
        Assert.Equal(FileService.ArchiveFolderName, pinned.Name);
        Assert.True(pinned.IsArchive);
        Assert.Equal(1, pinned.ItemCount);
        Assert.False(archive.CanUpload);
        Assert.True(Assert.Single(archive.Files).IsArchive);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal("archive-read-only", await CollabApiFactory.ErrorCodeAsync(upload));
        Assert.Equal("archive-read-only", await CollabApiFactory.ErrorCodeAsync(comment));
    }

    [Fact]
    public async Task Outsider_cannot_read_a_file()
    {
        await WorkspaceAsync();
        var file = await CollabApiFactory.ReadAsync<UploadFileResponse>(
            await _factory.ClientFor(_member).PostAsync($"/api/workspaces/{_workspace}/files", Upload("a.png")));

        var response = await _factory.ClientFor(TestPerson.Member("Người ngoài")).GetAsync($"/api/files/{file.File.Id}");

        Assert.Equal("not-workspace-member", await CollabApiFactory.ErrorCodeAsync(response));
    }

    [Theory]
    [InlineData("a.jpeg", "image/jpeg")]
    [InlineData("C:\\fakepath\\Bao cao.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public void Rules_normalize_name_and_map_content_type(string name, string contentType)
    {
        var (normalized, type) = FileRules.Validate(name, 10);

        Assert.DoesNotContain('\\', normalized);
        Assert.Equal(contentType, type);
    }

    [Fact]
    public void Rules_reject_files_over_25_mb()
    {
        var error = Assert.Throws<HttpStatusException>(() => FileRules.Validate("big.zip", FileRules.MaxSizeBytes + 1));

        Assert.Equal(413, error.Status);
        Assert.Equal("file-too-large", error.Code);
    }
}
