import 'package:flutter/foundation.dart';

/// Cấu hình truyền lúc build / chạy, ví dụ:
/// `flutter run --dart-define=API_BASE_URL=http://10.0.2.2:8080`
/// (10.0.2.2 là địa chỉ máy tính khi chạy trên Android emulator).
class Env {
  const Env._();

  static const apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:8080',
  );

  /// Web client ID của OAuth (cùng client với web) – Google Sign-In trả ID token cho client này,
  /// Identity kiểm tra audience. Không phải bí mật nên đặt sẵn; đổi project thì truyền `--dart-define`. Hướng dẫn: docs/setup.
  static const googleServerClientId = String.fromEnvironment(
    'GOOGLE_SERVER_CLIENT_ID',
    defaultValue: '221888974127-ho8c6t3rqct6f7r5o2e8gott7i8lrd7s.apps.googleusercontent.com',
  );

  /// App Check dùng debug provider (token debug in ra logcat, đăng ký ở Firebase Console → App Check).
  /// Mặc định bật ở bản debug; APK demo cài tay truyền `--dart-define=APP_CHECK_DEBUG=true` vì Play Integrity
  /// chỉ nhận app cài từ Google Play.
  static const appCheckDebug = bool.fromEnvironment('APP_CHECK_DEBUG', defaultValue: kDebugMode);
}
