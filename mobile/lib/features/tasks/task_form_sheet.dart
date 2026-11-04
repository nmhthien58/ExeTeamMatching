import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../workspace/format.dart';
import '../workspace/workspace_models.dart';
import 'task_form_view_model.dart';

/// Bottom sheet tạo / sửa việc (C04): tên, mô tả, người làm, hạn. Giảng viên hướng dẫn tạo bài nộp (bắt buộc hạn).
/// Trả `true` khi đã lưu.
Future<bool> showTaskFormSheet(BuildContext context, {required WorkspaceOverview workspace, TaskDetail? task}) async {
  final saved = await showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    showDragHandle: true,
    builder: (_) => TaskFormSheet(workspace: workspace, task: task),
  );
  return saved ?? false;
}

class TaskFormSheet extends ConsumerStatefulWidget {
  const TaskFormSheet({super.key, required this.workspace, this.task});

  final WorkspaceOverview workspace;
  final TaskDetail? task;

  @override
  ConsumerState<TaskFormSheet> createState() => _TaskFormSheetState();
}

class _TaskFormSheetState extends ConsumerState<TaskFormSheet> {
  late final _title = TextEditingController(text: widget.task?.card.title);
  late final _description = TextEditingController(text: widget.task?.description);
  late DateTime? _due = widget.task?.card.dueAt?.toLocal();
  late final Set<String> _assignees = {...?widget.task?.card.assignees.map((a) => a.personId)};

  TaskFormKey get _key => (workspaceId: widget.workspace.id, taskId: widget.task?.card.id);

  bool get _deliverable => widget.task?.card.isDeliverable ?? widget.workspace.myRole == WorkspaceRole.instructor;

  @override
  void dispose() {
    _title.dispose();
    _description.dispose();
    super.dispose();
  }

  Future<void> _pickDue() async {
    final now = DateTime.now();
    final date = await showDatePicker(context: context, initialDate: _due ?? now, firstDate: now.subtract(const Duration(days: 365)), lastDate: now.add(const Duration(days: 365)));
    if (date == null || !mounted) return;
    final time = await showTimePicker(context: context, initialTime: TimeOfDay.fromDateTime(_due ?? DateTime(now.year, now.month, now.day, 23, 59)));
    if (time == null) return;
    setState(() => _due = DateTime(date.year, date.month, date.day, time.hour, time.minute));
    ref.read(taskFormViewModelProvider(_key).notifier).clearDueError();
  }

  Future<void> _save() async {
    final saved = await ref.read(taskFormViewModelProvider(_key).notifier).save(
          title: _title.text,
          description: _description.text,
          deliverable: _deliverable,
          assigneeStudentIds: _assignees.toList(),
          dueAt: _due,
        );
    if (saved && mounted) Navigator.pop(context, true);
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final form = ref.watch(taskFormViewModelProvider(_key));
    final students = widget.workspace.people.where((p) => p.personType == 'STUDENT').toList();
    final heading = widget.task != null ? (_deliverable ? 'Sửa bài nộp' : 'Sửa việc') : (_deliverable ? 'Tạo bài nộp' : 'Tạo việc');

    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(context).bottom),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Flexible(
            child: ListView(
              shrinkWrap: true,
              padding: const EdgeInsets.fromLTRB(AppSpacing.s6, 0, AppSpacing.s6, AppSpacing.s4),
              children: [
                Text(heading, style: theme.textTheme.titleLarge),
                const SizedBox(height: AppSpacing.s4),
                if (form.error != null) ...[ErrorBanner(message: form.error!), const SizedBox(height: AppSpacing.s3)],
                TextField(
                  controller: _title,
                  maxLength: 200,
                  decoration: InputDecoration(labelText: 'Tên việc', errorText: form.titleError),
                ),
                const SizedBox(height: AppSpacing.s3),
                TextField(
                  controller: _description,
                  minLines: 2,
                  maxLines: 5,
                  maxLength: 4000,
                  decoration: const InputDecoration(labelText: 'Mô tả (không bắt buộc)'),
                ),
                const SizedBox(height: AppSpacing.s3),
                Text(_deliverable ? 'Hạn nộp' : 'Hạn (không bắt buộc)', style: theme.textTheme.labelLarge),
                const SizedBox(height: AppSpacing.s2),
                Row(
                  children: [
                    Expanded(
                      child: OutlinedButton.icon(
                        onPressed: _pickDue,
                        icon: const Icon(Icons.event, size: 20),
                        label: Text(_due == null ? 'Chọn hạn' : formatDateTime(_due!)),
                      ),
                    ),
                    if (_due != null && !_deliverable)
                      IconButton(tooltip: 'Bỏ hạn', onPressed: () => setState(() => _due = null), icon: const Icon(Icons.close)),
                  ],
                ),
                if (form.dueError != null)
                  Padding(
                    padding: const EdgeInsets.only(top: AppSpacing.s1),
                    child: Text(form.dueError!, style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.error)),
                  ),
                const SizedBox(height: AppSpacing.s4),
                Text('Người làm (không bắt buộc)', style: theme.textTheme.labelLarge),
                const SizedBox(height: AppSpacing.s2),
                if (students.isEmpty)
                  Text('Chưa tải được danh sách thành viên.', style: theme.textTheme.bodyMedium)
                else
                  Wrap(
                    spacing: AppSpacing.s2,
                    runSpacing: AppSpacing.s2,
                    children: [
                      for (final s in students)
                        FilterChip(
                          label: Text(s.displayName),
                          selected: _assignees.contains(s.personId),
                          onSelected: (v) => setState(() => v ? _assignees.add(s.personId) : _assignees.remove(s.personId)),
                        ),
                    ],
                  ),
              ],
            ),
          ),
          // Thanh nút cố định cuối sheet (DESIGN.md 6.1).
          DecoratedBox(
            decoration: BoxDecoration(border: Border(top: BorderSide(color: theme.colorScheme.outlineVariant))),
            child: Padding(
              padding: const EdgeInsets.all(AppSpacing.s4),
              child: Row(
                children: [
                  Expanded(child: OutlinedButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy'))),
                  const SizedBox(width: AppSpacing.s3),
                  Expanded(child: LoadingButton(label: widget.task == null ? heading : 'Lưu thay đổi', loading: form.saving, onPressed: _save)),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
