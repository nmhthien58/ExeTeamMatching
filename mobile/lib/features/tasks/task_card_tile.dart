import 'package:flutter/material.dart';

import '../../core/theme/app_tokens.dart';
import '../workspace/workspace_models.dart';
import '../workspace/workspace_widgets.dart';

/// Thẻ việc (DESIGN.md 6.3, flutter-handoff `TaskCard`): tiêu đề, người làm xếp chồng, chip hạn; bài nộp có icon
/// `assignment` và chip trạng thái nộp. Đổi trạng thái bằng menu "Chuyển sang…" (mobile không kéo thả).
class TaskCardTile extends StatelessWidget {
  const TaskCardTile({super.key, required this.task, required this.onTap, this.onMove});

  final TaskCard task;
  final VoidCallback onTap;

  /// Null = không được đổi trạng thái (cố vấn, việc EXE101).
  final ValueChanged<TaskStatus>? onMove;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card.outlined(
      margin: EdgeInsets.zero,
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s3, AppSpacing.s1, AppSpacing.s3),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (task.isDeliverable) ...[
                    Icon(Icons.assignment_outlined, color: context.appColors.info, semanticLabel: 'Bài nộp'),
                    const SizedBox(width: AppSpacing.s2),
                  ],
                  Expanded(child: Text(task.title, style: theme.textTheme.titleMedium, maxLines: 2, overflow: TextOverflow.ellipsis)),
                  if (onMove != null)
                    PopupMenuButton<TaskStatus>(
                      tooltip: 'Chuyển sang…',
                      onSelected: onMove,
                      itemBuilder: (_) => [
                        for (final s in TaskStatus.values.where((s) => s != task.status))
                          PopupMenuItem(value: s, child: Text('Chuyển sang ${s.label}')),
                      ],
                    )
                  else
                    const SizedBox(height: 48),
                ],
              ),
              Padding(
                padding: const EdgeInsets.only(right: AppSpacing.s3),
                child: Wrap(
                  spacing: AppSpacing.s2,
                  runSpacing: AppSpacing.s2,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    AvatarStack(people: task.assignees),
                    DueChip(task: task),
                    if (task.submissionStatus != null) SubmissionChip(status: task.submissionStatus!),
                    if (task.isArchive) const StatusChip(label: 'EXE101', tone: StatusTone.neutral, icon: Icons.history),
                    if (task.commentCount > 0)
                      Semantics(
                        label: '${task.commentCount} bình luận',
                        child: ExcludeSemantics(
                          child: Row(mainAxisSize: MainAxisSize.min, children: [
                            Icon(Icons.chat_bubble_outline, size: 20, color: theme.colorScheme.onSurfaceVariant),
                            const SizedBox(width: AppSpacing.s1),
                            Text('${task.commentCount}', style: theme.textTheme.bodyMedium),
                          ]),
                        ),
                      ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
