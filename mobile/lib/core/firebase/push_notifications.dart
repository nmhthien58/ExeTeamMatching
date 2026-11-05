import 'dart:async';

import 'package:app_settings/app_settings.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../features/notifications/device_tokens_api.dart';
import '../../features/notifications/notifications_view_model.dart';

enum PushPermission { unknown, authorized, denied }

enum PushUiEventKind { foreground, opened, permissionDenied }

class PushMessage {
  const PushMessage({
    required this.title,
    required this.body,
    required this.type,
    this.id,
    this.linkUrl,
  });

  final String title;
  final String body;
  final String type;
  final String? id;
  final String? linkUrl;

  factory PushMessage.fromRemote(RemoteMessage message) {
    String? value(String key) {
      final raw = message.data[key];
      return raw == null || '$raw'.isEmpty ? null : '$raw';
    }

    return PushMessage(
      title: message.notification?.title ?? value('title') ?? 'Thông báo mới',
      body:
          message.notification?.body ??
          value('body') ??
          'Mở ứng dụng để xem chi tiết.',
      type: value('type') ?? '',
      id: value('id'),
      linkUrl: value('linkUrl'),
    );
  }
}

class PushUiEvent {
  const PushUiEvent(this.serial, this.kind, [this.message]);

  final int serial;
  final PushUiEventKind kind;
  final PushMessage? message;
}

class PushNotificationsState {
  const PushNotificationsState({
    this.permission = PushPermission.unknown,
    this.token,
    this.event,
  });

  final PushPermission permission;
  final String? token;
  final PushUiEvent? event;

  PushNotificationsState copyWith({
    PushPermission? permission,
    String? token,
    PushUiEvent? event,
    bool clearToken = false,
  }) => PushNotificationsState(
    permission: permission ?? this.permission,
    token: clearToken ? null : token ?? this.token,
    event: event ?? this.event,
  );
}

abstract interface class PushMessagingGateway {
  Future<PushPermission> requestPermission();
  Future<void> register();
  Future<void> unregister();
  Stream<String> get onRegistered;
  Stream<PushMessage> get onMessage;
  Stream<PushMessage> get onMessageOpenedApp;
  Future<PushMessage?> getInitialMessage();
  Future<void> openNotificationSettings();
}

class FirebasePushMessagingGateway implements PushMessagingGateway {
  FirebasePushMessagingGateway([FirebaseMessaging? messaging])
    : _messagingOverride = messaging;

  final FirebaseMessaging? _messagingOverride;

  // Trì hoãn truy cập plugin đến lúc ứng dụng đã chạy qua initFirebase().
  // Nhờ đó widget test có thể dựng App mà không phải khởi tạo native Firebase.
  FirebaseMessaging get _messaging =>
      _messagingOverride ?? FirebaseMessaging.instance;

  @override
  Future<PushPermission> requestPermission() async {
    final settings = await _messaging.requestPermission(
      alert: true,
      badge: true,
      sound: true,
    );
    return switch (settings.authorizationStatus) {
      AuthorizationStatus.authorized ||
      AuthorizationStatus.provisional => PushPermission.authorized,
      AuthorizationStatus.denied ||
      AuthorizationStatus.deniedPermanently => PushPermission.denied,
      AuthorizationStatus.notDetermined => PushPermission.unknown,
    };
  }

  @override
  Future<void> register() => _messaging.register();

  @override
  Future<void> unregister() => _messaging.unregister();

  @override
  Stream<String> get onRegistered => _messaging.onRegistered;

  @override
  Stream<PushMessage> get onMessage =>
      FirebaseMessaging.onMessage.map(PushMessage.fromRemote);

  @override
  Stream<PushMessage> get onMessageOpenedApp =>
      FirebaseMessaging.onMessageOpenedApp.map(PushMessage.fromRemote);

  @override
  Future<PushMessage?> getInitialMessage() async {
    final message = await _messaging.getInitialMessage();
    return message == null ? null : PushMessage.fromRemote(message);
  }

  @override
  Future<void> openNotificationSettings() => AppSettings.openAppSettings(
    type: AppSettingsType.notification,
    asAnotherTask: true,
  );
}

final pushMessagingGatewayProvider = Provider<PushMessagingGateway>(
  (ref) => FirebasePushMessagingGateway(),
);

