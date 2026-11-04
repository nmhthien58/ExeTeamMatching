import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'collab_hub.dart';

/// ViewModel của [RealtimeStatusBanner] (MB-38, MVVM MB-49): giữ kết nối hub khi còn màn workspace đang mở
/// (`acquire` lúc có người nghe, thả khi màn cuối đóng) và đưa trạng thái kết nối ra state.
class RealtimeStatusViewModel extends Notifier<HubStatus> {
  @override
  HubStatus build() {
    final hub = ref.read(collabHubProvider);
    final release = hub.acquire();
    void onStatus() => state = hub.status.value;
    hub.status.addListener(onStatus);
    ref.onDispose(() {
      hub.status.removeListener(onStatus);
      release();
    });
    return hub.status.value;
  }

  /// "Thử lại" khi mất hẳn kết nối.
  void retry() => ref.read(collabHubProvider).retry();
}

final realtimeStatusViewModelProvider = NotifierProvider.autoDispose<RealtimeStatusViewModel, HubStatus>(RealtimeStatusViewModel.new);
