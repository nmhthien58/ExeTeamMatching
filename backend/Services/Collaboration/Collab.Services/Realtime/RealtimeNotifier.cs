namespace Collab.Services.Realtime;

/// <summary>Tên sự kiện server → client trên hub <c>/hubs/collab</c> (docs/contracts/events.md).</summary>
public static class HubEvents
{
    public const string MessageCreated = "messageCreated";
    public const string MessageUpdated = "messageUpdated";
    public const string ReadUpdated = "readUpdated";
    public const string Typing = "typing";

    /// <summary>Lịch họp hằng tuần: slot vừa được đặt / đổi (C09 cập nhật lưới không cần tải lại).</summary>
    public const string SlotsChanged = "slotsChanged";
}

/// <summary>
/// Đẩy sự kiện realtime tới client. Services gọi interface này (không phụ thuộc SignalR);
/// Collab.Api cài đặt bằng <c>IHubContext</c>. Gọi sau khi <c>SaveChangesAsync</c> thành công.
/// </summary>
public interface IRealtimeNotifier
{
    Task ToChannelAsync(Guid channelId, string eventName, object payload, CancellationToken ct = default);

    Task ToWorkspaceAsync(Guid workspaceId, string eventName, object payload, CancellationToken ct = default);

    /// <summary>Nhóm SignalR theo kênh / workspace (client vào bằng JoinChannel / JoinWorkspace).</summary>
    static string ChannelGroup(Guid channelId) => $"channel:{channelId}";

    static string WorkspaceGroup(Guid workspaceId) => $"workspace:{workspaceId}";
}
