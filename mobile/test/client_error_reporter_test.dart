import 'dart:convert';
import 'dart:ui';

import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/firebase/client_error_api.dart';
import 'package:exe_team_matching/core/firebase/client_error_reporter.dart';
import 'package:exe_team_matching/core/firebase/crash_reporter.dart';
import 'package:exe_team_matching/core/firebase/firebase_bootstrap.dart';
import 'package:firebase_crashlytics/firebase_crashlytics.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:shared_preferences/shared_preferences.dart';

class _MockCrashlytics extends Mock implements FirebaseCrashlytics {}

class _FakeApi implements ClientErrorApi {
  final sent = <Map<String, Object?>>[];

  /// Kết quả theo thứ tự gọi; hết danh sách thì trả [ClientErrorSendResult.sent].
  final results = <ClientErrorSendResult>[];
  Object? throws;

  @override
  Future<ClientErrorSendResult> send(Map<String, Object?> report) async {
    if (throws case final error?) throw error;
    sent.add(report);
    return results.isEmpty ? ClientErrorSendResult.sent : results.removeAt(0);
  }
}

class _StatusAdapter implements HttpClientAdapter {
  _StatusAdapter(this.status);

  final int? status;
  RequestOptions? last;

  @override
  Future<ResponseBody> fetch(RequestOptions options, Stream<List<int>>? requestStream, Future<void>? cancelFuture) async {
    last = options;
    if (status == null) throw DioException.connectionError(requestOptions: options, reason: 'offline');
    return ResponseBody.fromString('', status!);
  }

  @override
  void close({bool force = false}) {}
}

