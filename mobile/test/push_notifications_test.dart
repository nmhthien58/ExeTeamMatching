import 'dart:async';

import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/firebase/push_notifications.dart';
import 'package:exe_team_matching/features/change_requests/change_requests_api.dart';
import 'package:exe_team_matching/features/notifications/device_tokens_api.dart';
import 'package:exe_team_matching/features/notifications/notifications_api.dart';
import 'package:exe_team_matching/features/notifications/notifications_view_model.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

class _FakeMessaging implements PushMessagingGateway {
  PushPermission permission = PushPermission.authorized;
  PushMessage? initial;
  String? registrationId;
  int registerCalls = 0;
  int unregisterCalls = 0;
  int settingsCalls = 0;
  final registered = StreamController<String>.broadcast();
  final foreground = StreamController<PushMessage>.broadcast();
  final opened = StreamController<PushMessage>.broadcast();

  @override
  Future<PushMessage?> getInitialMessage() async => initial;

  @override
  Stream<PushMessage> get onMessage => foreground.stream;

  @override
  Stream<PushMessage> get onMessageOpenedApp => opened.stream;

  @override
  Stream<String> get onRegistered => registered.stream;

  @override
  Future<void> openNotificationSettings() async => settingsCalls++;

  @override
  Future<void> register() async {
    registerCalls++;
    if (registrationId case final id?) {
      scheduleMicrotask(() => registered.add(id));
    }
  }

  @override
  Future<PushPermission> requestPermission() async => permission;

  @override
  Future<void> unregister() async => unregisterCalls++;

  Future<void> close() async {
    await registered.close();
    await foreground.close();
    await opened.close();
  }
}

class _FakeDevices extends DeviceTokensApi {
  _FakeDevices() : super(Dio());

  final registered = <String>[];
  final removed = <String>[];
  bool failRegister = false;

  @override
  Future<void> register(String token) async {
    if (failRegister) throw Exception('mất mạng');
    registered.add(token);
  }

  @override
  Future<void> remove(String token) async => removed.add(token);
}

class _FakeNotifications extends NotificationsApi {
  _FakeNotifications() : super(Dio());

  int unreadCalls = 0;

  @override
  Future<int> unreadCount() async {
    unreadCalls++;
    return 1;
  }
}

class _FakeChangeRequests extends ChangeRequestsApi {
  _FakeChangeRequests() : super(Dio());

  int listCalls = 0;

  @override
  Future<List<ChangeRequest>> list(String teamId) async {
    listCalls++;
    return const [];
  }
}

const _message = PushMessage(
  title: 'Đã duyệt',
  body: 'Yêu cầu đổi nhóm đã được duyệt.',
  type: 'ChangeRequestApproved',
  id: 'notification-id',
  linkUrl: 'app://change-requests',
);

