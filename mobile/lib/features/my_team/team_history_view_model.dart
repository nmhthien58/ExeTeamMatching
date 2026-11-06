import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_exception.dart';
import '../../core/view_state.dart';
import '../discover/paged_list.dart';
import 'team_history_screen.dart' show AuditLogEntry, teamHistoryApiProvider;

/// ViewModel của S22 – Lịch sử hoạt động nhóm (MB-26, MVVM MB-49), theo id nhóm: phân trang, mới nhất trước.
class TeamHistoryViewModel extends Notifier<ViewState<PagedList<AuditLogEntry>>> {
  TeamHistoryViewModel(this.teamId);

  final String teamId;
  bool _busy = false;

  @override
  ViewState<PagedList<AuditLogEntry>> build() {
    Future.microtask(refresh);
    return const ViewLoading();
  }

  Future<void> refresh() => _load(reset: true);

  Future<void> loadMore() => _load();

  Future<void> _load({bool reset = false}) async {
    if (_busy) return;
    _busy = true;
    final current = state.valueOrNull;
    // Có danh sách thì giữ trên màn, chỉ hiện vòng xoay ở cuối; chưa có thì skeleton.
    state = current == null ? const ViewLoading() : ViewData(current.copyWith(loading: true));
    try {
      final next = reset || current == null ? 1 : current.page + 1;
      final (items, hasMore) = await ref.read(teamHistoryApiProvider).page(teamId, next);
      if (!ref.mounted) return;
      final all = [if (!reset && current != null) ...current.items, ...items];
      state = all.isEmpty ? const ViewEmpty() : ViewData(PagedList(items: all, page: next, hasMore: hasMore));
    } on ApiException catch (e) {
      if (!ref.mounted) return;
      state = current == null ? ViewError(e) : ViewData(current.copyWith(loading: false, loadMoreError: e));
    } finally {
      _busy = false;
    }
  }
}

final teamHistoryViewModelProvider = NotifierProvider.autoDispose.family<TeamHistoryViewModel, ViewState<PagedList<AuditLogEntry>>, String>(
  TeamHistoryViewModel.new,
);
