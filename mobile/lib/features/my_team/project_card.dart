import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/providers.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../suggestions/suggestion_widgets.dart' show DomainChips;
import '../workspace/format.dart';
import 'my_team_api.dart';

// Model theo docs/contracts/openapi/team.yaml (ProjectDetail, ProjectVersion) – S15, quy tắc 26.

class ProjectVersion {
  const ProjectVersion({
    required this.version,
    required this.name,
    required this.changeType,
    required this.semesterCode,
    required this.courseCode,
    required this.createdAt,
    this.reason,
    this.changedByName,
  });

  final int version;
  final String name;

  /// CREATED, EDITED hoặc PIVOT (đổi đề tài ở EXE201).
  final String changeType;
  final String? reason;
  final String semesterCode;
  final String courseCode;
  final String? changedByName;
  final DateTime createdAt;

  factory ProjectVersion.fromJson(Map<String, dynamic> j) => ProjectVersion(
        version: j['version'] as int? ?? 1,
        name: j['name'] as String? ?? '',
        changeType: j['changeType'] as String? ?? 'EDITED',
        reason: j['reason'] as String?,
        semesterCode: j['semesterCode'] as String? ?? '',
        courseCode: j['courseCode'] as String? ?? '',
        changedByName: j['changedByName'] as String?,
        createdAt: DateTime.parse(j['createdAt'] as String),
      );
}

class ProjectDetail {
  const ProjectDetail({required this.id, required this.name, required this.originCourseCode, this.originSemesterCode, this.versions = const []});

  final String id;
  final String name;
  final String originCourseCode;
  final String? originSemesterCode;

  /// Mới nhất trước.
  final List<ProjectVersion> versions;

  factory ProjectDetail.fromJson(Map<String, dynamic> j) => ProjectDetail(
        id: j['id'] as String,
        name: j['name'] as String? ?? '',
        originCourseCode: j['originCourseCode'] as String? ?? 'EXE101',
        originSemesterCode: j['originSemesterCode'] as String?,
        versions: [for (final v in (j['versions'] as List? ?? const [])) ProjectVersion.fromJson(Map<String, dynamic>.from(v as Map))],
      );
}

/// Đề tài của nhóm và lịch sử qua các kỳ (BE-76). Null nếu nhóm chưa có đề tài (404).
class TeamProjectApi {
  TeamProjectApi(this._dio);

  final Dio _dio;

  Future<ProjectDetail?> project(String teamId) async {
    try {
      return ProjectDetail.fromJson((await _dio.get<Map<String, dynamic>>('/api/teams/$teamId/project')).data!);
    } on DioException catch (e) {
      if (e.response?.statusCode == 404) return null;
      throw e.apiError;
    }
  }
}

final teamProjectApiProvider = Provider<TeamProjectApi>((ref) => TeamProjectApi(ref.watch(dioProvider)));

final teamProjectProvider = FutureProvider.autoDispose.family<ProjectDetail?, String>((ref, teamId) => ref.watch(teamProjectApiProvider).project(teamId));

/// Thẻ đề tài ở S15 (MB-45, DESIGN.md 6.3): tên đề tài, chip domain, "Tiếp tục từ EXE101 · SP26" khi nhóm EXE201 kế thừa,
/// nút "Lịch sử" mở [ProjectTimeline] trong bottom sheet. Chưa có đề tài hoặc tải lỗi thì giữ tên đề tài của thẻ nhóm.
class ProjectCard extends ConsumerWidget {
  const ProjectCard({super.key, required this.team});

