import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/phase.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/my_team/members_screen.dart';
import 'package:exe_team_matching/features/my_team/my_team_api.dart';
import 'package:exe_team_matching/features/my_team/my_team_screen.dart';
import 'package:exe_team_matching/features/my_team/requests_screen.dart';
import 'package:exe_team_matching/features/my_team/team_form_screen.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart' show CatalogRef;
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

const _spec = CatalogRef(id: 2, code: 'SE', name: 'Kỹ thuật phần mềm');
const _d04 = CatalogRef(id: 4, code: 'D04', name: 'Software / SaaS');

Member _member(String id, {bool leader = false}) => Member(id: id, studentCode: 'SE18000$id', fullName: 'Sinh viên $id', isLeader: leader, specialization: _spec);

MyTeam _team({bool leader = true, String status = 'DRAFT', bool inherited = false, int members = 2}) => MyTeam(
      id: 't1',
      name: 'Nhóm Bình Minh',
      status: status,
      courseCode: inherited ? 'EXE201' : 'EXE101',
      members: [_member('1', leader: true), for (var i = 2; i <= members; i++) _member('$i')],
      domains: const [_d04],
      isLeader: leader,
      memberCount: members,
      specializationCount: 1,
      projectName: 'EduSpark',
      isInherited: inherited,
    );

class _FakeApi extends MyTeamApi {
  _FakeApi({this.team, this.requests = const [], this.applied = true}) : super(Dio());

  final MyTeam? team;
  final List<JoinRequestItem> requests;
  final bool applied;
  final calls = <String>[];

  @override
  Future<MyTeam?> myTeam() async => team;

  @override
  Future<List<CatalogRef>> domains() async => const [_d04];

  @override
  Future<MyTeam> update(String teamId, TeamInput input) async {
    calls.add('update ${input.name} ${input.domainIds}');
    return team!;
  }

  @override
  Future<MemberChange> leave(String teamId) async {
    calls.add('leave $teamId');
    return MemberChange(applied: applied);
  }

  @override
  Future<List<JoinRequestItem>> joinRequests(String teamId) async => requests;

  @override
  Future<List<SentInvitation>> sentInvitations(String teamId) async => const [];

  @override
  Future<MemberChange> acceptJoinRequest(String id) async {
    calls.add('accept $id');
    return MemberChange(applied: applied);
  }
}

