import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;

import 'format.dart';

/// Nội dung báo cáo, đúng những gì C01 đang hiện.
class ProjectReportData {
  const ProjectReportData({
    required this.teamName,
    required this.courseCode,
    required this.generatedAt,
    this.projectName,
    this.people = const [],
    this.stats = const [],
    this.activity = const [],
  });

  final String teamName;
  final String courseCode;
  final String? projectName;
  final DateTime generatedAt;
  final List<({String name, String role})> people;
  final List<({String label, int value})> stats;
  final List<({String title, DateTime at})> activity;
}

/// Tạo file PDF báo cáo dự án (MB-53). Font nhúng trong app vì font mặc định của PDF không có dấu tiếng Việt.
class ReportPdfBuilder {
  ReportPdfBuilder({Future<ByteData> Function(String asset)? loadAsset}) : _loadAsset = loadAsset ?? rootBundle.load;

  final Future<ByteData> Function(String asset) _loadAsset;

  Future<Uint8List> build(ProjectReportData data) async {
    final regular = pw.Font.ttf(await _loadAsset('assets/fonts/BeVietnamPro-Regular.ttf'));
    final bold = pw.Font.ttf(await _loadAsset('assets/fonts/BeVietnamPro-SemiBold.ttf'));
    final heading = pw.TextStyle(font: bold, fontSize: 13);

    final doc = pw.Document(title: 'Báo cáo dự án ${data.teamName}', author: 'EXE Team Matching');
    doc.addPage(
      pw.MultiPage(
        pageFormat: PdfPageFormat.a4,
        margin: const pw.EdgeInsets.all(40),
        theme: pw.ThemeData.withFont(base: regular, bold: bold),
        footer: (context) => pw.Align(
          alignment: pw.Alignment.centerRight,
          child: pw.Text('Trang ${context.pageNumber}/${context.pagesCount}', style: const pw.TextStyle(fontSize: 9)),
        ),
        build: (context) => [
          pw.Text('Báo cáo dự án', style: pw.TextStyle(font: bold, fontSize: 20)),
          pw.SizedBox(height: 4),
          pw.Text('Xuất lúc ${formatDateTime(data.generatedAt)}', style: const pw.TextStyle(fontSize: 10)),
          pw.SizedBox(height: 16),
          pw.Text(data.teamName, style: pw.TextStyle(font: bold, fontSize: 16)),
          pw.Text(data.courseCode),
          if (data.projectName != null) pw.Text(data.projectName!),
          if (data.people.isNotEmpty) ...[
            _gap(),
            pw.Text('Thành viên và cố vấn', style: heading),
            pw.SizedBox(height: 6),
            for (final p in data.people) pw.Text(p.role.isEmpty ? p.name : '${p.name} (${p.role})'),
          ],
          if (data.stats.isNotEmpty) ...[
            _gap(),
            pw.Text('Số liệu', style: heading),
            pw.SizedBox(height: 6),
            pw.TableHelper.fromTextArray(
              headers: const ['Mục', 'Số lượng'],
              data: [for (final s in data.stats) [s.label, '${s.value}']],
              headerStyle: pw.TextStyle(font: bold),
              cellAlignments: const {1: pw.Alignment.centerRight},
            ),
          ],
          _gap(),
          pw.Text('Mới cập nhật', style: heading),
          pw.SizedBox(height: 6),
          if (data.activity.isEmpty) pw.Text('Chưa có cập nhật mới.'),
          for (final a in data.activity) pw.Text('${formatDateTime(a.at)}  ${a.title}'),
        ],
      ),
    );
    return doc.save();
  }

  pw.Widget _gap() => pw.SizedBox(height: 16);
}

final reportPdfBuilderProvider = Provider<ReportPdfBuilder>((ref) => ReportPdfBuilder());
