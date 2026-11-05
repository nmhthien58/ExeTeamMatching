/// Lỗi của service AI (MB-55). [code] là mã ngắn, dùng cho `ai_error` (Analytics) và để View chọn câu thông báo;
/// không chứa nội dung lỗi gốc hay dữ liệu người dùng.
class AiException implements Exception {
  const AiException(this.code);

  /// Cờ `ai_idea_enabled` trên Remote Config đang tắt – không gọi mạng.
  static const disabled = 'disabled';

  /// Hết quota Gemini (HTTP 429 / `RESOURCE_EXHAUSTED`).
  static const quota = 'quota';

  /// Mất mạng hoặc quá thời gian chờ.
  static const network = 'network';

  /// Mô hình trả JSON sai dạng (thiếu trường, sai kiểu).
  static const badJson = 'bad_json';

  /// Bị chặn bởi bộ lọc an toàn hoặc không có nội dung trả về.
  static const blocked = 'blocked';

  /// Lỗi khác (đã ghi non-fatal lên Crashlytics).
  static const unknown = 'unknown';

  static const codes = [disabled, quota, network, badJson, blocked, unknown];

  final String code;

  @override
  bool operator ==(Object other) => other is AiException && other.code == code;

  @override
  int get hashCode => code.hashCode;

  @override
  String toString() => 'AiException($code)';
}
