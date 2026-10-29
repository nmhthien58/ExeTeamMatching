import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/phase.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/discover/discover_api.dart';
import 'package:exe_team_matching/features/discover/discover_screen.dart';
import 'package:exe_team_matching/features/discover/team_detail_screen.dart';
import 'package:exe_team_matching/features/home/home_api.dart';
import 'package:exe_team_matching/features/home/home_screen.dart';
import 'package:exe_team_matching/features/invitations/invitations_api.dart';
import 'package:exe_team_matching/features/invitations/invitations_screen.dart';
import 'package:exe_team_matching/features/my_team/members_screen.dart';
import 'package:exe_team_matching/features/my_team/my_team_api.dart';
import 'package:exe_team_matching/features/my_team/my_team_screen.dart';
import 'package:exe_team_matching/features/notifications/notifications_api.dart';
import 'package:exe_team_matching/features/notifications/notifications_screen.dart';
import 'package:exe_team_matching/features/round_closed/round_closed_screen.dart';
import 'package:exe_team_matching/features/suggestions/suggested_teams_screen.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'support/suggestions_fakes.dart' as s;

/// MB-36 – checklist flutter-handoff §5 cho các màn sinh viên ngoài workspace (đã có trên develop):
/// 360 và 412 dp, chữ hệ thống 130% không tràn, vùng chạm ≥ 48, nút có nhãn, chữ đạt tương phản.
/// Màn workspace (C01, C04, C06) và S18 ở accessibility_test.dart.
void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  const spec = CatalogRef(id: 2, code: 'SE', name: 'Kỹ thuật phần mềm');
  const card = TeamCard(id: 't1', name: 'Nhóm Bình Minh Sáng Tạo', status: 'ACTIVE', memberCount: 4, projectName: 'EduSpark – nền tảng học nhóm');
  MyTeam teamAs(String relation) => MyTeam(
    id: 't1',
    name: 'Nhóm Bình Minh Sáng Tạo',
    status: 'ACTIVE',
    courseCode: 'EXE101',
    members: [
      for (var i = 1; i <= 4; i++)
        Member(id: 's$i', studentCode: 'SE18000$i', fullName: 'Nguyễn Hoàng Bảo Anh $i', isLeader: i == 1, specialization: spec),
    ],
    domains: const [CatalogRef(id: 4, code: 'D04', name: 'Software / SaaS')],
    isLeader: true,
    memberCount: 4,
    specializationCount: 1,
    projectName: 'EduSpark – nền tảng học nhóm',
    relation: relation,
  );
  final team = teamAs('LEADER');

  final overrides = [
    shellPhaseProvider.overrideWithValue(const ShellPhase(isTeamFormation: true, isMentoringWindow: false)),
    homeApiProvider.overrideWithValue(_FakeHome(const HomeData(
      isLeader: true, pendingInvitationCount: 2, pendingJoinRequestCount: 3, nextAction: 'ADD_SPECIALIZATION', isTeamFormation: true, team: card))),
    discoverApiProvider.overrideWithValue(_FakeDiscover([card], teamAs('NONE'))),
    invitationsApiProvider.overrideWithValue(_FakeInvitations([
      MyRequest(id: 'i1', team: card, status: 'PENDING', expiresAt: DateTime.now().add(const Duration(hours: 30)), message: 'Nhóm đang cần bạn làm thiết kế'),
    ])),
    myTeamApiProvider.overrideWithValue(_FakeMyTeam(team)),
    teamsForMeProvider.overrideWith((ref) async => const []),
    notificationsApiProvider.overrideWithValue(_FakeNotifications([
      AppNotification(id: 'n1', type: 'INVITATION_SENT', title: 'Bạn có lời mời vào nhóm', content: 'Nhóm Bình Minh Sáng Tạo mời bạn tham gia.',
          isRead: false, createdAt: DateTime.now().subtract(const Duration(minutes: 5))),
    ])),
    suggestionsApiProvider.overrideWithValue(s.FakeSuggestionsApi(teams: const [
      TeamSuggestion(team: card, score: 0.7, reasons: ['Nhóm đang cần chuyên ngành của bạn', 'Còn 2 chỗ trong nhóm']),
    ])),
  ];

  // Nhóm của tôi (myTeamDetailProvider) theo từng màn: S15, S17 là trưởng nhóm để kiểm tổng quan, danh sách thành viên
  // và nút của trưởng nhóm; các màn còn lại xem như sinh viên chưa có nhóm (S08 hiện nút xin vào nhóm).
  final screens = <String, (Widget, MyTeam?)>{
    'S01': (const Scaffold(body: HomeScreen()), null),
    'S05': (const Scaffold(body: DiscoverScreen()), null),
    'S07': (const SuggestedTeamsScreen(), null),
    'S08': (const TeamDetailScreen(teamId: 't1'), null),
    'S12': (const InvitationsScreen(), null),
    'S15': (const Scaffold(body: MyTeamScreen()), team),
    'S17': (const MembersScreen(), team),
    'S30': (const RoundClosedScreen(), null),
    'A06': (const NotificationsScreen(roleRoot: '/sv'), null),
  };

  for (final size in const [Size(360, 800), Size(412, 915)]) {
    for (final entry in screens.entries) {
      testWidgets('${entry.key} ở ${size.width.toInt()} dp, chữ 130%', (tester) async {
        tester.view.physicalSize = size * 3;
        tester.view.devicePixelRatio = 3;
        addTearDown(tester.view.reset);

        // Màn mở như màn cấp 2 để nút Back / pop có chỗ quay về như trong app.
        final (screen, myTeam) = entry.value;
        final router = GoRouter(initialLocation: '/sv/man', routes: [
          GoRoute(path: '/sv', builder: (_, _) => const Text('S01'), routes: [GoRoute(path: 'man', builder: (_, _) => screen)]),
        ]);
        // Không dùng addTearDown: flutter_test kiểm SemanticsHandle trước khi chạy tear-down.
        final semantics = tester.ensureSemantics();
        try {
          await tester.pumpWidget(ProviderScope(
            overrides: [...overrides, myTeamDetailProvider.overrideWith((ref) async => myTeam)],
            child: MaterialApp.router(
              theme: AppTheme.light(),
              routerConfig: router,
              builder: (context, child) => MediaQuery(
                data: MediaQuery.of(context).copyWith(textScaler: const TextScaler.linear(1.3)),
                child: child!,
              ),
            ),
          ));
          await tester.pumpAndSettle();
          // Màn có nhóm phải dựng nội dung nhóm thật, không rơi về trạng thái "Bạn chưa có nhóm".
          if (myTeam != null) expect(find.text('Bạn chưa có nhóm'), findsNothing);

          // Không tràn bố cục (RenderFlex overflow sẽ là exception).
          expect(tester.takeException(), isNull);
          await expectLater(tester, meetsGuideline(androidTapTargetGuideline));
          await expectLater(tester, meetsGuideline(labeledTapTargetGuideline));
          await expectLater(tester, meetsGuideline(textContrastGuideline));
        } finally {
          semantics.dispose();
        }
      });
    }
  }
}

