import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/auth/session.dart';
import 'package:exe_team_matching/core/providers.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/files/file_services.dart';
import 'package:exe_team_matching/features/files/files_screen.dart';
import 'package:exe_team_matching/features/tasks/task_board_screen.dart';
import 'package:exe_team_matching/features/tasks/task_detail_screen.dart';
import 'package:exe_team_matching/features/workspace/format.dart';
import 'package:exe_team_matching/features/workspace/project_overview_screen.dart';
import 'package:exe_team_matching/features/workspace/workspace_api.dart';
import 'package:exe_team_matching/features/workspace/workspace_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';

const me = PersonRef(personType: 'STUDENT', personId: 's1', displayName: 'Lê Thảo Vy', role: WorkspaceRole.leader);

WorkspaceOverview workspace({WorkspaceRole role = WorkspaceRole.leader, String? previous = 'w0'}) => WorkspaceOverview(
      id: 'w1',
      teamName: 'EduSpark',
      courseCode: 'EXE201',
      myRole: role,
      previousWorkspaceId: previous,
      people: const [me],
    );

TaskCard task(String id, {TaskStatus status = TaskStatus.todo, List<PersonRef> assignees = const [], DateTime? dueAt, bool deliverable = false}) =>
    TaskCard(id: id, workspaceId: 'w1', isDeliverable: deliverable, title: 'Việc $id', status: status, position: 0, assignees: assignees, dueAt: dueAt);

/// API giả: dữ liệu cho trước, ghi lại các lời gọi ghi.
class FakeCollabApi extends CollabApi {
  FakeCollabApi({this.workspaces = const [], this.board = const [], this.detail, this.folderContent}) : super(Dio());

  List<WorkspaceOverview> workspaces;
  List<TaskCard> board;
  TaskDetail? detail;
  FolderContent? folderContent;
  final calls = <String>[];

  @override
  Future<List<WorkspaceOverview>> myWorkspaces() async => workspaces;

  @override
  Future<WorkspaceOverview> workspace(String id) async => workspaces.firstWhere((w) => w.id == id);

  @override
  Future<List<TaskCard>> tasks(TaskBoardQuery q) async => board;

  @override
  Future<TaskDetail> task(String id) async => detail!;

  @override
  Future<void> moveTask(String taskId, TaskStatus status) async {
    calls.add('move $taskId ${status.code}');
    board = [for (final t in board) t.id == taskId ? t.copyWith(status: status) : t];
  }

  @override
  Future<FolderContent> folder(FolderQuery q) async => folderContent!;

  @override
  Future<(ProjectFile, bool)> upload(String workspaceId, MultipartFile content, {String? folderId, void Function(double)? onProgress}) async {
    calls.add('upload');
    throw UnimplementedError();
  }
}

class FakeAuth extends AuthController {
  @override
  Future<UserInfo?> build() async =>
      const UserInfo(id: 'u', email: 'vy@fpt.edu.vn', fullName: 'Lê Thảo Vy', roles: {Role.student}, studentId: 's1');
}

Future<void> pump(WidgetTester tester, Widget screen, FakeCollabApi api, {List<Override> overrides = const []}) async {
  await tester.pumpWidget(ProviderScope(
    overrides: [collabApiProvider.overrideWithValue(api), authControllerProvider.overrideWith(FakeAuth.new), ...overrides],
    child: MaterialApp(theme: AppTheme.light(), home: screen),
  ));
  await tester.pumpAndSettle();
}

