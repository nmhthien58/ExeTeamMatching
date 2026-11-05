import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/auth/session.dart';
import '../core/firebase/crash_reporter.dart';
import '../core/providers.dart';
import '../features/auth/login_screen.dart';
import '../features/onboarding/onboarding_screen.dart';
import '../features/onboarding/splash_screen.dart';
import '../shared/widgets/states.dart';
import 'feature_routes.dart';
import 'navigation.dart';
import 'role_shell.dart';
import '../core/theme/app_tokens.dart';

/// Router (MB-05): chưa đăng nhập → A03; đăng nhập xong vào đúng khung theo vai trò; mỗi khung có tab bar riêng.
/// Màn cấp 2 của từng mảng thêm vào `routes` của khung tương ứng.
final routerProvider = Provider<GoRouter>((ref) {
  final auth = ValueNotifier<AsyncValue<UserInfo?>>(ref.read(authControllerProvider));
  ref.listen(authControllerProvider, (_, next) => auth.value = next);
  ref.onDispose(auth.dispose);

  final router = GoRouter(
    initialLocation: '/',
    refreshListenable: auth,
    redirect: (context, state) {
      final value = auth.value;
      if (value.isLoading) return state.matchedLocation == '/' ? null : '/';
      final user = value.value;
      final loc = state.matchedLocation;
      final publicRoute = loc == '/dang-nhap' || loc == '/tai-khoan-chua-co' || loc == '/gioi-thieu';
      // Splash quyết định onboarding sau khi đọc phiên và trạng thái đã xem.
      if (loc == '/') return null;
      if (user == null) return publicRoute ? null : '/dang-nhap';
      final shell = shellOf(user);
      if (shell == null) return '/chi-dung-web';
      if (publicRoute || loc == '/') return shellHome[shell];
      // Không cho vào khung của vai trò khác.
      final inOwnShell = loc == shellHome[shell] || loc.startsWith('${shellHome[shell]}/');
      return inOwnShell || loc == '/chi-dung-web' ? null : shellHome[shell];
    },
    routes: [
      GoRoute(path: '/', builder: (context, _) => SplashScreen(onResolved: context.go)),
      GoRoute(path: '/gioi-thieu', builder: (context, _) => OnboardingScreen(onFinished: () => context.go('/dang-nhap'))),
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

  // MB-61: báo cáo lỗi ghi màn đang mở. Dùng mẫu route nên không lộ id nhóm, id người dùng.
  void trackScreen() {
    final path = router.routerDelegate.currentConfiguration.fullPath;
    if (path.isNotEmpty) unawaited(ref.read(crashReporterProvider).setKey('screen', path));
  }

  router.routerDelegate.addListener(trackScreen);
  ref.onDispose(() => router.routerDelegate.removeListener(trackScreen));
  return router;
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
          // Tab là màn cấp 1: không trượt, không có Back; đổi tab bằng hiệu ứng "fade through" ngắn.
          pageBuilder: (_, state) => _tabPage(
            state,
            tab.path == '/sv/kham-pha'
                ? TeamFormationOnly(child: tabScreens[tab.path]?.call(state) ?? ScreenPlaceholder(item: tab))
                : tabScreens[tab.path]?.call(state) ?? ScreenPlaceholder(item: tab),
          ),
        ),
      ...featureRoutes(shell),
      // Đường dẫn lạ trong khung → về trang chủ của khung.
      GoRoute(path: '$home/:rest(.*)', redirect: (_, _) => home),
    ],
  );
}

/// Đổi tab: màn mới hiện dần và phóng nhẹ 98% → 100% (Material "fade through"), 200 ms.
/// Người dùng bật giảm chuyển động thì đổi ngay (AppMotion, DESIGN.md 4.5).
Page<void> _tabPage(GoRouterState state, Widget child) => CustomTransitionPage<void>(
      key: state.pageKey,
      child: child,
      transitionDuration: AppMotion.base,
      reverseTransitionDuration: Duration.zero,
      transitionsBuilder: (context, animation, _, child) {
        if (MediaQuery.maybeDisableAnimationsOf(context) == true) return child;
        final curved = CurvedAnimation(parent: animation, curve: Curves.easeOutCubic);
        return FadeTransition(
          opacity: curved,
          child: ScaleTransition(scale: Tween<double>(begin: 0.98, end: 1).animate(curved), child: child),
        );
      },
    );
