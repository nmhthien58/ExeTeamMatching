import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/network/api_client.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../workspace/workspace_api.dart';
import '../workspace/workspace_models.dart';
import 'task_card_tile.dart';
import 'task_form_sheet.dart';

/// C04 – Bảng việc (MB-40, quy tắc 23): tab Cần làm · Đang làm · Xong kèm số, lọc nhanh Của tôi / Bài nộp / Trễ hạn,
/// nút nổi "+" (giảng viên: tạo bài nộp; mentor: không có). Đổi trạng thái bằng menu thẻ – cập nhật lạc quan, Hoàn tác.
class TaskBoardScreen extends ConsumerStatefulWidget {
  const TaskBoardScreen({
    super.key,
    required this.basePath,
    this.workspaceId,
    this.mine = false,
    this.deliverablesOnly = false,
    this.openCreate = false,
  });

  final String basePath;
  final String? workspaceId;
  final bool mine;
  final bool deliverablesOnly;

  /// Mở sẵn bottom sheet tạo việc (từ C01 "Tạo việc").
  final bool openCreate;

  @override
  ConsumerState<TaskBoardScreen> createState() => _TaskBoardScreenState();
}

class _TaskBoardScreenState extends ConsumerState<TaskBoardScreen> {
  late bool _mine = widget.mine;
  late bool _deliverables = widget.deliverablesOnly;
  bool _overdue = false;
  bool _archive = false;

  /// Trạng thái đổi lạc quan, chờ API trả lời.
  final Map<String, TaskStatus> _pending = {};
  String? _error;

  @override
  void initState() {
    super.initState();
    if (widget.openCreate) {
      WidgetsBinding.instance.addPostFrameCallback((_) async {
        final ws = await ref.read(currentWorkspaceProvider(widget.workspaceId).future);
        if (ws != null && mounted) _create(ws);
      });
    }
  }

  TaskBoardQuery _query(String workspaceId) =>
      (workspaceId: workspaceId, mine: _mine, deliverablesOnly: _deliverables, overdue: _overdue, includeArchive: _archive);

  Future<void> _create(WorkspaceOverview ws) async {
    final created = await showTaskFormSheet(context, workspace: ws);
    if (created && mounted) ref.invalidate(taskBoardProvider);
  }

  Future<void> _move(WorkspaceOverview ws, TaskCard task, TaskStatus status, {bool undo = false}) async {
    final previous = task.status;
    setState(() {
      _pending[task.id] = status;
      _error = null;
    });
    try {
      await ref.read(collabApiProvider).moveTask(task.id, status);
      if (!mounted) return;
      ref.invalidate(taskBoardProvider(_query(ws.id)));
      if (!undo) {
        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(SnackBar(
            content: Text('Đã chuyển sang ${status.label}'),
            duration: const Duration(seconds: 5),
            action: SnackBarAction(label: 'Hoàn tác', onPressed: () => _move(ws, task.copyWith(status: status), previous, undo: true)),
          ));
      }
    } catch (e) {
      if (mounted) setState(() => _error = '${e.apiError.title}. Việc đã trở lại cột cũ.');
    } finally {
      if (mounted) setState(() => _pending.remove(task.id));
    }
  }

  @override
  Widget build(BuildContext context) {
    final workspace = ref.watch(currentWorkspaceProvider(widget.workspaceId));
    return workspace.when(
      loading: () => Scaffold(appBar: AppBar(title: const Text('Việc')), body: const SkeletonList()),
      error: (e, _) => Scaffold(
        appBar: AppBar(title: const Text('Việc')),
        body: ErrorState(error: e, onRetry: () => ref.invalidate(currentWorkspaceProvider(widget.workspaceId))),
      ),
      data: (ws) => ws == null
          ? Scaffold(appBar: AppBar(title: const Text('Việc')), body: const EmptyState(title: 'Bạn chưa có nhóm'))
          : _board(ws),
    );
  }

  Widget _board(WorkspaceOverview ws) {
    final tasks = ref.watch(taskBoardProvider(_query(ws.id)));
    final isInstructor = ws.myRole == WorkspaceRole.instructor;
    final canCreate = ws.canEdit || isInstructor;
    final list = [for (final t in tasks.value ?? const <TaskCard>[]) _pending.containsKey(t.id) ? t.copyWith(status: _pending[t.id]) : t];

    return DefaultTabController(
      length: TaskStatus.values.length,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Việc'),
          bottom: TabBar(tabs: [
            for (final s in TaskStatus.values) Tab(text: '${s.label} (${list.where((t) => t.status == s).length})'),
          ]),
        ),
        floatingActionButton: canCreate
            ? FloatingActionButton(
                tooltip: isInstructor ? 'Tạo bài nộp' : 'Tạo việc',
                onPressed: () => _create(ws),
                child: const Icon(Icons.add),
              )
            : null,
        body: Column(
          children: [
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s3, AppSpacing.s4, 0),
              child: Row(
                children: [
                  if (ws.canEdit) _chip('Của tôi', _mine, (v) => setState(() => _mine = v)),
                  _chip('Bài nộp', _deliverables, (v) => setState(() => _deliverables = v)),
                  _chip('Trễ hạn', _overdue, (v) => setState(() => _overdue = v)),
                  if (ws.previousWorkspaceId != null) _chip('Việc EXE101', _archive, (v) => setState(() => _archive = v)),
                ],
              ),
            ),
            if (_error != null)
              Padding(padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s3, AppSpacing.s4, 0), child: ErrorBanner(message: _error!)),
            Expanded(
              child: tasks.when(
                skipLoadingOnReload: true,
                loading: () => const SkeletonList(),
                error: (e, _) => ErrorState(error: e, onRetry: () => ref.invalidate(taskBoardProvider(_query(ws.id)))),
                data: (_) => TabBarView(children: [
                  for (final s in TaskStatus.values)
                    _column(ws, s, list.where((t) => t.status == s).toList()..sort((a, b) => a.position.compareTo(b.position))),
                ]),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _chip(String label, bool selected, ValueChanged<bool> onSelected) => Padding(
        padding: const EdgeInsets.only(right: AppSpacing.s2),
        child: FilterChip(label: Text(label), selected: selected, onSelected: onSelected),
      );

  Widget _column(WorkspaceOverview ws, TaskStatus status, List<TaskCard> tasks) => RefreshIndicator(
        onRefresh: () async => ref.invalidate(taskBoardProvider(_query(ws.id))),
        child: tasks.isEmpty
            ? ListView(children: const [EmptyState(icon: Icons.inbox_outlined, title: 'Chưa có việc.')])
            : ListView.separated(
                // Chừa chỗ cho nút nổi.
                padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s3, AppSpacing.s4, 96),
                itemCount: tasks.length,
                separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.s3),
                itemBuilder: (_, i) {
                  final task = tasks[i];
                  return TaskCardTile(
                    task: task,
                    onTap: () => context.push('${widget.basePath}/viec/${task.id}'),
                    onMove: ws.canEdit && !task.isArchive ? (s) => _move(ws, task, s) : null,
                  );
                },
              ),
      );
}
