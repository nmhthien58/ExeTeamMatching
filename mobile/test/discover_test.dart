import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/core/phase.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/discover/discover_api.dart';
import 'package:exe_team_matching/features/discover/discover_screen.dart';
import 'package:exe_team_matching/features/discover/team_detail_screen.dart';
import 'package:exe_team_matching/features/home/home_api.dart';
import 'package:exe_team_matching/features/home/home_screen.dart';
import 'package:exe_team_matching/features/invitations/invitations_api.dart';
import 'package:exe_team_matching/features/invitations/invitations_screen.dart';
import 'package:exe_team_matching/features/my_team/my_team_api.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

final _past = DateTime(2026, 1, 1);

const _card = TeamCard(id: 't1', name: 'Nhóm Bình Minh', status: 'ACTIVE', memberCount: 3, projectName: 'EduSpark');

MyTeam _detail({String relation = 'NONE', int members = 3}) => MyTeam(
      id: 't1',
      name: 'Nhóm Bình Minh',
      status: 'DRAFT',
      courseCode: 'EXE101',
      members: [Member(id: 's1', studentCode: 'SE180001', fullName: 'Trưởng Nhóm', isLeader: true)],
      domains: const [],
      isLeader: relation == 'LEADER',
      memberCount: members,
      specializationCount: 1,
      relation: relation,
    );

class _FakeHome extends HomeApi {
  _FakeHome(this.data) : super(Dio());

  final HomeData data;

  @override
  Future<HomeData> home() async => data;
}

class _FakeDiscover extends DiscoverApi {
  _FakeDiscover({this.items = const [], this.detail, this.error}) : super(Dio());

  final List<TeamCard> items;
  final MyTeam? detail;
  final ApiException? error;
  final joins = <String>[];

  @override
  Future<TeamPage> search(String search, TeamFilter filter, int page) async {
    if (error != null) throw error!;
    return TeamPage(items: items.where((t) => t.name.toLowerCase().contains(search.toLowerCase())).toList(), page: 1, totalPages: 1);
  }

  @override
  Future<MyTeam> team(String teamId) async => detail!;

  @override
  Future<void> requestToJoin(String teamId, {String? message, required List<Idea> ideas, required bool hasNoIdea}) async =>
      joins.add('$teamId ideas=${ideas.length} noIdea=$hasNoIdea');
}

class _FakeInvitations extends InvitationsApi {
  _FakeInvitations(this.items, {this.sent = const []}) : super(Dio());

  final List<MyRequest> items;
  final List<MyRequest> sent;
  final accepted = <String>[];
  final cancelled = <String>[];

  @override
  Future<void> cancelJoinRequest(String joinRequestId) async => cancelled.add(joinRequestId);

  @override
  Future<List<MyRequest>> invitations() async => items;

  @override
  Future<List<MyRequest>> joinRequests() async => sent;

  @override
  Future<MemberChange> accept(String invitationId) async {
    accepted.add(invitationId);
    return const MemberChange(applied: true);
  }
}

Future<void> _pump(WidgetTester tester, Widget screen, {List<Override> overrides = const [], MyTeam? myTeam}) async {
  final router = GoRouter(initialLocation: '/sv/man', routes: [
    GoRoute(path: '/sv', builder: (_, _) => const Text('S01'), routes: [GoRoute(path: 'man', builder: (_, _) => screen)]),
    GoRoute(path: '/sv/nhom', builder: (_, _) => const Text('S15')),
  ]);
  await tester.pumpWidget(ProviderScope(
    overrides: [
      shellPhaseProvider.overrideWithValue(const ShellPhase(isTeamFormation: true, isMentoringWindow: false)),
      teamsForMeProvider.overrideWith((ref) async => const []),
      myTeamDetailProvider.overrideWith((ref) async => myTeam),
      ...overrides,
    ],
    child: MaterialApp.router(theme: AppTheme.light(), routerConfig: router),
  ));
  await tester.pumpAndSettle();
}