Future<void> _pump(WidgetTester tester, Widget screen, _FakeApi api, {bool teamFormation = true}) async {
  // Màn được mở như màn cấp 2 (push từ S15) để Back / pop có chỗ quay về như trong app.
  final router = GoRouter(initialLocation: '/sv/nhom/man', routes: [
    GoRoute(
      path: '/sv/nhom',
      builder: (_, _) => const Text('S15'),
      routes: [GoRoute(path: 'man', builder: (_, _) => screen)],
    ),
  ]);
  await tester.pumpWidget(ProviderScope(
    overrides: [
      myTeamApiProvider.overrideWithValue(api),
      shellPhaseProvider.overrideWithValue(ShellPhase(isTeamFormation: teamFormation, isMentoringWindow: false)),
    ],
    child: MaterialApp.router(theme: AppTheme.light(), routerConfig: router),
  ));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('S15: chưa có nhóm trong đợt lập nhóm → Tạo nhóm và Khám phá', (tester) async {
    await _pump(tester, const Scaffold(body: MyTeamScreen()), _FakeApi());
    expect(find.text('Bạn chưa có nhóm'), findsOneWidget);
    expect(find.text('Tạo nhóm'), findsOneWidget);
    expect(find.text('Khám phá'), findsOneWidget);
  });

  testWidgets('S15: sau hạn chót không còn nút tạo nhóm', (tester) async {
    await _pump(tester, const Scaffold(body: MyTeamScreen()), _FakeApi(), teamFormation: false);
    expect(find.text('Tạo nhóm'), findsNothing);
  });

  testWidgets('S15: trưởng nhóm thấy trạng thái, điều kiện và lối tắt của mình', (tester) async {
    await _pump(tester, const Scaffold(body: MyTeamScreen()), _FakeApi(team: _team()));
    expect(find.text('Nhóm Bình Minh'), findsOneWidget);
    expect(find.text('Chưa đủ người'), findsOneWidget);
    expect(find.text('Yêu cầu và lời mời'), findsOneWidget);
    expect(find.text('Sửa thông tin'), findsOneWidget);
    expect(find.text('Chưa ghép'), findsOneWidget);
  });

  testWidgets('S15: thành viên không có lối tắt của trưởng nhóm', (tester) async {
    await _pump(tester, const Scaffold(body: MyTeamScreen()), _FakeApi(team: _team(leader: false, status: 'ACTIVE')));
    expect(find.text('Hoạt động'), findsOneWidget);
    expect(find.text('Yêu cầu và lời mời'), findsNothing);
    expect(find.text('Sửa thông tin'), findsNothing);
  });

  testWidgets('S17: thành viên nhóm kế thừa rời nhóm → xác nhận → chờ Bộ môn duyệt', (tester) async {
    final api = _FakeApi(team: _team(leader: false, inherited: true, status: 'FINALIZED'), applied: false);
    await _pump(tester, const MembersScreen(), api);

    await tester.tap(find.text('Rời nhóm'));
    await tester.pumpAndSettle();
    expect(find.text('Rời nhóm?'), findsOneWidget);
    await tester.tap(find.widgetWithText(TextButton, 'Rời nhóm'));
    await tester.pumpAndSettle();

    expect(api.calls, ['leave t1']);
    expect(find.text('Đã ghi nhận, chờ Bộ môn duyệt'), findsOneWidget);
  });

  testWidgets('S20: yêu cầu hiện ý tưởng hoặc "Chưa có ý tưởng", trưởng nhóm nhận', (tester) async {
    final api = _FakeApi(team: _team(), requests: [
      JoinRequestItem(
        id: 'r1', student: _member('7'), ideas: const [Idea('Học nhóm AI', 'Gợi ý bạn học')], hasNoIdea: false, status: 'PENDING',
        skills: const {'TECHNOLOGY': 4, 'MARKETING': 2}, description: 'Thích làm frontend',
      ),
      JoinRequestItem(id: 'r2', student: _member('8'), ideas: const [], hasNoIdea: true, status: 'PENDING'),
    ]);
    await _pump(tester, const RequestsScreen(), api);

    expect(find.text('Học nhóm AI'), findsOneWidget);
    expect(find.text('Chưa có ý tưởng'), findsOneWidget);
    expect(find.text('Thích làm frontend'), findsOneWidget);
    expect(find.bySemanticsLabel('Công nghệ, 4 trên 5'), findsOneWidget);
    expect(find.text('Chưa đánh giá kỹ năng.'), findsOneWidget); // r2 chưa có kỹ năng
    await tester.tap(find.text('Nhận').first);
    await tester.pumpAndSettle();
    expect(api.calls, ['accept r1']);
  });

  testWidgets('S16: tên trống báo lỗi tại ô; sửa hợp lệ gửi tên và domain', (tester) async {
    final api = _FakeApi(team: _team());
    await _pump(tester, TeamFormScreen(team: _team()), api);

    await tester.enterText(find.widgetWithText(TextField, 'Tên nhóm'), ' ');
    await tester.tap(find.text('Lưu'));
    await tester.pumpAndSettle();
    expect(find.text('Tên nhóm cần ít nhất 2 ký tự.'), findsOneWidget);
    expect(api.calls, isEmpty);

    await tester.enterText(find.widgetWithText(TextField, 'Tên nhóm'), 'Nhóm Mới');
    await tester.tap(find.text('Lưu'));
    await tester.pumpAndSettle();
    expect(api.calls, ['update Nhóm Mới [4]']);
    expect(find.text('S15'), findsOneWidget); // lưu xong quay về S15
  });

  testWidgets('S13: tạo nhóm thiếu domain báo lỗi, không gọi API', (tester) async {
    final api = _FakeApi();
    await _pump(tester, const TeamFormScreen(), api);

    await tester.enterText(find.widgetWithText(TextField, 'Tên nhóm'), 'Nhóm Mới');
    await tester.tap(find.widgetWithText(FilledButton, 'Tạo nhóm'));
    await tester.pumpAndSettle();
    expect(find.text('Chọn ít nhất một domain.'), findsOneWidget);
    expect(api.calls, isEmpty);
  });
}
