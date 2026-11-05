import 'package:exe_team_matching/features/realtime/collab_hub.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';

class _FakeConnection implements HubConnectionLike {
  final calls = <String>[];
  final handlers = <String, List<void Function(Object?)>>{};
  bool failStart = false;
  late VoidCallback reconnecting;
  late VoidCallback reconnected;

  @override
  Future<void> start() async {
    calls.add('start');
    if (failStart) {
      failStart = false;
      throw Exception('401');
    }
  }

  @override
  Future<void> stop() async => calls.add('stop');

  @override
  Future<void> invoke(String method, String id) async => calls.add('$method $id');

  @override
  void on(String event, void Function(Object? payload) handler) => (handlers[event] ??= []).add(handler);

  @override
  void off(String event, void Function(Object? payload) handler) => handlers[event]?.remove(handler);

  @override
  void onStatus({required VoidCallback reconnecting, required VoidCallback reconnected, required VoidCallback closed}) {
    this.reconnecting = reconnecting;
    this.reconnected = reconnected;
  }
}

Future<void> _flush() => Future<void>.delayed(Duration.zero);

void main() {
  test('kết nối khi màn đầu mở, vào / rời kênh, ngắt khi màn cuối rời', () async {
    final fake = _FakeConnection();
    final hub = CollabHub(() => fake);
    final releaseA = hub.acquire();
    final releaseB = hub.acquire();
    await _flush();
    expect(hub.status.value, HubStatus.connected);

    final leave = hub.join(HubGroup.channel, 'c1');
    leave();
    releaseA();
    expect(fake.calls, ['start', 'JoinChannel c1', 'LeaveChannel c1']);
    releaseB();
    await _flush();
    expect(fake.calls.last, 'stop');
  });

  test('rớt mạng → đang kết nối lại; có mạng → vào lại workspace; sự kiện tới handler, gỡ được', () async {
    final fake = _FakeConnection();
    final hub = CollabHub(() => fake);
    final statuses = <HubStatus>[];
    hub.status.addListener(() => statuses.add(hub.status.value));
    hub.acquire();
    hub.join(HubGroup.workspace, 'w1');
    await _flush();
    final received = <Object?>[];
    final off = hub.on('messageCreated', received.add);
    for (final h in [...fake.handlers['messageCreated']!]) {
      h({'id': 'm1'});
    }
    off();

    fake.reconnecting();
    fake.reconnected();
    expect(statuses, [HubStatus.connecting, HubStatus.connected, HubStatus.reconnecting, HubStatus.connected]);
    expect(fake.calls.where((c) => c == 'JoinWorkspace w1'), hasLength(2));
    expect(received, [
      {'id': 'm1'},
    ]);
    expect(fake.handlers['messageCreated'], isEmpty);
  });

  test('mở app vào thẳng màn có hub: làm mới token trước rồi mới kết nối; refresh lỗi thì báo mất kết nối', () async {
    final fake = _FakeConnection();
    final hub = CollabHub(() => fake, ensureToken: () async => fake.calls.add('refresh'));
    hub.acquire();
    await _flush();
    expect(fake.calls, ['refresh', 'start']);
    expect(hub.status.value, HubStatus.connected);

    final failing = _FakeConnection()..failStart = true;
    final offline = CollabHub(() => failing, ensureToken: () async => throw Exception('mất mạng'));
    offline.acquire();
    await _flush();
    await _flush();
    expect(offline.status.value, HubStatus.disconnected);
  });

  test('không kết nối được → mất kết nối; Thử lại kết nối lần nữa', () async {
    final fake = _FakeConnection()..failStart = true;
    final hub = CollabHub(() => fake);
    hub.acquire();
    await _flush();
    expect(hub.status.value, HubStatus.disconnected);
    hub.retry();
    await _flush();
    expect(hub.status.value, HubStatus.connected);
  });
}
