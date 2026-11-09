import 'dart:async';

import 'package:firebase_remote_config/firebase_remote_config.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'crash_reporter.dart';

/// Giá trị Remote Config hiện tại (MB-51), bất biến. Mặc định trùng `firebase/remote-config-defaults.json`
/// ở gốc repo (test `remote_config_service_test.dart` so sánh hai bên).
@immutable
class AppRemoteConfig {
  const AppRemoteConfig({
    this.suggestionsMaxItems = defaultSuggestionsMaxItems,
    this.aiIdeaEnabled = false,
    this.aiModel = defaultAiModel,
    this.aiPrompt = defaultAiPrompt,
  });

  static const defaultSuggestionsMaxItems = 5;
  static const defaultAiModel = 'gemini-2.5-flash';
  static const defaultAiPrompt =
      'Bạn là trợ lý giúp sinh viên môn khởi nghiệp EXE (Đại học FPT) viết ý tưởng dự án. Từ mô tả ngắn của '
      'sinh viên, viết bản nháp gồm: title (tên ý tưởng ≤ 60 ký tự), problem (vấn đề), solution (giải pháp), '
      'targetCustomer (khách hàng mục tiêu), tags (chọn tối đa 3 tag trong danh sách được cho). Viết tiếng Việt, '
      'ngắn gọn, mỗi trường 1–3 câu, không bịa số liệu.';

  static const defaults = AppRemoteConfig();

  /// Tên tham số trên Firebase Console.
  static const keySuggestionsMaxItems = 'suggestions_max_items';
  static const keyAiIdeaEnabled = 'ai_idea_enabled';
  static const keyAiModel = 'ai_model';
  static const keyAiPrompt = 'ai_prompt';

  /// S07: số nhóm gợi ý tối đa, giới hạn 1–20 để giá trị sai trên Console không làm hỏng màn.
  final int suggestionsMaxItems;

  /// S09: hiện nút "Gợi ý ý tưởng bằng AI" (MB-55).
  final bool aiIdeaEnabled;

  /// Mô hình Gemini cho Firebase AI Logic (MB-55).
  final String aiModel;

  /// Prompt hệ thống cho gợi ý ý tưởng (MB-55).
  final String aiPrompt;

  Map<String, Object> toMap() => {
    keySuggestionsMaxItems: suggestionsMaxItems,
    keyAiIdeaEnabled: aiIdeaEnabled,
    keyAiModel: aiModel,
    keyAiPrompt: aiPrompt,
  };

  /// Đọc từ giá trị thô; giá trị trống hoặc ngoài khoảng thì dùng mặc định.
  factory AppRemoteConfig.from({
    required int suggestionsMaxItems,
    required bool aiIdeaEnabled,
    required String aiModel,
    required String aiPrompt,
  }) => AppRemoteConfig(
    suggestionsMaxItems: suggestionsMaxItems < 1 ? defaultSuggestionsMaxItems : suggestionsMaxItems.clamp(1, 20),
    aiIdeaEnabled: aiIdeaEnabled,
    aiModel: aiModel.trim().isEmpty ? defaultAiModel : aiModel.trim(),
    aiPrompt: aiPrompt.trim().isEmpty ? defaultAiPrompt : aiPrompt,
  );

  @override
  bool operator ==(Object other) =>
      other is AppRemoteConfig &&
      other.suggestionsMaxItems == suggestionsMaxItems &&
      other.aiIdeaEnabled == aiIdeaEnabled &&
      other.aiModel == aiModel &&
      other.aiPrompt == aiPrompt;

  @override
  int get hashCode => Object.hash(suggestionsMaxItems, aiIdeaEnabled, aiModel, aiPrompt);

  @override
  String toString() =>
      'AppRemoteConfig(suggestionsMaxItems: $suggestionsMaxItems, aiIdeaEnabled: $aiIdeaEnabled, aiModel: $aiModel)';
}

