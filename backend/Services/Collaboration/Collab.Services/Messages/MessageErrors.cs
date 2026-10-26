using BuildingBlocks.Errors;

namespace Collab.Services.Messages;

/// <summary>Lỗi của module Messages (mã ở docs/contracts/README.md và collab.yaml).</summary>
public static class MessageErrors
{
    public static NotFoundException NotFound(Guid messageId) => new("tin nhắn", messageId);

    public static ForbiddenException TeamChannelOnly() =>
        new("team-channel-only", "Kênh Nhóm chỉ dành cho thành viên nhóm", "Cố vấn trao đổi với nhóm ở kênh Cố vấn.");

    public static ForbiddenException NotAuthor() => new(title: "Chỉ người gửi được sửa / xóa tin này");

    // Mã chưa có trong docs/contracts/README.md – đề xuất bổ sung.
    public static ConflictException EditWindowClosed() =>
        new("edit-window-closed", "Hết thời gian sửa", "Chỉ sửa được tin trong 15 phút sau khi gửi.");

    public static RequestValidationException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
