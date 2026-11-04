import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../workspace/workspace_api.dart';
import '../workspace/workspace_models.dart';

/// Trạng thái C04: lọc nhanh, đổi trạng thái lạc quan đang chờ API, lỗi đổi trạng thái (banner).
class TaskBoardState {
  const TaskBoardState({
    this.mine = false,
    this.deliverablesOnly = false,
    this.overdue = false,
    this.includeArchive = false,
    this.pending = const {},
    this.error,
  });

  final bool mine;
  final bool deliverablesOnly;
  final bool overdue;
  final bool includeArchive;

  /// Trạng thái đổi lạc quan theo id việc, chờ API trả lời.
  final Map<String, TaskStatus> pending;
  final String? error;

  TaskBoardState copyWith({
    bool? mine,
    bool? deliverablesOnly,
    bool? overdue,
    bool? includeArchive,
    Map<String, TaskStatus>? pending,
    String? Function()? error,
  }) =>
      TaskBoardState(
        mine: mine ?? this.mine,
        deliverablesOnly: deliverablesOnly ?? this.deliverablesOnly,
        overdue: overdue ?? this.overdue,
        includeArchive: includeArchive ?? this.includeArchive,
        pending: pending ?? this.pending,
        error: error != null ? error() : this.error,
      );
}

/// Workspace và bộ lọc ban đầu (từ C01: "Việc của tôi", "Bài nộp chờ duyệt").
typedef TaskBoardKey = ({String workspaceId, bool mine, bool deliverablesOnly});

/// Kết quả một lần của lệnh đổi trạng thái; View hiện SnackBar "Hoàn tác" khi [TaskMoved].
sealed class TaskMoveOutcome {
  const TaskMoveOutcome();
}

final class TaskMoved extends TaskMoveOutcome {
  const TaskMoved(this.previous);

  final TaskStatus previous;
}

final class TaskMoveFailed extends TaskMoveOutcome {
  const TaskMoveFailed();
}

/// ViewModel của C04 – Bảng việc (MB-49): lọc nhanh, đổi trạng thái lạc quan, chia cột.
/// Danh sách việc đọc qua `taskBoardProvider(query)`; [query] đổi theo bộ lọc.
class TaskBoardViewModel extends Notifier<TaskBoardState> {
  TaskBoardViewModel(this.key);

  final TaskBoardKey key;

  @override
  TaskBoardState build() => TaskBoardState(mine: key.mine, deliverablesOnly: key.deliverablesOnly);

  TaskBoardQuery get query => (
        workspaceId: key.workspaceId,
        mine: state.mine,
        deliverablesOnly: state.deliverablesOnly,
        overdue: state.overdue,
        includeArchive: state.includeArchive,
      );

  void setMine(bool value) => state = state.copyWith(mine: value);
  void setDeliverablesOnly(bool value) => state = state.copyWith(deliverablesOnly: value);
  void setOverdue(bool value) => state = state.copyWith(overdue: value);
  void setIncludeArchive(bool value) => state = state.copyWith(includeArchive: value);

  void refresh() => ref.invalidate(taskBoardProvider(query));

  /// Sau khi tạo việc: tải lại mọi bảng việc.
  void onCreated() => ref.invalidate(taskBoardProvider);

  /// Việc đã áp trạng thái lạc quan.
  List<TaskCard> applyPending(List<TaskCard> tasks) =>
      [for (final t in tasks) state.pending.containsKey(t.id) ? t.copyWith(status: state.pending[t.id]) : t];

  /// Việc của một cột, theo thứ tự `position`.
  static List<TaskCard> column(List<TaskCard> tasks, TaskStatus status) =>
      tasks.where((t) => t.status == status).toList()..sort((a, b) => a.position.compareTo(b.position));

  Future<TaskMoveOutcome> move(TaskCard task, TaskStatus status) async {
    final previous = task.status;
    state = state.copyWith(pending: {...state.pending, task.id: status}, error: () => null);
    try {
      await ref.read(collabApiProvider).moveTask(task.id, status);
      if (!ref.mounted) return TaskMoved(previous);
      refresh();
      return TaskMoved(previous);
    } catch (e) {
      if (ref.mounted) state = state.copyWith(error: () => '${e.apiError.title}. Việc đã trở lại cột cũ.');
      return const TaskMoveFailed();
    } finally {
      if (ref.mounted) state = state.copyWith(pending: {...state.pending}..remove(task.id));
    }
  }
}

final taskBoardViewModelProvider =
    NotifierProvider.autoDispose.family<TaskBoardViewModel, TaskBoardState, TaskBoardKey>(TaskBoardViewModel.new);