void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  group('logic', () {
    test('thẻ số liệu C01: việc của tôi, sắp đến hạn (không tính việc trễ), bài nộp chờ duyệt', () {
      final now = DateTime(2026, 10, 22, 8);
      final stats = taskStats([
        task('a', assignees: [me], dueAt: now.add(const Duration(days: 1))),
        task('b', assignees: [me], status: TaskStatus.done),
        task('c', dueAt: now.subtract(const Duration(hours: 1))),
        const TaskCard(id: 'd', workspaceId: 'w1', isDeliverable: true, title: 'Bài', status: TaskStatus.todo, position: 0, submissionStatus: SubmissionStatus.pending),
      ], 's1', now: now);
      expect(stats, (mine: 1, dueSoon: 1, pendingReview: 1));
    });

    test('chip hạn và kích thước file theo quy ước chữ', () {
      final now = DateTime(2026, 10, 22, 8);
      expect(dueLabel(DateTime(2026, 10, 22, 17), now: now).$1, 'Hôm nay');
      expect(dueLabel(DateTime(2026, 10, 27, 9), now: now), ('Còn 5 ngày', DueTone.neutral));
      expect(dueLabel(DateTime(2026, 10, 21, 9), now: now), ('Trễ 1 ngày', DueTone.error));
      expect(formatSize(2516582), '2,4 MB');
      expect(initials('Nguyễn Thị Minh Anh'), 'MA');
    });

    test('quy tắc 24: chặn sai định dạng và quá 25 MB trước khi tải lên', () {
      expect(validateUpload('virus.exe', 10), contains('Chỉ nhận'));
      expect(validateUpload('a.PDF', 10), isNull);
      expect(validateUpload('big.zip', maxFileBytes + 1), contains('25 MB'));
    });
  });

  testWidgets('C01: tên nhóm, tiếp tục từ EXE101, thẻ Việc của tôi', (tester) async {
    final api = FakeCollabApi(workspaces: [workspace()], board: [task('a', assignees: [me])]);
    await pump(tester, const ProjectOverviewScreen(basePath: '/sv/du-an'), api);

    expect(find.text('EduSpark'), findsOneWidget);
    expect(find.text('Tiếp tục từ EXE101'), findsOneWidget);
    expect(find.bySemanticsLabel('Việc của tôi: 1'), findsOneWidget);
  });

  testWidgets('C01: sinh viên chưa có nhóm thấy lối ra', (tester) async {
    await pump(tester, const ProjectOverviewScreen(basePath: '/sv/du-an'), FakeCollabApi());

    expect(find.text('Bạn chưa có nhóm'), findsOneWidget);
    expect(find.text('Mở nhóm của tôi'), findsOneWidget);
  });

  testWidgets('C04: tab có số, đổi trạng thái bằng menu → gọi API và cho Hoàn tác', (tester) async {
    final api = FakeCollabApi(workspaces: [workspace()], board: [task('a'), task('b', status: TaskStatus.doing)]);
    await pump(tester, const TaskBoardScreen(basePath: '/sv/du-an'), api);

    expect(find.text('Cần làm (1)'), findsOneWidget);
    expect(find.text('Đang làm (1)'), findsOneWidget);

    await tester.tap(find.byTooltip('Chuyển sang…'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Chuyển sang Đang làm'));
    await tester.pumpAndSettle();

    expect(api.calls, ['move a DOING']);
    expect(find.text('Đang làm (2)'), findsOneWidget);
    expect(find.text('Đã chuyển sang Đang làm'), findsOneWidget);
    expect(find.text('Hoàn tác'), findsOneWidget);
  });

  testWidgets('C04: mentor không có nút tạo việc và không đổi trạng thái', (tester) async {
    final api = FakeCollabApi(workspaces: [workspace(role: WorkspaceRole.mentor)], board: [task('a')]);
    await pump(tester, const TaskBoardScreen(basePath: '/mentor/du-an/w1', workspaceId: 'w1'), api);

    expect(find.byType(FloatingActionButton), findsNothing);
    expect(find.byTooltip('Chuyển sang…'), findsNothing);
  });

  testWidgets('C05: giảng viên thấy Đạt / Cần sửa ở bản nộp đang chờ', (tester) async {
    final card = task('d', deliverable: true, dueAt: DateTime.now().add(const Duration(days: 1)));
    final api = FakeCollabApi(
      workspaces: [workspace(role: WorkspaceRole.instructor)],
      detail: TaskDetail(
        card: card,
        createdBy: const PersonRef(personType: 'LECTURER', personId: 'l1', displayName: 'Trần Thị Bình', role: WorkspaceRole.instructor),
        submissions: [
          Submission(
            id: 'sb1',
            fileId: 'f1',
            fileName: 'bao-cao.pdf',
            version: 2,
            submittedBy: me,
            submittedAt: DateTime.now(),
            isLate: false,
            reviewStatus: SubmissionStatus.pending,
          ),
        ],
      ),
    );
    await pump(tester, const TaskDetailScreen(taskId: 'd', workspaceId: 'w1'), api);

    expect(find.text('Bài nộp'), findsOneWidget);
    expect(find.text('bao-cao.pdf · v2'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, 'Đạt'), findsOneWidget);
    expect(find.widgetWithText(OutlinedButton, 'Cần sửa'), findsOneWidget);
    expect(find.text('Nộp bài'), findsNothing);
  });

  testWidgets('C06: thư mục EXE101 chỉ xem, trùng tên hỏi tạo phiên bản mới, file quá lớn báo tại chỗ', (tester) async {
    final api = FakeCollabApi(
      workspaces: [workspace()],
      folderContent: FolderContent(
        canUpload: true,
        folders: const [Folder(id: 'w0', name: 'EXE101 (kỳ trước)', isArchive: true, itemCount: 3)],
        files: [ProjectFile(id: 'f1', name: 'bao-cao.pdf', currentVersion: 2, sizeBytes: 1024, updatedByName: 'Vy', updatedAt: DateTime.now())],
      ),
    );
    var picked = PickedFile(name: 'Bao-cao.pdf', size: 2048, toMultipart: () async => MultipartFile.fromBytes([1]));
    await pump(tester, const FilesScreen(), api, overrides: [pickLocalFileProvider.overrideWithValue(() async => picked)]);

    expect(find.text('EXE101 (kỳ trước)'), findsOneWidget);
    expect(find.byIcon(Icons.lock_outline), findsOneWidget);

    await tester.tap(find.text('Tải lên'));
    await tester.pumpAndSettle();
    expect(find.text('Tạo phiên bản mới?'), findsOneWidget);
    await tester.tap(find.text('Hủy'));
    await tester.pumpAndSettle();

    picked = PickedFile(name: 'video.zip', size: maxFileBytes + 1, toMultipart: () async => MultipartFile.fromBytes([1]));
    await tester.tap(find.text('Tải lên'));
    await tester.pumpAndSettle();
    expect(find.textContaining('File quá 25 MB'), findsOneWidget);
    expect(api.calls, isEmpty);
  });
}
