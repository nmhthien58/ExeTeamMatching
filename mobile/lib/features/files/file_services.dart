import 'package:dio/dio.dart';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:url_launcher/url_launcher.dart';

/// Quy tắc 24: tối đa 25 MB; pdf, docx, pptx, xlsx, png, jpg, zip. Kiểm tra trước khi tải lên (server vẫn kiểm tra lại).
const maxFileBytes = 25 * 1024 * 1024;
const allowedExtensions = ['pdf', 'docx', 'pptx', 'xlsx', 'png', 'jpg', 'jpeg', 'zip'];

/// Câu lỗi tại chỗ; null = hợp lệ.
String? validateUpload(String name, int size) {
  final ext = name.contains('.') ? name.split('.').last.toLowerCase() : '';
  if (!allowedExtensions.contains(ext)) return 'Chỉ nhận pdf, docx, pptx, xlsx, png, jpg, zip.';
  if (size <= 0) return 'File rỗng.';
  if (size > maxFileBytes) return 'File quá 25 MB. Hãy nén hoặc chia nhỏ rồi tải lại.';
  return null;
}

/// File người dùng chọn từ máy, sẵn sàng gửi multipart.
class PickedFile {
  const PickedFile({required this.name, required this.size, required this.toMultipart});

  final String name;
  final int size;
  final Future<MultipartFile> Function() toMultipart;
}

/// Chọn một file từ máy (ghi đè trong test). Null = người dùng hủy.
final pickLocalFileProvider = Provider<Future<PickedFile?> Function()>((ref) => () async {
      final result = await FilePicker.pickFiles(type: FileType.custom, allowedExtensions: allowedExtensions, withData: kIsWeb);
      final file = result?.files.singleOrNull;
      if (file == null) return null;
      return PickedFile(
        name: file.name,
        size: file.size,
        toMultipart: () async => file.path != null && !kIsWeb
            ? MultipartFile.fromFile(file.path!, filename: file.name)
            : MultipartFile.fromBytes(file.bytes!, filename: file.name),
      );
    });

/// Mở URL ký sẵn của file bằng ứng dụng ngoài (ghi đè trong test).
final openUrlProvider = Provider<Future<bool> Function(String url)>(
  (ref) => (url) => launchUrl(Uri.parse(url), mode: LaunchMode.externalApplication),
);

/// Icon theo loại file (dòng file – DESIGN.md 6.3).
IconData fileIcon(String name) => switch (name.split('.').last.toLowerCase()) {
      'pdf' => Icons.picture_as_pdf_outlined,
      'docx' => Icons.description_outlined,
      'pptx' => Icons.slideshow_outlined,
      'xlsx' => Icons.table_chart_outlined,
      'png' || 'jpg' || 'jpeg' => Icons.image_outlined,
      'zip' => Icons.folder_zip_outlined,
      _ => Icons.insert_drive_file_outlined,
    };
