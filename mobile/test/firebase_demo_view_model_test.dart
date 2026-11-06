import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/auth/session.dart';
import 'package:exe_team_matching/core/firebase/crash_reporter.dart';
import 'package:exe_team_matching/core/firebase/remote_config_service.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/features/discover/discover_api.dart';
import 'package:exe_team_matching/features/discover/teams_view_model.dart';
import 'package:exe_team_matching/features/settings/firebase_demo_view_model.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/firebase_fakes.dart';

class _FailingApi extends DiscoverApi {
  _FailingApi(this.error) : super(Dio());

  final Object error;

  @override
  Future<TeamPage> search(String search, TeamFilter filter, int page) async => throw error;
}

/// MB-54 (phần Crashlytics) – mục Demo Firebase ở A07 và non-fatal từ luồng lỗi thật ở S05.
void main() {
  test('Demo Firebase: lỗi thử ghi non-fatal kèm key demo_action; crash thử gọi testCrash', () async {
    final crash = FakeCrashReporter();
    final c = ProviderContainer(overrides: [
      crashReporterProvider.overrideWithValue(crash),
      remoteConfigServiceProvider.overrideWithValue(FakeRemoteConfigService(const AppRemoteConfig(suggestionsMaxItems: 7))),
    ]);
    addTearDown(c.dispose);
    c.listen(firebaseDemoViewModelProvider, (_, _) {});

    expect(c.read(firebaseDemoViewModelProvider).config.suggestionsMaxItems, 7);

    await c.read(firebaseDemoViewModelProvider.notifier).sendNonFatal();
    expect(crash.errors, hasLength(1));
    expect(crash.errors.single.error, isA<DemoNonFatalError>());
    expect(crash.keys['demo_action'], 'non_fatal');

    await c.read(firebaseDemoViewModelProvider.notifier).crash();
    expect(crash.testCrashes, 1);
    expect(crash.keys['demo_action'], 'test_crash');
  });

  group('Lối vào file và thông báo', () {
    UserInfo user(Set<Role> roles) => UserInfo(id: 'u', email: 'a@example.com', fullName: 'A', roles: roles);

    test('sinh viên vào Dự án', () {
      expect(firebaseDemoLinksFor(user({Role.student})), (files: '/sv/du-an', notifications: '/sv/thong-bao'));
    });

    test('giảng viên, mentor chọn nhóm trước', () {
      expect(firebaseDemoLinksFor(user({Role.lecturer})), (files: '/gv/nhom', notifications: '/gv/thong-bao'));
      expect(firebaseDemoLinksFor(user({Role.mentor})), (files: '/mentor/nhom', notifications: '/mentor/thong-bao'));
    });

    test('chưa đăng nhập hoặc chỉ là Admin thì không có lối vào', () {
      expect(firebaseDemoLinksFor(null), isNull);
      expect(firebaseDemoLinksFor(user({Role.admin})), isNull);
    });
  });

  Future<FakeCrashReporter> loadWith(Object error) async {
    final crash = FakeCrashReporter();
    final c = ProviderContainer(overrides: [
      discoverApiProvider.overrideWithValue(_FailingApi(error)),
      crashReporterProvider.overrideWithValue(crash),
    ]);
    addTearDown(c.dispose);
    c.listen(teamsViewModelProvider, (_, _) {});
    await Future<void>.delayed(Duration.zero);
    return crash;
  }

  test('S05: lỗi ngoài dự kiến ghi non-fatal; lỗi API (ProblemDetails) thì không', () async {
    final unexpected = await loadWith(const FormatException('dữ liệu sai dạng'));
    expect(unexpected.errors, hasLength(1));
    expect(unexpected.errors.single.reason, 'S05 tải danh sách nhóm');

    final api = await loadWith(const ApiException(status: 409, code: 'round-closed', title: 'Đợt đã khóa'));
    expect(api.errors, isEmpty);
  });
}