  final MyTeam team;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final project = ref.watch(teamProjectProvider(team.id)).value;
    final name = project?.name ?? team.projectName;
    if (name == null) return team.domains.isEmpty ? const SizedBox.shrink() : DomainChips(domains: team.domains);
    final continued = project != null && team.courseCode == 'EXE201' && project.originCourseCode == 'EXE101';
    return Card.outlined(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.s4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Đề tài', style: theme.textTheme.labelLarge),
            Text(name, style: theme.textTheme.titleMedium),
            if (team.domains.isNotEmpty) ...[const SizedBox(height: AppSpacing.s2), DomainChips(domains: team.domains)],
            if (continued) ...[
              const SizedBox(height: AppSpacing.s2),
              Row(
                children: [
                  Icon(Icons.history, size: 20, color: context.appColors.info),
                  const SizedBox(width: AppSpacing.s2),
                  Expanded(
                    child: Text(
                      ['Tiếp tục từ EXE101', ?project.originSemesterCode].join(' · '),
                      style: theme.textTheme.bodyMedium?.copyWith(color: context.appColors.info),
                    ),
                  ),
                ],
              ),
            ],
            if (project != null && project.versions.isNotEmpty)
              Align(
                alignment: Alignment.centerRight,
                child: TextButton.icon(
                  icon: const Icon(Icons.timeline),
                  label: const Text('Lịch sử'),
                  onPressed: () => showModalBottomSheet<void>(
                    context: context,
                    isScrollControlled: true,
                    useSafeArea: true,
                    showDragHandle: true,
                    builder: (_) => ProjectTimeline(project: project),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

/// Dòng thời gian đề tài (bottom sheet): mỗi mốc là một chấm + kỳ / môn + thay đổi một dòng, mới nhất ở trên.
class ProjectTimeline extends StatelessWidget {
  const ProjectTimeline({super.key, required this.project});

  final ProjectDetail project;

  /// "Đổi đề tài: EduSpark → EduSpark AI · lý do…"; tên cũ là mốc liền sau (cũ hơn) trong danh sách.
  static String describe(ProjectVersion v, ProjectVersion? older) {
    final text = switch (v.changeType) {
      'CREATED' => 'Tạo đề tài: ${v.name}',
      'PIVOT' => older == null ? 'Đổi đề tài: ${v.name}' : 'Đổi đề tài: ${older.name} → ${v.name}',
      _ => 'Sửa đề tài: ${v.name}',
    };
    return v.reason == null || v.reason!.isEmpty ? text : '$text · ${v.reason}';
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final versions = project.versions;
    return ConstrainedBox(
      constraints: BoxConstraints(maxHeight: MediaQuery.sizeOf(context).height * 0.8),
      child: ListView(
        shrinkWrap: true,
        padding: const EdgeInsets.fromLTRB(AppSpacing.s4, 0, AppSpacing.s4, AppSpacing.s4),
        children: [
          Text('Lịch sử đề tài', style: theme.textTheme.titleLarge),
          const SizedBox(height: AppSpacing.s3),
          if (versions.isEmpty) const EmptyState(icon: Icons.history, title: 'Chưa có thay đổi'),
          for (var i = 0; i < versions.length; i++)
            IntrinsicHeight(
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Column(
                    children: [
                      Container(
                        margin: const EdgeInsets.only(top: 6),
                        width: 12,
                        height: 12,
                        decoration: BoxDecoration(shape: BoxShape.circle, color: i == 0 ? theme.colorScheme.primary : theme.colorScheme.outline),
                      ),
                      if (i < versions.length - 1) Expanded(child: Container(width: 2, color: theme.colorScheme.outlineVariant)),
                    ],
                  ),
                  const SizedBox(width: AppSpacing.s3),
                  Expanded(
                    child: Padding(
                      padding: const EdgeInsets.only(bottom: AppSpacing.s4),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('${versions[i].semesterCode} · ${versions[i].courseCode}', style: theme.textTheme.labelLarge),
                          Text(describe(versions[i], i + 1 < versions.length ? versions[i + 1] : null), style: theme.textTheme.bodyLarge),
                          Text(
                            [?versions[i].changedByName, formatDateTime(versions[i].createdAt)].join(' · '),
                            style: theme.textTheme.bodySmall,
                          ),
                        ],
                      ),
                    ),
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}
