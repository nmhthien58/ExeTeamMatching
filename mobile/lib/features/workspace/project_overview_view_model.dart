import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers.dart';
import 'workspace_api.dart';
import 'workspace_models.dart';

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

/// Phần C01 tính từ bảng việc: số liệu thẻ, đang tải, nhóm mới (chưa có việc lẫn hoạt động).
class OverviewTasksState {
  const OverviewTasksState({required this.stats, required this.loading, required this.isNewTeam});

  final ({int mine, int dueSoon, int pendingReview}) stats;
  final bool loading;
  final bool isNewTeam;
}

typedef OverviewKey = ({String workspaceId, bool hasRecentActivity});

/// ViewModel của C01 – Dự án (MB-49): ghép bảng việc với người xem thành số liệu, làm mới.
/// Workspace vẫn đọc qua `currentWorkspaceProvider` (chỉ đọc).
class ProjectOverviewViewModel extends Notifier<OverviewTasksState> {
  ProjectOverviewViewModel(this.key);

  final OverviewKey key;

  TaskBoardQuery get _query =>
      (workspaceId: key.workspaceId, mine: false, deliverablesOnly: false, overdue: false, includeArchive: false);

  @override
  OverviewTasksState build() {
    final tasks = ref.watch(taskBoardProvider(_query));
    final studentId = ref.watch(authControllerProvider).value?.studentId;
    return OverviewTasksState(
      stats: taskStats(tasks.value ?? const [], studentId),
      loading: tasks.isLoading,
      isNewTeam: tasks.hasValue && tasks.value!.isEmpty && !key.hasRecentActivity,
    );
  }

  Future<void> refresh() async {
    ref.invalidate(workspaceProvider(key.workspaceId));
    ref.invalidate(taskBoardProvider(_query));
  }
}

final projectOverviewViewModelProvider = NotifierProvider.autoDispose
    .family<ProjectOverviewViewModel, OverviewTasksState, OverviewKey>(ProjectOverviewViewModel.new);
