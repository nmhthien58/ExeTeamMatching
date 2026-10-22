using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Collab.Repositories.Context;
using Collab.Repositories.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Collab.Repositories.Files;

/// <summary>File kèm phiên bản mới nhất (dòng của danh sách C06).</summary>
public sealed record FileWithLatest(ProjectFile File, FileVersion Latest);

public interface IFileRepository
{
    Task<Folder?> GetFolderAsync(Guid folderId, CancellationToken ct);

    /// <summary>Thư mục con trực tiếp; <paramref name="parentId"/> null = thư mục gốc.</summary>
    Task<List<Folder>> ListFoldersAsync(Guid workspaceId, Guid? parentId, CancellationToken ct);

    /// <summary>Số mục (thư mục con + file chưa xóa) của từng thư mục.</summary>
    Task<Dictionary<Guid, int>> CountItemsAsync(IReadOnlyCollection<Guid> folderIds, CancellationToken ct);

    Task<bool> FolderNameExistsAsync(Guid workspaceId, Guid? parentId, string name, CancellationToken ct);

    void AddFolder(Folder folder);

    /// <summary>File chưa xóa trong một thư mục, kèm phiên bản mới nhất.</summary>
    Task<List<FileWithLatest>> ListFilesAsync(Guid workspaceId, Guid? folderId, CancellationToken ct);

    /// <summary>File vừa cập nhật gần nhất của workspace (thẻ "File mới" ở C01).</summary>
    Task<List<FileWithLatest>> ListRecentFilesAsync(Guid workspaceId, int count, CancellationToken ct);

    Task<ProjectFile?> GetFileAsync(Guid fileId, CancellationToken ct);

    /// <summary>File theo id kèm bản mới nhất, kể cả file đã xóa mềm (bài nộp cũ vẫn hiện file đã nộp).</summary>
    Task<List<FileWithLatest>> ListWithLatestAsync(IReadOnlyCollection<Guid> fileIds, CancellationToken ct);

    /// <summary>File chưa xóa cùng tên (không phân biệt hoa thường) trong thư mục – tải lên trùng tên tạo phiên bản mới.</summary>
    Task<ProjectFile?> FindByNameAsync(Guid workspaceId, Guid? folderId, string name, CancellationToken ct);

    void AddFile(ProjectFile file);

    Task<List<FileVersion>> ListVersionsAsync(Guid fileId, CancellationToken ct);

    Task<FileVersion?> GetVersionAsync(Guid fileId, int version, CancellationToken ct);

    Task<FileVersion?> GetVersionByIdAsync(Guid versionId, CancellationToken ct);

    Task<List<FileVersion>> GetVersionsByIdsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken ct);

    void AddVersion(FileVersion version);

    /// <summary>File đã từng được nộp cho bài nộp – phiên bản mới của nó cần báo cố vấn (FileUploaded).</summary>
    Task<bool> HasSubmissionAsync(Guid fileId, CancellationToken ct);

    Task<List<FileComment>> ListCommentsAsync(Guid fileId, CancellationToken ct);

    void AddComment(FileComment comment);

    /// <summary>Ghi sự kiện exe.collab vào outbox, cùng transaction với <see cref="SaveChangesAsync"/>.</summary>
    void AddEvent<TPayload>(string eventType, Guid workspaceId, TPayload payload);

    Task SaveChangesAsync(CancellationToken ct);
}

public sealed class FileRepository(CollabDbContext db) : IFileRepository
{
    public Task<Folder?> GetFolderAsync(Guid folderId, CancellationToken ct) =>
        db.Set<Folder>().FirstOrDefaultAsync(f => f.Id == folderId, ct);

    public Task<List<Folder>> ListFoldersAsync(Guid workspaceId, Guid? parentId, CancellationToken ct) =>
        db.Set<Folder>().AsNoTracking()
            .Where(f => f.WorkspaceId == workspaceId && f.ParentId == parentId)
            .OrderBy(f => f.Name)
            .ToListAsync(ct);

