import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/ai/ai_service.dart';
import '../../core/config/env.dart';
import '../../app/navigation.dart';
import '../../core/auth/session.dart';
import '../../core/firebase/crash_reporter.dart';
import '../../core/providers.dart';
import '../../core/firebase/remote_config_service.dart';
import '../../core/view_state.dart';

/// Lỗi thử gửi lên Crashlytics từ mục Demo Firebase (Lab 3 §6: một lỗi non-fatal và một crash thử).
class DemoNonFatalError implements Exception {
  const DemoNonFatalError();

  @override
  String toString() => 'DemoNonFatalError: lỗi thử từ Cài đặt → Demo Firebase';
}

/// Đường dẫn lối vào từ mục Demo Firebase, theo khung của người dùng.
typedef FirebaseDemoLinks = ({String files, String notifications});

/// Sinh viên xuất và tải file ở Dự án; giảng viên, mentor chọn nhóm trước rồi mới vào Dự án.
FirebaseDemoLinks? firebaseDemoLinksFor(UserInfo? user) {
  final shell = user == null ? null : shellOf(user);
  final home = shellHome[shell];
  if (home == null) return null;
  return (files: shell == Shell.student ? '$home/du-an' : '$home/nhom', notifications: '$home/thong-bao');
}

/// Dữ liệu mục Demo Firebase: Crashlytics có đang gửi không, giá trị Remote Config đang dùng, các lối vào.
class FirebaseDemoState {
  const FirebaseDemoState({required this.crashlyticsEnabled, required this.config, this.links});

  final bool crashlyticsEnabled;
  final AppRemoteConfig config;
  final FirebaseDemoLinks? links;
}

/// ViewModel của mục Demo Firebase trong A07 (MB-54, phần Crashlytics và Remote Config).
class FirebaseDemoViewModel extends Notifier<FirebaseDemoState> {
  @override
  FirebaseDemoState build() => FirebaseDemoState(
    crashlyticsEnabled: Env.crashlyticsEnabled,
    config: ref.watch(remoteConfigProvider),
    links: firebaseDemoLinksFor(ref.watch(authControllerProvider).value),
  );

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
    await crash.testCrash();
  }
}

final firebaseDemoViewModelProvider =
    NotifierProvider.autoDispose<FirebaseDemoViewModel, FirebaseDemoState>(FirebaseDemoViewModel.new);

/// Tag mẫu cho nút "Thử AI" (S09 thật dùng danh mục lĩnh vực từ API – MB-56).
const demoAiTags = ['EdTech', 'FinTech', 'HealthTech', 'AgriTech', 'E-commerce', 'Du lịch', 'Môi trường'];

/// ViewModel của nút "Thử AI" trong mục Demo Firebase (MB-55). `null` = chưa thử;
/// `ViewError(AiException)` mang mã lỗi để chụp bằng chứng.
class AiDemoViewModel extends Notifier<ViewState<IdeaDraft>?> {
  int _generation = 0;

  @override
  ViewState<IdeaDraft>? build() => null;

  Future<void> suggest(String description) async {
    if (description.trim().isEmpty) return;
    final generation = ++_generation;
    state = const ViewLoading();
    ViewState<IdeaDraft> result;
    try {
      final draft = await ref
          .read(aiServiceProvider)
          .suggestIdea(description: description, allowedTags: demoAiTags);
      result = ViewData(draft);
    } on AiException catch (e) {
      result = ViewError(e);
    }
    if (ref.mounted && generation == _generation) state = result;
  }
}

final aiDemoViewModelProvider =
    NotifierProvider.autoDispose<AiDemoViewModel, ViewState<IdeaDraft>?>(AiDemoViewModel.new);
