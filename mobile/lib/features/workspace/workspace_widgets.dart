import 'package:flutter/material.dart';

import '../../core/theme/app_tokens.dart';
import 'format.dart';
import 'workspace_models.dart';

/// Năm nhóm màu trạng thái (DESIGN.md mục 7), mỗi nhóm một icon mặc định.
enum StatusTone { warning, success, error, info, neutral }

/// Chip trạng thái: luôn có chữ, icon đứng trước; không cắt chữ.
class StatusChip extends StatelessWidget {
  const StatusChip({super.key, required this.label, required this.tone, this.icon});

  final String label;
  final StatusTone tone;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final colors = context.appColors;
    final (Color bg, Color fg, IconData defaultIcon) = switch (tone) {
      StatusTone.warning => (colors.warningContainer, colors.warning, Icons.schedule),
      StatusTone.success => (colors.successContainer, colors.success, Icons.check_circle_outline),
      StatusTone.error => (scheme.errorContainer, scheme.error, Icons.cancel_outlined),
      StatusTone.info => (colors.infoContainer, colors.onInfoContainer, Icons.verified_outlined),
      StatusTone.neutral => (colors.neutralContainer, scheme.onSurfaceVariant, Icons.remove_circle_outline),
    };
    return Container(
      constraints: const BoxConstraints(minHeight: 32),
      padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s3, vertical: AppSpacing.s1),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(AppRadius.full)),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon ?? defaultIcon, size: 16, color: fg),
          const SizedBox(width: AppSpacing.s1),
          Flexible(child: Text(label, style: Theme.of(context).textTheme.labelLarge?.copyWith(color: fg))),
        ],
      ),
    );
  }
}

/// Chip hạn của việc: "Hôm nay", "Còn 2 ngày", "Trễ 1 ngày" (icon `alarm` khi trễ).
class DueChip extends StatelessWidget {
  const DueChip({super.key, required this.task});

  final TaskCard task;

  @override
  Widget build(BuildContext context) {
    final due = task.dueAt;
    if (due == null || task.status == TaskStatus.done) return const SizedBox.shrink();
    final (label, tone) = dueLabel(due, overdue: task.isOverdue);
    return switch (tone) {
      DueTone.error => StatusChip(label: label, tone: StatusTone.error, icon: Icons.alarm),
      DueTone.warning => StatusChip(label: label, tone: StatusTone.warning),
      DueTone.neutral => StatusChip(label: label, tone: StatusTone.neutral, icon: Icons.event),
    };
  }
}

class SubmissionChip extends StatelessWidget {
  const SubmissionChip({super.key, required this.status});

  final SubmissionStatus status;

  @override
  Widget build(BuildContext context) => switch (status) {
        SubmissionStatus.notSubmitted => StatusChip(label: status.label, tone: StatusTone.neutral),
        SubmissionStatus.pending => StatusChip(label: status.label, tone: StatusTone.warning),
        SubmissionStatus.accepted => StatusChip(label: status.label, tone: StatusTone.success),
        SubmissionStatus.needsRevision => StatusChip(label: status.label, tone: StatusTone.warning, icon: Icons.edit_note),
      };
}

/// Avatar chữ (DESIGN.md mục 5): chữ Primary Dark trên nền Primary Border; mentor viền 2 px Secondary.
class Avatar extends StatelessWidget {
  const Avatar({super.key, required this.name, this.size = 40, this.isMentor = false});

  final String name;
  final double size;
  final bool isMentor;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return ExcludeSemantics(
      child: Container(
        width: size,
        height: size,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          color: scheme.primaryContainer,
          border: isMentor ? Border.all(color: context.appColors.info, width: 2) : null,
        ),
        child: Text(
          initials(name),
          style: (size <= 24 ? Theme.of(context).textTheme.labelSmall : Theme.of(context).textTheme.labelLarge)
              ?.copyWith(color: scheme.onPrimaryContainer),
          textScaler: TextScaler.noScaling,
        ),
      ),
    );
  }
}

/// Avatar 24 xếp chồng (người làm việc); quá [max] thì "+n". Trình đọc màn hình đọc tên.
class AvatarStack extends StatelessWidget {
  const AvatarStack({super.key, required this.people, this.max = 4});

  final List<PersonRef> people;
  final int max;

