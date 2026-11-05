import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/firebase/analytics_service.dart';
import '../../core/network/api_client.dart';
import '../../core/providers.dart';
import '../files/file_services.dart';
import 'report_api.dart';
import 'report_pdf.dart';
import 'workspace_models.dart';

/// Trạng thái xuất báo cáo ở C01.
sealed class ReportExportState {
  const ReportExportState();
}

final class ReportIdle extends ReportExportState {
  const ReportIdle();
}

final class ReportGenerating extends ReportExportState {
  const ReportGenerating();
}

final class ReportUploading extends ReportExportState {
  const ReportUploading(this.progress);

  /// 0–1.
  final double progress;
}

final class ReportSaved extends ReportExportState {
  const ReportSaved({required this.report, required this.url, this.openError});

  final SavedReport report;
  final String url;

  /// Câu lỗi khi không mở được file; báo cáo vẫn đã lưu.
  final String? openError;
}

final class ReportFailed extends ReportExportState {
  const ReportFailed(this.message);

  final String message;
}

/// Dữ liệu báo cáo từ những gì C01 đang hiện.
ProjectReportData reportDataFrom(
  WorkspaceOverview workspace, {
  required ({int mine, int dueSoon, int pendingReview}) stats,
  required DateTime now,
}) => ProjectReportData(
  teamName: workspace.teamName,
  courseCode: workspace.courseCode,
  projectName: workspace.projectName,
  generatedAt: now,
  people: [for (final p in workspace.people) (name: p.displayName, role: _roleLabel(p.role))],
  stats: [
    workspace.canEdit ? (label: 'Việc của tôi', value: stats.mine) : (label: 'Bài nộp chờ duyệt', value: stats.pendingReview),
    (label: 'Sắp đến hạn', value: stats.dueSoon),
    (label: 'Họp sắp tới', value: workspace.upcomingMeetingCount),
    (label: 'Câu hỏi chờ', value: workspace.openQuestionCount),
  ],
  activity: [for (final a in workspace.recentActivity.take(5)) (title: a.title, at: a.at)],
);

String _roleLabel(WorkspaceRole? role) => switch (role) {
  WorkspaceRole.leader => 'Trưởng nhóm',
  WorkspaceRole.instructor => 'Giảng viên',
  WorkspaceRole.mentor => 'Mentor',
  _ => '',
};

/// Tên hiện khi tải về; bỏ ký tự không dùng được trong tên file.
String reportFileName(String teamName) {
  final safe = teamName.replaceAll(RegExp(r'[\\/:*?"<>|\x00-\x1f]'), ' ').trim();
  final short = safe.length > 80 ? safe.substring(0, 80).trim() : safe;
  return 'Bao cao ${short.isEmpty ? 'du an' : short}.pdf';
}

/// ViewModel xuất báo cáo PDF (MB-53): tạo PDF → tải lên → cho mở file.
class ReportExportViewModel extends Notifier<ReportExportState> {
  ReportExportViewModel(this.workspaceId);

  final String workspaceId;

  @override
  ReportExportState build() => const ReportIdle();

  Future<void> export(ProjectReportData data) async {
    if (state is ReportGenerating || state is ReportUploading) return;
    state = const ReportGenerating();
    try {
      final ownerId = (await ref.read(authControllerProvider.future))?.id;
      if (!ref.mounted) return;
      if (ownerId == null) {
        state = const ReportFailed('Phiên đăng nhập đã hết. Vui lòng đăng nhập lại.');
        return;
      }
      final pdf = await ref.read(reportPdfBuilderProvider).build(data);
      if (!ref.mounted) return;
      state = const ReportUploading(0);
      final saved = await ref.read(reportApiProvider).upload(
        ownerId,
        pdf,
        reportFileName(data.teamName),
        onProgress: (progress) {
          if (ref.mounted) state = ReportUploading(progress);
        },
      );
      if (!ref.mounted) return;
      state = ReportSaved(report: saved.report, url: saved.url);
      unawaited(ref.read(analyticsServiceProvider).logExportReport(reportType: 'project_report', format: 'pdf'));
    } catch (e) {
      if (ref.mounted) state = ReportFailed(_message(e));
    }
  }

  /// Xin link mới rồi mở; link cũ có thể đã hết hạn.
  Future<void> open() async {
    final current = state;
    if (current is! ReportSaved) return;
    try {
      final ownerId = (await ref.read(authControllerProvider.future))?.id;
      if (ownerId == null) return;
      final url = await ref.read(reportApiProvider).downloadUrl(ownerId, current.report.fileName);
      await ref.read(openUrlProvider)(url);
      if (ref.mounted) state = ReportSaved(report: current.report, url: url);
    } catch (e) {
      if (ref.mounted) state = ReportSaved(report: current.report, url: current.url, openError: _message(e));
    }
  }

  String _message(Object error) {
    final api = error.apiError;
    return api.isNetwork ? 'Mất kết nối. Kiểm tra mạng rồi thử lại.' : api.detail ?? api.title;
  }
}

final reportExportViewModelProvider =
    NotifierProvider.autoDispose.family<ReportExportViewModel, ReportExportState, String>(ReportExportViewModel.new);
