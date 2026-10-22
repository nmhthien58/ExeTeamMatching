using Collab.Services.Files;
using Collab.Services.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Collab.Api.Controllers.Files;

/// <summary>File dự án C06 (BE-65, rule 24) và kho EXE101 chỉ đọc (BE-77) – collab.yaml tag Files.</summary>
[ApiController]
[Authorize]
[Route("api")]
public sealed class FilesController(IFileService files) : ControllerBase
{
    /// <summary>Giới hạn body của request tải lên: lớn hơn 25 MB một chút để Services trả 413 <c>file-too-large</c> có mã lỗi.</summary>
    private const long UploadRequestLimit = 30 * 1024 * 1024;

    /// <summary>Nội dung một thư mục; bỏ trống <paramref name="folderId"/> = thư mục gốc.</summary>
    [HttpGet("workspaces/{workspaceId:guid}/files")]
    [Authorize(Policy = CollabPolicies.WorkspaceMember)]
    public async Task<ActionResult<FolderContentResponse>> List(Guid workspaceId, [FromQuery] Guid? folderId, CancellationToken ct) =>
        Ok(await files.GetFolderContentAsync(workspaceId, folderId, HttpContext.GetWorkspaceAccess(), ct));

    /// <summary>Tải lên; trùng tên trong thư mục → phiên bản mới (<c>createdNewVersion = true</c>).</summary>
    [HttpPost("workspaces/{workspaceId:guid}/files")]
    [Authorize(Policy = CollabPolicies.WorkspaceWriter)]
    [RequestSizeLimit(UploadRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadRequestLimit)]
    public async Task<ActionResult<UploadFileResponse>> Upload(Guid workspaceId, [FromForm] UploadFileForm form, CancellationToken ct)
    {
        await using var content = form.File.OpenReadStream();
        var result = await files.UploadAsync(workspaceId,
            new UploadFileCommand(form.FolderId, form.File.FileName, form.File.Length, content), HttpContext.GetWorkspaceAccess(), ct);
        return Created($"/api/files/{result.File.Id}", result);
    }

    [HttpPost("workspaces/{workspaceId:guid}/folders")]
    [Authorize(Policy = CollabPolicies.WorkspaceWriter)]
    public async Task<ActionResult<FolderResponse>> CreateFolder(Guid workspaceId, CreateFolderRequest request, CancellationToken ct)
    {
        var folder = await files.CreateFolderAsync(workspaceId, request, HttpContext.GetWorkspaceAccess(), ct);
        return Created($"/api/workspaces/{workspaceId}/files?folderId={folder.Id}", folder);
    }

    /// <summary>Chi tiết file – các phiên bản và bình luận.</summary>
    [HttpGet("files/{fileId:guid}")]
    public async Task<ActionResult<ProjectFileDetailResponse>> Get(Guid fileId, CancellationToken ct) =>
        Ok(await files.GetDetailAsync(fileId, User, ct));

    /// <summary>Xóa mềm (người tải lên, trưởng nhóm).</summary>
    [HttpDelete("files/{fileId:guid}")]
    public async Task<IActionResult> Delete(Guid fileId, CancellationToken ct)
    {
        await files.DeleteAsync(fileId, User, ct);
        return NoContent();
    }

    /// <summary>URL ký sẵn để tải / xem; bỏ trống <paramref name="version"/> = bản mới nhất.</summary>
    [HttpGet("files/{fileId:guid}/download-url")]
    public async Task<ActionResult<DownloadUrlResponse>> DownloadUrl(Guid fileId, [FromQuery] int? version, CancellationToken ct) =>
        Ok(await files.GetDownloadUrlAsync(fileId, version, User, ct));

    [HttpPost("files/{fileId:guid}/comments")]
    public async Task<ActionResult<CommentResponse>> Comment(Guid fileId, AddCommentRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await files.AddCommentAsync(fileId, request, User, ct));
}

/// <summary>Form multipart của <c>POST /api/workspaces/{id}/files</c>.</summary>
public sealed class UploadFileForm
{
    public required IFormFile File { get; init; }

    /// <summary>Bỏ trống = thư mục gốc.</summary>
    public Guid? FolderId { get; init; }
}