  @override
  Widget build(BuildContext context) {
    if (people.isEmpty) return const SizedBox.shrink();
    final shown = people.take(max).toList();
    return Semantics(
      label: 'Người làm: ${people.map((p) => p.displayName).join(', ')}',
      child: ExcludeSemantics(
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            SizedBox(
              width: 24 + (shown.length - 1) * 16,
              height: 24,
              child: Stack(
                children: [
                  for (var i = 0; i < shown.length; i++)
                    Positioned(left: i * 16.0, child: Avatar(name: shown[i].displayName, size: 24, isMentor: shown[i].role == WorkspaceRole.mentor)),
                ],
              ),
            ),
            if (people.length > max) Text(' +${people.length - max}', style: Theme.of(context).textTheme.bodySmall),
          ],
        ),
      ),
    );
  }
}

/// Nhãn vai trò cạnh tên: "Trưởng nhóm", "Giảng viên", "Mentor".
class RoleChip extends StatelessWidget {
  const RoleChip({super.key, required this.role});

  final WorkspaceRole? role;

  @override
  Widget build(BuildContext context) {
    final label = switch (role) {
      WorkspaceRole.leader => 'Trưởng nhóm',
      WorkspaceRole.instructor => 'Giảng viên',
      WorkspaceRole.mentor => 'Mentor',
      _ => null,
    };
    if (label == null) return const SizedBox.shrink();
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s2, vertical: 2),
      decoration: BoxDecoration(color: context.appColors.neutralContainer, borderRadius: BorderRadius.circular(AppRadius.full)),
      child: Text(label, style: Theme.of(context).textTheme.labelSmall?.copyWith(color: Theme.of(context).colorScheme.onSurfaceVariant)),
    );
  }
}

/// Danh sách bình luận (việc, file) – avatar, tên, nhãn vai trò, thời gian.
class CommentList extends StatelessWidget {
  const CommentList({super.key, required this.comments});

  final List<Comment> comments;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      children: [
        for (final c in comments)
          Padding(
            padding: const EdgeInsets.only(bottom: AppSpacing.s3),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Avatar(name: c.author.displayName, size: 32, isMentor: c.author.role == WorkspaceRole.mentor),
                const SizedBox(width: AppSpacing.s3),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Wrap(
                        spacing: AppSpacing.s2,
                        crossAxisAlignment: WrapCrossAlignment.center,
                        children: [
                          Text(c.author.displayName, style: theme.textTheme.labelLarge),
                          RoleChip(role: c.author.role),
                          if (c.version != null) Text('v${c.version}', style: theme.textTheme.bodySmall),
                          Text(formatRelative(c.createdAt), style: theme.textTheme.bodySmall),
                        ],
                      ),
                      Text(c.body, style: theme.textTheme.bodyMedium),
                    ],
                  ),
                ),
              ],
            ),
          ),
      ],
    );
  }
}

/// Ô bình luận + nút gửi; lỗi hiện dưới ô, nội dung đã nhập được giữ (DESIGN.md 6.1).
class CommentComposer extends StatefulWidget {
  const CommentComposer({super.key, required this.onSend});

  final Future<void> Function(String body) onSend;

  @override
  State<CommentComposer> createState() => _CommentComposerState();
}

class _CommentComposerState extends State<CommentComposer> {
  final _controller = TextEditingController();
  bool _sending = false;
  String? _error;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final body = _controller.text.trim();
    if (body.isEmpty) return;
    setState(() {
      _sending = true;
      _error = null;
    });
    try {
      await widget.onSend(body);
      _controller.clear();
    } catch (e) {
      _error = 'Chưa gửi được. Nội dung vẫn được giữ, hãy thử lại.';
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  @override
  Widget build(BuildContext context) => Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: TextField(
              controller: _controller,
              minLines: 1,
              maxLines: 5,
              maxLength: 2000,
              decoration: InputDecoration(hintText: 'Viết bình luận…', errorText: _error, counterText: ''),
            ),
          ),
          const SizedBox(width: AppSpacing.s2),
          IconButton(
            tooltip: 'Gửi bình luận',
            onPressed: _sending ? null : _send,
            icon: _sending ? const SizedBox.square(dimension: 20, child: CircularProgressIndicator(strokeWidth: 2)) : const Icon(Icons.send),
            color: Theme.of(context).colorScheme.primary,
          ),
        ],
      );
}
