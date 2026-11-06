import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:signalr_netcore/signalr_client.dart';

import '../../core/auth/session.dart';
import '../../core/config/env.dart';
import '../../core/providers.dart';
import '../../shared/widgets/states.dart';
import 'realtime_status_view_model.dart';

/// Trạng thái kết nối hiển thị cho người dùng.
enum HubStatus { idle, connecting, connected, reconnecting, disconnected }

/// Nhóm trên hub (collab.yaml): `JoinChannel` / `JoinWorkspace` và `Leave…` tương ứng.
enum HubGroup { channel, workspace }

/// Phần của HubConnection mà client dùng – test thay bằng bản giả.
abstract class HubConnectionLike {
  Future<void> start();
  Future<void> stop();
  Future<void> invoke(String method, String id);
  void on(String event, void Function(Object? payload) handler);
  void off(String event, void Function(Object? payload) handler);
  void onStatus({required VoidCallback reconnecting, required VoidCallback reconnected, required VoidCallback closed});
}

/// `/hubs/collab` qua Gateway; JWT qua query `access_token`; tự kết nối lại.
// Token lấy lúc (re)kết nối; chưa có token (vừa mở app) thì CollabHub gọi ensureToken trước để REST refresh.
class SignalRConnection implements HubConnectionLike {
  SignalRConnection(SessionStore session)
      : _connection = HubConnectionBuilder()
            .withUrl('${Env.apiBaseUrl}/hubs/collab', options: HttpConnectionOptions(accessTokenFactory: () async => session.accessToken ?? ''))
            .withAutomaticReconnect()
            .build();

  final HubConnection _connection;
  final _wrapped = <void Function(Object?), MethodInvocationFunc>{};

  @override
  Future<void> start() => _connection.start() ?? Future.value();

  @override
  Future<void> stop() => _connection.stop();

  @override
  Future<void> invoke(String method, String id) async => _connection.invoke(method, args: [id]);

  @override
  void on(String event, void Function(Object? payload) handler) =>
      _connection.on(event, _wrapped[handler] = (args) => handler(args == null || args.isEmpty ? null : args.first));

  @override
  void off(String event, void Function(Object? payload) handler) => _connection.off(event, method: _wrapped.remove(handler));

  @override
  void onStatus({required VoidCallback reconnecting, required VoidCallback reconnected, required VoidCallback closed}) {
    _connection.onreconnecting(({error}) => reconnecting());
    _connection.onreconnected(({connectionId}) => reconnected());
    _connection.onclose(({error}) => closed());
  }
}

/// Một kết nối hub dùng chung cho mọi màn workspace (MB-38). Kết nối khi màn đầu tiên dùng, ngắt khi màn cuối rời;
/// vào lại các kênh / workspace đang nghe sau khi kết nối lại.
class CollabHub {
  CollabHub(this._factory, {this._ensureToken});

  final HubConnectionLike Function() _factory;

  /// Mở app vào thẳng màn có hub: access token chỉ có sau lời gọi REST đầu tiên, negotiate không token bị 401.
  final Future<void> Function()? _ensureToken;
  HubConnectionLike? _connection;
  int _users = 0;
  final _groups = <(HubGroup, String), int>{};
  final status = ValueNotifier(HubStatus.idle);

  /// Màn hình bắt đầu dùng hub; trả hàm thôi dùng.
  VoidCallback acquire() {
    if (_users++ == 0) _start();
    return () {
      if (--_users == 0) _stop();
    };
  }

  /// Thử kết nối lại sau khi mất hẳn kết nối (nút "Thử lại").
  void retry() {
    if (status.value == HubStatus.disconnected) _start();
  }

  VoidCallback on(String event, void Function(Object? payload) handler) {
    _ensure().on(event, handler);
    return () => _connection?.off(event, handler);
  }

  VoidCallback join(HubGroup group, String id) {
    final key = (group, id);
    _groups[key] = (_groups[key] ?? 0) + 1;
    if (status.value == HubStatus.connected && _groups[key] == 1) _invoke('Join', key);
    return () {
      final left = (_groups[key] ?? 1) - 1;
      if (left > 0) {
        _groups[key] = left;
        return;
      }
      _groups.remove(key);
      if (status.value == HubStatus.connected) _invoke('Leave', key);
    };
  }

  HubConnectionLike _ensure() => _connection ??= _factory()
    ..onStatus(
      reconnecting: () => status.value = HubStatus.reconnecting,
      reconnected: () {
        status.value = HubStatus.connected;
        _rejoin();
      },
      closed: () => status.value = _users > 0 ? HubStatus.disconnected : HubStatus.idle,
    );

  Future<void> _start() async {
    status.value = HubStatus.connecting;
    try {
      await _ensureToken?.call();
    } catch (_) {} // refresh lỗi thì start tự báo mất kết nối, người dùng bấm Thử lại
    try {
      await _ensure().start();
      status.value = HubStatus.connected;
      _rejoin();
    } catch (_) {
      status.value = HubStatus.disconnected;
    }
  }

  Future<void> _stop() async {
    await _connection?.stop();
    status.value = HubStatus.idle;
  }

  void _rejoin() {
    for (final key in _groups.keys) {
      _invoke('Join', key);
    }
  }

  // Lỗi vào kênh (không có quyền, kênh không tồn tại) không chặn màn hình – dữ liệu vẫn tải bằng REST.
  Future<void> _invoke(String verb, (HubGroup, String) key) async {
    final name = key.$1 == HubGroup.channel ? 'Channel' : 'Workspace';
    try {
      await _connection?.invoke('$verb$name', key.$2);
    } catch (_) {}
  }

  void dispose() {
    _connection?.stop();
    status.dispose();
  }
}

final collabHubProvider = Provider<CollabHub>((ref) {
  final session = ref.watch(sessionStoreProvider);
  final hub = CollabHub(
    () => SignalRConnection(session),
    // Lời gọi có xác thực bất kỳ: interceptor của Dio tự refresh khi 401.
    ensureToken: () async {
      if (session.accessToken == null) await ref.read(dioProvider).get<Object>('/api/auth/me');
    },
  );
  ref.onDispose(hub.dispose);
  return hub;
});

/// Dải trạng thái realtime đặt đầu màn workspace: giữ kết nối khi màn còn mở; "Đang kết nối lại…" khi rớt mạng,
/// mất hẳn kết nối thì có Thử lại. Kết nối bình thường không hiện gì.
class RealtimeStatusBanner extends ConsumerWidget {
  const RealtimeStatusBanner({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) => switch (ref.watch(realtimeStatusViewModelProvider)) {
        HubStatus.reconnecting => Semantics(
            liveRegion: true,
            child: const ListTile(dense: true, leading: SizedBox.square(dimension: 20, child: CircularProgressIndicator(strokeWidth: 2)), title: Text('Đang kết nối lại…')),
          ),
        HubStatus.disconnected => ErrorBanner(message: 'Mất kết nối realtime – tin mới không tự hiện.', onRetry: ref.read(realtimeStatusViewModelProvider.notifier).retry),
        _ => const SizedBox.shrink(),
      };
}
