import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/notifications/notifications_api.dart';
import 'package:exe_team_matching/features/notifications/notifications_screen.dart';
import 'package:exe_team_matching/features/notifications/notification_tab_icon.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

class FakeNotificationsApi extends NotificationsApi {
  FakeNotificationsApi(this.items, {this.secondPage = const []}) : super(Dio());

  final List<AppNotification> items;
  final List<AppNotification> secondPage;
  final List<int> requestedPages = [];
  int readAllCalls = 0;
  int readCalls = 0;
  bool failRead = false;
  bool failReadAll = false;
  bool failList = false;
  int unread = 0;

  @override
  Future<NotificationPage> list({int page = 1}) async {
    requestedPages.add(page);
    if (failList) throw Exception('Không tải được thông báo');
    return NotificationPage(
      items: page == 1 ? items : secondPage,
      totalPages: secondPage.isEmpty ? 1 : 2,
    );
  }

  @override
  Future<void> readAll() async {
    readAllCalls++;
    if (failReadAll) throw Exception('Không thể đánh dấu tất cả đã đọc');
  }

  @override
  Future<void> read(String id) async {
    readCalls++;
    if (failRead) throw Exception('Không thể đánh dấu đã đọc');
  }

  @override
  Future<int> unreadCount() async => unread;
}

