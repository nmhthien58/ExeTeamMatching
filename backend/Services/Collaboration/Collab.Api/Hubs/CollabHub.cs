using BuildingBlocks.Errors;
using Collab.Repositories.Workspaces;
using Collab.Services.Realtime;
using Collab.Services.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Collab.Api.Hubs;

/// <summary>
/// Hub realtime của workspace (BE-63), qua Gateway tại <c>/hubs/collab</c>; JWT gửi bằng query <c>access_token</c>.
/// Hub chỉ đẩy dữ liệu; gửi tin nhắn vẫn qua REST (PROJECT_CONTEXT §5.8).
/// Client: kết nối → <c>JoinWorkspace(workspaceId)</c> / <c>JoinChannel(channelId)</c> → nghe <see cref="HubEvents"/>.
/// </summary>
[Authorize]
public sealed class CollabHub(IWorkspaceRepository workspaces, IWorkspaceAccessService access) : Hub
{
    public async Task JoinWorkspace(Guid workspaceId)
    {
        await access.RequireAsync(workspaceId, Context.User!, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, IRealtimeNotifier.WorkspaceGroup(workspaceId));
    }

    public Task LeaveWorkspace(Guid workspaceId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, IRealtimeNotifier.WorkspaceGroup(workspaceId));

    /// <summary>Kênh TEAM chỉ cho sinh viên của nhóm; kênh ADVISORS cho cả cố vấn (rule 22).</summary>
    public async Task JoinChannel(Guid channelId)
    {
        var channel = await workspaces.GetChannelAsync(channelId, Context.ConnectionAborted)
            ?? throw new HubException("Không tìm thấy kênh.");
        WorkspaceAccess role;
        try
        {
            role = await access.RequireAsync(channel.WorkspaceId, Context.User!, Context.ConnectionAborted);
        }
        catch (AppException ex)
        {
            throw new HubException(ex.Title);
        }
        if (channel.Kind == "TEAM" && !role.IsStudent)
        {
            throw new HubException("Kênh Nhóm chỉ dành cho thành viên nhóm.");
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, IRealtimeNotifier.ChannelGroup(channelId));
    }

    public Task LeaveChannel(Guid channelId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, IRealtimeNotifier.ChannelGroup(channelId));

    /// <summary>"Đang nhập…" – chỉ gửi cho người khác trong kênh, không lưu.</summary>
    public Task Typing(Guid channelId) =>
        Clients.OthersInGroup(IRealtimeNotifier.ChannelGroup(channelId))
            .SendAsync(HubEvents.Typing, new { channelId, userId = Context.UserIdentifier });
}

/// <summary>Cài đặt <see cref="IRealtimeNotifier"/> bằng SignalR.</summary>
public sealed class SignalRRealtimeNotifier(IHubContext<CollabHub> hub) : IRealtimeNotifier
{
    public Task ToChannelAsync(Guid channelId, string eventName, object payload, CancellationToken ct = default) =>
        hub.Clients.Group(IRealtimeNotifier.ChannelGroup(channelId)).SendAsync(eventName, payload, ct);

    public Task ToWorkspaceAsync(Guid workspaceId, string eventName, object payload, CancellationToken ct = default) =>
        hub.Clients.Group(IRealtimeNotifier.WorkspaceGroup(workspaceId)).SendAsync(eventName, payload, ct);
}
