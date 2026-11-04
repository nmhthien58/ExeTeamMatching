import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/phase.dart';
import '../../core/providers.dart';

/// Nội dung header trang chủ (S01, sau này L01, M01), tính sẵn để View chỉ việc vẽ.
class BrandHeaderData {
  const BrandHeaderData({
    required this.greeting,
    required this.dateLine,
    this.context,
    this.chip,
    this.semesterProgress,
    this.progressLabel,
  });

  /// "Chào Thiên".
  final String greeting;

  /// "Thứ Năm, 05/11/2026 · Tuần 2/15".
  final String dateLine;

  /// Dòng dưới tên ứng dụng: "EXE101 · Học kỳ SP90".
  final String? context;

  /// Chip góc phải: "Còn 20 ngày" (hạn lập nhóm) hoặc "Đang làm dự án".
  final String? chip;

  /// Phần học kỳ đã qua (0–1) cho vạch nhấn; `null` khi chưa có học kỳ.
  final double? semesterProgress;
  final String? progressLabel;

  static const _weekdays = ['Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy', 'Chủ nhật'];

  /// Hàm thuần (test không cần widget). [now] theo giờ máy.
  static BrandHeaderData compute({
    required DateTime now,
    String? fullName,
    String? courseCode,
    CurrentSemester? semester,
    CoursePhase? phase,
  }) {
    String two(int v) => v.toString().padLeft(2, '0');
    final today = DateTime(now.year, now.month, now.day);
    final name = fullName?.trim().split(RegExp(r'\s+')).last ?? '';

    String? week;
    double? progress;
    if (semester != null) {
      final start = DateTime(semester.startDate.year, semester.startDate.month, semester.startDate.day);
      final end = DateTime(semester.endDate.year, semester.endDate.month, semester.endDate.day);
      final totalDays = end.difference(start).inDays + 1;
      final totalWeeks = (totalDays / 7).ceil();
      final elapsed = today.difference(start).inDays;
      if (elapsed >= 0 && !today.isAfter(end)) week = 'Tuần ${elapsed ~/ 7 + 1}/$totalWeeks';
      progress = (elapsed / totalDays).clamp(0.0, 1.0);
    }

    String? chip;
    final deadline = phase?.teamFormationDeadlineAt?.toLocal();
    if (phase != null && phase.isTeamFormation && deadline != null) {
      final days = DateTime(deadline.year, deadline.month, deadline.day).difference(today).inDays;
      // Ngắn để vừa cạnh tên ứng dụng; giờ và ngày cụ thể ở DeadlineBanner ngay bên dưới.
      chip = days <= 0 ? 'Hạn hôm nay' : 'Còn $days ngày';
    } else if (phase != null) {
      chip = 'Đang làm dự án';
    }

    return BrandHeaderData(
      greeting: name.isEmpty ? 'Xin chào' : 'Chào $name',
      dateLine: [
        '${_weekdays[now.weekday - 1]}, ${two(now.day)}/${two(now.month)}/${now.year}',
        ?week,
      ].join(' · '),
      context: [?courseCode, if (semester != null) 'Học kỳ ${semester.code}'].join(' · ').ifEmptyNull,
      chip: chip,
      semesterProgress: progress,
      progressLabel: progress == null ? null : 'Học kỳ đã qua ${(progress * 100).round()}%',
    );
  }
}

extension on String {
  String? get ifEmptyNull => isEmpty ? null : this;
}

/// Đồng hồ cho header; test ghi đè để cố định ngày.
final headerClockProvider = Provider<DateTime Function()>((ref) => DateTime.now);

/// ViewModel của header (chỉ dẫn xuất, không có lệnh): ghép phiên, môn, học kỳ, giai đoạn.
/// Dữ liệu nào chưa tải được thì phần đó ẩn, header vẫn hiện logo và lời chào.
final brandHeaderProvider = Provider.autoDispose<BrandHeaderData>((ref) {
  final user = ref.watch(authControllerProvider).value;
  return BrandHeaderData.compute(
    now: ref.watch(headerClockProvider)(),
    fullName: user?.fullName,
    courseCode: ref.watch(myCourseProvider).value,
    semester: ref.watch(currentSemesterProvider).value,
    phase: ref.watch(shellPhaseProvider).studentPhase,
  );
});
