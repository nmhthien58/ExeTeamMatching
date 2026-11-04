import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/firebase/analytics_service.dart';
import 'package:exe_team_matching/core/firebase/crash_reporter.dart';
import 'package:exe_team_matching/core/firebase/remote_config_service.dart';
import 'package:exe_team_matching/core/phase.dart';
import 'package:exe_team_matching/core/providers.dart';
import 'package:firebase_analytics/firebase_analytics.dart';
import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_crashlytics/firebase_crashlytics.dart';
import 'package:firebase_remote_config/firebase_remote_config.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

import 'support/firebase_fakes.dart';

class _MockAnalytics extends Mock implements FirebaseAnalytics {}

class _MockCrashlytics extends Mock implements FirebaseCrashlytics {}

class _MockRemoteConfig extends Mock implements FirebaseRemoteConfig {}

/// MB-50 / MB-51 – Crashlytics, Analytics, Remote Config qua service được inject.
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUpAll(() {
    registerFallbackValue(RemoteConfigSettings(fetchTimeout: Duration.zero, minimumFetchInterval: Duration.zero));
    registerFallbackValue(StackTrace.empty);
  });

  group('FirebaseAnalyticsService', () {
    late _MockAnalytics sdk;
    late FirebaseAnalyticsService analytics;
    late List<(String, Map<String, Object>?)> logged;

    setUp(() {
      sdk = _MockAnalytics();
      analytics = FirebaseAnalyticsService(analytics: () => sdk);
      logged = [];
      when(
        () => sdk.logEvent(
          name: any(named: 'name'),
          parameters: any(named: 'parameters'),
        ),
      ).thenAnswer((i) async {
        logged.add((i.namedArguments[#name] as String, i.namedArguments[#parameters] as Map<String, Object>?));
      });
      when(() => sdk.setUserId(id: any(named: 'id'))).thenAnswer((_) async {});
    });

    test('mỗi method ghi đúng tên sự kiện và tham số trong kế hoạch', () async {
      await analytics.logLogin();
      await analytics.logLogout();
      await analytics.logSearch('  edu ', 3, filter: 'missing_major');
      await analytics.logViewTeam('t1', source: TeamViewSource.suggestions);
      await analytics.logJoinGroup('t1', ideaCount: 2);
      await analytics.logSendInvitation('t1', source: InviteSource.search);
      await analytics.logSubmitChangeRequest(changeCount: 1, elapsed: const Duration(seconds: 95));
      await analytics.logExportReport(reportType: 'workspace_file', format: 'pdf');
      await analytics.logAiRequest(feature: 'idea_suggestion', model: 'gemini-2.5-flash');
      await analytics.logAiResultAccepted(feature: 'idea_suggestion', index: 1);
      await analytics.logAiError(feature: 'idea_suggestion', errorCode: 'network');

      expect(
        [
          for (final (n, p) in logged) [n, p],
        ],
        [
          [
            'login',
            {'method': 'google'},
          ],
          ['logout', null],
          [
            'search',
            {'search_term': 'edu', 'result_count': 3, 'filter': 'missing_major'},
          ],
          [
            'view_team',
            {'team_id': 't1', 'source': 'suggestions'},
          ],
          [
            'join_group',
            {'group_id': 't1', 'idea_count': 2},
          ],
          [
            'send_invitation',
            {'team_id': 't1', 'source': 'search'},
          ],
          [
            'submit_change_request',
            {'change_count': 1, 'elapsed_sec': 95},
          ],
          [
            'export_report',
            {'report_type': 'workspace_file', 'format': 'pdf'},
          ],
          [
            'ai_request',
            {'feature': 'idea_suggestion', 'model': 'gemini-2.5-flash'},
          ],
          [
            'ai_result_accepted',
            {'feature': 'idea_suggestion', 'index': 1},
          ],
          [
            'ai_error',
            {'feature': 'idea_suggestion', 'error_code': 'network'},
          ],
        ],
      );
    });

    test('search không gửi chuỗi giống email, cắt từ khóa dài còn 100 ký tự', () async {
      await analytics.logSearch('vy@fpt.edu.vn', 0);
      await analytics.logSearch('a' * 150, 0);

      expect(logged[0].$2!['search_term'], '(redacted)');
      expect((logged[1].$2!['search_term'] as String).length, 100);
    });

    test('setUserId chuyển id nội bộ cho SDK, null khi đăng xuất', () async {
      await analytics.setUserId('u1');
      await analytics.setUserId(null);

      verify(() => sdk.setUserId(id: 'u1')).called(1);
      verify(() => sdk.setUserId(id: null)).called(1);
    });

    test('SDK lỗi hoặc chưa khởi tạo Firebase: không ném lỗi', () async {
      when(
        () => sdk.logEvent(
          name: any(named: 'name'),
          parameters: any(named: 'parameters'),
        ),
      ).thenThrow(Exception('offline'));
      await expectLater(analytics.logLogin(), completes);
      await expectLater(FirebaseAnalyticsService(analytics: () => throw StateError('no app')).logLogout(), completes);
    });
  });

  group('FirebaseCrashReporter', () {
    late _MockCrashlytics sdk;
    late FirebaseCrashReporter crash;

    setUp(() {
      sdk = _MockCrashlytics();
      crash = FirebaseCrashReporter(crashlytics: () => sdk);
      when(
        () => sdk.recordError(
          any(),
          any(),
          reason: any(named: 'reason'),
          fatal: any(named: 'fatal'),
        ),
      ).thenAnswer((_) async {});
      when(() => sdk.setUserIdentifier(any())).thenAnswer((_) async {});
      when(() => sdk.setCustomKey(any(), any())).thenAnswer((_) async {});
    });

    test('recordNonFatal ghi lỗi không fatal kèm lý do', () async {
      final error = Exception('boom');
      await crash.recordNonFatal(error, StackTrace.current, reason: 'remote_config_fetch');

      verify(() => sdk.recordError(error, any(), reason: 'remote_config_fetch', fatal: false)).called(1);
    });

    test('setUser, setKey; clearUser xóa id và key role, course', () async {
      await crash.setUser('u1');
      await crash.setKey('role', 'student');
      await crash.clearUser();

      verify(() => sdk.setUserIdentifier('u1')).called(1);
      verify(() => sdk.setUserIdentifier('')).called(1);
      verify(() => sdk.setCustomKey('role', 'student')).called(1);
      verify(() => sdk.setCustomKey('role', '')).called(1);
      verify(() => sdk.setCustomKey('course', '')).called(1);
    });

    test('testCrash gọi crash của SDK', () {
      when(() => sdk.crash()).thenReturn(null);
      crash.testCrash();
      verify(() => sdk.crash()).called(1);
    });

    test('SDK lỗi: không ném lỗi', () async {
      when(() => sdk.setUserIdentifier(any())).thenThrow(Exception('x'));
      await expectLater(crash.setUser('u1'), completes);
      await expectLater(
        FirebaseCrashReporter(crashlytics: () => throw StateError('no app')).recordNonFatal('e', null),
        completes,
      );
    });
  });

  group('FirebaseRemoteConfigService', () {
    late _MockRemoteConfig sdk;
    late FakeCrashReporter crash;
    late FirebaseRemoteConfigService service;
    late StreamController<RemoteConfigUpdate> updates;
    late Map<String, Object> values;

    setUp(() {
      sdk = _MockRemoteConfig();
      crash = FakeCrashReporter();
      service = FirebaseRemoteConfigService(crash: crash, remoteConfig: () => sdk);
      updates = StreamController<RemoteConfigUpdate>.broadcast();
      values = Map.of(AppRemoteConfig.defaults.toMap());
      when(() => sdk.setDefaults(any())).thenAnswer((_) async {});
      when(() => sdk.setConfigSettings(any())).thenAnswer((_) async {});
      when(() => sdk.fetchAndActivate()).thenAnswer((_) async => true);
      when(() => sdk.activate()).thenAnswer((_) async => true);
      when(() => sdk.onConfigUpdated).thenAnswer((_) => updates.stream);
      when(() => sdk.getInt(any())).thenAnswer((i) => values[i.positionalArguments.first] as int);
      when(() => sdk.getBool(any())).thenAnswer((i) => values[i.positionalArguments.first] as bool);
      when(() => sdk.getString(any())).thenAnswer((i) => values[i.positionalArguments.first] as String);
    });

    tearDown(() => updates.close());

    test('mặc định trong app trùng firebase/remote-config-defaults.json', () {
      final json =
          jsonDecode(File('../firebase/remote-config-defaults.json').readAsStringSync()) as Map<String, dynamic>;
      expect(json, AppRemoteConfig.defaults.toMap());
    });

    test('initialize đặt mặc định, timeout rồi fetchAndActivate', () async {
      await service.initialize(timeout: const Duration(seconds: 3));

      verify(() => sdk.setDefaults(AppRemoteConfig.defaults.toMap())).called(1);
      final settings = verify(() => sdk.setConfigSettings(captureAny())).captured.single as RemoteConfigSettings;
      expect(settings.fetchTimeout, const Duration(seconds: 3));
      verify(() => sdk.fetchAndActivate()).called(1);
      expect(crash.errors, isEmpty);
    });

    test('mất mạng: giữ mặc định, ghi non-fatal, không ném lỗi', () async {
      when(() => sdk.fetchAndActivate()).thenThrow(FirebaseException(plugin: 'remote_config', code: 'internal'));

      await service.initialize();

      expect(crash.errors.single.reason, 'remote_config_fetch');
      expect(service.current, AppRemoteConfig.defaults);
    });

    test('fetch quá thời gian: ghi non-fatal, không chờ mãi', () async {
      when(() => sdk.fetchAndActivate()).thenAnswer((_) => Completer<bool>().future);

      await service.initialize(timeout: const Duration(milliseconds: 10));

      expect(crash.errors.single.error, isA<TimeoutException>());
    });

    test('current đọc giá trị từ SDK; giá trị sai thì dùng mặc định', () {
      values[AppRemoteConfig.keySuggestionsMaxItems] = 8;
      values[AppRemoteConfig.keyAiIdeaEnabled] = true;
      expect(service.current.suggestionsMaxItems, 8);
      expect(service.current.aiIdeaEnabled, isTrue);

      values[AppRemoteConfig.keySuggestionsMaxItems] = 0;
      values[AppRemoteConfig.keyAiModel] = ' ';
      expect(service.current.suggestionsMaxItems, AppRemoteConfig.defaultSuggestionsMaxItems);
      expect(service.current.aiModel, AppRemoteConfig.defaultAiModel);

      values[AppRemoteConfig.keySuggestionsMaxItems] = 500;
      expect(service.current.suggestionsMaxItems, 20);
    });

    test('SDK chưa khởi tạo: current là mặc định, updates rỗng', () async {
      final broken = FirebaseRemoteConfigService(crash: crash, remoteConfig: () => throw StateError('no app'));
      expect(broken.current, AppRemoteConfig.defaults);
      expect(await broken.updates.toList(), isEmpty);
    });

    test('remoteConfigProvider cập nhật khi Console publish (onConfigUpdated)', () async {
      final container = ProviderContainer(overrides: [remoteConfigServiceProvider.overrideWithValue(service)]);
      addTearDown(container.dispose);
      final sub = container.listen(remoteConfigProvider, (_, _) {});
      expect(sub.read().aiIdeaEnabled, isFalse);

      values[AppRemoteConfig.keyAiIdeaEnabled] = true;
      updates.add(RemoteConfigUpdate({AppRemoteConfig.keyAiIdeaEnabled}));
      await pumpEventQueue();

      verify(() => sdk.activate()).called(1);
      expect(sub.read().aiIdeaEnabled, isTrue);
    });
  });

  group('FakeRemoteConfigService (cho test màn hình)', () {
    test('push đổi giá trị của remoteConfigProvider', () async {
      final fake = FakeRemoteConfigService();
      final container = ProviderContainer(overrides: [remoteConfigServiceProvider.overrideWithValue(fake)]);
      addTearDown(container.dispose);
      final sub = container.listen(remoteConfigProvider, (_, _) {});

      fake.push(const AppRemoteConfig(suggestionsMaxItems: 3));
      await pumpEventQueue();

      expect(sub.read().suggestionsMaxItems, 3);
    });
  });

  group('AuthController gắn Analytics / Crashlytics', () {
    late FakeAnalyticsService analytics;
    late FakeCrashReporter crash;
    late ProviderContainer container;

    setUp(() {
      FlutterSecureStorage.setMockInitialValues({});
      analytics = FakeAnalyticsService();
      crash = FakeCrashReporter();
      final dio = Dio(BaseOptions(baseUrl: 'http://test'))
        ..interceptors.add(
          InterceptorsWrapper(
            onRequest: (o, h) {
              if (o.path == '/api/auth/google') {
                h.resolve(
                  Response(
                    requestOptions: o,
                    statusCode: 200,
                    data: {
                      'accessToken': 'a',
                      'accessTokenExpiresAt': '2030-01-01T00:00:00Z',
                      'refreshToken': 'r',
                      'refreshTokenExpiresAt': '2030-01-08T00:00:00Z',
                      'user': {
                        'id': 'u1',
                        'email': 'vy@fpt.edu.vn',
                        'fullName': 'Vy',
                        'roles': ['Student'],
                        'studentId': 's1',
                      },
                    },
                  ),
                );
              } else {
                h.resolve(Response(requestOptions: o, statusCode: 204));
              }
            },
          ),
        );
      container = ProviderContainer(
        overrides: [
          dioProvider.overrideWithValue(dio),
          analyticsServiceProvider.overrideWithValue(analytics),
          crashReporterProvider.overrideWithValue(crash),
          myCourseProvider.overrideWith((ref) async => 'EXE101'),
        ],
      );
      addTearDown(container.dispose);
    });

    test('đăng nhập: login(google), user id nội bộ, key role và course – không có email', () async {
      await container.read(authControllerProvider.future);
      await container.read(authControllerProvider.notifier).signInWithIdToken('id-token');
      await pumpEventQueue();

      expect(analytics.events.single.name, 'login');
      expect(analytics.events.single.parameters, {'method': 'google'});
      expect(analytics.userIds, ['u1']);
      expect(crash.userId, 'u1');
      expect(crash.keys, {'role': 'student', 'course': 'EXE101'});
      expect('${analytics.events}${crash.keys}', isNot(contains('@')));
    });

    test('đăng xuất: logout, bỏ user id và key', () async {
      await container.read(authControllerProvider.future);
      await container.read(authControllerProvider.notifier).signInWithIdToken('id-token');
      await pumpEventQueue();
      await container.read(authControllerProvider.notifier).signOut();

      expect(analytics.names, ['login', 'logout']);
      expect(analytics.userIds.last, isNull);
      expect(crash.userId, isNull);
      expect(crash.keys, isEmpty);
    });
  });
}
