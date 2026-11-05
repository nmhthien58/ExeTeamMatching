import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/config/env.dart';
import '../../core/firebase/crash_reporter.dart';
import '../../core/firebase/remote_config_service.dart';

/// Lỗi thử gửi lên Crashlytics từ mục Demo Firebase (Lab 3 §6: một lỗi non-fatal và một crash thử).
class DemoNonFatalError implements Exception {
  const DemoNonFatalError();

  @override
  String toString() => 'DemoNonFatalError: lỗi thử từ Cài đặt → Demo Firebase';
}

/// Dữ liệu mục Demo Firebase: Crashlytics có đang gửi không và giá trị Remote Config đang dùng.
class FirebaseDemoState {
  const FirebaseDemoState({required this.crashlyticsEnabled, required this.config});

  final bool crashlyticsEnabled;
  final AppRemoteConfig config;
}

/// ViewModel của mục Demo Firebase trong A07 (MB-54, phần Crashlytics và Remote Config).
class FirebaseDemoViewModel extends Notifier<FirebaseDemoState> {
  @override
  FirebaseDemoState build() =>
      FirebaseDemoState(crashlyticsEnabled: Env.crashlyticsEnabled, config: ref.watch(remoteConfigProvider));

  /// Ghi một lỗi non-fatal (app không dừng); có user ID và custom key đã gắn lúc đăng nhập.
  Future<void> sendNonFatal() async {
    final crash = ref.read(crashReporterProvider);
    await crash.setKey('demo_action', 'non_fatal');
    await crash.recordNonFatal(const DemoNonFatalError(), StackTrace.current, reason: 'A07 Demo Firebase');
  }

  /// Crash thật để Crashlytics ghi nhận; báo cáo được gửi ở lần mở app kế tiếp.
  Future<void> crash() async {
    final crash = ref.read(crashReporterProvider);
    await crash.setKey('demo_action', 'test_crash');
    crash.testCrash();
  }
}

final firebaseDemoViewModelProvider =
    NotifierProvider.autoDispose<FirebaseDemoViewModel, FirebaseDemoState>(FirebaseDemoViewModel.new);