void main() {
  late _FakeMessaging messaging;
  late _FakeDevices devices;
  late _FakeNotifications notifications;
  late _FakeChangeRequests changeRequests;
  late ProviderContainer container;

  setUp(() {
    messaging = _FakeMessaging();
    devices = _FakeDevices();
    notifications = _FakeNotifications();
    changeRequests = _FakeChangeRequests();
    container = ProviderContainer(
      overrides: [
        pushMessagingGatewayProvider.overrideWithValue(messaging),
        deviceTokensApiProvider.overrideWithValue(devices),
        notificationsApiProvider.overrideWithValue(notifications),
        changeRequestsApiProvider.overrideWithValue(changeRequests),
      ],
    );
    container.listen(pushNotificationsProvider, (_, _) {});
  });

  tearDown(() async {
    container.dispose();
    await messaging.close();
  });

  Future<void> settle() async {
    await Future<void>.delayed(Duration.zero);
    await Future<void>.delayed(Duration.zero);
  }

  test('sau đăng nhập đăng ký FID và gửi lại khi FID đổi', () async {
    messaging.registrationId = 'fid-1';

    await container.read(pushNotificationsProvider.notifier).start();
    await settle();
    messaging.registered.add('fid-2');
    await settle();

    expect(messaging.registerCalls, 1);
    expect(devices.registered, ['fid-1', 'fid-2']);
    expect(container.read(pushNotificationsProvider).token, 'fid-2');
  });

  test('A07 gửi lại token hiện tại và báo đúng kết quả (MB-54)', () async {
    final push = container.read(pushNotificationsProvider.notifier);
    expect(await push.resendToken(), DeviceTokenResend.noToken);
    expect(devices.registered, isEmpty);

    messaging.registrationId = 'fid-1';
    await push.start();
    await settle();

    expect(await push.resendToken(), DeviceTokenResend.sent);
    expect(devices.registered, ['fid-1', 'fid-1']);

    devices.failRegister = true;
    expect(await push.resendToken(), DeviceTokenResend.failed);
  });

  test('foreground làm mới A06 và phát sự kiện banner', () async {
    await container.read(pushNotificationsProvider.notifier).start();
    messaging.foreground.add(_message);
    await settle();

    final state = container.read(pushNotificationsProvider);
    expect(state.event?.kind, PushUiEventKind.foreground);
    expect(state.event?.message?.type, 'ChangeRequestApproved');
    expect(notifications.unreadCalls, 1);
  });

  test('message mở app từ trạng thái đóng phát sự kiện điều hướng', () async {
    messaging.initial = _message;

    await container.read(pushNotificationsProvider.notifier).start();

    expect(
      container.read(pushNotificationsProvider).event?.kind,
      PushUiEventKind.opened,
    );
  });

  test('từ chối quyền báo nhẹ và mở được cài đặt', () async {
    messaging.permission = PushPermission.denied;
    final controller = container.read(pushNotificationsProvider.notifier);

    await controller.start();
    await controller.openSettings();

    final state = container.read(pushNotificationsProvider);
    expect(state.permission, PushPermission.denied);
    expect(state.event?.kind, PushUiEventKind.permissionDenied);
    expect(messaging.settingsCalls, 1);
    expect(devices.registered, isEmpty);
  });

  test('resume 2 lần khi đang denied chỉ phát sinh 1 sự kiện permissionDenied (CR-07)', () async {
    messaging.permission = PushPermission.denied;
    final controller = container.read(pushNotificationsProvider.notifier);

    await controller.start();
    final firstState = container.read(pushNotificationsProvider);
    expect(firstState.event?.kind, PushUiEventKind.permissionDenied);
    final firstSerial = firstState.event?.serial;

    await controller.refreshPermission();
    expect(container.read(pushNotificationsProvider).event?.serial, firstSerial);

    await controller.refreshPermission();
    expect(container.read(pushNotificationsProvider).event?.serial, firstSerial);
  });

  test('nhận push ChangeRequestApproved làm mới S26 changeRequestsProvider', () async {
    container.listen(changeRequestsProvider('team-1'), (_, _) {});
    await settle();
    expect(changeRequests.listCalls, 1);

    await container.read(pushNotificationsProvider.notifier).start();
    messaging.foreground.add(_message);
    await settle();

    expect(changeRequests.listCalls, 2);
  });

  test('đăng xuất gỡ FID ở backend rồi unregister Firebase', () async {
    messaging.registrationId = 'fid-logout';
    final controller = container.read(pushNotificationsProvider.notifier);
    await controller.start();
    await settle();

    await controller.unregister();

    expect(devices.removed, ['fid-logout']);
    expect(messaging.unregisterCalls, 1);
    expect(container.read(pushNotificationsProvider).token, isNull);
  });

  test('fallback type/id của Firebase Console mở đúng màn', () {
    expect(
      notificationRouteFor(
        roleRoot: '/sv',
        type: 'ChangeRequestApproved',
        id: 'request-id',
      ),
      '/sv/yeu-cau-doi-nhom',
    );
    expect(
      notificationRouteFor(
        roleRoot: '/gv',
        type: 'WorkspaceUpdated',
        id: 'workspace-id',
      ),
      '/gv/du-an/workspace-id',
    );
  });
}
