import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../files/file_services.dart';
import '../workspace/workspace_api.dart';
import '../workspace/workspace_models.dart';

/// Trạng thái lệnh ghi của C05: đang gửi (khóa bấm lần hai) và lỗi hiện ở banner đầu màn.
class TaskActionState {
  const TaskActionState({this.busy = false, this.error});

  final bool busy;
  final String? error;
}

/// ViewModel của C05 – Chi tiết việc / bài nộp (MB-49): đổi trạng thái, nộp bài, duyệt, xóa, bình luận.
/// Chi tiết việc đọc qua `taskDetailProvider(taskId)`. Lệnh trả `true` khi thành công (View hiện SnackBar / đóng màn).
class TaskDetailViewModel extends Notifier<TaskActionState> {
  TaskDetailViewModel(this.taskId);

  final String taskId;

  @override
  TaskActionState build() => const TaskActionState();

  CollabApi get _api => ref.read(collabApiProvider);

  /// Được sửa / xóa: việc còn hiệu lực; bài nộp thì chỉ giảng viên hướng dẫn, việc thường thì người sửa được.
  static bool canManage(WorkspaceOverview? ws, TaskDetail? detail) =>
      ws != null &&
      detail != null &&
      !detail.card.isArchive &&
      (detail.card.isDeliverable ? ws.myRole == WorkspaceRole.instructor : ws.canEdit);

  void refresh() {
    ref.invalidate(taskDetailProvider(taskId));
    ref.invalidate(taskBoardProvider);
  }

  Future<bool> move(TaskStatus status) => _run(() => _api.moveTask(taskId, status));

  Future<bool> submit(String versionId) => _run(() => _api.submit(taskId, versionId));

  Future<bool> review(Submission submission, {required bool accepted, required String comment}) =>
      _run(() => _api.review(submission.id, accepted: accepted, comment: comment));

  Future<bool> delete() => _run(() => _api.deleteTask(taskId));

  /// Mở link tải bản đã nộp; trả câu lỗi (SnackBar) hoặc null. Widget không gọi API trực tiếp (Lab 3 §5, CR-06).
  Future<String?> openSubmission(Submission submission) async {
    try {
      await ref.read(openUrlProvider)(await _api.downloadUrl(submission.fileId, version: submission.version));
      return null;
    } catch (e) {
      return e.apiError.title;
    }
  }

  /// Bình luận: lỗi ném lại để ô soạn giữ nội dung và báo lỗi.
  Future<void> comment(String body) async {
    await _api.commentTask(taskId, body);
    if (ref.mounted) refresh();
  }

  Future<bool> _run(Future<void> Function() action) async {
    state = const TaskActionState(busy: true);
    try {
      await action();
      if (ref.mounted) refresh();
      return true;
    } catch (e) {
      if (ref.mounted) state = TaskActionState(busy: true, error: e.apiError.detail ?? e.apiError.title);
      return false;
    } finally {
      if (ref.mounted) state = TaskActionState(error: state.error);
    }
  }
}

final taskDetailViewModelProvider =
    NotifierProvider.autoDispose.family<TaskDetailViewModel, TaskActionState, String>(TaskDetailViewModel.new);