void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  testWidgets('S01: chưa có nhóm → việc nên làm Tạo nhóm / Khám phá và lời mời đang chờ', (tester) async {
    const home = HomeData(isLeader: false, pendingInvitationCount: 2, pendingJoinRequestCount: 0, nextAction: 'CREATE_OR_JOIN', isTeamFormation: true);
    await _pump(tester, const Scaffold(body: HomeScreen()), overrides: [homeApiProvider.overrideWithValue(_FakeHome(home))]);

    expect(find.text('Bạn chưa có nhóm'), findsOneWidget);
    expect(find.text('Tạo nhóm'), findsOneWidget);
    expect(find.text('Khám phá'), findsOneWidget);
    expect(find.text('Lời mời vào nhóm'), findsOneWidget);
  });

  testWidgets('S05: thẻ nhóm ghi số chỗ; tìm không thấy → Không tìm thấy nhóm', (tester) async {
    await _pump(tester, const Scaffold(body: DiscoverScreen()), overrides: [discoverApiProvider.overrideWithValue(_FakeDiscover(items: const [_card]))]);
    expect(find.text('3/6 thành viên · còn 3 chỗ'), findsOneWidget);

    await tester.enterText(find.byType(TextField), 'robot');
    await tester.testTextInput.receiveAction(TextInputAction.search);
    await tester.pumpAndSettle();
    expect(find.text('Không tìm thấy nhóm'), findsOneWidget);
  });

  testWidgets('S05: API trả round-closed → hộp nhắc đợt đã đóng', (tester) async {
    final api = _FakeDiscover(error: const ApiException(status: 409, code: 'round-closed', title: 'Đợt đã khóa'));
    await _pump(tester, const Scaffold(body: DiscoverScreen()), overrides: [discoverApiProvider.overrideWithValue(api)]);
    expect(find.text('Đợt lập nhóm đã đóng'), findsOneWidget);
  });

  testWidgets('S08/S09: thiếu ý tưởng báo lỗi tại mục, chọn "Chưa có ý tưởng" thì gửi được', (tester) async {
    final api = _FakeDiscover(detail: _detail());
    await _pump(tester, const TeamDetailScreen(teamId: 't1'), overrides: [discoverApiProvider.overrideWithValue(api)]);

    await tester.tap(find.text('Xin tham gia'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Xin tham gia').last); // nút trong sheet
    await tester.pumpAndSettle();
    expect(find.text('Thêm ít nhất 1 ý tưởng hoặc chọn "Chưa có ý tưởng".'), findsOneWidget);
    expect(api.joins, isEmpty);

    await tester.tap(find.text('Chưa có ý tưởng'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Xin tham gia').last); // nút trong sheet
    await tester.pumpAndSettle();
    expect(api.joins, ['t1 ideas=0 noIdea=true']);
    expect(find.text('Đã gửi yêu cầu'), findsOneWidget);
  });

  testWidgets('S08: đã có nhóm thì không xin tham gia được, có lý do', (tester) async {
    await _pump(tester, const TeamDetailScreen(teamId: 't1'),
        overrides: [discoverApiProvider.overrideWithValue(_FakeDiscover(detail: _detail()))], myTeam: _detail(relation: 'LEADER'));
    expect(find.text('Xin tham gia'), findsNothing);
    expect(find.text('Bạn đã có nhóm.'), findsOneWidget);
  });

  testWidgets('S08: đã gửi yêu cầu → Hủy yêu cầu, hỏi xác nhận rồi mới hủy', (tester) async {
    final invitations = _FakeInvitations(const [], sent: [
      MyRequest(id: 'j1', team: _card, status: 'PENDING', expiresAt: DateTime.now().add(const Duration(hours: 30))),
    ]);
    await _pump(tester, const TeamDetailScreen(teamId: 't1'), overrides: [
      discoverApiProvider.overrideWithValue(_FakeDiscover(detail: _detail(relation: 'REQUESTED'))),
      invitationsApiProvider.overrideWithValue(invitations),
    ]);
    expect(find.text('Đã gửi yêu cầu · chờ trưởng nhóm'), findsOneWidget);

    await tester.tap(find.text('Hủy yêu cầu'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Không'));
    await tester.pumpAndSettle();
    expect(invitations.cancelled, isEmpty);

    await tester.tap(find.text('Hủy yêu cầu'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Hủy yêu cầu'));
    await tester.pumpAndSettle();
    expect(invitations.cancelled, ['j1']);
    expect(find.text('Đã hủy yêu cầu'), findsOneWidget);
  });

  testWidgets('S12 tab Yêu cầu đã gửi: trạng thái, ngày gửi; Hủy có xác nhận', (tester) async {
    final api = _FakeInvitations(const [], sent: [
      MyRequest(id: 'j1', team: _card, status: 'PENDING', expiresAt: DateTime.now().add(const Duration(hours: 30)), createdAt: DateTime(2026, 11, 7, 9)),
      MyRequest(id: 'j2', team: const TeamCard(id: 't2', name: 'Nhóm Khởi Hành', status: 'ACTIVE', memberCount: 4), status: 'DECLINED', expiresAt: _past),
    ]);
    await _pump(tester, const InvitationsScreen(initialTab: 1), overrides: [invitationsApiProvider.overrideWithValue(api)]);
    expect(find.text('Đang chờ'), findsOneWidget);
    expect(find.text('Bị từ chối'), findsOneWidget);
    expect(find.textContaining('Gửi lúc'), findsOneWidget);

    await tester.tap(find.widgetWithText(TextButton, 'Hủy'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Hủy yêu cầu'));
    await tester.pumpAndSettle();
    expect(api.cancelled, ['j1']);
  });

  testWidgets('S12 tab Yêu cầu đã gửi: chưa gửi → trạng thái trống', (tester) async {
    await _pump(tester, const InvitationsScreen(initialTab: 1), overrides: [invitationsApiProvider.overrideWithValue(_FakeInvitations(const []))]);
    expect(find.text('Chưa gửi yêu cầu nào'), findsOneWidget);
  });

  testWidgets('S12: chấp nhận lời mời → xác nhận → vào nhóm, về S15', (tester) async {
    final api = _FakeInvitations([
      MyRequest(id: 'i1', team: _card, status: 'PENDING', expiresAt: DateTime.now().add(const Duration(hours: 30)), message: 'Vào nhóm nhé'),
    ]);
    await _pump(tester, const InvitationsScreen(), overrides: [invitationsApiProvider.overrideWithValue(api)]);

    expect(find.text('Còn 1 ngày 5 giờ'), findsOneWidget);
    await tester.tap(find.widgetWithText(FilledButton, 'Chấp nhận'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Vào nhóm'));
    await tester.pumpAndSettle();
    expect(api.accepted, ['i1']);
    expect(find.text('S15'), findsOneWidget);
  });
}