/// Firebase Remote Config (MB-51). Widget và ViewModel đọc qua [remoteConfigProvider], không gọi SDK.
abstract interface class RemoteConfigService {
  /// Đặt mặc định rồi `fetchAndActivate` trong [timeout]. Lỗi hoặc mất mạng: giữ giá trị mặc định / lần trước,
  /// ghi non-fatal. Không ném lỗi.
  Future<void> initialize({Duration timeout});

  /// Giá trị đang hiệu lực; chưa khởi tạo được thì là [AppRemoteConfig.defaults].
  AppRemoteConfig get current;

  /// Giá trị mới mỗi khi Console publish thay đổi (real-time `onConfigUpdated`), đã activate.
  Stream<AppRemoteConfig> get updates;
}

class FirebaseRemoteConfigService implements RemoteConfigService {
  /// [remoteConfig] để test truyền bản giả; mặc định lấy instance khi gọi, không lấy lúc tạo.
  FirebaseRemoteConfigService({required this._crash, FirebaseRemoteConfig Function()? remoteConfig})
    : _remoteConfig = remoteConfig ?? (() => FirebaseRemoteConfig.instance);

  final CrashReporter _crash;
  final FirebaseRemoteConfig Function() _remoteConfig;

  static const defaultTimeout = Duration(seconds: 5);

  @override
  Future<void> initialize({Duration timeout = defaultTimeout}) async {
    try {
      final rc = _remoteConfig();
      await rc.setDefaults(AppRemoteConfig.defaults.toMap());
      await rc.setConfigSettings(
        RemoteConfigSettings(
          fetchTimeout: timeout,
          // Bản debug tải lại thường xuyên để thử trên Console; bản thật dùng real-time update nên 1 giờ là đủ.
          minimumFetchInterval: kDebugMode ? const Duration(minutes: 1) : const Duration(hours: 1),
        ),
      );
      await rc.fetchAndActivate().timeout(timeout);
    } catch (e, s) {
      await _crash.recordNonFatal(e, s, reason: 'remote_config_fetch');
    }
  }

  @override
  AppRemoteConfig get current {
    try {
      final rc = _remoteConfig();
      return AppRemoteConfig.from(
        suggestionsMaxItems: rc.getInt(AppRemoteConfig.keySuggestionsMaxItems),
        aiIdeaEnabled: rc.getBool(AppRemoteConfig.keyAiIdeaEnabled),
        aiModel: rc.getString(AppRemoteConfig.keyAiModel),
        aiPrompt: rc.getString(AppRemoteConfig.keyAiPrompt),
      );
    } catch (_) {
      return AppRemoteConfig.defaults;
    }
  }

  @override
  Stream<AppRemoteConfig> get updates {
    final Stream<RemoteConfigUpdate> source;
    try {
      source = _remoteConfig().onConfigUpdated;
    } catch (e, s) {
      unawaited(_crash.recordNonFatal(e, s, reason: 'remote_config_listen'));
      return const Stream.empty();
    }
    return source
        .asyncMap((_) async {
          await _remoteConfig().activate();
          return current;
        })
        .handleError((Object e, StackTrace s) {
          // Mất mạng khi đang nghe: bỏ qua lần cập nhật này, giữ giá trị cũ.
          unawaited(_crash.recordNonFatal(e, s, reason: 'remote_config_update'));
        });
  }
}

final remoteConfigServiceProvider = Provider<RemoteConfigService>(
  (ref) => FirebaseRemoteConfigService(crash: ref.watch(crashReporterProvider)),
);

/// Giá trị Remote Config hiện tại; tự cập nhật khi Console publish (không cần build lại app).
/// Ví dụ S07: `ref.watch(remoteConfigProvider).suggestionsMaxItems`.
final remoteConfigProvider = NotifierProvider<RemoteConfigNotifier, AppRemoteConfig>(RemoteConfigNotifier.new);

class RemoteConfigNotifier extends Notifier<AppRemoteConfig> {
  @override
  AppRemoteConfig build() {
    final service = ref.watch(remoteConfigServiceProvider);
    final subscription = service.updates.listen((value) {
      if (ref.mounted) state = value;
    });
    ref.onDispose(subscription.cancel);
    return service.current;
  }
}
