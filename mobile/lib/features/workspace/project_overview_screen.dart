import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/providers.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import 'format.dart';
import 'workspace_api.dart';
import 'workspace_models.dart';
import 'workspace_widgets.dart';

/// Số liệu 4 thẻ của C01 tính từ bảng việc.
({int mine, int dueSoon, int pendingReview}) taskStats(List<TaskCard> tasks, String? myStudentId, {DateTime? now}) {
  final n = now ?? DateTime.now();
  final open = tasks.where((t) => t.status != TaskStatus.done && !t.isArchive);
  final soon = n.add(const Duration(days: 3));
  return (
    mine: myStudentId == null ? 0 : open.where((t) => t.assignees.any((a) => a.personId == myStudentId)).length,
    dueSoon: open.where((t) => t.dueAt != null && !t.dueAt!.isBefore(n) && !t.dueAt!.isAfter(soon)).length,
    pendingReview: tasks.where((t) => t.isDeliverable && t.submissionStatus == SubmissionStatus.pending).length,
  );
}

/// C01 – Dự án (MB-40, flutter-handoff 4a): đầu trang nhóm và cố vấn, 4 thẻ số liệu bấm được, lối tắt, "Mới cập nhật".
/// Mentor thấy thêm thẻ tóm tắt với "Buổi mentor n/3". Trạng thái: có dữ liệu · nhóm mới · đang tải · lỗi tải.
class ProjectOverviewScreen extends ConsumerWidget {
  const ProjectOverviewScreen({super.key, required this.basePath, this.workspaceId});

  /// `/sv/du-an` hoặc `/gv/du-an/{id}`, `/mentor/du-an/{id}`.
  final String basePath;

  /// Null với sinh viên (workspace của mình).
  final String? workspaceId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final workspace = ref.watch(currentWorkspaceProvider(workspaceId));
    return Scaffold(
      appBar: AppBar(title: const Text('Dự án'), automaticallyImplyLeading: workspaceId != null),
      body: workspace.when(
        loading: () => const SkeletonList(count: 3),
        error: (error, _) => ErrorState(error: error, onRetry: () => ref.invalidate(currentWorkspaceProvider(workspaceId))),
        data: (value) => value == null
            ? EmptyState(
                icon: Icons.group_off_outlined,
                title: 'Bạn chưa có nhóm',
                description: 'Dự án mở khi bạn đã có nhóm.',
                action: OutlinedButton(onPressed: () => context.go('/sv/nhom'), child: const Text('Mở nhóm của tôi')),
              )
            : _Overview(workspace: value, basePath: basePath),
      ),
    );
  }
}

class _Overview extends ConsumerWidget {
  const _Overview({required this.workspace, required this.basePath});

  final WorkspaceOverview workspace;
  final String basePath;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final query = (workspaceId: workspace.id, mine: false, deliverablesOnly: false, overdue: false, includeArchive: false);
    final tasks = ref.watch(taskBoardProvider(query));
    final studentId = ref.watch(authControllerProvider).value?.studentId;
    final stats = taskStats(tasks.value ?? const [], studentId);
    final isNewTeam = tasks.hasValue && tasks.value!.isEmpty && workspace.recentActivity.isEmpty;

