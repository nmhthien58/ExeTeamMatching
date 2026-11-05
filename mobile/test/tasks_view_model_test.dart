import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/features/files/file_services.dart';
import 'package:exe_team_matching/features/tasks/task_board_view_model.dart';
import 'package:exe_team_matching/features/tasks/task_detail_view_model.dart';
import 'package:exe_team_matching/features/tasks/task_form_view_model.dart';
import 'package:exe_team_matching/features/workspace/workspace_api.dart';
import 'package:exe_team_matching/features/workspace/workspace_models.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/workspace_fakes.dart';

/// API giả cho lệnh ghi của C04 / C05: ghi lời gọi, ném [error] nếu có.
class _Api extends FakeCollabApi {
  _Api({super.board});

  Object? error;

  void _maybeThrow() {
    if (error != null) throw error!;
  }

  @override
  Future<void> moveTask(String taskId, TaskStatus status) async {
    _maybeThrow();
    await super.moveTask(taskId, status);
  }

  @override
  Future<void> createTask(String workspaceId, TaskInput input) async {
    calls.add('create $workspaceId ${input.title} ${input.description}');
    _maybeThrow();
  }

  @override
  Future<void> updateTask(String taskId, TaskInput input) async {
    calls.add('update $taskId');
    _maybeThrow();
  }

  @override
  Future<void> deleteTask(String taskId) async {
    calls.add('delete $taskId');
    _maybeThrow();
  }

  @override
  Future<void> submit(String taskId, String fileVersionId) async {
    calls.add('submit $taskId $fileVersionId');
    _maybeThrow();
  }

  @override
  Future<void> commentTask(String taskId, String body) async {
    calls.add('comment $taskId $body');
    _maybeThrow();
  }

  @override
  Future<String> downloadUrl(String fileId, {int? version}) async {
    _maybeThrow();
    return 'https://files/$fileId?v=$version';
  }
}

const _conflict = ApiException(status: 409, code: 'conflict', title: 'Xung đột', detail: 'Việc đã bị xóa');