void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  testWidgets('A06 hiển thị trạng thái trống', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          notificationsApiProvider.overrideWithValue(FakeNotificationsApi([])),
        ],
        child: MaterialApp(
          theme: AppTheme.light(),
          home: const NotificationsScreen(roleRoot: '/sv'),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Chưa có thông báo'), findsOneWidget);
  });

  testWidgets('A06 thử lại sau lỗi tải ban đầu', (tester) async {
    final api = FakeNotificationsApi([
      AppNotification(
        id: 'n-1',
        type: 'InvitationSent',
        title: 'Lời mời thử',
        content: 'Nhóm A mời bạn',
        isRead: false,
        createdAt: DateTime.now(),
      ),
    ])..failList = true;
    await tester.pumpWidget(
      ProviderScope(
        overrides: [notificationsApiProvider.overrideWithValue(api)],
        child: MaterialApp(
          theme: AppTheme.light(),
          home: const NotificationsScreen(roleRoot: '/sv'),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Thử lại'), findsOneWidget);
    api.failList = false;
    await tester.tap(find.text('Thử lại'));
    await tester.pumpAndSettle();
    expect(find.text('Lời mời thử'), findsOneWidget);
    expect(api.requestedPages, [1, 1]);
  });

  testWidgets('A06 cuộn tải trang tiếp theo đúng một lần', (tester) async {
    final api = FakeNotificationsApi(
      [
        for (var i = 0; i < 15; i++)
          AppNotification(
            id: 'n-$i',
            type: 'InvitationSent',
            title: 'Thông báo $i',
            content: 'Nội dung $i',
            isRead: true,
            createdAt: DateTime.now(),
          ),
      ],
      secondPage: [
        AppNotification(
          id: 'n-15',
          type: 'InvitationSent',
          title: 'Thông báo trang 2',
          content: 'Nội dung trang 2',
          isRead: true,
          createdAt: DateTime.now(),
        ),
      ],
    );
    await tester.pumpWidget(
      ProviderScope(
        overrides: [notificationsApiProvider.overrideWithValue(api)],
        child: MaterialApp(
          theme: AppTheme.light(),
          home: const NotificationsScreen(roleRoot: '/sv'),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.drag(find.byType(ListView), const Offset(0, -2000));
    await tester.pumpAndSettle();
    expect(find.text('Thông báo trang 2'), findsOneWidget);
    expect(api.requestedPages, [1, 2]);
  });

  testWidgets('A06 nhóm theo ngày và đánh dấu tất cả đã đọc', (tester) async {
    final api = FakeNotificationsApi([
      AppNotification(
        id: '1',
        type: 'InvitationSent',
        title: 'Lời mời vào nhóm',
        content: 'Nhóm A mời bạn',
        isRead: false,
        createdAt: DateTime.now(),
      ),
    ]);
    await tester.pumpWidget(
      ProviderScope(
        overrides: [notificationsApiProvider.overrideWithValue(api)],
        child: MaterialApp(
          theme: AppTheme.light(),
          home: const NotificationsScreen(roleRoot: '/sv'),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Hôm nay'), findsOneWidget);
    expect(find.text('Lời mời vào nhóm'), findsOneWidget);
    await tester.tap(find.text('Đọc tất cả'));
    await tester.pumpAndSettle();
    expect(api.readAllCalls, 1);
    expect(
      tester
          .widget<TextButton>(find.widgetWithText(TextButton, 'Đọc tất cả'))
          .onPressed,
      isNull,
    );
  });

  testWidgets('A06 giữ thông báo chưa đọc khi đọc tất cả thất bại', (
    tester,
  ) async {
    final api = FakeNotificationsApi([
      AppNotification(
        id: 'n-1',
        type: 'InvitationSent',
        title: 'Lời mời thử',
        content: 'Nhóm A mời bạn',
        isRead: false,
        createdAt: DateTime.now(),
      ),
    ])..failReadAll = true;
    await tester.pumpWidget(
      ProviderScope(
        overrides: [notificationsApiProvider.overrideWithValue(api)],
        child: MaterialApp(
          theme: AppTheme.light(),
          home: const NotificationsScreen(roleRoot: '/sv'),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Đọc tất cả'));
    await tester.pumpAndSettle();
    expect(api.readAllCalls, 1);
    expect(
      tester
          .widget<TextButton>(find.widgetWithText(TextButton, 'Đọc tất cả'))
          .onPressed,
      isNotNull,
    );
    expect(find.byType(SnackBar), findsOneWidget);
  });

  testWidgets('A06 đánh dấu một thông báo đã đọc khi mở', (tester) async {
    final api = FakeNotificationsApi([
      AppNotification(
        id: 'n-1',
        type: 'InvitationSent',
        title: 'Lời mời thử',
        content: 'Nhóm A mời bạn',
        isRead: false,
        createdAt: DateTime.now(),
      ),
    ]);
    await tester.pumpWidget(
      ProviderScope(
        overrides: [notificationsApiProvider.overrideWithValue(api)],
        child: MaterialApp(
          theme: AppTheme.light(),
          home: const NotificationsScreen(roleRoot: '/sv'),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Lời mời thử'));
    await tester.pumpAndSettle();
    expect(api.readCalls, 1);
    expect(
      tester
          .widget<TextButton>(find.widgetWithText(TextButton, 'Đọc tất cả'))
          .onPressed,
      isNull,
    );
  });

  testWidgets('A06 giữ trạng thái chưa đọc khi API đánh dấu lỗi', (
    tester,
  ) async {
    final api = FakeNotificationsApi([
      AppNotification(
        id: 'n-1',
        type: 'InvitationSent',
        title: 'Lời mời thử',
        content: 'Nhóm A mời bạn',
        isRead: false,
        createdAt: DateTime.now(),
      ),
    ])..failRead = true;
    await tester.pumpWidget(
      ProviderScope(
        overrides: [notificationsApiProvider.overrideWithValue(api)],
        child: MaterialApp(
          theme: AppTheme.light(),
          home: const NotificationsScreen(roleRoot: '/sv'),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Lời mời thử'));
    await tester.pumpAndSettle();
    expect(api.readCalls, 1);
    expect(
      tester
          .widget<TextButton>(find.widgetWithText(TextButton, 'Đọc tất cả'))
          .onPressed,
      isNotNull,
    );
    expect(find.byType(SnackBar), findsOneWidget);
  });

  testWidgets('A06 badge hiện số chưa đọc và giới hạn hiển thị 99', (
    tester,
  ) async {
    final api = FakeNotificationsApi([])..unread = 103;
    await tester.pumpWidget(
      ProviderScope(
        overrides: [notificationsApiProvider.overrideWithValue(api)],
        child: MaterialApp(
          theme: AppTheme.light(),
          home: const Scaffold(
            body: NotificationTabIcon(icon: Icons.notifications),
          ),
        ),
      ),
    );
    await tester.pump();
    expect(find.text('99'), findsOneWidget);
  });

  testWidgets('A06 chạm thông báo mở màn dự án liên quan', (tester) async {
    final api = FakeNotificationsApi([
      AppNotification(
        id: 'n-1',
        type: 'TaskAssigned',
        title: 'Việc mới',
        content: 'Có việc cần làm',
        linkUrl: 'app://workspaces/workspace-1',
        isRead: false,
        createdAt: DateTime.now(),
      ),
    ]);
    final router = GoRouter(
      initialLocation: '/sv/thong-bao',
      routes: [
        GoRoute(
          path: '/sv/thong-bao',
          builder: (_, _) => const NotificationsScreen(roleRoot: '/sv'),
        ),
        GoRoute(
          path: '/sv/du-an',
          builder: (_, _) => const Scaffold(body: Text('Dự án của tôi')),
        ),
      ],
    );
    addTearDown(router.dispose);
    await tester.pumpWidget(
      ProviderScope(
        overrides: [notificationsApiProvider.overrideWithValue(api)],
        child: MaterialApp.router(
          theme: AppTheme.light(),
          routerConfig: router,
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Việc mới'));
    await tester.pumpAndSettle();
    expect(api.readCalls, 1);
    expect(find.text('Dự án của tôi'), findsOneWidget);
  });

  testWidgets('A06 mở yêu cầu đổi nhóm từ thông báo kết quả', (tester) async {
    final api = FakeNotificationsApi([
      AppNotification(
        id: 'n-1',
        type: 'ChangeRequestApproved',
        title: 'Đổi nhóm đã được duyệt',
        content: 'Bộ môn đã duyệt yêu cầu đổi nhóm.',
        linkUrl: 'app://change-requests',
        isRead: false,
        createdAt: DateTime.now(),
      ),
    ]);
    final router = GoRouter(
      initialLocation: '/sv/thong-bao',
      routes: [
        GoRoute(
          path: '/sv/thong-bao',
          builder: (_, _) => const NotificationsScreen(roleRoot: '/sv'),
        ),
        GoRoute(
          path: '/sv/yeu-cau-doi-nhom',
          builder: (_, _) => const Scaffold(body: Text('Yêu cầu đổi nhóm')),
        ),
      ],
    );
    addTearDown(router.dispose);
    await tester.pumpWidget(
      ProviderScope(
        overrides: [notificationsApiProvider.overrideWithValue(api)],
        child: MaterialApp.router(theme: AppTheme.light(), routerConfig: router),
      ),
    );

    await tester.pumpAndSettle();
    await tester.tap(find.text('Đổi nhóm đã được duyệt'));
    await tester.pumpAndSettle();

    expect(api.readCalls, 1);
    expect(find.text('Yêu cầu đổi nhóm'), findsOneWidget);
  });

  testWidgets('A06 mở đúng màn lời mời từ thông báo', (tester) async {
    final api = FakeNotificationsApi([
      AppNotification(
        id: 'n-1',
        type: 'InvitationSent',
        title: 'Bạn được mời vào nhóm',
        content: 'Nhóm A mời bạn',
        linkUrl: 'app://invitations',
        isRead: false,
        createdAt: DateTime.now(),
      ),
    ]);
    final router = GoRouter(
      initialLocation: '/sv/thong-bao',
      routes: [
        GoRoute(
          path: '/sv/thong-bao',
          builder: (_, _) => const NotificationsScreen(roleRoot: '/sv'),
        ),
        GoRoute(
          path: '/sv/loi-moi',
          builder: (_, _) => const Scaffold(body: Text('Lời mời của tôi')),
        ),
      ],
    );
    addTearDown(router.dispose);
    await tester.pumpWidget(
      ProviderScope(
        overrides: [notificationsApiProvider.overrideWithValue(api)],
        child: MaterialApp.router(theme: AppTheme.light(), routerConfig: router),
      ),
    );

    await tester.pumpAndSettle();
    await tester.tap(find.text('Bạn được mời vào nhóm'));
    await tester.pumpAndSettle();

    expect(find.text('Lời mời của tôi'), findsOneWidget);
  });
}
