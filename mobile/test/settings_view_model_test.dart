import 'dart:async';

import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/features/settings/settings_screen.dart';
import 'package:exe_team_matching/features/settings/settings_view_model.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

class _FakeApi extends SettingsApi {
  _FakeApi() : super(Dio());

  bool enabled = true;
  Object? error;
  Completer<void>? gate;

  @override
  Future<bool> emailEnabled() async => enabled;

  @override
  Future<void> setEmailEnabled(bool value) async {
    if (gate != null) await gate!.future;
    if (error != null) throw error!;
    enabled = value;
  }
}

/// MB-49 – ViewModel của A07 test riêng, không dựng widget.
void main() {
  late _FakeApi api;
  late ProviderContainer c;

  setUp(() {
    api = _FakeApi();
    c = ProviderContainer(overrides: [settingsApiProvider.overrideWithValue(api)]);
    addTearDown(c.dispose);
    c
      ..listen(settingsViewModelProvider, (_, _) {})
      ..listen(emailEnabledProvider, (_, _) {});
  });

  test('tắt email: công tắc đổi ngay khi đang gửi, lưu xong đọc lại giá trị đã lưu', () async {
    expect(await c.read(emailEnabledProvider.future), isTrue);
    api.gate = Completer();
    final pending = c.read(settingsViewModelProvider.notifier).setEmailEnabled(false);
    expect(c.read(settingsViewModelProvider), isFalse);
    api.gate!.complete();
    expect(await pending, isTrue);
    expect(c.read(settingsViewModelProvider), isNull);
    expect(c.read(emailEnabledProvider).value, isFalse);
  });

  test('lỗi lưu: trả false, công tắc trở lại giá trị cũ', () async {
    api.error = const ApiException(status: 0, code: 'network', title: 'Mất kết nối');
    expect(await c.read(settingsViewModelProvider.notifier).setEmailEnabled(false), isFalse);
    expect(c.read(settingsViewModelProvider), isNull);
    expect(await c.read(emailEnabledProvider.future), isTrue);
  });
}
