import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/phase.dart';
import '../../core/providers.dart';
import '../../core/theme/app_tokens.dart';
import '../workspace/format.dart';
import '../workspace/workspace_api.dart' show myWorkspacesProvider;
import 'my_team_api.dart';

/// Cố vấn đang gán cho nhóm (LecturerAssignment, MentorAssignment – team.yaml): tên và nhãn lý do ghép (quy tắc 9, 20).
class TeamAdvisor {
  const TeamAdvisor({required this.name, this.reason});

  final String name;
  final String? reason;
}

String? _lecturerReason(String? reason, int? rank) => switch (reason) {
      'MUTUAL' => 'Hai bên cùng chọn',
      'PREFERENCE' => rank == null ? 'Nguyện vọng' : 'Nguyện vọng $rank',
      'LECTURER_INTEREST' => 'Giảng viên quan tâm',
      'PREVIOUS_INSTRUCTOR' => 'Đã hướng dẫn ở EXE101',
      'DOMAIN' => 'Theo chuyên môn',
      'MANUAL' => 'Bộ môn phân công',
      _ => null,
    };

String? _mentorReason(String? reason) => switch (reason) {
      'MENTOR_INTEREST' => 'Mentor quan tâm',
      'DOMAIN' => 'Theo chuyên môn',
      'MANUAL' => 'Bộ môn phân công',
      _ => null,
    };

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
        return j == null ? null : TeamAdvisor(name: j['lecturerName'] as String? ?? '', reason: _lecturerReason(j['reason'] as String?, j['preferenceRank'] as int?));
      });

  Future<TeamAdvisor?> mentor(String teamId) => _call(() async {
        final j = (await _dio.get<Map<String, dynamic>>('/api/teams/$teamId/mentor')).data;
        return j == null ? null : TeamAdvisor(name: j['mentorName'] as String? ?? '', reason: _mentorReason(j['reason'] as String?));
      });

  /// Số checkpoint đã trình bày trên 4.
  Future<int> checkpointsDone(String workspaceId) =>
      _call(() async => (await _dio.get<Map<String, dynamic>>('/api/workspaces/$workspaceId/checkpoints')).data?['doneCount'] as int? ?? 0);
}

final teamAdvisorsApiProvider = Provider<TeamAdvisorsApi>((ref) => TeamAdvisorsApi(ref.watch(dioProvider)));

final teamLecturerProvider = FutureProvider.autoDispose.family<TeamAdvisor?, String>((ref, teamId) => ref.watch(teamAdvisorsApiProvider).lecturer(teamId));

final teamMentorProvider = FutureProvider.autoDispose.family<TeamAdvisor?, String>((ref, teamId) => ref.watch(teamAdvisorsApiProvider).mentor(teamId));

final checkpointsDoneProvider = FutureProvider.autoDispose.family<int, String>((ref, workspaceId) => ref.watch(teamAdvisorsApiProvider).checkpointsDone(workspaceId));

/// Mục Cố vấn ở S15 (flutter-handoff S15): giảng viên và mentor kèm lý do ghép; chưa ghép → "Chốt giảng viên lúc 18:00 · 17/09/2026"
/// / "Ghép từ tuần 6". API lỗi hoặc đang tải thì dùng tên có sẵn trong thẻ nhóm.
class AdvisorsCard extends ConsumerWidget {
  const AdvisorsCard({super.key, required this.team});

  final MyTeam team;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final lecturer = ref.watch(teamLecturerProvider(team.id)).value;
    final mentor = ref.watch(teamMentorProvider(team.id)).value;
    final deadline = ref.watch(shellPhaseProvider).studentPhase?.teamFormationDeadlineAt;
    Widget row(IconData icon, String title, String? name, String? reason, String empty) => ListTile(
          leading: Icon(icon),
          title: Text(title),
          subtitle: Text(name ?? empty),
          trailing: reason == null ? null : ConstrainedBox(constraints: const BoxConstraints(maxWidth: 150), child: Chip(label: Text(reason, overflow: TextOverflow.ellipsis))),
        );
    return Card.outlined(
      child: Column(
        children: [
          row(Icons.school_outlined, 'Giảng viên', lecturer?.name ?? team.lecturerName, lecturer?.reason,
              deadline == null ? 'Chưa ghép' : 'Chốt giảng viên lúc ${formatDateTime(deadline)}'),
          row(Icons.support_agent_outlined, 'Mentor', mentor?.name ?? team.mentorName, mentor?.reason, 'Ghép từ tuần 6'),
        ],
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
    final workspaces = ref.watch(myWorkspacesProvider).value;
    if (team.courseCode != 'EXE101' || workspaces == null || workspaces.isEmpty) return const SizedBox.shrink();
    final done = ref.watch(checkpointsDoneProvider(workspaces.first.id)).value;
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
