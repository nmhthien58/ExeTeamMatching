// Map deploy/db/01_identity_db.sql (BE-10). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Identity.Repositories.Accounts;

/// <summary>Bảng <c>identity.user_accounts</c> – tài khoản kiêm email whitelist (rule 5).</summary>
public class UserAccount
{
    public Guid Id { get; set; }

    /// <summary>Cột <c>citext</c>: so sánh không phân biệt hoa thường ở DB.</summary>
    public string Email { get; set; } = string.Empty;

    public string? GoogleSubject { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Status { get; set; } = AccountStatuses.Pending;

    /// <summary>Hết hạn whitelist (= end_date học kỳ); null là không hạn.</summary>
    public DateTimeOffset? AllowedUntil { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Do trigger <c>trg_user_accounts_updated_at</c> cập nhật.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    public List<UserAccountRole> Roles { get; set; } = [];
}

/// <summary>Giá trị cột <c>status</c>.</summary>
public static class AccountStatuses
{
    public const string Pending = "PENDING";
    public const string Active = "ACTIVE";
    public const string Locked = "LOCKED";
}
