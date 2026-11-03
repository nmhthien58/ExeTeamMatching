import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/phase.dart';
import '../../core/providers.dart';
import '../../core/theme/app_tokens.dart';
import '../workspace/format.dart';
import 'my_team_api.dart';
import '../meeting_schedule/meeting_schedule_api.dart' show teamWorkspaceIdProvider;
import '../lecturers/match_reason.dart';

/// Cố vấn đang gán cho nhóm (LecturerAssignment, MentorAssignment – team.yaml): tên và nhãn lý do ghép (quy tắc 9, 20).
class TeamAdvisor {
  const TeamAdvisor({required this.name, this.reason});

  final String name;
  final String? reason;
}




/// Đọc cố vấn của nhóm (BE-47, BE-74) và tiến độ Checkpoint (BE-83). Chưa ghép → null.
class TeamAdvisorsApi {
  TeamAdvisorsApi(this._dio);

  final Dio _dio;

  Future<T> _call<T>(Future<T> Function() request) async {
    try {
      return await request();
    } on DioException catch (e) {
      throw e.apiError;
    }
  }

  Future<TeamAdvisor?> lecturer(String teamId) => _call(() async {
        final j = (await _dio.get<Map<String, dynamic>>('/api/teams/$teamId/lecturer')).data;
        return j == null ? null : TeamAdvisor(name: j['lecturerName'] as String? ?? '', reason: lecturerMatchReason(j['reason'] as String?, j['preferenceRank'] as int?));
      });

  Future<TeamAdvisor?> mentor(String teamId) => _call(() async {
        final j = (await _dio.get<Map<String, dynamic>>('/api/teams/$teamId/mentor')).data;
        return j == null ? null : TeamAdvisor(name: j['mentorName'] as String? ?? '', reason: mentorMatchReason(j['reason'] as String?));
      });

  /// Số checkpoint đã trình bày trên 4.
  Future<int> checkpointsDone(String workspaceId) =>
      _call(() async => (await _dio.get<Map<String, dynamic>>('/api/workspaces/$workspaceId/checkpoints')).data?['doneCount'] as int? ?? 0);
}

final teamAdvisorsApiProvider = Provider<TeamAdvisorsApi>((ref) => TeamAdvisorsApi(ref.watch(dioProvider)));

final teamLecturerProvider = FutureProvider.autoDispose.family<TeamAdvisor?, String>((ref, teamId) => ref.watch(teamAdvisorsApiProvider).lecturer(teamId));

final teamMentorProvider = FutureProvider.autoDispose.family<TeamAdvisor?, String>((ref, teamId) => ref.watch(teamAdvisorsApiProvider).mentor(teamId));

final checkpointsDoneProvider = FutureProvider.autoDispose.family<int, String>((ref, workspaceId) => ref.watch(teamAdvisorsApiProvider).checkpointsDone(workspaceId));

/// Ghi chú dưới mục Cố vấn ở S15 (flutter-handoff S15): lý do ghép giảng viên, mentor; chưa ghép giảng viên → "Chốt giảng viên lúc
/// 18:00 · 17/09/2026". Đang tải hoặc API lỗi thì không hiện gì.
class AdvisorNotes extends ConsumerWidget {
  const AdvisorNotes({super.key, required this.team});

  final MyTeam team;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final lecturer = ref.watch(teamLecturerProvider(team.id)).value;
    final mentor = ref.watch(teamMentorProvider(team.id)).value;
    final deadline = ref.watch(shellPhaseProvider).studentPhase?.teamFormationDeadlineAt;
    final notes = [
      if (lecturer?.reason != null) '${lecturer!.name} · ${lecturer.reason}',
      if (mentor?.reason != null) '${mentor!.name} · ${mentor.reason}',
      if (lecturer == null && team.lecturerName == null && deadline != null) 'Chốt giảng viên lúc ${formatDateTime(deadline)}',
    ];
    if (notes.isEmpty) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s2, AppSpacing.s4, 0),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [for (final note in notes) Text(note, style: Theme.of(context).textTheme.bodySmall)],
      ),
    );
  }
}

/// "Checkpoint n/4" của nhóm EXE101 (quy tắc 27); ẩn khi chưa có dự án hoặc nhóm EXE201.
class CheckpointCard extends ConsumerWidget {
  const CheckpointCard({super.key, required this.team});

  final MyTeam team;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (team.courseCode != 'EXE101') return const SizedBox.shrink();
    // Khớp dự án theo nhóm, không lấy dự án đầu tiên.
    final workspaceId = ref.watch(teamWorkspaceIdProvider(team.id)).value;
    if (workspaceId == null) return const SizedBox.shrink();
    final done = ref.watch(checkpointsDoneProvider(workspaceId)).value;
    if (done == null) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.only(top: AppSpacing.s3),
      child: Card.outlined(
        margin: EdgeInsets.zero,
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.s4),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Checkpoint $done/4', style: Theme.of(context).textTheme.titleSmall),
              const SizedBox(height: AppSpacing.s2),
              LinearProgressIndicator(value: done / 4, semanticsLabel: 'Đã trình bày $done trên 4 checkpoint'),
            ],
          ),
        ),
      ),
    );
  }
}
