import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/view_state.dart';
import '../workspace/workspace_api.dart' show myWorkspacesProvider;
import 'messages_api.dart';

/// Hộp thư của cố vấn: kênh Cố vấn của mọi nhóm đã lọc và sắp theo tin mới nhất, tên nhóm theo workspace.
class AdvisorInbox {
  const AdvisorInbox({required this.channels, required this.teamNames});

  /// Lỗi / đang tải của danh sách kênh; [ViewEmpty] khi không có kênh nào sau khi lọc.
  final ViewState<List<Channel>> channels;
  final Map<String, String> teamNames;

  String titleOf(Channel c) => '${teamNames[c.workspaceId] ?? 'Nhóm'} · ${c.label}';
}

class AdvisorConversationsState {
  const AdvisorConversationsState({this.needsReplyOnly = false, this.waiting = const {}, this.inbox = const ViewLoading()});

  /// Bộ lọc "Cần trả lời".
  final bool needsReplyOnly;

  /// Kênh có câu hỏi chờ trả lời.
  final Set<String> waiting;

  /// Trạng thái tải danh sách workspace (dự án đã ghép).
  final ViewState<AdvisorInbox> inbox;

  String get emptyTitle => needsReplyOnly ? 'Không còn câu hỏi chờ trả lời' : 'Bạn chưa được ghép với nhóm nào';
}

/// ViewModel của C02 giảng viên / mentor (MB-39, MVVM MB-49): gộp kênh của mọi dự án, lọc "Tất cả / Cần trả lời",
/// sắp theo tin mới nhất. Dữ liệu lấy từ các provider đọc (`myWorkspacesProvider`, `channelsProvider`,
/// `openQuestionChannelsProvider`) nên tự tính lại khi chúng đổi; bộ lọc được giữ qua các lần tính lại.
class AdvisorConversationsViewModel extends Notifier<AdvisorConversationsState> {
  bool _needsReplyOnly = false;

  @override
  AdvisorConversationsState build() {
    final waiting = ref.watch(openQuestionChannelsProvider).value ?? const <String>{};
    final workspaces = ref.watch(myWorkspacesProvider);
    final ViewState<AdvisorInbox> inbox;
    if (workspaces.hasError) {
      inbox = ViewError(workspaces.error!);
    } else if (!workspaces.hasValue) {
      inbox = const ViewLoading();
    } else {
      final list = workspaces.requireValue;
      final perWorkspace = [for (final w in list) ref.watch(channelsProvider(w.id))];
      final error = perWorkspace.where((c) => c.hasError).firstOrNull?.error;
      final channels = [for (final c in perWorkspace) ...?c.value].where((c) => !_needsReplyOnly || waiting.contains(c.id)).toList()
        ..sort((a, b) => (b.lastMessage?.createdAt ?? DateTime(0)).compareTo(a.lastMessage?.createdAt ?? DateTime(0)));
      inbox = ViewData(
        AdvisorInbox(
          channels: error != null
              ? ViewError(error)
              : perWorkspace.any((c) => c.isLoading)
              ? const ViewLoading()
              : channels.isEmpty
              ? const ViewEmpty()
              : ViewData(channels),
          teamNames: {for (final w in list) w.id: w.teamName},
        ),
      );
    }
    return AdvisorConversationsState(needsReplyOnly: _needsReplyOnly, waiting: waiting, inbox: inbox);
  }

  void setNeedsReplyOnly(bool value) {
    _needsReplyOnly = value;
    ref.invalidateSelf();
  }

  /// Thử lại khi tải danh sách dự án / kênh lỗi.
  void retry() => ref.invalidate(myWorkspacesProvider);

  /// Kéo để làm mới: tải lại câu hỏi chờ và kênh của từng dự án.
  Future<void> refresh() async {
    ref.invalidate(openQuestionChannelsProvider);
    for (final w in ref.read(myWorkspacesProvider).value ?? const []) {
      ref.invalidate(channelsProvider(w.id));
    }
  }
}

final advisorConversationsViewModelProvider = NotifierProvider.autoDispose<AdvisorConversationsViewModel, AdvisorConversationsState>(
  AdvisorConversationsViewModel.new,
);
