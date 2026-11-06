import 'package:dio/dio.dart';

import '../config/env.dart';

/// Kết quả gửi một báo cáo lỗi.
enum ClientErrorSendResult {
  /// Server đã nhận.
  sent,

  /// Server từ chối dữ liệu; gửi lại cũng vậy nên bỏ báo cáo này.
  rejected,

  /// Mất mạng, server lỗi hoặc bị giới hạn tần suất; giữ lại để lần sau gửi.
  retryLater,
}

/// Gửi báo cáo lỗi về backend (BE-87) để Bộ môn xem ở W30.
abstract interface class ClientErrorApi {
  Future<ClientErrorSendResult> send(Map<String, Object?> report);
}

/// Không dùng `dioProvider`: báo cáo được tạo cả khi chưa có `ProviderScope` (lỗi lúc khởi động),
/// endpoint không cần JWT, và không được kéo theo luồng refresh token khi đang xử lý lỗi.
class DioClientErrorApi implements ClientErrorApi {
  DioClientErrorApi({Dio? dio})
    : _dio =
          dio ??
          Dio(
            BaseOptions(
              baseUrl: Env.apiBaseUrl,
              connectTimeout: const Duration(seconds: 5),
              sendTimeout: const Duration(seconds: 5),
              receiveTimeout: const Duration(seconds: 5),
              contentType: Headers.jsonContentType,
            ),
          );

  final Dio _dio;

  @override
  Future<ClientErrorSendResult> send(Map<String, Object?> report) async {
    try {
      await _dio.post<void>('/api/client-errors', data: report);
      return ClientErrorSendResult.sent;
    } on DioException catch (e) {
      final status = e.response?.statusCode;
      final rejected = status != null && status >= 400 && status < 500 && status != 429;
      return rejected ? ClientErrorSendResult.rejected : ClientErrorSendResult.retryLater;
    }
  }
}