/// MB-49 – ViewModel của C04, C05 và form việc, không dựng widget.
void main() {
  late _Api api;
  late ProviderContainer container;

  late List<String> opened;

  setUp(() {
    api = _Api(board: [task('a'), task('b', status: TaskStatus.doing)]);
    opened = [];
    container = ProviderContainer(overrides: [
      collabApiOverride(api),
      openUrlProvider.overrideWithValue((url) async {
        opened.add(url);
        return true;
      }),
    ]);
    addTearDown(container.dispose);
  });

  group('C04 bảng việc', () {
    const key = (workspaceId: 'w1', mine: true, deliverablesOnly: false);
    TaskBoardViewModel vm() => container.read(taskBoardViewModelProvider(key).notifier);
    TaskBoardState state() => container.read(taskBoardViewModelProvider(key));

    setUp(() => container.listen(taskBoardViewModelProvider(key), (_, _) {}));

    test('bộ lọc ban đầu theo khóa và đổi bộ lọc đổi truy vấn', () {
      expect(vm().query.mine, isTrue);
      vm()
        ..setOverdue(true)
        ..setIncludeArchive(true)
        ..setMine(false);
      expect(vm().query, (workspaceId: 'w1', mine: false, deliverablesOnly: false, overdue: true, includeArchive: true));
    });

    test('đổi trạng thái: lạc quan khi chờ, xong trả trạng thái cũ để hoàn tác', () async {
      final future = vm().move(api.board.first, TaskStatus.done);
      expect(state().pending, {'a': TaskStatus.done});
      expect(vm().applyPending(api.board).first.status, TaskStatus.done);
      final outcome = await future;
      expect((outcome as TaskMoved).previous, TaskStatus.todo);
      expect(state().pending, isEmpty);
    });

    test('đổi trạng thái lỗi: banner lỗi, việc về cột cũ', () async {
      api.error = _conflict;
      expect(await vm().move(api.board.first, TaskStatus.done), isA<TaskMoveFailed>());
      expect(state().error, 'Xung đột. Việc đã trở lại cột cũ.');
      expect(state().pending, isEmpty);
    });

    test('chia cột theo trạng thái và vị trí', () {
      final tasks = [task('x').copyWith(), task('y', status: TaskStatus.doing)];
      expect(TaskBoardViewModel.column(tasks, TaskStatus.todo).map((t) => t.id), ['x']);
    });
  });

  group('C05 chi tiết việc', () {
    TaskDetailViewModel vm() => container.read(taskDetailViewModelProvider('a').notifier);
    TaskActionState state() => container.read(taskDetailViewModelProvider('a'));

    setUp(() => container.listen(taskDetailViewModelProvider('a'), (_, _) {}));

    test('nộp bài: đang gửi rồi xong', () async {
      final future = vm().submit('v1');
      expect(state().busy, isTrue);
      expect(await future, isTrue);
      expect((state().busy, state().error), (false, null));
      expect(api.calls, ['submit a v1']);
    });

    test('xóa lỗi: câu lỗi lấy từ detail', () async {
      api.error = _conflict;
      expect(await vm().delete(), isFalse);
      expect((state().busy, state().error), (false, 'Việc đã bị xóa'));
    });

    test('bình luận lỗi ném lại cho ô soạn', () async {
      api.error = _conflict;
      expect(() => vm().comment('Chào'), throwsA(isA<ApiException>()));
    });

    // CR-06 (MB-59): mở bản đã nộp đi qua ViewModel thay vì widget gọi API.
    test('mở bản đã nộp: lấy link đúng phiên bản, lỗi trả câu cho SnackBar', () async {
      final s = Submission(id: 'sb1', fileId: 'f1', fileName: 'a.pdf', version: 2,
          submittedBy: const PersonRef(personType: 'STUDENT', personId: 'p1', displayName: 'An'),
          submittedAt: DateTime.utc(2026, 11, 1), isLate: false, reviewStatus: SubmissionStatus.values.first);
      expect(await vm().openSubmission(s), isNull);
      expect(opened, ['https://files/f1?v=2']);
      api.error = _conflict;
      expect(await vm().openSubmission(s), 'Xung đột');
      expect(opened, hasLength(1));
    });

    test('quyền sửa: bài nộp chỉ giảng viên hướng dẫn', () {
      final ws = workspace();
      expect(TaskDetailViewModel.canManage(null, null), isFalse);
      expect(TaskDetailViewModel.canManage(ws, null), isFalse);
    });
  });

  group('form việc', () {
    const key = (workspaceId: 'w1', taskId: null);
    TaskFormViewModel vm() => container.read(taskFormViewModelProvider(key).notifier);
    TaskFormState state() => container.read(taskFormViewModelProvider(key));

    setUp(() => container.listen(taskFormViewModelProvider(key), (_, _) {}));

    test('thiếu tên và bài nộp thiếu hạn → lỗi ô, không gọi API', () async {
      expect(await vm().save(title: '  ', description: '', deliverable: true, assigneeStudentIds: const [], dueAt: null), isFalse);
      expect((state().titleError, state().dueError), (TaskFormViewModel.titleRequired, TaskFormViewModel.dueRequired));
      expect(api.calls, isEmpty);
      vm().clearDueError();
      expect(state().dueError, isNull);
    });

    test('tạo việc: cắt khoảng trắng, mô tả rỗng → null', () async {
      expect(await vm().save(title: ' Viết báo cáo ', description: '  ', deliverable: false, assigneeStudentIds: const ['s1'], dueAt: null), isTrue);
      expect(api.calls, ['create w1 Viết báo cáo null']);
      expect(state().saving, isFalse);
    });

    test('lỗi API: lỗi ô tên từ fieldErrors, lỗi chung khi không có fieldErrors', () async {
      api.error = const ApiException(status: 400, code: 'validation', title: 'Sai', fieldErrors: {'title': ['Tên quá dài']});
      await vm().save(title: 'X', description: '', deliverable: false, assigneeStudentIds: const [], dueAt: null);
      expect((state().titleError, state().error), ('Tên quá dài', null));
      api.error = _conflict;
      await vm().save(title: 'X', description: '', deliverable: false, assigneeStudentIds: const [], dueAt: null);
      expect((state().titleError, state().error), (null, 'Việc đã bị xóa'));
    });
  });
}
