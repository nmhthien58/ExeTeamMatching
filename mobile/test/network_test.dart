import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/auth/session.dart';
import 'package:exe_team_matching/core/network/api_client.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';

/// Máy chủ giả: 401 khi token khác `fresh`; `/problem` trả ProblemDetails 409; `/api/auth/refresh` cấp token mới.
class FakeAdapter implements HttpClientAdapter {
  FakeAdapter({this.refreshFails = false});

  final bool refreshFails;
  int refreshCalls = 0;

  @override
  Future<ResponseBody> fetch(RequestOptions options, Stream<Uint8List>? requestStream, Future<void>? cancelFuture) async {
    ResponseBody json(int status, Object body) => ResponseBody.fromString(jsonEncode(body), status, headers: {
          Headers.contentTypeHeader: [Headers.jsonContentType],
        });
    if (options.path == '/api/auth/refresh') {
      refreshCalls++;
      if (refreshFails) return json(401, {'title': 'Phiên hết hạn'});
      return json(200, {
        'accessToken': 'fresh',
        'refreshToken': 'r2',
        'user': {'id': 'u', 'email': 'a@fpt.edu.vn', 'fullName': 'A', 'roles': ['Student']},
      });
    }
    if (options.path == '/problem') {
      return json(409, {
        'type': 'https://exe-team-matching/errors/round-closed',
        'title': 'Đợt đăng ký đã khóa',
        'status': 409,
        'traceId': 't-1',
        'errors': {
          'name': ['Bắt buộc'],
        },
      });
    }
    if (options.headers['Authorization'] != 'Bearer fresh') return json(401, {});
    return json(200, {'ok': true});
  }

  @override
  void close({bool force = false}) {}
}

Future<SessionStore> sessionWith(String access) async {
  final store = SessionStore();
  await store.save(TokenResponse.fromJson({
    'accessToken': access,
    'refreshToken': 'r1',
    'user': {'id': 'u', 'email': 'a@fpt.edu.vn', 'fullName': 'A', 'roles': ['Student']},
  }));
  return store;
}

Future<Object?> errorOf(Future<Object?> call) async {
  try {
    await call;
    return null;
  } catch (e) {
    return e;
  }
}

void main() {
  setUp(() => FlutterSecureStorage.setMockInitialValues({}));

  test('refreshes once on 401 for concurrent requests and retries them', () async {
    final session = await sessionWith('expired');
    final dio = createDio(session: session, baseUrl: 'http://gw');
    final adapter = FakeAdapter();
    dio.httpClientAdapter = adapter;
    // Dio refresh riêng dùng adapter mặc định – thay bằng adapter giả qua interceptor của nó.
    final interceptor = dio.interceptors.whereType<AuthInterceptor>().single;
    interceptor.refreshDioAdapter = adapter;

    final results = await Future.wait([dio.get<dynamic>('/a'), dio.get<dynamic>('/b')]);

    expect(results.map((r) => r.data['ok']), [true, true]);
    expect(adapter.refreshCalls, 1);
    expect(session.accessToken, 'fresh');
    expect(await session.readRefreshToken(), 'r2');
  });

  test('clears the session and reports expiry when refresh fails', () async {
    final session = await sessionWith('expired');
    var expired = false;
    final dio = createDio(session: session, baseUrl: 'http://gw', onSessionExpired: () => expired = true);
    final adapter = FakeAdapter(refreshFails: true);
    dio.httpClientAdapter = adapter;
    dio.interceptors.whereType<AuthInterceptor>().single.refreshDioAdapter = adapter;

    final error = await errorOf(dio.get<dynamic>('/a'));

    expect(error!.apiError.status, 401);
    expect(expired, isTrue);
    expect(await session.readUser(), isNull);
  });

  test('maps ProblemDetails to ApiException', () async {
    final dio = createDio(session: await sessionWith('fresh'), baseUrl: 'http://gw')..httpClientAdapter = FakeAdapter();

    final error = await errorOf(dio.get<dynamic>('/problem'));

    final api = error!.apiError;
    expect(api, isA<ApiException>());
    expect(api.status, 409);
    expect(api.code, 'round-closed');
    expect(api.traceId, 't-1');
    expect(api.fieldErrors['name'], ['Bắt buộc']);
  });
}
