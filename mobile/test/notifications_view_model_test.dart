import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/view_state.dart';
import 'package:exe_team_matching/features/notifications/notifications_api.dart';
import 'package:exe_team_matching/features/notifications/notifications_view_model.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

AppNotification _n(String id, {bool read = false, String? link}) => AppNotification(
      id: id,
      type: 'TEAM',
      title: 'Tiêu đề $id',
      content: 'Nội dung',
      isRead: read,
      createdAt: DateTime.utc(2026, 11, 4, 8),
      linkUrl: link,
    );

class _FakeApi extends NotificationsApi {
  _FakeApi() : super(Dio());

  List<List<AppNotification>> pages = const [];
  Object? error;
  Object? actionError;
  int unread = 0;
  final calls = <String>[];

  @override
  Future<NotificationPage> list({int page = 1}) async {
    calls.add('list $page');
    if (error != null) throw error!;
    return NotificationPage(items: page <= pages.length ? pages[page - 1] : const [], totalPages: pages.length);
  }

  @override
  Future<void> read(String id) async {
    calls.add('read $id');
    if (actionError != null) throw actionError!;
  }

  @override
  Future<void> readAll() async {
    calls.add('readAll');
    if (actionError != null) throw actionError!;
  }

  @override
  Future<int> unreadCount() async {
    if (error != null) throw error!;
    return unread;
  }
}

/// MB-49 – ViewModel của A06 và badge thông báo, không dựng widget.
void main() {
  late _FakeApi api;
  late ProviderContainer container;

  setUp(() {
    api = _FakeApi();
    container = ProviderContainer(overrides: [notificationsApiProvider.overrideWithValue(api)]);
    addTearDown(container.dispose);
  });

  Future<void> settle() => Future<void>.delayed(Duration.zero);

  NotificationsViewModel vm([String role = '/sv']) => container.read(notificationsViewModelProvider(role).notifier);
  NotificationsState state([String role = '/sv']) => container.read(notificationsViewModelProvider(role));
  void open([String role = '/sv']) => container.listen(notificationsViewModelProvider(role), (_, _) {});

  test('mở màn: đang tải rồi có dữ liệu', () async {
    api.pages = [
      [_n('a'), _n('b', read: true)],
    ];
    open();
    expect(state().list, isA<ViewLoading<NotificationList>>());
    await settle();
    final list = state().list.valueOrNull!;
    expect((list.items.length, list.page, list.hasUnread), (2, 1, true));
  });

  test('trống và lỗi; làm mới im lặng lỗi thì giữ dữ liệu cũ', () async {
    open();
    await settle();
    expect(state().list, isA<ViewEmpty<NotificationList>>());

    api.error = Exception('500');
    await vm().refresh();
    expect(state().list, isA<ViewError<NotificationList>>());

    api
      ..error = null
      ..pages = [
        [_n('a')],
      ];
    await vm().refresh();
    api.error = Exception('500');
    await vm().refresh(silent: true);
    expect(state().list.valueOrNull?.items.single.id, 'a');
  });

  test('phân trang: tải trang 2, hết trang thì dừng; làm mới im lặng bỏ qua khi đã quá trang 1', () async {
    api.pages = [
      [_n('a')],
      [_n('b')],
    ];
    open();
    await settle();
    expect(await vm().loadMore(), isA<NotificationDone>());
    expect(state().list.valueOrNull!.items.map((n) => n.id), ['a', 'b']);
    await vm().loadMore();
    await vm().refresh(silent: true);
    expect(api.calls, ['list 1', 'list 2']);
  });

  test('tải trang kế lỗi → câu cho SnackBar, giữ danh sách', () async {
    api.pages = [
      [_n('a')],
      [_n('b')],
    ];
    open();
    await settle();
    api.error = Exception('500');
    expect(await vm().loadMore(), isA<NotificationFailed>());
    expect(state().list.valueOrNull!.loadingMore, isFalse);
  });

  test('bấm thông báo: đánh dấu đã đọc và trả route theo vai trò', () async {
    api.pages = [
      [_n('a', link: 'app://workspaces/w9'), _n('b')],
    ];
    open('/gv');
    await settle();
    final outcome = await vm('/gv').open(state('/gv').list.valueOrNull!.items.first);
    expect((outcome as NotificationOpen).route, '/gv/du-an/w9');
    expect(state('/gv').list.valueOrNull!.items.first.isRead, isTrue);
    expect(await vm('/gv').open(_n('c', read: true)), isA<NotificationDone>());
    expect(api.calls.where((c) => c.startsWith('read')), ['read a']);
  });

  test('đọc tất cả; lỗi trả câu', () async {
    api.pages = [
      [_n('a'), _n('b')],
    ];
    open();
    await settle();
    await vm().readAll();
    expect(state().list.valueOrNull!.hasUnread, isFalse);
    api.actionError = Exception('500');
    expect(await vm().readAll(), isA<NotificationFailed>());
    expect(state().markingAll, isFalse);
  });

  test('route theo link', () {
    final sv = vm();
    expect(sv.routeFor(null), isNull);
    expect(sv.routeFor('https://x'), isNull);
    expect(sv.routeFor('app://invitations'), '/sv/loi-moi');
    expect(sv.routeFor('app://meeting-schedule'), '/sv/du-an/lich-hop');
    expect(vm('/mentor').routeFor('app://change-requests'), '/mentor/nhom');
    expect(vm('/gv').routeFor('app://lecturer'), '/gv/de-tai');
  });

  test('badge: đếm chưa đọc, lỗi giữ số cũ', () async {
    container.listen(unreadCountViewModelProvider, (_, _) {});
    api.unread = 3;
    await container.read(unreadCountViewModelProvider.notifier).load();
    expect(container.read(unreadCountViewModelProvider), 3);
    api.error = Exception('500');
    await container.read(unreadCountViewModelProvider.notifier).load();
    expect(container.read(unreadCountViewModelProvider), 3);
  });
}
