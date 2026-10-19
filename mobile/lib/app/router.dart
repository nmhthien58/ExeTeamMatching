import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/auth/session.dart';
import '../core/providers.dart';
import '../features/auth/login_screen.dart';
import '../shared/widgets/states.dart';
import 'navigation.dart';
import 'role_shell.dart';

/// Router (MB-05): chưa đăng nhập → A03; đăng nhập xong vào đúng khung theo vai trò; mỗi khung có tab bar riêng.
/// Màn cấp 2 của từng mảng thêm vào `routes` của khung tương ứng.
final routerProvider = Provider<GoRouter>((ref) {
  final auth = ValueNotifier<AsyncValue<UserInfo?>>(ref.read(authControllerProvider));
  ref.listen(authControllerProvider, (_, next) => auth.value = next);
  ref.onDispose(auth.dispose);

  return GoRouter(
    initialLocation: '/',
    refreshListenable: auth,
    redirect: (context, state) {
      final value = auth.value;
      if (value.isLoading) return state.matchedLocation == '/' ? null : '/';
      final user = value.value;
      final loc = state.matchedLocation;
      final publicRoute = loc == '/dang-nhap' || loc == '/tai-khoan-chua-co';
      if (user == null) return publicRoute ? null : '/dang-nhap';
      final shell = shellOf(user);
      if (shell == null) return '/chi-dung-web';
      if (publicRoute || loc == '/') return shellHome[shell];
      // Không cho vào khung của vai trò khác.
      final inOwnShell = loc == shellHome[shell] || loc.startsWith('${shellHome[shell]}/');
      return inOwnShell || loc == '/chi-dung-web' ? null : shellHome[shell];
    },
    routes: [
      GoRoute(path: '/', builder: (_, _) => const Scaffold(body: Center(child: CircularProgressIndicator()))),
      GoRoute(path: '/dang-nhap', builder: (_, _) => const LoginScreen()),
      GoRoute(path: '/tai-khoan-chua-co', builder: (_, state) => NotInListScreen(email: state.extra as String?)),
      GoRoute(
        path: '/chi-dung-web',
        builder: (_, _) => const Scaffold(
          body: EmptyState(
            icon: Icons.computer,
            title: 'Tài khoản Bộ môn dùng trên web',
            description: 'Mở trang quản trị trên máy tính để tiếp tục.',
          ),
        ),
      ),
      for (final shell in Shell.values) _shellRoute(shell),
    ],
  );
});

ShellRoute _shellRoute(Shell shell) {
  final home = shellHome[shell]!;
  final tabs = allTabs(shell);
  return ShellRoute(
    builder: (context, state, child) => RoleShell(shell: shell, location: state.matchedLocation, child: child),
    routes: [
      for (final tab in tabs)
        GoRoute(
          path: tab.path,
          // Tab là màn cấp 1: không có hiệu ứng trượt, không có Back.
          pageBuilder: (_, state) => NoTransitionPage(
            key: state.pageKey,
            child: tab.path == '/sv/kham-pha'
                ? TeamFormationOnly(child: ScreenPlaceholder(item: tab))
                : ScreenPlaceholder(item: tab),
          ),
        ),
      // Đường dẫn lạ trong khung → về trang chủ của khung.
      GoRoute(path: '$home/:rest(.*)', redirect: (_, _) => home),
    ],
  );
}
