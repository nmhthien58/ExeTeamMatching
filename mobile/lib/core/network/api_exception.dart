import 'package:dio/dio.dart';

/// Lỗi API thống nhất từ ProblemDetails (docs/contracts/README.md). Màn hình đọc [code]
/// (ví dụ `round-closed`, `not-in-list`) để hiện câu phù hợp và [fieldErrors] để báo lỗi tại ô nhập.
class ApiException implements Exception {
  const ApiException({
    required this.status,
    required this.code,
    required this.title,
    this.detail,
    this.traceId,
    this.fieldErrors = const {},
  });

  /// 0 = không tới được máy chủ (mất mạng, hết thời gian chờ).
  final int status;
  final String code;
  final String title;
  final String? detail;
  final String? traceId;
  final Map<String, List<String>> fieldErrors;

  bool get isNetwork => status == 0;

  factory ApiException.fromDio(DioException e) {
    final response = e.response;
    if (response == null) {
      return const ApiException(status: 0, code: 'network', title: 'Mất kết nối');
    }
    final data = response.data is Map ? Map<String, dynamic>.from(response.data as Map) : const <String, dynamic>{};
    final type = data['type'] as String?;
    final errors = <String, List<String>>{};
    if (data['errors'] is Map) {
      (data['errors'] as Map).forEach((k, v) => errors['$k'] = [for (final m in (v as List)) '$m']);
    }
    return ApiException(
      status: response.statusCode ?? -1,
      code: type?.split('/').last ?? 'http-${response.statusCode}',
      title: data['title'] as String? ?? 'Đã có lỗi xảy ra',
      detail: data['detail'] as String?,
      traceId: data['traceId'] as String? ?? response.headers.value('x-correlation-id'),
      fieldErrors: errors,
    );
  }

  @override
  String toString() => 'ApiException($status $code: $title)';
}