class _FakeHome extends HomeApi {
  _FakeHome(this.data) : super(Dio());

  final HomeData data;

  @override
  Future<HomeData> home() async => data;
}

class _FakeDiscover extends DiscoverApi {
  _FakeDiscover(this.items, this.detail) : super(Dio());

  final List<TeamCard> items;
  final MyTeam detail;

  @override
  Future<TeamPage> search(String search, TeamFilter filter, int page) async => TeamPage(items: items, page: 1, totalPages: 1);

  @override
  Future<MyTeam> team(String teamId) async => detail;
}

class _FakeInvitations extends InvitationsApi {
  _FakeInvitations(this.items) : super(Dio());

  final List<MyRequest> items;

  @override
  Future<List<MyRequest>> invitations() async => items;

  @override
  Future<List<MyRequest>> joinRequests() async => const [];
}

class _FakeMyTeam extends MyTeamApi {
  _FakeMyTeam(this.team) : super(Dio());

  final MyTeam team;

  @override
  Future<MyTeam?> myTeam() async => team;

  @override
  Future<List<CatalogRef>> domains() async => const [];

  @override
  Future<List<JoinRequestItem>> joinRequests(String teamId) async => const [];

  @override
  Future<List<SentInvitation>> sentInvitations(String teamId) async => const [];
}

class _FakeNotifications extends NotificationsApi {
  _FakeNotifications(this.items) : super(Dio());

  final List<AppNotification> items;

  @override
  Future<NotificationPage> list({int page = 1}) async => NotificationPage(items: items, totalPages: 1);

  @override
  Future<int> unreadCount() async => items.where((n) => !n.isRead).length;
}
