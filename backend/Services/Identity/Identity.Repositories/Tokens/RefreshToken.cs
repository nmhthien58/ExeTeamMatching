// Map deploy/db/01_identity_db.sql (BE-10). Sửa DB script trước, rồi sửa file này cho khớp.
using System.Net;

namespace Identity.Repositories.Tokens;

/// <summary>
/// Bảng <c>identity.refresh_tokens</c>. Chỉ lưu SHA-256 (hex) của token. Rotation: token cũ có
/// <see cref="RevokedAt"/> và <see cref="ReplacedByTokenId"/> trỏ sang token mới.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserAccountId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public IPAddress? CreatedByIp { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
