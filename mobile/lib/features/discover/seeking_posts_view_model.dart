import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_exception.dart';
import '../../core/view_state.dart';
import 'paged_list.dart';
import 'posts_api.dart';

class SeekingPostsState {
  const SeekingPostsState({this.specializationId, this.results = const ViewLoading(), this.invited = const {}});

  final int? specializationId;
  final ViewState<PagedList<SeekingPost>> results;

  /// Sinh viên đã được mời trong phiên xem này (thẻ chuyển "Đã mời").
  final Set<String> invited;

  SeekingPostsState copyWith({int? Function()? specializationId, ViewState<PagedList<SeekingPost>>? results, Set<String>? invited}) => SeekingPostsState(
    specializationId: specializationId == null ? this.specializationId : specializationId(),
    results: results ?? this.results,
    invited: invited ?? this.invited,
  );
}

/// ViewModel của S10 – Sinh viên tìm nhóm (MB-17, MVVM MB-49): lọc chuyên ngành, phân trang, đánh dấu đã mời.
class SeekingPostsViewModel extends Notifier<SeekingPostsState> {
  bool _busy = false;

  @override
  SeekingPostsState build() {
    Future.microtask(() => _load(reset: true));
    return const SeekingPostsState();
  }

  /// Kéo để làm mới: tải lại cả bài của mình (thẻ S11).
  Future<void> refresh() {
    ref.invalidate(mySeekingPostProvider);
    return _load(reset: true);
  }

  Future<void> retry() => _load(reset: true);

  Future<void> loadMore() => _load();

  Future<void> selectSpecialization(int? id) {
    state = state.copyWith(specializationId: () => id);
    return _load(reset: true);
  }

  void markInvited(String studentId) => state = state.copyWith(invited: {...state.invited, studentId});

  Future<void> _load({bool reset = false}) async {
    if (_busy) return;
    _busy = true;
    final current = state.results.valueOrNull;
    // Có danh sách thì giữ trên màn, chỉ hiện vòng xoay ở cuối; chưa có thì skeleton.
    state = state.copyWith(results: current == null ? const ViewLoading() : ViewData(current.copyWith(loading: true)));
    try {
      final page = await ref.read(postsApiProvider).seeking(specializationId: state.specializationId, page: reset || current == null ? 1 : current.page + 1);
      if (!ref.mounted) return;
      final items = [if (!reset && current != null) ...current.items, ...page.items];
      state = state.copyWith(
        results: items.isEmpty ? const ViewEmpty() : ViewData(PagedList(items: items, page: page.page, hasMore: page.hasMore)),
      );
    } on ApiException catch (e) {
      if (!ref.mounted) return;
      state = state.copyWith(results: current == null ? ViewError(e) : ViewData(current.copyWith(loading: false, loadMoreError: e)));
    } finally {
      _busy = false;
    }
  }
}

final seekingPostsViewModelProvider = NotifierProvider.autoDispose<SeekingPostsViewModel, SeekingPostsState>(SeekingPostsViewModel.new);
