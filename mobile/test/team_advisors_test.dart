import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/phase.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/my_team/my_team_api.dart';
import 'package:exe_team_matching/features/my_team/my_team_screen.dart';
import 'package:exe_team_matching/features/my_team/project_card.dart';
import 'package:exe_team_matching/features/meeting_schedule/meeting_schedule_api.dart';
import 'package:exe_team_matching/features/my_team/team_advisors.dart';
import 'package:exe_team_matching/features/workspace/workspace_api.dart';
import 'package:exe_team_matching/features/workspace/workspace_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

const _team = MyTeam(id: 't1', name: 'Nhóm A', status: 'ACTIVE', courseCode: 'EXE101', members: [], domains: [], isLeader: false, memberCount: 4, specializationCount: 2);
const _workspace = WorkspaceOverview(id: 'w1', teamName: 'Nhóm A', courseCode: 'EXE101', myRole: WorkspaceRole.member);

class _Api extends TeamAdvisorsApi {
  _Api({this.lecturerResult}) : super(Dio());

  final TeamAdvisor? lecturerResult;

  @override
  Future<TeamAdvisor?> lecturer(String teamId) async => lecturerResult;

  @override
  Future<TeamAdvisor?> mentor(String teamId) async => null;

  @override
  Future<int> checkpointsDone(String workspaceId) async => 2;
}

class _NoProject extends TeamProjectApi {
  _NoProject() : super(Dio());

  @override
  Future<ProjectDetail?> project(String teamId) async => null;
}

Future<void> _pump(WidgetTester tester, {required _Api api, MyTeam team = _team, List<WorkspaceOverview> workspaces = const [_workspace], DateTime? deadline}) async {
  final router = GoRouter(initialLocation: '/sv/nhom', routes: [
    GoRoute(path: '/sv/nhom', builder: (_, _) => const Scaffold(body: MyTeamScreen())),
    GoRoute(path: '/sv/du-an', builder: (_, _) => const Text('C01')),
  ]);
  await tester.pumpWidget(ProviderScope(
    overrides: [
      shellPhaseProvider.overrideWithValue(ShellPhase(
        isTeamFormation: false,
        isMentoringWindow: false,
        studentPhase: CoursePhase(courseCode: 'EXE101', isTeamFormation: false, teamFormationDeadlineAt: deadline),
      )),
      teamAdvisorsApiProvider.overrideWithValue(api),
      teamProjectApiProvider.overrideWithValue(_NoProject()),
      myTeamDetailProvider.overrideWith((ref) async => team),
      myWorkspacesProvider.overrideWith((ref) async => workspaces),
      teamWorkspaceIdProvider.overrideWith((ref, teamId) async => workspaces.isEmpty ? null : workspaces.first.id),
    ],
    child: MaterialApp.router(theme: AppTheme.light(), routerConfig: router),
  ));
  await tester.pumpAndSettle();
}

void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  testWidgets('S15: giảng viên kèm lý do ghép, Checkpoint n/4 ở EXE101, Mở dự án sang C01', (tester) async {
    await _pump(tester, api: _Api(lecturerResult: const TeamAdvisor(name: 'Cô Hạnh', reason: 'Nguyện vọng 1')));

    expect(find.text('Giảng viên'), findsOneWidget);
    expect(find.text('Cô Hạnh · Nguyện vọng 1'), findsOneWidget);
    expect(find.text('Ghép từ tuần 6'), findsOneWidget);
    expect(find.text('Checkpoint 2/4'), findsOneWidget);

    await tester.scrollUntilVisible(find.text('Mở dự án'), 200);
    await tester.tap(find.text('Mở dự án'));
    await tester.pumpAndSettle();
    expect(find.text('C01'), findsOneWidget);
  });

  testWidgets('S15: chưa ghép giảng viên → giờ chốt; EXE201 không có Checkpoint; chưa có dự án thì ẩn Mở dự án', (tester) async {
    const exe201 = MyTeam(id: 't1', name: 'Nhóm A', status: 'ACTIVE', courseCode: 'EXE201', members: [], domains: [], isLeader: false, memberCount: 4, specializationCount: 2);
    await _pump(tester, api: _Api(), team: exe201, workspaces: const [], deadline: DateTime(2026, 9, 17, 18));

    expect(find.text('Chốt giảng viên lúc 18:00 · 17/09/2026'), findsOneWidget);
    expect(find.textContaining('Checkpoint'), findsNothing);
    expect(find.text('Mở dự án', skipOffstage: false), findsNothing);
  });
}
