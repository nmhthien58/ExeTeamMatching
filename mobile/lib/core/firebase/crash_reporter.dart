import 'package:firebase_crashlytics/firebase_crashlytics.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Báo lỗi lên Firebase Crashlytics (MB-50). ViewModel và service khác chỉ dùng interface này,
/// không gọi `FirebaseCrashlytics.instance`. Mọi method không ném lỗi: báo lỗi hỏng thì app vẫn chạy.
abstract interface class CrashReporter {
  /// Lỗi đã bắt được nhưng đáng theo dõi (ví dụ Remote Config không tải được).
  Future<void> recordNonFatal(Object error, StackTrace? stack, {String? reason});

  /// Gắn báo cáo với người dùng bằng **id nội bộ** (không dùng email). `null` = bỏ gắn.
  Future<void> setUser(String? id);

  /// Custom key, ví dụ `role`, `course`. [value] là `String`, `num` hoặc `bool`.
  Future<void> setKey(String key, Object value);

  /// Đăng xuất: bỏ id người dùng và các key [userKeys].
  Future<void> clearUser();

  /// Cố ý làm app crash để kiểm tra Crashlytics (chỉ dùng ở nút thử trong bản debug / demo).
  void testCrash();
}

/// Key gắn theo người dùng; đăng xuất thì xóa.
const userKeys = ['role', 'course'];

class FirebaseCrashReporter implements CrashReporter {
  /// [crashlytics] để test truyền bản giả; mặc định lấy instance khi gọi, không lấy lúc tạo,
  /// để provider tạo được cả khi chưa khởi tạo Firebase (test).
  FirebaseCrashReporter({FirebaseCrashlytics Function()? crashlytics})
    : _crashlytics = crashlytics ?? (() => FirebaseCrashlytics.instance);

  final FirebaseCrashlytics Function() _crashlytics;

  Future<void> _safe(String what, Future<void> Function(FirebaseCrashlytics c) call) async {
    try {
      await call(_crashlytics());
    } catch (e) {
      debugPrint('Crashlytics $what failed: $e');
    }
  }

  @override
  Future<void> recordNonFatal(Object error, StackTrace? stack, {String? reason}) =>
      _safe('recordError', (c) => c.recordError(error, stack, reason: reason, fatal: false));

  @override
  Future<void> setUser(String? id) => _safe('setUserIdentifier', (c) => c.setUserIdentifier(id ?? ''));

  @override
  Future<void> setKey(String key, Object value) => _safe('setCustomKey', (c) => c.setCustomKey(key, value));

  @override
  Future<void> clearUser() async {
    await setUser(null);
    for (final key in userKeys) {
      await setKey(key, '');
    }
  }

  @override
  void testCrash() => _crashlytics().crash();
}

/// Ghi đè trong test bằng `FakeCrashReporter` (`test/support/firebase_fakes.dart`).
final crashReporterProvider = Provider<CrashReporter>((ref) => FirebaseCrashReporter());
