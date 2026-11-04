import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Chỉ hiển thị A02 một lần trên thiết bị, kể cả khi người dùng chưa đăng nhập.
class OnboardingStore {
  OnboardingStore([FlutterSecureStorage? storage])
    : _storage = storage ?? const FlutterSecureStorage();

  static const _seenKey = 'exe.onboardingSeen';
  final FlutterSecureStorage _storage;

  Future<bool> hasSeen() async =>
      (await _storage.read(key: _seenKey)) == 'true';

  Future<void> markSeen() => _storage.write(key: _seenKey, value: 'true');
}

/// Service đánh dấu đã xem A02; test thay bằng `overrideWithValue` hoặc truyền thẳng vào màn.
final onboardingStoreProvider = Provider<OnboardingStore>((ref) => OnboardingStore());