/// Điều phối FCM của MB-52. SDK nằm sau gateway để test dùng fake, còn API device token
/// nằm trong `device_tokens_api.dart` theo cùng mẫu MVVM/Riverpod của A06.
class PushNotificationsController extends Notifier<PushNotificationsState> {
  StreamSubscription<String>? _tokenSubscription;
  StreamSubscription<PushMessage>? _foregroundSubscription;
  StreamSubscription<PushMessage>? _openedSubscription;
  bool _started = false;
  int _eventSerial = 0;

  PushMessagingGateway get _messaging => ref.read(pushMessagingGatewayProvider);
  DeviceTokensApi get _devices => ref.read(deviceTokensApiProvider);

  @override
  PushNotificationsState build() {
    ref.onDispose(() {
      unawaited(_cancelSubscriptions());
    });
    return const PushNotificationsState();
  }

  /// Gọi sau khi phiên đăng nhập sẵn sàng. Có thể gọi lại khi app resume; subscription chỉ tạo một lần.
  Future<void> start() async {
    if (!_started) {
      try {
        // Firebase Messaging 17 phát FID hiện tại và mọi lần FID đổi qua onRegistered.
        // Firebase Console và FCM send API đều nhận FID làm direct-send target.
        _tokenSubscription = _messaging.onRegistered.listen(_registerToken);
        _foregroundSubscription = _messaging.onMessage.listen(_onForeground);
        _openedSubscription = _messaging.onMessageOpenedApp.listen(_onOpened);
        _started = true;
        final initial = await _messaging.getInitialMessage();
        if (initial != null) _onOpened(initial);
      } catch (error) {
        await _cancelSubscriptions();
        _started = false;
        debugPrint('Start FCM listeners failed: $error');
      }
    }
    await refreshPermission();
  }

  /// Kiểm tra lại sau khi người dùng quay về từ Cài đặt hệ thống.
  Future<void> refreshPermission() async {
    try {
      final permission = await _messaging.requestPermission();
      state = state.copyWith(permission: permission);
      if (permission != PushPermission.authorized) {
        _emit(PushUiEventKind.permissionDenied);
        return;
      }
      // Subscribe onRegistered trước register để không bỏ lỡ FID được phát đồng bộ.
      await _messaging.register();
    } catch (error) {
      debugPrint('FCM permission/token setup failed: $error');
    }
  }

  Future<void> openSettings() async {
    try {
      await _messaging.openNotificationSettings();
    } catch (error) {
      debugPrint('Open notification settings failed: $error');
    }
  }

  /// Gỡ token trước khi xóa JWT. Lỗi mạng không được chặn đăng xuất trên máy.
  Future<void> unregister() async {
    final token = state.token;
    if (!_started && token == null) {
      state = state.copyWith(clearToken: true);
      return;
    }
    try {
      if (token != null && token.isNotEmpty) await _devices.remove(token);
      await _messaging.unregister();
    } catch (error) {
      debugPrint('Remove FCM device token failed: $error');
    } finally {
      state = state.copyWith(clearToken: true);
      await stop();
    }
  }

  Future<void> stop() async {
    await _cancelSubscriptions();
    _started = false;
  }

  Future<void> _registerToken(String token) async {
    state = state.copyWith(token: token);
    try {
      await _devices.register(token);
    } catch (error) {
      // App resume hoặc onRegistered sẽ thử lại; đăng nhập vẫn thành công khi BE-84 tạm thời lỗi.
      debugPrint('Register FCM device token failed: $error');
    }
  }

  void _onForeground(PushMessage message) {
    _refreshInbox();
    _emit(PushUiEventKind.foreground, message);
  }

  void _onOpened(PushMessage message) {
    _refreshInbox();
    _emit(PushUiEventKind.opened, message);
  }

  void _refreshInbox() {
    ref.invalidate(notificationsViewModelProvider);
    unawaited(ref.read(unreadCountViewModelProvider.notifier).load());
  }

  void _emit(PushUiEventKind kind, [PushMessage? message]) {
    state = state.copyWith(event: PushUiEvent(++_eventSerial, kind, message));
  }

  Future<void> _cancelSubscriptions() async {
    await _tokenSubscription?.cancel();
    await _foregroundSubscription?.cancel();
    await _openedSubscription?.cancel();
    _tokenSubscription = null;
    _foregroundSubscription = null;
    _openedSubscription = null;
  }
}

final pushNotificationsProvider =
    NotifierProvider<PushNotificationsController, PushNotificationsState>(
      PushNotificationsController.new,
    );