    public async Task<Dictionary<Guid, int>> CountItemsAsync(IReadOnlyCollection<Guid> folderIds, CancellationToken ct)
    {
        var folders = await db.Set<Folder>()
            .Where(f => f.ParentId != null && folderIds.Contains(f.ParentId.Value))
            .GroupBy(f => f.ParentId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var files = await db.Set<ProjectFile>()
            .Where(f => f.DeletedAt == null && f.FolderId != null && folderIds.Contains(f.FolderId.Value))
            .GroupBy(f => f.FolderId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        return folderIds.ToDictionary(
            id => id,
            id => (folders.FirstOrDefault(x => x.Id == id)?.Count ?? 0) + (files.FirstOrDefault(x => x.Id == id)?.Count ?? 0));
    }

    public Task<bool> FolderNameExistsAsync(Guid workspaceId, Guid? parentId, string name, CancellationToken ct) =>
        db.Set<Folder>().AnyAsync(f => f.WorkspaceId == workspaceId && f.ParentId == parentId && f.Name.ToLower() == name.ToLower(), ct);

    public void AddFolder(Folder folder) => db.Set<Folder>().Add(folder);

    public async Task<List<FileWithLatest>> ListFilesAsync(Guid workspaceId, Guid? folderId, CancellationToken ct)
    {
        var rows = await (
            from f in db.Set<ProjectFile>().AsNoTracking()
            join v in db.Set<FileVersion>().AsNoTracking() on new { FileId = f.Id, Version = f.CurrentVersion } equals new { v.FileId, v.Version }
            where f.WorkspaceId == workspaceId && f.FolderId == folderId && f.DeletedAt == null
            orderby f.Name
            select new { f, v }).ToListAsync(ct);
        return rows.Select(r => new FileWithLatest(r.f, r.v)).ToList();
    }

    public async Task<List<FileWithLatest>> ListRecentFilesAsync(Guid workspaceId, int count, CancellationToken ct)
    {
        var rows = await (
            from f in db.Set<ProjectFile>().AsNoTracking()
            join v in db.Set<FileVersion>().AsNoTracking() on new { FileId = f.Id, Version = f.CurrentVersion } equals new { v.FileId, v.Version }
            where f.WorkspaceId == workspaceId && f.DeletedAt == null
            orderby f.UpdatedAt descending
            select new { f, v }).Take(count).ToListAsync(ct);
        return rows.Select(r => new FileWithLatest(r.f, r.v)).ToList();
    }

    public Task<ProjectFile?> GetFileAsync(Guid fileId, CancellationToken ct) =>
        db.Set<ProjectFile>().FirstOrDefaultAsync(f => f.Id == fileId && f.DeletedAt == null, ct);

    public Task<ProjectFile?> FindByNameAsync(Guid workspaceId, Guid? folderId, string name, CancellationToken ct) =>
        db.Set<ProjectFile>().FirstOrDefaultAsync(f =>
            f.WorkspaceId == workspaceId && f.FolderId == folderId && f.DeletedAt == null && f.Name.ToLower() == name.ToLower(), ct);

    public async Task<List<FileWithLatest>> ListWithLatestAsync(IReadOnlyCollection<Guid> fileIds, CancellationToken ct)
    {
        var rows = await (
            from f in db.Set<ProjectFile>().AsNoTracking()
            join v in db.Set<FileVersion>().AsNoTracking() on new { FileId = f.Id, Version = f.CurrentVersion } equals new { v.FileId, v.Version }
            where fileIds.Contains(f.Id)
            select new { f, v }).ToListAsync(ct);
        return rows.Select(r => new FileWithLatest(r.f, r.v)).ToList();
    }

    public void AddFile(ProjectFile file) => db.Set<ProjectFile>().Add(file);

    public Task<List<FileVersion>> ListVersionsAsync(Guid fileId, CancellationToken ct) =>
        db.Set<FileVersion>().AsNoTracking().Where(v => v.FileId == fileId).OrderByDescending(v => v.Version).ToListAsync(ct);

    public Task<FileVersion?> GetVersionAsync(Guid fileId, int version, CancellationToken ct) =>
        db.Set<FileVersion>().AsNoTracking().FirstOrDefaultAsync(v => v.FileId == fileId && v.Version == version, ct);

    public Task<FileVersion?> GetVersionByIdAsync(Guid versionId, CancellationToken ct) =>
        db.Set<FileVersion>().AsNoTracking().FirstOrDefaultAsync(v => v.Id == versionId, ct);

    public Task<List<FileVersion>> GetVersionsByIdsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken ct) =>
        db.Set<FileVersion>().AsNoTracking().Where(v => versionIds.Contains(v.Id)).ToListAsync(ct);

    public void AddVersion(FileVersion version) => db.Set<FileVersion>().Add(version);

    public Task<bool> HasSubmissionAsync(Guid fileId, CancellationToken ct) =>
        db.Set<DeliverableSubmission>()
            .Join(db.Set<FileVersion>(), s => s.FileVersionId, v => v.Id, (s, v) => v.FileId)
            .AnyAsync(id => id == fileId, ct);

    public Task<List<FileComment>> ListCommentsAsync(Guid fileId, CancellationToken ct) =>
        db.Set<FileComment>().AsNoTracking().Where(c => c.FileId == fileId).OrderBy(c => c.CreatedAt).ToListAsync(ct);

    public void AddComment(FileComment comment) => db.Set<FileComment>().Add(comment);

    public void AddEvent<TPayload>(string eventType, Guid workspaceId, TPayload payload) =>
        db.AddOutboxMessage(Topics.Collab, eventType, "Workspace", workspaceId, payload);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