/// MB-61 – báo cáo lỗi gửi về backend, với API giả.
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  final now = DateTime.utc(2026, 11, 5, 3);
  late _FakeApi api;
  late ClientErrorReporter reporter;

  setUpAll(() {
    registerFallbackValue(FlutterErrorDetails(exception: Exception('x')));
    registerFallbackValue(StackTrace.empty);
  });

  setUp(() {
    SharedPreferences.setMockInitialValues({});
    api = _FakeApi();
    reporter = ClientErrorReporter(
      api: api,
      now: () => now,
      deviceInfo: () async => const ClientDeviceInfo(
        platform: 'android',
        appVersion: '1.0.0+1',
        deviceModel: 'Google Pixel 8',
        osVersion: 'Android 15',
      ),
    );
  });

  Future<List<String>> pending() async =>
      (await SharedPreferences.getInstance()).getStringList(ClientErrorReporter.pendingKey) ?? [];

  group('non-fatal', () {
    test('gửi ngay với đủ thông tin máy và người dùng', () async {
      reporter
        ..userId = 'u1'
        ..role = 'Student'
        ..screen = '/sinh-vien/nhom/:id';

      await reporter.reportNonFatal(StateError('hỏng'), StackTrace.fromString('#0 main'), reason: 'S05');

      expect(api.sent.single, {
        'kind': 'non_fatal',
        'message': 'S05: Bad state: hỏng',
        'exceptionType': 'StateError',
        'stackTrace': '#0 main',
        'platform': 'android',
        'appVersion': '1.0.0+1',
        'deviceModel': 'Google Pixel 8',
        'osVersion': 'Android 15',
        'userId': 'u1',
        'role': 'Student',
        'screen': '/sinh-vien/nhom/:id',
        'occurredAt': '2026-11-05T03:00:00.000Z',
      });
      expect(await pending(), isEmpty);
    });

    test('che email, không có trường email hay tên', () async {
      await reporter.reportNonFatal(
        Exception('Không tìm thấy sv.a+1@fpt.edu.vn'),
        StackTrace.fromString('#0 load (a@b.co)'),
      );

      final report = api.sent.single;
      expect(report['message'], 'Exception: Không tìm thấy [email]');
      expect(report['stackTrace'], '#0 load ([email])');
      expect(report.keys, isNot(anyOf(contains('email'), contains('name'), contains('fullName'))));
    });

    test('cắt theo giới hạn của backend', () async {
      reporter.role = 'Admin,Lecturer,Mentor,Student';

      await reporter.reportNonFatal('x' * 3000, StackTrace.fromString('y' * 9000));

      final report = api.sent.single;
      expect((report['message'] as String).length, 2000);
      expect((report['stackTrace'] as String).length, 8192);
      expect((report['role'] as String).length, 20);
    });

    test('lỗi mạng không ném ra ngoài', () async {
      api.throws = Exception('offline');

      await expectLater(reporter.reportNonFatal('e', null), completes);
    });

    test('đọc thông tin máy một lần', () async {
      var reads = 0;
      final counted = ClientErrorReporter(
        api: api,
        deviceInfo: () async {
          reads++;
          return const ClientDeviceInfo(platform: 'android', appVersion: '1');
        },
      );

      await counted.reportNonFatal('a', null);
      await counted.reportNonFatal('b', null);

      expect(reads, 1);
    });
  });

  group('fatal', () {
    test('chỉ lưu lại, không gửi ngay', () async {
      reporter.userId = 'u1';

      await reporter.saveFatal(StateError('hỏng'), StackTrace.fromString('#0 main'));

      expect(api.sent, isEmpty);
      final saved = jsonDecode((await pending()).single) as Map<String, dynamic>;
      expect(saved['kind'], 'fatal');
      expect(saved['userId'], 'u1');
    });

    test('lần mở app sau gửi rồi xóa', () async {
      await reporter.saveFatal('a', null);
      await reporter.saveFatal('b', null);

      await reporter.flushPending();

      expect([for (final r in api.sent) r['message']], ['a', 'b']);
      expect(await pending(), isEmpty);
    });

    test('mất mạng thì giữ báo cáo chưa gửi được', () async {
      await reporter.saveFatal('a', null);
      await reporter.saveFatal('b', null);
      await reporter.saveFatal('c', null);
      api.results.addAll([ClientErrorSendResult.sent, ClientErrorSendResult.retryLater]);

      await reporter.flushPending();

      expect(api.sent, hasLength(2));
      expect(await pending(), hasLength(2));

      await reporter.flushPending();
      expect(await pending(), isEmpty);
    });

    test('server từ chối hoặc dữ liệu hỏng thì bỏ, không kẹt hàng đợi', () async {
      await reporter.saveFatal('a', null);
      final prefs = await SharedPreferences.getInstance();
      await prefs.setStringList(ClientErrorReporter.pendingKey, ['không phải json', '[1]', ...await pending()]);
      api.results.add(ClientErrorSendResult.rejected);

      await reporter.flushPending();

      expect(api.sent, hasLength(1));
      expect(await pending(), isEmpty);
    });

    test('lỗi mới lưu trong lúc gửi không bị mất', () async {
      await reporter.saveFatal('a', null);
      final saving = _SavingApi(() => reporter.saveFatal('mới', null));
      final flushing = ClientErrorReporter(api: saving, deviceInfo: () async => const ClientDeviceInfo(platform: 'android', appVersion: '1'));

      await flushing.flushPending();

      final left = jsonDecode((await pending()).single) as Map<String, dynamic>;
      expect(left['message'], 'mới');
    });

    test('hàng đợi giữ tối đa 20 báo cáo mới nhất', () async {
      for (var i = 0; i < ClientErrorReporter.maxPending + 5; i++) {
        await reporter.saveFatal('lỗi $i', null);
      }

      final saved = await pending();
      expect(saved, hasLength(ClientErrorReporter.maxPending));
      expect((jsonDecode(saved.first) as Map)['message'], 'lỗi 5');
      expect((jsonDecode(saved.last) as Map)['message'], 'lỗi 24');
    });
  });

  group('DioClientErrorApi', () {
    Future<ClientErrorSendResult> sendWith(_StatusAdapter adapter) {
      final dio = Dio(BaseOptions(baseUrl: 'http://api.test'))..httpClientAdapter = adapter;
      return DioClientErrorApi(dio: dio).send({'kind': 'fatal'});
    }

    test('202 là đã gửi, gọi đúng endpoint', () async {
      final adapter = _StatusAdapter(202);

      expect(await sendWith(adapter), ClientErrorSendResult.sent);
      expect(adapter.last!.path, '/api/client-errors');
      expect(adapter.last!.method, 'POST');
    });

    test('400 thì bỏ; 429, 500, mất mạng thì gửi lại sau', () async {
      expect(await sendWith(_StatusAdapter(400)), ClientErrorSendResult.rejected);
      expect(await sendWith(_StatusAdapter(429)), ClientErrorSendResult.retryLater);
      expect(await sendWith(_StatusAdapter(500)), ClientErrorSendResult.retryLater);
      expect(await sendWith(_StatusAdapter(null)), ClientErrorSendResult.retryLater);
    });
  });

  group('FirebaseCrashReporter gửi thêm về backend', () {
    late _MockCrashlytics sdk;

    setUp(() {
      sdk = _MockCrashlytics();
      when(() => sdk.recordError(any(), any(), reason: any(named: 'reason'), fatal: any(named: 'fatal')))
          .thenAnswer((_) async {});
      when(() => sdk.setUserIdentifier(any())).thenAnswer((_) async {});
      when(() => sdk.setCustomKey(any(), any())).thenAnswer((_) async {});
      when(() => sdk.crash()).thenReturn(null);
    });

    FirebaseCrashReporter crashReporter({bool mirror = true}) =>
        FirebaseCrashReporter(crashlytics: () => sdk, clientErrors: reporter, mirrorToBackend: mirror);

    test('recordNonFatal gửi cả Crashlytics và backend', () async {
      await crashReporter().recordNonFatal(Exception('x'), null, reason: 'S05');
      await pumpEventQueue();

      verify(() => sdk.recordError(any(), any(), reason: 'S05', fatal: false)).called(1);
      expect(api.sent.single['kind'], 'non_fatal');
    });

    test('Crashlytics tắt thu thập thì không gửi backend', () async {
      final crash = crashReporter(mirror: false);

      await crash.recordNonFatal(Exception('x'), null);
      await crash.testCrash();
      await pumpEventQueue();

      expect(api.sent, isEmpty);
      expect(await pending(), isEmpty);
    });

    test('Crash thử lưu báo cáo trước rồi mới crash', () async {
      final prefs = await SharedPreferences.getInstance();
      List<String>? savedAtCrash;
      when(() => sdk.crash()).thenAnswer((_) => savedAtCrash = prefs.getStringList(ClientErrorReporter.pendingKey));

      await crashReporter().testCrash();

      verify(() => sdk.crash()).called(1);
      expect(savedAtCrash, hasLength(1));
      final saved = jsonDecode((await pending()).single) as Map<String, dynamic>;
      expect(saved['kind'], 'fatal');
      expect(saved['exceptionType'], 'TestCrashError');
    });

    test('user, role, screen đi theo; đăng xuất thì bỏ', () async {
      final crash = crashReporter();

      await crash.setUser('u1');
      await crash.setKey('role', 'Student');
      await crash.setKey('screen', '/sinh-vien');
      await crash.setKey('course', 'EXE101');

      expect(reporter.userId, 'u1');
      expect(reporter.role, 'Student');
      expect(reporter.screen, '/sinh-vien');

      await crash.clearUser();

      expect(reporter.userId, isNull);
      expect(reporter.role, isNull);
    });
  });

  group('installFatalHandlers', () {
    late _MockCrashlytics sdk;
    late FlutterExceptionHandler? oldFlutterHandler;
    late ErrorCallback? oldPlatformHandler;

    setUp(() {
      sdk = _MockCrashlytics();
      oldFlutterHandler = FlutterError.onError;
      oldPlatformHandler = PlatformDispatcher.instance.onError;
      when(() => sdk.recordFlutterFatalError(any())).thenAnswer((_) async {});
      when(() => sdk.recordError(any(), any(), fatal: any(named: 'fatal'))).thenAnswer((_) async {});
      installFatalHandlers(sdk, reporter);
    });

    tearDown(() {
      FlutterError.onError = oldFlutterHandler;
      PlatformDispatcher.instance.onError = oldPlatformHandler;
    });

    test('lỗi Flutter: lưu báo cáo và báo Crashlytics', () async {
      final details = FlutterErrorDetails(exception: StateError('build hỏng'));

      FlutterError.onError!(details);
      await pumpEventQueue();

      verify(() => sdk.recordFlutterFatalError(details)).called(1);
      expect(await pending(), hasLength(1));
    });

    test('lỗi bất đồng bộ: lưu báo cáo, báo Crashlytics và coi là đã xử lý', () async {
      final handled = PlatformDispatcher.instance.onError!(StateError('async hỏng'), StackTrace.empty);
      await pumpEventQueue();

      expect(handled, isTrue);
      verify(() => sdk.recordError(any(), any(), fatal: true)).called(1);
      expect(await pending(), hasLength(1));
    });
  });
}

/// Trong lúc gửi thì chạy [during], giả lập lỗi mới xảy ra khi đang gửi báo cáo cũ.
class _SavingApi implements ClientErrorApi {
  _SavingApi(this.during);

  final Future<void> Function() during;

  @override
  Future<ClientErrorSendResult> send(Map<String, Object?> report) async {
    await during();
    return ClientErrorSendResult.sent;
  }
}