    return RefreshIndicator(
      onRefresh: () async {
        ref.invalidate(workspaceProvider(workspace.id));
        ref.invalidate(taskBoardProvider(query));
      },
      child: ListView(
        padding: const EdgeInsets.all(AppSpacing.s4),
        children: [
          if (workspace.myRole == WorkspaceRole.mentor) ...[_MentorBrief(workspace: workspace), const SizedBox(height: AppSpacing.s3)],
          _TeamHeader(workspace: workspace),
          const SizedBox(height: AppSpacing.s3),
          GridView.count(
            crossAxisCount: 2,
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            mainAxisSpacing: AppSpacing.s3,
            crossAxisSpacing: AppSpacing.s3,
            childAspectRatio: 1.6,
            children: [
              workspace.canEdit
                  ? _StatCard(label: 'Việc của tôi', value: stats.mine, loading: tasks.isLoading, onTap: () => context.push('$basePath/viec?mine=1'))
                  : _StatCard(
                      label: 'Bài nộp chờ duyệt',
                      value: stats.pendingReview,
                      loading: tasks.isLoading,
                      onTap: () => context.push('$basePath/viec?kind=DELIVERABLE'),
                    ),
              _StatCard(label: 'Sắp đến hạn', value: stats.dueSoon, loading: tasks.isLoading, onTap: () => context.push('$basePath/viec')),
              _StatCard(label: 'Họp sắp tới', value: workspace.upcomingMeetingCount, onTap: () => context.push('$basePath/lich-hop')),
              _StatCard(label: 'Câu hỏi chờ', value: workspace.openQuestionCount, onTap: () => context.push('$basePath/tin-nhan')),
            ],
          ),
          const SizedBox(height: AppSpacing.s3),
          _Shortcuts(basePath: basePath),
          const SizedBox(height: AppSpacing.s3),
          Card.outlined(
            child: Padding(
              padding: const EdgeInsets.all(AppSpacing.s4),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Mới cập nhật', style: Theme.of(context).textTheme.titleMedium),
                  if (isNewTeam)
                    EmptyState(
                      icon: Icons.rocket_launch_outlined,
                      title: 'Dự án vừa bắt đầu',
                      description: workspace.canEdit ? 'Tạo việc đầu tiên để cả nhóm cùng theo dõi.' : 'Nhóm chưa có việc hay file nào.',
                      action: workspace.canEdit
                          ? FilledButton(onPressed: () => context.push('$basePath/viec?tao=1'), child: const Text('Tạo việc'))
                          : null,
                    )
                  else if (workspace.recentActivity.isEmpty)
                    Padding(
                      padding: const EdgeInsets.only(top: AppSpacing.s2),
                      child: Text('Chưa có cập nhật mới.', style: Theme.of(context).textTheme.bodyMedium),
                    )
                  else
                    for (final a in workspace.recentActivity.take(5))
                      ListTile(
                        contentPadding: EdgeInsets.zero,
                        leading: Icon(switch (a.kind) {
                          'FILE' => Icons.description_outlined,
                          'MEETING' => Icons.event_outlined,
                          'QUESTION' => Icons.help_outline,
                          _ => Icons.task_alt,
                        }),
                        title: Text(a.title, maxLines: 2, overflow: TextOverflow.ellipsis),
                        trailing: Text(formatRelative(a.at), style: Theme.of(context).textTheme.bodySmall),
                      ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _TeamHeader extends StatelessWidget {
  const _TeamHeader({required this.workspace});

  final WorkspaceOverview workspace;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card.outlined(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.s4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Wrap(
              alignment: WrapAlignment.spaceBetween,
              crossAxisAlignment: WrapCrossAlignment.center,
              spacing: AppSpacing.s2,
              runSpacing: AppSpacing.s2,
              children: [
                Text(workspace.teamName, style: theme.textTheme.titleLarge, maxLines: 2, overflow: TextOverflow.ellipsis),
                StatusChip(label: workspace.courseCode, tone: StatusTone.secondary, icon: Icons.school_outlined),
              ],
            ),
            if (workspace.projectName != null) Text(workspace.projectName!, style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
            if (workspace.previousWorkspaceId != null)
              Padding(
                padding: const EdgeInsets.only(top: AppSpacing.s2),
                child: Row(
                  children: [
                    Icon(Icons.history, size: 20, color: theme.colorScheme.secondary),
                    const SizedBox(width: AppSpacing.s1),
                    Text('Tiếp tục từ EXE101', style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.secondary)),
                  ],
                ),
              ),
            if (workspace.people.isNotEmpty) ...[
              const SizedBox(height: AppSpacing.s3),
              Wrap(
                spacing: AppSpacing.s4,
                runSpacing: AppSpacing.s2,
                children: [
                  for (final p in workspace.people)
                    Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Avatar(name: p.displayName, size: 32, isMentor: p.role == WorkspaceRole.mentor),
                        const SizedBox(width: AppSpacing.s2),
                        Text(p.displayName, style: theme.textTheme.bodyMedium),
                        const SizedBox(width: AppSpacing.s1),
                        RoleChip(role: p.role),
                      ],
                    ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }
}

/// Thẻ "Tóm tắt cho mentor" với bộ đếm "Buổi mentor n/m" (DESIGN.md 6.3).
class _MentorBrief extends StatelessWidget {
  const _MentorBrief({required this.workspace});

  final WorkspaceOverview workspace;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final done = workspace.mentorSessionsDone;
    final expected = workspace.expectedMentorSessions;
    return Card(
      color: theme.colorScheme.primaryContainer,
      elevation: 0,
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.s4),
        child: Wrap(
          alignment: WrapAlignment.spaceBetween,
          crossAxisAlignment: WrapCrossAlignment.center,
          spacing: AppSpacing.s2,
          runSpacing: AppSpacing.s2,
          children: [
            Text(workspace.projectName ?? workspace.teamName,
                style: theme.textTheme.titleMedium?.copyWith(color: theme.colorScheme.onPrimaryContainer)),
            done >= expected
                ? StatusChip(label: 'Đã đủ $expected buổi', tone: StatusTone.success)
                : Semantics(
                    label: 'Buổi mentor $done trên $expected',
                    child: ExcludeSemantics(child: StatusChip(label: 'Buổi mentor $done/$expected', tone: StatusTone.secondary, icon: Icons.handshake_outlined)),
                  ),
          ],
        ),
      ),
    );
  }
}

/// Thẻ số liệu (DESIGN.md 6.1): nhãn Body màu Text Secondary, con số Display; cả thẻ bấm được.
class _StatCard extends StatelessWidget {
  const _StatCard({required this.label, required this.value, required this.onTap, this.loading = false});

  final String label;
  final int value;
  final VoidCallback onTap;
  final bool loading;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card.outlined(
      clipBehavior: Clip.antiAlias,
      margin: EdgeInsets.zero,
      child: InkWell(
        onTap: onTap,
        child: Semantics(
          button: true,
          label: '$label: $value',
          child: ExcludeSemantics(
            child: Padding(
              padding: const EdgeInsets.all(AppSpacing.s4),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(label, style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant), maxLines: 2),
                  loading ? const Skeleton(width: 40, height: 28) : Text('$value', style: theme.textTheme.headlineSmall),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// Lối tắt Tin nhắn · Việc · File · Lịch họp (Tin nhắn và Lịch họp do module khác làm).
class _Shortcuts extends StatelessWidget {
  const _Shortcuts({required this.basePath});

  final String basePath;

  @override
  Widget build(BuildContext context) {
    const items = [('tin-nhan', 'Tin nhắn', Icons.forum_outlined), ('viec', 'Việc', Icons.task_alt), ('file', 'File', Icons.folder_outlined), ('lich-hop', 'Lịch họp', Icons.event_outlined)];
    return Row(
      children: [
        for (final (path, label, icon) in items)
          Expanded(
            child: InkWell(
              borderRadius: BorderRadius.circular(AppRadius.md),
              onTap: () => context.push('$basePath/$path'),
              child: ConstrainedBox(
                constraints: const BoxConstraints(minHeight: 64),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Icon(icon, color: Theme.of(context).colorScheme.secondary),
                    const SizedBox(height: AppSpacing.s1),
                    Text(label, style: Theme.of(context).textTheme.labelLarge),
                  ],
                ),
              ),
            ),
          ),
      ],
    );
  }
}
