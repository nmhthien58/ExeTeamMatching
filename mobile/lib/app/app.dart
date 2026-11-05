import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/auth/session.dart';
import '../core/firebase/push_notifications.dart';
import '../core/providers.dart';
import '../core/theme/app_theme.dart';
import '../features/notifications/notifications_view_model.dart';
import 'navigation.dart';
import 'router.dart';

final rootScaffoldMessengerKey = GlobalKey<ScaffoldMessengerState>();

/// Gốc ứng dụng: theme bản 4 (MB-02, MB-37), router theo vai trò và listener FCM (MB-52).
class ExeTeamMatchingApp extends ConsumerStatefulWidget {
  const ExeTeamMatchingApp({super.key});

  @override
  ConsumerState<ExeTeamMatchingApp> createState() => _ExeTeamMatchingAppState();
}

class _ExeTeamMatchingAppState extends ConsumerState<ExeTeamMatchingApp>
    with WidgetsBindingObserver {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    ref.listenManual<AsyncValue<UserInfo?>>(authControllerProvider, (_, next) {
      final controller = ref.read(pushNotificationsProvider.notifier);
      if (next.value == null) {
        unawaited(controller.stop());
      } else {
        unawaited(controller.start());
      }
    }, fireImmediately: true);
    ref.listenManual<PushNotificationsState>(
      pushNotificationsProvider,
      _onPushState,
    );
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed &&
        ref.read(authControllerProvider).value != null) {
      unawaited(
        ref.read(pushNotificationsProvider.notifier).refreshPermission(),
      );
    }
  }

  void _onPushState(
    PushNotificationsState? previous,
    PushNotificationsState next,
  ) {
    if (next.permission == PushPermission.authorized &&
        previous?.permission != PushPermission.authorized) {
      rootScaffoldMessengerKey.currentState?.hideCurrentMaterialBanner();
    }
    final event = next.event;
    if (event == null || event.serial == previous?.event?.serial) return;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) _handleEvent(event);
    });
  }

  void _handleEvent(PushUiEvent event) {
    switch (event.kind) {
      case PushUiEventKind.permissionDenied:
        _showPermissionBanner();
      case PushUiEventKind.foreground:
        final message = event.message;
        if (message != null) _showMessageBanner(message);
      case PushUiEventKind.opened:
        final message = event.message;
        if (message != null) _openMessage(message);
    }
  }

  void _showPermissionBanner() {
    final messenger = rootScaffoldMessengerKey.currentState;
    if (messenger == null) return;
    messenger
      ..hideCurrentMaterialBanner()
      ..showMaterialBanner(
        MaterialBanner(
          leading: const Icon(Icons.notifications_off_outlined),
          content: const Text(
            'Thông báo đang tắt. Bạn vẫn xem được tin trong mục Thông báo.',
          ),
          actions: [
            TextButton(
              onPressed: messenger.hideCurrentMaterialBanner,
              child: const Text('Để sau'),
            ),
            TextButton(
              onPressed: () {
                messenger.hideCurrentMaterialBanner();
                unawaited(
                  ref.read(pushNotificationsProvider.notifier).openSettings(),
                );
              },
              child: const Text('Mở cài đặt'),
            ),
          ],
        ),
      );
  }

  void _showMessageBanner(PushMessage message) {
    final messenger = rootScaffoldMessengerKey.currentState;
    if (messenger == null) return;
    messenger
      ..hideCurrentMaterialBanner()
      ..showMaterialBanner(
        MaterialBanner(
          leading: const Icon(Icons.notifications_active_outlined),
          content: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                message.title,
                style: Theme.of(context).textTheme.titleSmall,
              ),
              Text(message.body),
            ],
          ),
          actions: [
            TextButton(
              onPressed: messenger.hideCurrentMaterialBanner,
              child: const Text('Đóng'),
            ),
            TextButton(
              onPressed: () {
                messenger.hideCurrentMaterialBanner();
                _openMessage(message);
              },
              child: const Text('Xem'),
            ),
          ],
        ),
      );
  }

  void _openMessage(PushMessage message) {
    final roleRoot = _roleRoot(ref.read(authControllerProvider).value);
    if (roleRoot == null) return;
    final route = notificationRouteFor(
      roleRoot: roleRoot,
      link: message.linkUrl,
      type: message.type,
      id: message.id,
    );
    if (route != null) ref.read(routerProvider).go(route);
  }

  static String? _roleRoot(UserInfo? user) {
    if (user == null) return null;
    final shell = shellOf(user);
    return shell == null ? null : shellHome[shell];
  }

  @override
  Widget build(BuildContext context) => MaterialApp.router(
    title: 'EXE Team Matching',
    debugShowCheckedModeBanner: false,
    theme: AppTheme.light(),
    scaffoldMessengerKey: rootScaffoldMessengerKey,
    routerConfig: ref.watch(routerProvider),
  );
}
