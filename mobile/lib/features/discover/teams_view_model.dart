import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/view_state.dart';
import '../suggestions/suggestions_api.dart' show TeamCard;
import 'discover_api.dart';
import '../../core/firebase/analytics_service.dart';
import 'dart:async';

/// Kết quả đã tải của S05: các trang đã ghép lại và trạng thái tải trang kế.
class TeamResults {
  const TeamResults({required this.items, required this.page, required this.hasMore, this.loadingMore = false, this.loadMoreError});

  final List<TeamCard> items;
  final int page;
  final bool hasMore;
  final bool loadingMore;

  /// Lỗi khi tải trang kế: giữ danh sách cũ, hiện banner "Không tải được trang tiếp theo".
  final Object? loadMoreError;

  TeamResults copyWith({List<TeamCard>? items, int? page, bool? hasMore, bool? loadingMore, Object? loadMoreError}) => TeamResults(
        items: items ?? this.items,
        page: page ?? this.page,
        hasMore: hasMore ?? this.hasMore,
        loadingMore: loadingMore ?? this.loadingMore,
        loadMoreError: loadMoreError,
      );
}

class TeamsState {
  const TeamsState({this.query = '', this.filter = const TeamFilter(), this.results = const ViewLoading()});

  final String query;
  final TeamFilter filter;
  final ViewState<TeamResults> results;

  /// Màn trống có nút "Xóa bộ lọc" khi đang lọc hoặc đang tìm.
  bool get canClear => filter.count > 0 || query.isNotEmpty;

  TeamsState copyWith({String? query, TeamFilter? filter, ViewState<TeamResults>? results}) =>
      TeamsState(query: query ?? this.query, filter: filter ?? this.filter, results: results ?? this.results);
}

/// ViewModel của S05 – Nhóm đang tuyển (MB-14, mẫu MVVM MB-48): tìm kiếm, bộ lọc S06, phân trang.
/// Lỗi `round-closed` nằm trong [ViewError]; View tự hiện dialog đợt đã khóa.
class TeamsViewModel extends Notifier<TeamsState> {
  /// Tăng mỗi lần tải lại từ đầu: bỏ kết quả của lượt cũ về muộn (đổi từ khóa / bộ lọc khi đang tải).
  int _generation = 0;

  @override
  TeamsState build() {
    Future.microtask(refresh);
    return const TeamsState();
  }

  DiscoverApi get _api => ref.read(discoverApiProvider);

  /// Tải lại trang 1 với từ khóa và bộ lọc hiện tại (kéo để làm mới, Thử lại).
  Future<void> refresh() => _loadFirstPage();

  Future<void> search(String query) {
    state = state.copyWith(query: query.trim());
    return _loadFirstPage(logSearch: true);
  }

  Future<void> applyFilter(TeamFilter filter) {
    state = state.copyWith(filter: filter);
    return _loadFirstPage(logSearch: true);
  }

  /// "Xóa n bộ lọc" giữ từ khóa; "Xóa bộ lọc" ở màn trống bỏ cả từ khóa.
  Future<void> clearFilters({bool includeQuery = false}) {
    state = state.copyWith(filter: const TeamFilter(), query: includeQuery ? '' : null);
    return _loadFirstPage();
  }

  Future<void> loadMore() async {
    final current = state.results.valueOrNull;
    if (current == null || !current.hasMore || current.loadingMore) return;
    final generation = _generation;
    state = state.copyWith(results: ViewData(current.copyWith(loadingMore: true)));
    try {
      final page = await _api.search(state.query, state.filter, current.page + 1);
      if (!ref.mounted || generation != _generation) return;
      state = state.copyWith(
        results: ViewData(current.copyWith(items: [...current.items, ...page.items], page: page.page, hasMore: page.hasMore)),
      );
    } catch (e) {
      if (!ref.mounted || generation != _generation) return;
      state = state.copyWith(results: ViewData(current.copyWith(loadMoreError: e)));
    }
  }

  /// [logSearch]: người dùng chủ động tìm / lọc → sự kiện `search` (analytics-plan.md); mở màn, làm mới thì không.
  Future<void> _loadFirstPage({bool logSearch = false}) async {
    final generation = ++_generation;
    // Đang có dữ liệu thì giữ trên màn khi tải lại (kéo để làm mới không làm nháy skeleton).
    if (state.results is! ViewData<TeamResults>) state = state.copyWith(results: const ViewLoading());
    try {
      final page = await _api.search(state.query, state.filter, 1);
      if (!ref.mounted || generation != _generation) return;
      if (logSearch) {
        unawaited(ref.read(analyticsServiceProvider).logSearch(state.query, page.items.length, filter: state.filter.count > 0 ? 'filtered' : null));
      }
      state = state.copyWith(
        results: page.items.isEmpty
            ? const ViewEmpty()
            : ViewData(TeamResults(items: page.items, page: page.page, hasMore: page.hasMore)),
      );
    } catch (e) {
      if (!ref.mounted || generation != _generation) return;
      state = state.copyWith(results: ViewError(e));
    }
  }
}

final teamsViewModelProvider = NotifierProvider.autoDispose<TeamsViewModel, TeamsState>(TeamsViewModel.new);
