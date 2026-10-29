import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/phase.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/discover/posts_api.dart';
import 'package:exe_team_matching/features/discover/seeking_posts_screen.dart';
import 'package:exe_team_matching/features/messages/channel_screen.dart';
import 'package:exe_team_matching/features/messages/conversations_screen.dart';
import 'package:exe_team_matching/features/messages/messages_api.dart';
import 'package:exe_team_matching/features/my_team/my_team_api.dart';
import 'package:exe_team_matching/features/my_team/recruitment_post_screen.dart';
import 'package:exe_team_matching/features/my_team/team_history_screen.dart';
import 'package:exe_team_matching/features/realtime/collab_hub.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart';
import 'package:exe_team_matching/features/workspace/workspace_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'support/workspace_fakes.dart' as w;

/// MB-36 – checklist flutter-handoff §5 cho bài tìm nhóm / tuyển thành viên (S10, S11, S21), lịch sử nhóm (S22)
/// và tin nhắn (C02 của sinh viên và giảng viên, C03): 360 và 412 dp, chữ 130%, vùng chạm ≥ 48, nút có nhãn, tương phản.
void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  const spec = CatalogRef(id: 2, code: 'MKT', name: 'Marketing');
  const domain = CatalogRef(id: 5, code: 'D05', name: 'Giáo dục');
  const post = SeekingPost(
    id: 'p1',
    title: 'Frontend và thiết kế giao diện người dùng',
    description: 'Đã làm hai dự án React, muốn tham gia nhóm làm sản phẩm giáo dục.',
    student: StudentRef(id: 's9', studentCode: 'SE180009', fullName: 'Nguyễn Hoàng Bảo Anh', specialization: spec),
    domainIds: [5],
  );
  const team = MyTeam(
    id: 't1',
    name: 'Nhóm Bình Minh Sáng Tạo',
    status: 'DRAFT',
    courseCode: 'EXE101',
    members: [],
    domains: [],
    isLeader: true,
    memberCount: 3,
    specializationCount: 1,
    relation: 'LEADER',
    recruitmentPost: RecruitmentPost(id: 'r1', title: 'Cần một bạn Marketing', description: 'Lên kế hoạch truyền thông', specializationIds: [2]),
  );

  const leader = PersonRef(personType: 'STUDENT', personId: 's1', displayName: 'Lê Thảo Vy', role: WorkspaceRole.leader);
  const lecturer = PersonRef(personType: 'LECTURER', personId: 'l1', displayName: 'Trần Minh Khoa', role: WorkspaceRole.instructor);
  Message msg(String id, String body, PersonRef author, int minute, {bool question = false}) => Message(
        id: id,
        channelId: 'c2',
        author: author,
        body: body,
        createdAt: DateTime.utc(2026, 10, 27, 3, minute),
        isAdvisorQuestion: question,
        questionStatus: question ? 'OPEN' : null,
      );
  final messages = [
    msg('m1', 'Thầy ơi, nhóm em muốn hỏi về phạm vi MVP cho đợt checkpoint đầu tiên ạ.', leader, 1, question: true),
    msg('m2', 'Các em tập trung vào luồng đăng ký và tìm nhóm trước nhé.', lecturer, 5),
  ];
  final channels = [
    Channel(id: 'c1', workspaceId: 'w1', kind: 'TEAM', unreadCount: 3, lastMessage: messages[0]),
    Channel(id: 'c2', workspaceId: 'w1', kind: 'ADVISORS', unreadCount: 12, lastMessage: messages[1]),
  ];

  final screens = <String, Widget>{
    'S10': const Scaffold(body: SeekingPostsTab()),
    'S11': const SeekingPostFormScreen(),
    'S21': const RecruitmentPostScreen(),
    'S22': const TeamHistoryScreen(),
    'C02': const ConversationsScreen(basePath: '/sv/du-an'),
    'C02 giảng viên': const Scaffold(body: AdvisorConversationsScreen(basePath: '/gv/tin-nhan')),
    'C03': const ChannelScreen(workspaceId: 'w1', channelId: 'c2'),
  };

  for (final size in const [Size(360, 800), Size(412, 915)]) {
    for (final entry in screens.entries) {
      testWidgets('${entry.key} ở ${size.width.toInt()} dp, chữ 130%', (tester) async {
        tester.view.physicalSize = size * 3;
        tester.view.devicePixelRatio = 3;
        addTearDown(tester.view.reset);

        // S10 xem như sinh viên chưa có nhóm (thấy lối đăng bài); S21, S22 là trưởng nhóm.
        final myTeam = entry.key == 'S10' ? null : team;
        // Màn mở như màn cấp 2 để nút Back / pop có chỗ quay về như trong app.
        final router = GoRouter(initialLocation: '/sv/man', routes: [
          GoRoute(path: '/sv', builder: (_, _) => const Text('S01'), routes: [GoRoute(path: 'man', builder: (_, _) => entry.value)]),
        ]);
        // Không dùng addTearDown: flutter_test kiểm SemanticsHandle trước khi chạy tear-down.
        final semantics = tester.ensureSemantics();
        try {
          await tester.pumpWidget(ProviderScope(
            overrides: [
              shellPhaseProvider.overrideWithValue(const ShellPhase(isTeamFormation: true, isMentoringWindow: false)),
              postsApiProvider.overrideWithValue(_FakePosts([post], mine: post)),
              myTeamDetailProvider.overrideWith((ref) async => myTeam),
              specializationsProvider.overrideWith((ref) async => const [spec, CatalogRef(id: 3, code: 'SE', name: 'Kỹ thuật phần mềm')]),
              domainsProvider.overrideWith((ref) async => const [domain]),
              teamHistoryApiProvider.overrideWithValue(_FakeHistory([
                AuditLogEntry(id: 2, action: 'MEMBER_JOINED', actorName: 'Nguyễn Hoàng Bảo Anh', summary: 'SE180009 vào nhóm qua lời mời',
                    occurredAt: DateTime.utc(2026, 10, 27, 3, 5)),
                AuditLogEntry(id: 1, action: 'TEAM_CREATED', actorName: 'Lê Thảo Vy', summary: 'Tạo nhóm Nhóm Bình Minh Sáng Tạo',
                    occurredAt: DateTime.utc(2026, 10, 26, 2)),
              ])),
              w.collabApiOverride(w.FakeCollabApi(workspaces: [w.workspace()])),
              w.authOverride(),
              messagesApiProvider.overrideWithValue(_FakeMessages(channels, messages)),
              collabHubProvider.overrideWithValue(CollabHub(_NoopConnection.new)),
              myPersonIdsProvider.overrideWithValue({'s1'}),
            ],
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
          // Màn phải dựng nội dung thật, không rơi về trạng thái trống / chưa có nhóm.
          expect(find.textContaining('chưa có nhóm'), findsNothing);
          expect(find.text('Không tìm thấy kênh'), findsNothing);

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

class _FakePosts extends PostsApi {
  _FakePosts(this.items, {this.mine}) : super(Dio());

  final List<SeekingPost> items;
  final SeekingPost? mine;

  @override
  Future<SeekingPage> seeking({int? specializationId, required int page}) async => SeekingPage(items: items, page: 1, totalPages: 1);

  @override
  Future<SeekingPost?> mySeeking() async => mine;
}

class _FakeHistory extends TeamHistoryApi {
  _FakeHistory(this.items) : super(Dio());

  final List<AuditLogEntry> items;

  @override
  Future<(List<AuditLogEntry>, bool)> page(String teamId, int page) async => (items, false);
}

class _FakeMessages extends MessagesApi {
  _FakeMessages(this.channelList, this.items) : super(Dio());

  final List<Channel> channelList;
  final List<Message> items;

  @override
  Future<List<Channel>> channels(String workspaceId) async => channelList;

  @override
  Future<(List<Message>, String?)> history(String channelId, {String? before}) async => (items, null);

  @override
  Future<Set<String>> channelsWithOpenQuestions() async => {'c2'};

  @override
  Future<void> markRead(String channelId, String lastReadMessageId) async {}
}

class _NoopConnection implements HubConnectionLike {
  @override
  Future<void> start() async {}
  @override
  Future<void> stop() async {}
  @override
  Future<void> invoke(String method, String id) async {}
  @override
  void on(String event, void Function(Object? payload) handler) {}
  @override
  void off(String event, void Function(Object? payload) handler) {}
  @override
  void onStatus({required VoidCallback reconnecting, required VoidCallback reconnected, required VoidCallback closed}) {}
}
