import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../auth/session.dart';
import '../config/env.dart';
import 'api_exception.dart';

/// Tạo Dio dùng chung (MB-04): gắn JWT, tự refresh khi 401 (một lần cho mọi request đang chờ),
/// đổi lỗi thành [ApiException]. Màn hình không tạo Dio riêng mà dùng `dioProvider`.
Dio createDio({required SessionStore session, String? baseUrl, void Function()? onSessionExpired}) {
  final dio = Dio(BaseOptions(
    baseUrl: baseUrl ?? Env.apiBaseUrl,
    connectTimeout: const Duration(seconds: 10),
    receiveTimeout: const Duration(seconds: 20),
    contentType: Headers.jsonContentType,
  ));
  dio.interceptors.add(AuthInterceptor(dio: dio, session: session, onSessionExpired: onSessionExpired));
  return dio;
}

class AuthInterceptor extends Interceptor {
  AuthInterceptor({required this.dio, required this.session, this.onSessionExpired});

  final Dio dio;
  final SessionStore session;
  final void Function()? onSessionExpired;
  Completer<String?>? _refreshing;

  /// Cho test: adapter giả của lời gọi refresh.
  @visibleForTesting
  HttpClientAdapter? refreshDioAdapter;

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    final token = session.accessToken;
    if (token != null) options.headers['Authorization'] = 'Bearer $token';
    handler.next(options);
  }

  @override
  Future<void> onError(DioException err, ErrorInterceptorHandler handler) async {
    final options = err.requestOptions;
    final isAuthCall = options.path.contains('/api/auth/');
    if (err.response?.statusCode == 401 && options.extra['retried'] != true && !isAuthCall) {
      final token = await _refresh();
      if (token != null) {
        options.extra['retried'] = true;
        options.headers['Authorization'] = 'Bearer $token';
        try {
          return handler.resolve(await dio.fetch<dynamic>(options));
        } on DioException catch (e) {
          return handler.reject(_wrap(e));
        }
      }
    }
    handler.reject(_wrap(err));
  }

  DioException _wrap(DioException e) =>
      e.error is ApiException ? e : e.copyWith(error: ApiException.fromDio(e));

  /// Một lần refresh cho mọi request đang chờ.
  Future<String?> _refresh() {
    final running = _refreshing;
    if (running != null) return running.future;
    final completer = _refreshing = Completer<String?>();
    () async {
      try {
        final refreshToken = await session.readRefreshToken();
        if (refreshToken == null) {
          completer.complete(null);
          return;
        }
        // Dio riêng để không đi lại vào interceptor này.
        final plain = Dio(BaseOptions(baseUrl: dio.options.baseUrl, contentType: Headers.jsonContentType));
        if (refreshDioAdapter != null) plain.httpClientAdapter = refreshDioAdapter!;
        final response = await plain.post<Map<String, dynamic>>('/api/auth/refresh', data: {'refreshToken': refreshToken});
        final tokens = TokenResponse.fromJson(response.data!);
        await session.save(tokens);
        completer.complete(tokens.accessToken);
      } catch (_) {
        await session.clear();
        onSessionExpired?.call();
        completer.complete(null);
      } finally {
        _refreshing = null;
      }
    }();
    return completer.future;
  }
}

extension ApiErrorOf on Object {
  /// Lấy [ApiException] từ lỗi bắt được ở màn hình (`catch (e) { e.apiError }`).
  ApiException get apiError {
    final self = this;
    if (self is ApiException) return self;
    if (self is DioException) return self.error is ApiException ? self.error as ApiException : ApiException.fromDio(self);
    return const ApiException(status: -1, code: 'unknown', title: 'Đã có lỗi xảy ra');
  }
}
