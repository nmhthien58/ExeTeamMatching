using System.Security.Claims;
using BuildingBlocks.Errors;
using Collab.Repositories.Files;
using Collab.Repositories.Workspaces;
using Collab.Services.Storage;
using Collab.Services.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Collab.Services.Files;

public interface IFileService
{
    /// <summary>C06 – nội dung một thư mục; thư mục gốc có thêm "EXE101 (kỳ trước)" ghim ở đầu nếu có (BE-77).</summary>
    Task<FolderContentResponse> GetFolderContentAsync(Guid workspaceId, Guid? folderId, WorkspaceAccess access, CancellationToken ct);

    Task<FolderResponse> CreateFolderAsync(Guid workspaceId, CreateFolderRequest request, WorkspaceAccess access, CancellationToken ct);

    /// <summary>Thành viên tải lên; trùng tên trong thư mục → phiên bản mới (rule 24).</summary>
    Task<UploadFileResponse> UploadAsync(Guid workspaceId, UploadFileCommand command, WorkspaceAccess access, CancellationToken ct);

    Task<ProjectFileDetailResponse> GetDetailAsync(Guid fileId, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>URL ký sẵn của một phiên bản (mặc định bản mới nhất).</summary>
    Task<DownloadUrlResponse> GetDownloadUrlAsync(Guid fileId, int? version, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Xóa mềm – người tải lên hoặc trưởng nhóm.</summary>
    Task DeleteAsync(Guid fileId, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Thành viên, giảng viên hướng dẫn, mentor bình luận (Admin chỉ xem).</summary>
    Task<CommentResponse> AddCommentAsync(Guid fileId, AddCommentRequest request, ClaimsPrincipal user, CancellationToken ct);
}

/// <summary>Payload <c>FileUploaded</c> (events.md) – chỉ phát khi file đã từng nộp bài có phiên bản mới.</summary>
public sealed record FileUploaded(Guid WorkspaceId, Guid FileId, int Version, string Name, Guid UploadedByStudentId);

/// <summary>Module Files của Collaboration (BE-65, BE-77).</summary>
public sealed class FileService(
    IFileRepository files,
    IWorkspaceRepository workspaces,
    IWorkspaceLookup lookup,
    IWorkspaceReader reader,
    IObjectStorage storage,
    IOptions<ObjectStorageOptions> storageOptions,
    TimeProvider clock,
    ILogger<FileService> logger) : IFileService
{
    public const string ArchiveFolderName = "EXE101 (kỳ trước)";

    public async Task<FolderContentResponse> GetFolderContentAsync(Guid workspaceId, Guid? folderId, WorkspaceAccess viewer, CancellationToken ct)
    {
        var workspace = await workspaces.GetByIdAsync(workspaceId, ct) ?? throw new NotFoundException("workspace", workspaceId);
        var location = await LocateAsync(workspace, folderId, ct);

        var folders = await files.ListFoldersAsync(location.WorkspaceId, location.Folder?.Id, ct);
        var counts = await files.CountItemsAsync(folders.Select(f => f.Id).ToList(), ct);
        var folderRows = folders.Select(f => ToResponse(f, counts[f.Id], location.IsArchive)).ToList();
        if (folderId is null && workspace.PreviousWorkspaceId is { } archiveId)
        {
            folderRows.Insert(0, await ArchiveRootAsync(archiveId, ct));
        }

        var fileRows = (await files.ListFilesAsync(location.WorkspaceId, location.Folder?.Id, ct))
            .Select(f => ToResponse(f, location.IsArchive)).ToList();

        return new FolderContentResponse(
            location.Current,
            await BreadcrumbAsync(location, ct),
            folderRows,
            fileRows,
            CanUpload: viewer.IsStudent && !location.IsArchive);
    }

    public async Task<FolderResponse> CreateFolderAsync(Guid workspaceId, CreateFolderRequest request, WorkspaceAccess writer, CancellationToken ct)
    {
        RequireStudent(writer, "Chỉ thành viên nhóm được tạo thư mục");
        var workspace = await workspaces.GetByIdAsync(workspaceId, ct) ?? throw new NotFoundException("workspace", workspaceId);
        var location = await LocateAsync(workspace, request.ParentId, ct);
        if (location.IsArchive)
        {
            throw ArchiveErrors.ReadOnly();
        }

        var name = request.Name.Trim();
        if (await files.FolderNameExistsAsync(workspaceId, request.ParentId, name, ct))
        {
            throw new ConflictException("folder-exists", "Thư mục đã tồn tại", $"Đã có thư mục \"{name}\" ở đây.");
        }

        var folder = new Folder { Id = Guid.NewGuid(), WorkspaceId = workspaceId, ParentId = request.ParentId, Name = name, CreatedAt = clock.GetUtcNow() };
        files.AddFolder(folder);
        await SaveAsync(() => new ConflictException("folder-exists", "Thư mục đã tồn tại", $"Đã có thư mục \"{name}\" ở đây."), ct);
        return ToResponse(folder, 0, isArchive: false);
    }

    public async Task<UploadFileResponse> UploadAsync(Guid workspaceId, UploadFileCommand command, WorkspaceAccess writer, CancellationToken ct)
    {
        RequireStudent(writer, "Chỉ thành viên nhóm được tải file lên");
        var (name, contentType) = FileRules.Validate(command.FileName, command.SizeBytes);
        var workspace = await workspaces.GetByIdAsync(workspaceId, ct) ?? throw new NotFoundException("workspace", workspaceId);
        if ((await LocateAsync(workspace, command.FolderId, ct)).IsArchive)
        {
            throw ArchiveErrors.ReadOnly();
        }

        var now = clock.GetUtcNow();
        var file = await files.FindByNameAsync(workspaceId, command.FolderId, name, ct);
        var isNewVersion = file is not null;
        if (file is null)
        {
            file = new ProjectFile
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                FolderId = command.FolderId,
                Name = name,
                CurrentVersion = 1,
                CreatedByType = PersonTypes.Student,
                CreatedById = writer.PersonId,
                CreatedAt = now,
                UpdatedAt = now,
            };
            files.AddFile(file);
        }
        else
        {
            file.CurrentVersion++;
            file.UpdatedAt = now;
        }

        var uploaderName = new WorkspacePeople(await lookup.ListMembersAsync(workspaceId, ct)).NameOf(PersonTypes.Student, writer.PersonId);
        var version = new FileVersion
        {
            Id = Guid.NewGuid(),
            FileId = file.Id,
            Version = file.CurrentVersion,
            ObjectKey = FileKey.For(workspaceId, file.Id, file.CurrentVersion),
            SizeBytes = command.SizeBytes,
            ContentType = contentType,
            UploadedById = writer.PersonId,
            UploadedByName = uploaderName,
            CreatedAt = now,
        };
        files.AddVersion(version);
        if (isNewVersion && await files.HasSubmissionAsync(file.Id, ct))
        {
            files.AddEvent(nameof(FileUploaded), workspaceId, new FileUploaded(workspaceId, file.Id, version.Version, file.Name, writer.PersonId));
        }

        await storage.PutAsync(version.ObjectKey, command.Content, contentType, file.Name, ct);
        try
        {
            await SaveAsync(() => new ConflictException("file-conflict", "File vừa được cập nhật",
                "Có người vừa tải lên cùng file này. Vui lòng thử lại."), ct);
        }
        catch
        {
            await DeleteObjectQuietlyAsync(version.ObjectKey);
            throw;
        }

        return new UploadFileResponse(ToResponse(new FileWithLatest(file, version), isArchive: false), isNewVersion);
    }

    public async Task<ProjectFileDetailResponse> GetDetailAsync(Guid fileId, ClaimsPrincipal user, CancellationToken ct)
    {
        var file = await files.GetFileAsync(fileId, ct) ?? throw new NotFoundException("file", fileId);
        var read = await reader.RequireReadAsync(file.WorkspaceId, user, ct);

        var versions = await files.ListVersionsAsync(fileId, ct);
        var people = new WorkspacePeople(await lookup.ListMembersAsync(file.WorkspaceId, ct));
        var versionNumbers = versions.ToDictionary(v => v.Id, v => v.Version);
        var comments = (await files.ListCommentsAsync(fileId, ct))
            .Select(c => ToResponse(c, people, c.FileVersionId is { } id ? versionNumbers.GetValueOrDefault(id) : null))
            .ToList();

        var latest = ToResponse(new FileWithLatest(file, versions.First(v => v.Version == file.CurrentVersion)), read.IsArchive);
        return new ProjectFileDetailResponse(
            latest.Id, latest.FolderId, latest.Name, latest.CurrentVersion, latest.SizeBytes, latest.ContentType,
            latest.UpdatedByName, latest.UpdatedAt, latest.IsArchive,
            versions.Select(v => new FileVersionResponse(v.Id, v.Version, v.SizeBytes, v.UploadedByName, v.CreatedAt)).ToList(),
            comments);
    }

    public async Task<DownloadUrlResponse> GetDownloadUrlAsync(Guid fileId, int? version, ClaimsPrincipal user, CancellationToken ct)
    {
        var file = await files.GetFileAsync(fileId, ct) ?? throw new NotFoundException("file", fileId);
        await reader.RequireReadAsync(file.WorkspaceId, user, ct);
        var target = await files.GetVersionAsync(fileId, version ?? file.CurrentVersion, ct)
            ?? throw new NotFoundException("phiên bản", version);
        return new DownloadUrlResponse(storage.GetDownloadUrl(target.ObjectKey), clock.GetUtcNow().AddMinutes(storageOptions.Value.PresignMinutes));
    }

    public async Task DeleteAsync(Guid fileId, ClaimsPrincipal user, CancellationToken ct)
    {
        var file = await files.GetFileAsync(fileId, ct) ?? throw new NotFoundException("file", fileId);
        var writer = await reader.RequireWriteAsync(file.WorkspaceId, user, ct);
        RequireStudent(writer, "Chỉ thành viên nhóm được xóa file");
        if (!writer.IsLeader && file.CreatedById != writer.PersonId)
        {
            throw new ForbiddenException(title: "Chỉ người tải lên hoặc trưởng nhóm được xóa file");
        }

        file.DeletedAt = clock.GetUtcNow();
        await files.SaveChangesAsync(ct);
        logger.LogInformation("Xóa mềm file {FileId} của workspace {WorkspaceId}", file.Id, file.WorkspaceId);
    }

    public async Task<CommentResponse> AddCommentAsync(Guid fileId, AddCommentRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var file = await files.GetFileAsync(fileId, ct) ?? throw new NotFoundException("file", fileId);
        var writer = await reader.RequireWriteAsync(file.WorkspaceId, user, ct);
        Guid? versionId = null;
        if (request.Version is { } number)
        {
            versionId = (await files.GetVersionAsync(fileId, number, ct) ?? throw new NotFoundException("phiên bản", number)).Id;
        }

        var people = new WorkspacePeople(await lookup.ListMembersAsync(file.WorkspaceId, ct));
        var comment = new FileComment
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            FileVersionId = versionId,
            AuthorPersonType = writer.PersonType,
            AuthorPersonId = writer.PersonId,
            AuthorName = people.NameOf(writer.PersonType, writer.PersonId),
            Body = request.Body.Trim(),
            CreatedAt = clock.GetUtcNow(),
        };
        files.AddComment(comment);
        await files.SaveChangesAsync(ct);
        return ToResponse(comment, people, request.Version);
    }

    // ---------- Vị trí thư mục và kho EXE101 ----------

    /// <summary>
    /// Thư mục đang xem. Thư mục ảo "EXE101 (kỳ trước)" có id = id workspace EXE101 (<c>previous_workspace_id</c>):
    /// mở nó là xem thư mục gốc của workspace đó; thư mục con của nó cũng chỉ đọc.
    /// </summary>
    private async Task<FolderLocation> LocateAsync(Workspace workspace, Guid? folderId, CancellationToken ct)
    {
        if (folderId is null)
        {
            return new FolderLocation(workspace.Id, null, null, workspace.PreviousWorkspaceId, IsArchive: false);
        }
        if (folderId == workspace.PreviousWorkspaceId)
        {
            return new FolderLocation(folderId.Value, null, ArchiveFolder(folderId.Value, 0), folderId, IsArchive: true);
        }

        var folder = await files.GetFolderAsync(folderId.Value, ct);
        if (folder is not null && folder.WorkspaceId == workspace.Id)
        {
            return new FolderLocation(workspace.Id, folder, null, workspace.PreviousWorkspaceId, folder.IsArchive);
        }
        if (folder is not null && folder.WorkspaceId == workspace.PreviousWorkspaceId)
        {
            return new FolderLocation(folder.WorkspaceId, folder, null, workspace.PreviousWorkspaceId, IsArchive: true);
        }
        throw new NotFoundException("thư mục", folderId);
    }

    private sealed record FolderLocation(Guid WorkspaceId, Folder? Folder, FolderResponse? VirtualFolder, Guid? ArchiveWorkspaceId, bool IsArchive)
    {
        public FolderResponse? Current => VirtualFolder ?? (Folder is null ? null : ToResponse(Folder, 0, IsArchive));
    }

    private async Task<FolderResponse> ArchiveRootAsync(Guid archiveWorkspaceId, CancellationToken ct)
    {
        var count = (await files.ListFoldersAsync(archiveWorkspaceId, null, ct)).Count
            + (await files.ListFilesAsync(archiveWorkspaceId, null, ct)).Count;
        return ArchiveFolder(archiveWorkspaceId, count);
    }

    private static FolderResponse ArchiveFolder(Guid archiveWorkspaceId, int itemCount) =>
        new(archiveWorkspaceId, null, ArchiveFolderName, IsArchive: true, itemCount);

    /// <summary>Đường dẫn từ gốc tới thư mục đang xem (không gồm "Tài liệu" gốc).</summary>
    private async Task<IReadOnlyList<FolderResponse>> BreadcrumbAsync(FolderLocation location, CancellationToken ct)
    {
        var trail = new List<FolderResponse>();
        if (location.VirtualFolder is not null)
        {
            trail.Add(location.VirtualFolder);
            return trail;
        }

        for (var folder = location.Folder; folder is not null; folder = folder.ParentId is { } parentId ? await files.GetFolderAsync(parentId, ct) : null)
        {
            trail.Insert(0, ToResponse(folder, 0, location.IsArchive));
        }
        if (location.IsArchive && location.Folder?.WorkspaceId == location.ArchiveWorkspaceId)
        {
            trail.Insert(0, ArchiveFolder(location.ArchiveWorkspaceId!.Value, 0));
        }
        return trail;
    }

    // ---------- Quyền ----------

    /// <summary>Tải lên, tạo thư mục, xóa: chỉ sinh viên của nhóm (cố vấn chỉ đọc và bình luận – rule 24).</summary>
    private static void RequireStudent(WorkspaceAccess writer, string title)
    {
        if (!writer.IsStudent)
        {
            throw new ForbiddenException(title: title);
        }
    }

    private async Task SaveAsync(Func<AppException> onConflict, CancellationToken ct)
    {
        try
        {
            await files.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogInformation(ex, "Ghi file / thư mục bị trùng");
            throw onConflict();
        }
    }

    private async Task DeleteObjectQuietlyAsync(string key)
    {
        try
        {
            await storage.DeleteAsync(key, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không xóa được object {Key} sau khi ghi DB lỗi", key);
        }
    }

    // ---------- Chuyển đổi ----------

    private static FolderResponse ToResponse(Folder folder, int itemCount, bool isArchive) =>
        new(folder.Id, folder.ParentId, folder.Name, isArchive || folder.IsArchive, itemCount);

    public static ProjectFileResponse ToResponse(FileWithLatest row, bool isArchive) => new(
        row.File.Id, row.File.FolderId, row.File.Name, row.File.CurrentVersion, row.Latest.SizeBytes, row.Latest.ContentType,
        row.Latest.UploadedByName, row.File.UpdatedAt, isArchive);

    private static CommentResponse ToResponse(FileComment comment, WorkspacePeople people, int? version) => new(
        comment.Id, people.Ref(comment.AuthorPersonType, comment.AuthorPersonId, comment.AuthorName), comment.Body, version, comment.CreatedAt);
}
