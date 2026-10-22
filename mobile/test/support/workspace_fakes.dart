import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/auth/session.dart';
import 'package:exe_team_matching/core/providers.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/workspace/workspace_api.dart';
import 'package:exe_team_matching/features/workspace/workspace_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';

/// Dữ liệu và API giả cho test màn hình workspace (C01, C04–C06).
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
    overrides: [collabApiOverride(api), authOverride(), ...overrides],
    child: MaterialApp(theme: AppTheme.light(), home: screen),
  ));
  await tester.pumpAndSettle();
}

Override collabApiOverride(CollabApi api) => collabApiProvider.overrideWithValue(api);

Override authOverride() => authControllerProvider.overrideWith(FakeAuth.new);
