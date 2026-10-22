import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../files/file_services.dart';
import '../files/project_file_picker_sheet.dart';
import '../workspace/format.dart';
import '../workspace/workspace_api.dart';
import '../workspace/workspace_models.dart';
import '../workspace/workspace_widgets.dart';
import 'task_form_sheet.dart';

/// C05 – Chi tiết việc / bài nộp (MB-40, quy tắc 23): tiêu đề, trạng thái, người làm, hạn, mô tả, bình luận.
/// Bài nộp: bản đã nộp, "Nộp bài" (nhóm, chọn file từ C06), "Đạt" / "Cần sửa" (giảng viên, bắt buộc nhận xét khi cần sửa).
class TaskDetailScreen extends ConsumerStatefulWidget {
  const TaskDetailScreen({super.key, required this.taskId, this.workspaceId});

  final String taskId;
  final String? workspaceId;

  @override
  ConsumerState<TaskDetailScreen> createState() => _TaskDetailScreenState();
}

class _TaskDetailScreenState extends ConsumerState<TaskDetailScreen> {
  bool _busy = false;
  String? _error;

  void _refresh() {
    ref.invalidate(taskDetailProvider(widget.taskId));
    ref.invalidate(taskBoardProvider);
  }

  void _snack(String message) => ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(message)));

  /// Chạy một thao tác ghi: khóa bấm lần hai, lỗi hiện ở banner đầu màn (không dùng snackbar cho lỗi).
  Future<bool> _run(Future<void> Function() action, {String? success}) async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await action();
      _refresh();
      if (success != null && mounted) _snack(success);
      return true;
    } catch (e) {
      if (mounted) setState(() => _error = e.apiError.detail ?? e.apiError.title);
      return false;
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _submit(WorkspaceOverview ws) async {
    final versionId = await pickProjectFileVersion(context, ws.id);
    if (versionId == null) return;
    await _run(() => ref.read(collabApiProvider).submit(widget.taskId, versionId), success: 'Đã nộp bài');
  }

  Future<void> _review(Submission submission, {required bool accepted}) async {
    final comment = await showDialog<String>(context: context, builder: (_) => _ReviewDialog(accepted: accepted));
    if (comment == null) return;
    await _run(
      () => ref.read(collabApiProvider).review(submission.id, accepted: accepted, comment: comment),
      success: accepted ? 'Đã đánh dấu đạt' : 'Đã yêu cầu sửa',
    );
  }

  Future<void> _delete(TaskDetail task) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(task.card.isDeliverable ? 'Xóa bài nộp?' : 'Xóa việc?'),
        content: const Text('Cả nhóm sẽ không thấy việc này nữa.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy')),
          TextButton(
            style: TextButton.styleFrom(foregroundColor: Theme.of(context).colorScheme.error),
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Xóa'),
          ),
        ],
      ),
    );
    if (ok != true) return;
    if (await _run(() => ref.read(collabApiProvider).deleteTask(task.card.id)) && mounted) Navigator.pop(context);
  }

  @override
  Widget build(BuildContext context) {
    final workspace = ref.watch(currentWorkspaceProvider(widget.workspaceId));
    final task = ref.watch(taskDetailProvider(widget.taskId));
    final ws = workspace.value;
    final detail = task.value;
    final canManage = ws != null && detail != null && !detail.card.isArchive &&
        (detail.card.isDeliverable ? ws.myRole == WorkspaceRole.instructor : ws.canEdit);

    return Scaffold(
      appBar: AppBar(
        title: Text(detail?.card.isDeliverable ?? false ? 'Bài nộp' : 'Việc'),
        actions: [
          if (canManage)
            PopupMenuButton<String>(
              tooltip: 'Tùy chọn',
              onSelected: (v) async {
                if (v == 'edit') {
                  if (await showTaskFormSheet(context, workspace: ws, task: detail)) _refresh();
                } else {
                  await _delete(detail);
                }
              },
              itemBuilder: (_) => const [PopupMenuItem(value: 'edit', child: Text('Sửa')), PopupMenuItem(value: 'delete', child: Text('Xóa'))],
            ),
        ],
      ),
      body: switch ((workspace, task)) {
        (AsyncError(:final error), _) || (_, AsyncError(:final error)) => ErrorState(error: error, onRetry: _refresh),
        _ when ws == null || detail == null => const SkeletonList(count: 2),
        _ => RefreshIndicator(onRefresh: () async => _refresh(), child: _body(ws, detail)),
      },
    );
  }

  Widget _body(WorkspaceOverview ws, TaskDetail task) {
    final theme = Theme.of(context);
    final card = task.card;
    return ListView(
      padding: const EdgeInsets.all(AppSpacing.s4),
      children: [
        if (_error != null) ...[ErrorBanner(message: _error!), const SizedBox(height: AppSpacing.s3)],
        if (card.isArchive) ...[
          const StatusChip(label: 'Việc của EXE101 – chỉ xem', tone: StatusTone.neutral, icon: Icons.history),
          const SizedBox(height: AppSpacing.s3),
        ],
        Text(card.title, style: theme.textTheme.titleLarge),
        const SizedBox(height: AppSpacing.s2),
        Wrap(spacing: AppSpacing.s2, runSpacing: AppSpacing.s2, children: [
          StatusChip(
            label: card.status.label,
            tone: switch (card.status) { TaskStatus.done => StatusTone.success, TaskStatus.doing => StatusTone.info, _ => StatusTone.neutral },
          ),
          DueChip(task: card),
          if (card.submissionStatus != null) SubmissionChip(status: card.submissionStatus!),
        ]),
        if (ws.canEdit && !card.isArchive) ...[
          const SizedBox(height: AppSpacing.s4),
          SegmentedButton<TaskStatus>(
            segments: [for (final s in TaskStatus.values) ButtonSegment(value: s, label: Text(s.label))],
            selected: {card.status},
            onSelectionChanged: _busy ? null : (s) => _run(() => ref.read(collabApiProvider).moveTask(card.id, s.single)),
          ),
        ],
        const SizedBox(height: AppSpacing.s4),
        _InfoRow(label: 'Người làm', child: card.assignees.isEmpty ? const Text('Chưa giao') : Text(card.assignees.map((a) => a.displayName).join(', '))),
        _InfoRow(label: 'Hạn', child: Text(card.dueAt == null ? 'Không có hạn' : formatDateTime(card.dueAt!))),
        _InfoRow(
          label: 'Người tạo',
          child: Wrap(spacing: AppSpacing.s2, crossAxisAlignment: WrapCrossAlignment.center, children: [Text(task.createdBy.displayName), RoleChip(role: task.createdBy.role)]),
        ),
        if (task.fromMeeting) const _InfoRow(label: 'Nguồn', child: Text('Từ biên bản họp')),
        if (task.description != null) ...[
          const SizedBox(height: AppSpacing.s3),
          Text(task.description!, style: theme.textTheme.bodyLarge),
        ],
        if (card.isDeliverable) ...[
          const SizedBox(height: AppSpacing.s6),
          _submissions(ws, task),
        ],
        const SizedBox(height: AppSpacing.s6),
        Text('Bình luận (${task.comments.length})', style: theme.textTheme.titleMedium),
        const SizedBox(height: AppSpacing.s2),
        CommentList(comments: task.comments),
        if (ws.myRole != WorkspaceRole.admin && !card.isArchive)
          CommentComposer(onSend: (body) async {
            await ref.read(collabApiProvider).commentTask(card.id, body);
            _refresh();
          }),
      ],
    );
  }

  Widget _submissions(WorkspaceOverview ws, TaskDetail task) {
    final theme = Theme.of(context);
    final isInstructor = ws.myRole == WorkspaceRole.instructor;
    final latest = task.submissions.firstOrNull;
    final canSubmit = ws.canEdit && !task.card.isArchive && task.card.submissionStatus != SubmissionStatus.accepted;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Bản đã nộp', style: theme.textTheme.titleMedium),
        const SizedBox(height: AppSpacing.s2),
        if (task.submissions.isEmpty) Text('Nhóm chưa nộp bản nào.', style: theme.textTheme.bodyMedium),
        for (final s in task.submissions)
          Card.outlined(
            child: Padding(
              padding: const EdgeInsets.all(AppSpacing.s3),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    leading: Icon(fileIcon(s.fileName)),
                    title: Text('${s.fileName} · v${s.version}', maxLines: 2, overflow: TextOverflow.ellipsis),
                    subtitle: Text('${s.submittedBy.displayName} · ${formatRelative(s.submittedAt)}'),
                    onTap: () async => ref.read(openUrlProvider)(await ref.read(collabApiProvider).downloadUrl(s.fileId, version: s.version)),
                  ),
                  Wrap(spacing: AppSpacing.s2, runSpacing: AppSpacing.s2, children: [
                    SubmissionChip(status: s.reviewStatus),
                    if (s.isLate) const StatusChip(label: 'Nộp muộn', tone: StatusTone.error, icon: Icons.alarm),
                  ]),
                  if (s.reviewComment != null)
                    Container(
                      width: double.infinity,
                      margin: const EdgeInsets.only(top: AppSpacing.s2),
                      padding: const EdgeInsets.all(AppSpacing.s3),
                      decoration: BoxDecoration(color: context.appColors.warningContainer, borderRadius: BorderRadius.circular(AppRadius.sm)),
                      child: Text(s.reviewComment!, style: theme.textTheme.bodyMedium),
                    ),
                  if (isInstructor && s.id == latest?.id && s.reviewStatus == SubmissionStatus.pending) ...[
                    const SizedBox(height: AppSpacing.s3),
                    Row(children: [
                      Expanded(child: FilledButton(onPressed: _busy ? null : () => _review(s, accepted: true), child: const Text('Đạt'))),
                      const SizedBox(width: AppSpacing.s3),
                      Expanded(child: OutlinedButton(onPressed: _busy ? null : () => _review(s, accepted: false), child: const Text('Cần sửa'))),
                    ]),
                  ],
                ],
              ),
            ),
          ),
        if (canSubmit) ...[
          const SizedBox(height: AppSpacing.s3),
          LoadingButton(label: 'Nộp bài', loading: _busy, onPressed: () => _submit(ws)),
        ],
      ],
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.label, required this.child});

  final String label;
  final Widget child;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: AppSpacing.s2),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SizedBox(width: 96, child: Text(label, style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: Theme.of(context).colorScheme.onSurfaceVariant))),
            Expanded(child: DefaultTextStyle.merge(style: Theme.of(context).textTheme.bodyMedium, child: child)),
          ],
        ),
      );
}

