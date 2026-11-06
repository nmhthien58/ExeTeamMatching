import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../workspace/workspace_api.dart';

/// Trạng thái form tạo / sửa việc: đang lưu và lỗi từng ô / lỗi chung.
class TaskFormState {
  const TaskFormState({this.saving = false, this.titleError, this.dueError, this.error});

  final bool saving;
  final String? titleError;
  final String? dueError;
  final String? error;

  TaskFormState copyWith({bool? saving, String? Function()? titleError, String? Function()? dueError, String? Function()? error}) =>
      TaskFormState(
        saving: saving ?? this.saving,
        titleError: titleError != null ? titleError() : this.titleError,
        dueError: dueError != null ? dueError() : this.dueError,
        error: error != null ? error() : this.error,
      );
}

/// Workspace và việc đang sửa (null = tạo mới).
typedef TaskFormKey = ({String workspaceId, String? taskId});

/// ViewModel của form việc C04 (MB-49): kiểm tra form, tạo / sửa, đổi lỗi API thành lỗi ô.
class TaskFormViewModel extends Notifier<TaskFormState> {
  TaskFormViewModel(this.key);

  final TaskFormKey key;

  static const titleRequired = 'Nhập tên việc.';
  static const dueRequired = 'Bài nộp phải có hạn nộp.';

  @override
  TaskFormState build() => const TaskFormState();

  /// Người dùng đã chọn hạn: bỏ lỗi hạn.
  void clearDueError() => state = state.copyWith(dueError: () => null);

  /// Trả `true` khi đã lưu.
  Future<bool> save({
    required String title,
    required String description,
    required bool deliverable,
    required List<String> assigneeStudentIds,
    required DateTime? dueAt,
  }) async {
    final name = title.trim();
    state = state.copyWith(
      titleError: () => name.isEmpty ? titleRequired : null,
      dueError: () => deliverable && dueAt == null ? dueRequired : null,
      error: () => null,
    );
    if (state.titleError != null || state.dueError != null) return false;

    final input = TaskInput(
      title: name,
      description: description.trim().isEmpty ? null : description.trim(),
      deliverable: deliverable,
      assigneeStudentIds: assigneeStudentIds,
      dueAt: dueAt,
    );
    state = state.copyWith(saving: true);
    try {
      final api = ref.read(collabApiProvider);
      final taskId = key.taskId;
      if (taskId == null) {
        await api.createTask(key.workspaceId, input);
      } else {
        await api.updateTask(taskId, input);
      }
      if (ref.mounted) state = state.copyWith(saving: false);
      return true;
    } catch (e) {
      final error = e.apiError;
      if (ref.mounted) {
        state = state.copyWith(
          saving: false,
          titleError: () => error.fieldErrors['title']?.first ?? state.titleError,
          error: () => error.fieldErrors.isEmpty ? (error.detail ?? error.title) : null,
        );
      }
      return false;
    }
  }
}

final taskFormViewModelProvider =
    NotifierProvider.autoDispose.family<TaskFormViewModel, TaskFormState, TaskFormKey>(TaskFormViewModel.new);
