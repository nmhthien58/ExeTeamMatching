namespace BuildingBlocks.Errors;

/// <summary>
/// Lỗi nghiệp vụ có mã. <see cref="GlobalExceptionHandler"/> đổi thành ProblemDetails với
/// <c>type = https://exe-team-matching/errors/{Code}</c> (bảng mã ở docs/contracts/README.md).
/// Services ném các lỗi này; Controller không tự bắt.
/// </summary>
public abstract class AppException(int status, string code, string title, string? detail = null)
    : Exception(detail ?? title)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public string Title { get; } = title;
    public string? Detail { get; } = detail;
}

/// <summary>404 – không tìm thấy tài nguyên.</summary>
public sealed class NotFoundException(string resource, object? key = null)
    : AppException(404, "not-found", "Không tìm thấy", key is null ? $"Không tìm thấy {resource}." : $"Không tìm thấy {resource} '{key}'.");

/// <summary>409 – vi phạm quy tắc nghiệp vụ, ví dụ <c>round-closed</c>, <c>team-full</c>.</summary>
public sealed class ConflictException(string code, string title, string? detail = null)
    : AppException(409, code, title, detail);

/// <summary>403 – có đăng nhập nhưng không đủ quyền, ví dụ <c>not-workspace-member</c>.</summary>
public sealed class ForbiddenException(string code = "forbidden", string title = "Không có quyền", string? detail = null)
    : AppException(403, code, title, detail);

/// <summary>400 – dữ liệu không hợp lệ theo từng field.</summary>
public sealed class RequestValidationException(IDictionary<string, string[]> errors, string? detail = null)
    : AppException(400, "validation", "Dữ liệu không hợp lệ", detail)
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}

/// <summary>Lỗi có mã HTTP tùy ý (413 file-too-large, 415 file-type-not-allowed…).</summary>
public sealed class HttpStatusException(int status, string code, string title, string? detail = null)
    : AppException(status, code, title, detail);
