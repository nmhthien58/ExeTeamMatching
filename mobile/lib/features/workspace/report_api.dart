import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers.dart';

/// Báo cáo PDF đã lưu ở `reports/{ownerId}/`.
class SavedReport {
  const SavedReport({required this.fileName, required this.sizeBytes, required this.createdAt});

  final String fileName;
  final int sizeBytes;
  final DateTime createdAt;

  factory SavedReport.fromJson(Map<String, dynamic> j) => SavedReport(
    fileName: j['fileName'] as String,
    sizeBytes: (j['sizeBytes'] as num).toInt(),
    createdAt: DateTime.parse(j['createdAt'] as String),
  );
}

/// API báo cáo của Collaboration Service (collab.yaml tag Reports). `ownerId` là id tài khoản đang đăng nhập;
/// backend từ chối 403 nếu không trùng JWT. Vì sao lưu S3: docs/file-storage.md.
class ReportApi {
  ReportApi(this._dio);

  final Dio _dio;

  /// Tải lên và trả báo cáo kèm link ký sẵn. [onProgress] nhận tỉ lệ 0–1.
  Future<({SavedReport report, String url})> upload(
    String ownerId,
    Uint8List pdf,
    String fileName, {
    void Function(double progress)? onProgress,
  }) async {
    final form = FormData.fromMap({'file': MultipartFile.fromBytes(pdf, filename: fileName)});
    final response = await _dio.post<Map<String, dynamic>>(
      '/api/reports/$ownerId',
      data: form,
      onSendProgress: (sent, total) {
        if (total > 0) onProgress?.call(sent / total);
      },
    );
    final json = response.data!;
    return (report: SavedReport.fromJson(json), url: json['url'] as String);
  }

  Future<List<SavedReport>> list(String ownerId) async {
    final response = await _dio.get<List<dynamic>>('/api/reports/$ownerId');
    return [for (final item in response.data!) SavedReport.fromJson(item as Map<String, dynamic>)];
  }

  /// Link ký sẵn mới; link cũ chỉ sống vài phút.
  Future<String> downloadUrl(String ownerId, String fileName) async {
    final response = await _dio.get<Map<String, dynamic>>('/api/reports/$ownerId/$fileName/download-url');
    return response.data!['url'] as String;
  }
}

final reportApiProvider = Provider<ReportApi>((ref) => ReportApi(ref.watch(dioProvider)));
