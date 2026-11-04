import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/view_state.dart';
import 'notifications_api.dart';

/// Danh sách A06 đã tải: các trang đã ghép và trạng thái tải trang kế.
class NotificationList {
  const NotificationList({required this.items, required this.page, required this.totalPages, this.loadingMore = false});

  final List<AppNotification> items;
  final int page;
  final int totalPages;
  final bool loadingMore;

  bool get hasUnread => items.any((x) => !x.isRead);

  NotificationList copyWith({List<AppNotification>? items, int? page, int? totalPages, bool? loadingMore}) => NotificationList(
        items: items ?? this.items,
        page: page ?? this.page,
        totalPages: totalPages ?? this.totalPages,
        loadingMore: loadingMore ?? this.loadingMore,
      );
}

class NotificationsState {
  const NotificationsState({this.list = const ViewLoading(), this.markingAll = false});

  final ViewState<NotificationList> list;
  final bool markingAll;

  NotificationsState copyWith({ViewState<NotificationList>? list, bool? markingAll}) =>
      NotificationsState(list: list ?? this.list, markingAll: markingAll ?? this.markingAll);
}

/// Kết quả một lần của lệnh A06: lỗi hiện SnackBar; mở thông báo trả route cần đi tới.
sealed class NotificationOutcome {
  const NotificationOutcome();
}

final class NotificationDone extends NotificationOutcome {
  const NotificationDone();
}

final class NotificationOpen extends NotificationOutcome {
  const NotificationOpen(this.route);

  final String route;
}

final class NotificationFailed extends NotificationOutcome {
  const NotificationFailed(this.message);

  final String message;
}

/// ViewModel của A06 – Thông báo (MB-49): tải, làm mới im lặng (View gọi mỗi 30 giây), phân trang,
/// đánh dấu đã đọc, đổi link `app://` thành route theo vai trò.
class NotificationsViewModel extends Notifier<NotificationsState> {
  NotificationsViewModel(this.roleRoot);

  /// `/sv`, `/gv` hoặc `/mentor`.
  final String roleRoot;

  @override
  NotificationsState build() {
    Future.microtask(refresh);
    return const NotificationsState();
  }

  NotificationsApi get _api => ref.read(notificationsApiProvider);

  /// [silent]: lượt tự làm mới – bỏ qua khi đang tải hoặc đã cuộn quá trang 1, lỗi thì giữ danh sách cũ.
  Future<void> refresh({bool silent = false}) async {
    final current = state.list;
    if (silent) {
      final loaded = current.valueOrNull;
      if (current is ViewLoading || (loaded != null && (loaded.page > 1 || loaded.loadingMore))) return;
    } else {
      state = state.copyWith(list: const ViewLoading());
    }
    try {
      final result = await _api.list();
      if (!ref.mounted) return;
      state = state.copyWith(
        list: result.items.isEmpty
            ? const ViewEmpty()
            : ViewData(NotificationList(items: result.items, page: 1, totalPages: result.totalPages)),
      );
    } catch (error) {
      if (ref.mounted && !silent) state = state.copyWith(list: ViewError(error.apiError));
    }
  }

  /// Tải trang kế khi cuộn gần cuối; lỗi trả câu cho SnackBar.
  Future<NotificationOutcome> loadMore() async {
    final current = state.list.valueOrNull;
    if (current == null || current.loadingMore || current.page >= current.totalPages) return const NotificationDone();
    state = state.copyWith(list: ViewData(current.copyWith(loadingMore: true)));
    try {
      final result = await _api.list(page: current.page + 1);
      if (!ref.mounted) return const NotificationDone();
      _update((list) => list.copyWith(
            items: [...list.items, ...result.items],
            page: list.page + 1,
            totalPages: result.totalPages,
            loadingMore: false,
          ));
      return const NotificationDone();
    } catch (error) {
      if (ref.mounted) _update((list) => list.copyWith(loadingMore: false));
      return NotificationFailed(error.apiError.title);
    }
  }

  /// Bấm một thông báo: đánh dấu đã đọc (nếu chưa) rồi trả route cần mở (nếu có).
  Future<NotificationOutcome> open(AppNotification item) async {
    try {
      if (!item.isRead) {
        await _api.read(item.id);
        if (ref.mounted) _update((list) => list.copyWith(items: [for (final row in list.items) row.id == item.id ? row.read() : row]));
      }
      final route = routeFor(item.linkUrl);
      return route == null ? const NotificationDone() : NotificationOpen(route);
    } catch (error) {
      return NotificationFailed(error.apiError.title);
    }
  }

  Future<NotificationOutcome> readAll() async {
    if (state.markingAll) return const NotificationDone();
    state = state.copyWith(markingAll: true);
    try {
      await _api.readAll();
      if (ref.mounted) _update((list) => list.copyWith(items: [for (final item in list.items) item.read()]));
      return const NotificationDone();
    } catch (error) {
      return NotificationFailed(error.apiError.title);
    } finally {
      if (ref.mounted) state = state.copyWith(markingAll: false);
    }
  }

  /// Link `app://<host>/<id>` → route của vai trò; null khi không có link hoặc không phải `app://`.
  String? routeFor(String? link) {
    if (link == null) return null;
    final uri = Uri.tryParse(link);
    if (uri == null || uri.scheme != 'app') return null;
    return switch (uri.host) {
      'teams' => '$roleRoot/nhom',
      'invitations' when roleRoot == '/sv' => '/sv/loi-moi',
      'invitations' => roleRoot,
      'change-requests' when roleRoot == '/sv' => '/sv/yeu-cau-doi-nhom',
      'change-requests' => '$roleRoot/nhom',
      'workspaces' when roleRoot == '/sv' => '/sv/du-an',
      'workspaces' when uri.pathSegments.isNotEmpty => '$roleRoot/du-an/${uri.pathSegments.first}',
      'meeting-schedule' => roleRoot == '/sv'
          ? '/sv/du-an/lich-hop'
          : roleRoot == '/gv'
              ? '/gv/lich'
              : roleRoot,
      'lecturer' => '$roleRoot/de-tai',
      _ => roleRoot,
    };
  }

  void _update(NotificationList Function(NotificationList) change) {
    final current = state.list.valueOrNull;
    if (current != null) state = state.copyWith(list: ViewData(change(current)));
  }
}

final notificationsViewModelProvider =
    NotifierProvider.autoDispose.family<NotificationsViewModel, NotificationsState, String>(NotificationsViewModel.new);

/// ViewModel của badge A06 trên thanh điều hướng: số thông báo chưa đọc (View gọi [load] mỗi 30 giây).
class UnreadCountViewModel extends Notifier<int> {
  @override
  int build() => 0;

  Future<void> load() async {
    try {
      final count = await ref.read(notificationsApiProvider).unreadCount();
      if (ref.mounted) state = count;
    } catch (_) {
      // Giữ badge gần nhất; màn A06 hiển thị lỗi và nút thử lại.
    }
  }
}

final unreadCountViewModelProvider = NotifierProvider.autoDispose<UnreadCountViewModel, int>(UnreadCountViewModel.new);
