import 'dart:ui' show VoidCallback;

import 'package:exe_team_matching/features/realtime/collab_hub.dart';
import 'package:exe_team_matching/features/realtime/realtime_status_view_model.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

class _Connection implements HubConnectionLike {
  final calls = <String>[];
  bool failStart = false;
  late VoidCallback reconnecting, reconnected, closed;

  @override
  Future<void> start() async {
    calls.add('start');
    if (failStart) throw Exception('offline');
  }

  @override
  Future<void> stop() async => calls.add('stop');
  @override
  Future<void> invoke(String method, String id) async {}
  @override
  void on(String event, void Function(Object? payload) handler) {}
  @override
  void off(String event, void Function(Object? payload) handler) {}
  @override
  void onStatus({required VoidCallback reconnecting, required VoidCallback reconnected, required VoidCallback closed}) {
    this.reconnecting = reconnecting;
    this.reconnected = reconnected;
    this.closed = closed;
  }
}

Future<void> _flush() => Future<void>.delayed(Duration.zero);

/// MB-49 – ViewModel của dải trạng thái realtime test riêng, không dựng widget.
void main() {
  late _Connection connection;
  late ProviderContainer c;

  setUp(() {
    connection = _Connection();
    c = ProviderContainer(overrides: [collabHubProvider.overrideWithValue(CollabHub(() => connection))]);
    addTearDown(c.dispose);
  });

  test('có người nghe thì kết nối, state theo trạng thái hub, hết người nghe thì ngắt', () async {
    final sub = c.listen(realtimeStatusViewModelProvider, (_, _) {});
    expect(c.read(realtimeStatusViewModelProvider), HubStatus.connecting);
    await _flush();
    expect(c.read(realtimeStatusViewModelProvider), HubStatus.connected);
    connection.reconnecting();
    expect(c.read(realtimeStatusViewModelProvider), HubStatus.reconnecting);
    sub.close();
    await _flush();
    expect(connection.calls, ['start', 'stop']);
  });

  test('mất kết nối: Thử lại kết nối lần nữa', () async {
    connection.failStart = true;
    c.listen(realtimeStatusViewModelProvider, (_, _) {});
    await _flush();
    expect(c.read(realtimeStatusViewModelProvider), HubStatus.disconnected);
    connection.failStart = false;
    c.read(realtimeStatusViewModelProvider.notifier).retry();
    await _flush();
    expect(c.read(realtimeStatusViewModelProvider), HubStatus.connected);
  });
}
