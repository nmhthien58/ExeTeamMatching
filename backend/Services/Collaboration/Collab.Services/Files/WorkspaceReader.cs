using System.Security.Claims;
using BuildingBlocks.Errors;
using Collab.Repositories.Files;
using Collab.Services.Workspaces;

namespace Collab.Services.Files;

/// <summary>
/// Quyền xem dữ liệu của một workspace. <see cref="IsArchive"/> = người xem đến từ workspace EXE201 cùng đề tài
/// và đang xem kho EXE101 (rule 26) – chỉ đọc; <see cref="Viewer"/> là vai trò của họ ở workspace EXE201.
/// </summary>
public sealed record ReadAccess(WorkspaceAccess Viewer, bool IsArchive);

public interface IWorkspaceReader
{
    /// <summary>Thành viên / cố vấn / Admin của workspace, hoặc thành viên workspace EXE201 nối tới nó (BE-77); còn lại 403.</summary>
    Task<ReadAccess> RequireReadAsync(Guid workspaceId, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>
    /// Quyền ghi cho route không có <c>{workspaceId}</c> (theo id việc, file…): như policy WorkspaceWriter;
    /// người đang xem kho EXE101 nhận 409 <c>archive-read-only</c>.
    /// </summary>
    Task<WorkspaceAccess> RequireWriteAsync(Guid workspaceId, ClaimsPrincipal user, CancellationToken ct);
}

public sealed class WorkspaceReader(IWorkspaceAccessService access, IWorkspaceLookup lookup) : IWorkspaceReader
{
    public async Task<ReadAccess> RequireReadAsync(Guid workspaceId, ClaimsPrincipal user, CancellationToken ct)
    {
        try
        {
            return new ReadAccess(await access.RequireAsync(workspaceId, user, ct), IsArchive: false);
        }
        catch (ForbiddenException)
        {
            var linked = await lookup.FindLinkedWorkspaceAsync(workspaceId, access.PeopleOf(user), ct);
            if (linked is null)
            {
                throw;
            }
            return new ReadAccess(await access.RequireAsync(linked.Id, user, ct), IsArchive: true);
        }
    }

    public async Task<WorkspaceAccess> RequireWriteAsync(Guid workspaceId, ClaimsPrincipal user, CancellationToken ct)
    {
        var read = await RequireReadAsync(workspaceId, user, ct);
        if (read.IsArchive)
        {
            throw ArchiveErrors.ReadOnly();
        }
        WorkspaceMemberHandler.Check(read.Viewer, WorkspaceAccessLevel.Writer);
        return read.Viewer;
    }
}

public static class ArchiveErrors
{
    public static ConflictException ReadOnly() =>
        new("archive-read-only", "Kho EXE101 chỉ xem được", "Dữ liệu của kỳ EXE101 trước không sửa được.");
}
