// Map deploy/db/migrations/BE-87_client_error_reports.sql. Sửa DB script trước, rồi sửa file này cho khớp.
namespace Identity.Repositories.ClientErrors;

/// <summary>Giá trị cột <c>kind</c>.</summary>
public static class ClientErrorKinds
{
    public const string Fatal = "fatal";
    public const string NonFatal = "non_fatal";
}

/// <summary>Bảng <c>identity.client_error_reports</c>: một lỗi app gửi về. Không có email, tên hay IP.</summary>
public class ClientErrorReport
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ExceptionType { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string FirstStackLine { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string? DeviceModel { get; set; }
    public string? OsVersion { get; set; }
    public Guid? UserId { get; set; }
    public string? Role { get; set; }
    public string? Screen { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}
