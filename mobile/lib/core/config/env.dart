/// Cấu hình truyền lúc build / chạy, ví dụ:
/// `flutter run --dart-define=API_BASE_URL=http://10.0.2.2:8080 --dart-define=GOOGLE_SERVER_CLIENT_ID=xxx.apps.googleusercontent.com`
/// (10.0.2.2 là địa chỉ máy tính khi chạy trên Android emulator).
class Env {
  const Env._();

  static const apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:8080',
  );

  /// Web client ID của OAuth (cùng client với web) – Google Sign-In trả ID token cho client này,
  /// Identity kiểm tra audience. Hướng dẫn tạo: docs/setup.
  static const googleServerClientId = String.fromEnvironment('GOOGLE_SERVER_CLIENT_ID');
}
