import 'dart:async';

import 'package:firebase_crashlytics/firebase_crashlytics.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../config/env.dart';
import 'client_error_reporter.dart';

/// Báo lỗi lên Firebase Crashlytics (MB-50). ViewModel và service khác chỉ dùng interface này,
/// không gọi `FirebaseCrashlytics.instance`. Mọi method không ném lỗi: báo lỗi hỏng thì app vẫn chạy.
abstract interface class CrashReporter {
  /// Lỗi đã bắt được nhưng đáng theo dõi (ví dụ Remote Config không tải được).
  Future<void> recordNonFatal(Object error, StackTrace? stack, {String? reason});

  /// Gắn báo cáo với người dùng bằng **id nội bộ** (không dùng email). `null` = bỏ gắn.
  Future<void> setUser(String? id);

  /// Custom key, ví dụ `role`, `course`, `screen`. [value] là `String`, `num` hoặc `bool`.
  Future<void> setKey(String key, Object value);

  /// Đăng xuất: bỏ id người dùng và các key [userKeys].
  Future<void> clearUser();

  /// Cố ý làm app crash để kiểm tra Crashlytics (chỉ dùng ở nút thử trong bản debug / demo).
  Future<void> testCrash();
}

/// Lỗi ghi vào báo cáo gửi backend khi bấm "Crash thử".
class TestCrashError implements Exception {
  const TestCrashError();

  @override
  String toString() => 'TestCrashError: crash thử từ Cài đặt → Demo Firebase';
}

/// Key gắn theo người dùng; đăng xuất thì xóa.
const userKeys = ['role', 'course'];

class FirebaseCrashReporter implements CrashReporter {
  /// [crashlytics] để test truyền bản giả; mặc định lấy instance khi gọi, không lấy lúc tạo,
  /// để provider tạo được cả khi chưa khởi tạo Firebase (test).
  /// [mirrorToBackend]: gửi thêm bản báo cáo về backend (MB-61), theo đúng việc Crashlytics có thu thập hay không.
  FirebaseCrashReporter({
    FirebaseCrashlytics Function()? crashlytics,
    ClientErrorReporter? clientErrors,
    this._mirrorToBackend = Env.crashlyticsEnabled,
  }) : _crashlytics = crashlytics ?? (() => FirebaseCrashlytics.instance),
       _clientErrors = clientErrors ?? clientErrorReporter;

  final FirebaseCrashlytics Function() _crashlytics;
  final ClientErrorReporter _clientErrors;
  final bool _mirrorToBackend;

  Future<void> _safe(String what, Future<void> Function(FirebaseCrashlytics c) call) async {
    try {
      await call(_crashlytics());
    } catch (e) {
      debugPrint('Crashlytics $what failed: $e');
    }
  }

  @override
  Future<void> recordNonFatal(Object error, StackTrace? stack, {String? reason}) {
    if (_mirrorToBackend) unawaited(_clientErrors.reportNonFatal(error, stack, reason: reason));
    return _safe('recordError', (c) => c.recordError(error, stack, reason: reason, fatal: false));
  }

  @override
  Future<void> setUser(String? id) {
    _clientErrors.userId = id;
    return _safe('setUserIdentifier', (c) => c.setUserIdentifier(id ?? ''));
  }

  @override
  Future<void> setKey(String key, Object value) {
    final text = value.toString();
    if (key == 'role') _clientErrors.role = text.isEmpty ? null : text;
    if (key == 'screen') _clientErrors.screen = text;
    return _safe('setCustomKey', (c) => c.setCustomKey(key, value));
  }

  @override
  Future<void> clearUser() async {
    await setUser(null);
    for (final key in userKeys) {
      await setKey(key, '');
    }
  }

  /// `crash()` làm process chết ở tầng native, không qua `FlutterError.onError` hay `PlatformDispatcher.onError`,
  /// nên phải lưu báo cáo trước khi gọi.
  @override
  Future<void> testCrash() async {
    if (_mirrorToBackend) await _clientErrors.saveFatal(const TestCrashError(), StackTrace.current);
    _crashlytics().crash();
  }
}

/// Ghi đè trong test bằng `FakeCrashReporter` (`test/support/firebase_fakes.dart`).
final crashReporterProvider = Provider<CrashReporter>((ref) => FirebaseCrashReporter());
