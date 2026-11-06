import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'auth/session.dart';
import 'providers.dart';

/// Giai đoạn của từng môn (quy tắc 18, PROJECT_CONTEXT §1.1) – shell đổi tab Khám phá ↔ Dự án.
class CoursePhase {
  const CoursePhase({required this.courseCode, required this.isTeamFormation, this.teamFormationDeadlineAt, this.mentoringStartsOn});

  final String courseCode;
  final bool isTeamFormation;
  final DateTime? teamFormationDeadlineAt;
  final DateTime? mentoringStartsOn;

  factory CoursePhase.fromJson(Map<String, dynamic> json) => CoursePhase(
        courseCode: json['courseCode'] as String,
        isTeamFormation: json['phase'] == 'TEAM_FORMATION',
        teamFormationDeadlineAt: DateTime.tryParse('${json['teamFormationDeadlineAt']}'),
        mentoringStartsOn: DateTime.tryParse('${json['mentoringStartsOn']}'),
      );
}

final phasesProvider = FutureProvider<List<CoursePhase>>((ref) async {
  final user = ref.watch(authControllerProvider).value;
  if (user == null) return const [];
  final response = await ref.watch(dioProvider).get<List<dynamic>>('/api/semesters/current/phase');
  return [for (final p in response.data!) CoursePhase.fromJson(Map<String, dynamic>.from(p as Map))];
});

/// Học kỳ hiện hành (`GET /api/semesters/current`): mã, ngày bắt đầu, ngày kết thúc – cho header trang chủ.
class CurrentSemester {
  const CurrentSemester({required this.code, required this.startDate, required this.endDate});

  final String code;
  final DateTime startDate;
  final DateTime endDate;

  factory CurrentSemester.fromJson(Map<String, dynamic> json) => CurrentSemester(
        code: json['code'] as String,
        startDate: DateTime.parse(json['startDate'] as String),
        endDate: DateTime.parse(json['endDate'] as String),
      );
}

final currentSemesterProvider = FutureProvider<CurrentSemester?>((ref) async {
  final user = ref.watch(authControllerProvider).value;
  if (user == null) return null;
  final response = await ref.watch(dioProvider).get<Map<String, dynamic>>('/api/semesters/current');
  return response.data == null ? null : CurrentSemester.fromJson(response.data!);
});

/// Môn của sinh viên đang đăng nhập (từ hồ sơ).
final myCourseProvider = FutureProvider<String?>((ref) async {
  final user = ref.watch(authControllerProvider).value;
  if (user?.studentId == null) return null;
  final response = await ref.watch(dioProvider).get<Map<String, dynamic>>('/api/students/me');
  return response.data?['courseCode'] as String?;
});

/// Bối cảnh giai đoạn của tab bar. Chưa tải được thì coi như đang lập nhóm (API vẫn trả 409 round-closed nếu đã đóng).
class ShellPhase {
  const ShellPhase({required this.isTeamFormation, required this.isMentoringWindow, this.studentPhase});

  final bool isTeamFormation;
  final bool isMentoringWindow;

  /// Giai đoạn của môn sinh viên (để hộp nhắc ghi giờ đóng đợt).
  final CoursePhase? studentPhase;
}

final shellPhaseProvider = Provider<ShellPhase>((ref) {
  final phases = ref.watch(phasesProvider).value;
  final user = ref.watch(authControllerProvider).value;
  if (phases == null || phases.isEmpty) return const ShellPhase(isTeamFormation: true, isMentoringWindow: false);

  final isStudent = user?.roles.contains(Role.student) ?? false;
  final course = isStudent ? ref.watch(myCourseProvider).value : null;
  final mine = phases.where((p) => p.courseCode == course).firstOrNull ?? phases.first;
  final starts = phases.map((p) => p.mentoringStartsOn).whereType<DateTime>().toList()..sort();
  return ShellPhase(
    isTeamFormation: isStudent ? mine.isTeamFormation : phases.any((p) => p.isTeamFormation),
    isMentoringWindow: starts.isNotEmpty && !starts.first.isAfter(DateTime.now()),
    studentPhase: isStudent ? mine : null,
  );
});

/// Hộp nhắc khi mở Khám phá sau khi đợt lập nhóm đã đóng, hoặc khi API trả `409 round-closed`
/// (user-flow.md mục 0). Trả `true` nếu người dùng chọn "Mở dự án".
Future<bool> showPhaseClosedDialog(BuildContext context, {DateTime? closedAt}) async {
  String two(int v) => v.toString().padLeft(2, '0');
  final at = closedAt?.toLocal();
  final when = at == null ? '' : ' lúc ${two(at.hour)}:${two(at.minute)} · ${two(at.day)}/${two(at.month)}';
  final result = await showDialog<bool>(
    context: context,
    builder: (context) => AlertDialog(
      icon: const Icon(Icons.event_busy, size: 48),
      title: const Text('Đợt lập nhóm đã đóng'),
      content: Text('Đợt lập nhóm đã đóng$when. Khám phá nhóm không còn mở.'),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Đóng')),
        TextButton(onPressed: () => Navigator.pop(context, true), child: const Text('Mở dự án')),
      ],
    ),
  );
  return result ?? false;
}
