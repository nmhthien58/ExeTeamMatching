import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers.dart';

final deviceTokensApiProvider = Provider<DeviceTokensApi>(
  (ref) => DeviceTokensApi(ref.watch(dioProvider)),
);

/// API đăng ký thiết bị của BE-84. Token chỉ được gửi sau khi người dùng đã đăng nhập.
class DeviceTokensApi {
  const DeviceTokensApi(this._dio);

  final Dio _dio;

  Future<void> register(String token) => _dio.post<void>(
    '/api/notifications/devices',
    data: {
      'token': token,
      'platform': defaultTargetPlatform == TargetPlatform.iOS
          ? 'ios'
          : 'android',
    },
  );

  Future<void> remove(String token) => _dio.delete<void>(
    '/api/notifications/devices/${Uri.encodeComponent(token)}',
  );
}
