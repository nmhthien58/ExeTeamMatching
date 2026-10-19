using BuildingBlocks.Errors;

namespace Identity.Services.Auth;

/// <summary>Lỗi của luồng đăng nhập (identity.yaml, docs/contracts/README.md).</summary>
public static class AuthErrors
{
    public static AppException InvalidGoogleToken() =>
        new HttpStatusException(401, "invalid-token", "Google ID token không hợp lệ", "Token Google không hợp lệ hoặc đã hết hạn. Vui lòng đăng nhập lại.");

    public static AppException InvalidRefreshToken() =>
        new HttpStatusException(401, "invalid-refresh-token", "Phiên đăng nhập đã hết hạn", "Refresh token không hợp lệ, đã dùng hoặc đã hết hạn. Vui lòng đăng nhập lại.");

    public static AppException NotInList() =>
        new ForbiddenException("not-in-list", "Email không có trong danh sách", "Email này chưa có trong danh sách của học kỳ. Liên hệ Bộ môn EXE nếu bạn nghĩ đây là nhầm lẫn.");

    public static AppException AccountLocked(int status = 403) =>
        new HttpStatusException(status, "account-locked", "Tài khoản đã bị khóa", "Tài khoản của bạn đã bị khóa. Liên hệ Bộ môn EXE.");

    public static AppException WrongClient() =>
        new ForbiddenException("wrong-client", "Không dùng được trên ứng dụng này", "Tài khoản Bộ môn chỉ đăng nhập trên web.");
}
