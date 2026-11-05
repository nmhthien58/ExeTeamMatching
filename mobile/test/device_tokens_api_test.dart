import 'package:dio/dio.dart';
import 'package:exe_team_matching/features/notifications/device_tokens_api.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('đăng ký và gỡ đúng device target qua BE-84', () async {
    final requests = <RequestOptions>[];
    final dio = Dio(BaseOptions(baseUrl: 'http://test'))
      ..interceptors.add(
        InterceptorsWrapper(
          onRequest: (request, handler) {
            requests.add(request);
            handler.resolve(
              Response<void>(requestOptions: request, statusCode: 204),
            );
          },
        ),
      );
    final api = DeviceTokensApi(dio);

    await api.register('fid:test/device');
    await api.remove('fid:test/device');

    expect(requests, hasLength(2));
    expect(
      (requests[0].method, requests[0].path),
      ('POST', '/api/notifications/devices'),
    );
    expect(requests[0].data, {
      'token': 'fid:test/device',
      'platform': 'android',
    });
    expect(requests[1].method, 'DELETE');
    expect(requests[1].path, '/api/notifications/devices/fid%3Atest%2Fdevice');
  });
}