/// Xác nhận duyệt bài nộp (không đổi lại được): "Cần sửa" bắt buộc nhận xét. Trả nhận xét ('' khi để trống), null = hủy.
class _ReviewDialog extends StatefulWidget {
  const _ReviewDialog({required this.accepted});

  final bool accepted;

  @override
  State<_ReviewDialog> createState() => _ReviewDialogState();
}

class _ReviewDialogState extends State<_ReviewDialog> {
  final _controller = TextEditingController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final needsComment = !widget.accepted;
    return AlertDialog(
      title: Text(widget.accepted ? 'Đánh dấu bài nộp đạt?' : 'Yêu cầu nhóm sửa?'),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('Nhóm sẽ nhận thông báo ngay.'),
          const SizedBox(height: AppSpacing.s3),
          TextField(
            controller: _controller,
            minLines: 2,
            maxLines: 4,
            maxLength: 2000,
            onChanged: (_) => setState(() {}),
            decoration: InputDecoration(labelText: needsComment ? 'Nhận xét' : 'Nhận xét (không bắt buộc)'),
          ),
          if (needsComment && _controller.text.trim().isEmpty)
            Text('Cần ghi nhận xét để nhóm biết sửa gì.', style: Theme.of(context).textTheme.bodyMedium),
        ],
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: const Text('Hủy')),
        TextButton(
          onPressed: needsComment && _controller.text.trim().isEmpty ? null : () => Navigator.pop(context, _controller.text.trim()),
          child: Text(widget.accepted ? 'Đạt' : 'Cần sửa'),
        ),
      ],
    );
  }
}
