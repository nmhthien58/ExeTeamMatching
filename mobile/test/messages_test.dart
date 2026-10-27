import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/messages/channel_screen.dart';
import 'package:exe_team_matching/features/messages/conversations_screen.dart';
import 'package:exe_team_matching/features/messages/messages_api.dart';
import 'package:exe_team_matching/features/realtime/collab_hub.dart';
import 'package:exe_team_matching/features/workspace/workspace_api.dart';
import 'package:exe_team_matching/features/workspace/workspace_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';

const _leader = PersonRef(personType: 'STUDENT', personId: 's1', displayName: 'Trưởng Nhóm', role: WorkspaceRole.leader);
const _channel = Channel(id: 'c1', workspaceId: 'w1', kind: 'ADVISORS');

Message _msg(String id, String body, {PersonRef author = _leader, bool question = false, int minute = 0}) => Message(
      id: id,
      channelId: 'c1',
      author: author,
      body: body,
      createdAt: DateTime.utc(2026, 10, 27, 3, minute),
      isAdvisorQuestion: question,
      questionStatus: question ? 'OPEN' : null,
    );

class _FakeApi extends MessagesApi {
  _FakeApi({this.items = const [], this.channelsOf = const {}, this.waiting = const {}}) : super(Dio());

  final List<Message> items;
  final Map<String, List<Channel>> channelsOf;
  final Set<String> waiting;
  final sent = <String>[];
  final read = <String>[];
  int failNext = 0;

  @override
  Future<(List<Message>, String?)> history(String channelId, {String? before}) async => (items, null);

  @override
  Future<List<Channel>> channels(String workspaceId) async => channelsOf[workspaceId] ?? const [];

  @override
  Future<Set<String>> channelsWithOpenQuestions() async => waiting;

  @override
  Future<void> markRead(String channelId, String lastReadMessageId) async => read.add(lastReadMessageId);

  @override
  Future<Message> send(String channelId, {required String clientMessageId, required String body, String? parentMessageId, bool isAdvisorQuestion = false}) async {
    sent.add('$clientMessageId $body question=$isAdvisorQuestion');
    if (failNext-- > 0) throw const ApiException(status: 0, code: 'network', title: 'Mất kết nối');
    return _msg('new', body, minute: 9);
  }
}

class _FakeConnection implements HubConnectionLike {
  final handlers = <String, List<void Function(Object?)>>{};

  void emit(String event, Object? payload) {
    for (final h in [...?handlers[event]]) {
      h(payload);
    }
  }

  @override
  Future<void> start() async {}
  @override
  Future<void> stop() async {}
  @override
  Future<void> invoke(String method, String id) async {}
  @override
  void on(String event, void Function(Object? payload) handler) => (handlers[event] ??= []).add(handler);
  @override
  void off(String event, void Function(Object? payload) handler) => handlers[event]?.remove(handler);
  @override
  void onStatus({required VoidCallback reconnecting, required VoidCallback reconnected, required VoidCallback closed}) {}
}

Future<_FakeConnection> _pump(WidgetTester tester, Widget child, _FakeApi api, {List<Override> overrides = const []}) async {
  final connection = _FakeConnection();
  await tester.pumpWidget(ProviderScope(
    overrides: [
      messagesApiProvider.overrideWithValue(api),
      collabHubProvider.overrideWithValue(CollabHub(() => connection)),
      myPersonIdsProvider.overrideWithValue({'s1'}),
      ...overrides,
    ],
    child: MaterialApp(theme: AppTheme.light(), home: Scaffold(body: child)),
  ));
  await tester.pumpAndSettle();
  return connection;
}

void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  testWidgets('C03: câu hỏi có chip Chờ trả lời, đánh dấu đã đọc tin mới nhất, tin qua hub hiện ngay', (tester) async {
    final api = _FakeApi(items: [_msg('m2', 'Cô xem giúp mô hình', question: true, minute: 2), _msg('m1', 'Chào cô')]);
    final hub = await _pump(tester, const ChannelView(channel: _channel, readOnly: false, isStudent: true), api);

    expect(find.text('Chào cô'), findsOneWidget);
    expect(find.text('Chờ trả lời'), findsOneWidget);
    expect(api.read, ['m2']);

    hub.emit('messageCreated', {
      'id': 'm3', 'channelId': 'c1', 'body': 'Cô sẽ góp ý', 'createdAt': '2026-10-27T03:05:00Z',
      'author': {'personType': 'LECTURER', 'personId': 'l1', 'displayName': 'Cô Hạnh', 'role': 'INSTRUCTOR'},
    });
    await tester.pumpAndSettle();
    expect(find.text('Cô sẽ góp ý'), findsOneWidget);
    expect(api.read.last, 'm3');
  });

  testWidgets('C03: gửi kèm Hỏi cố vấn; gửi lỗi hiện Gửi lại, gửi lại dùng cùng clientMessageId', (tester) async {
    final api = _FakeApi()..failNext = 1;
    await _pump(tester, const ChannelView(channel: _channel, readOnly: false, isStudent: true), api);
    expect(find.text('Chưa có tin nhắn.'), findsOneWidget);

    await tester.tap(find.text('Hỏi cố vấn'));
    await tester.enterText(find.byType(TextField), 'Nhờ cô duyệt đề tài');
    await tester.pump();
    await tester.tap(find.byTooltip('Gửi'));
    await tester.pumpAndSettle();
    expect(find.text('Gửi lỗi · Gửi lại'), findsOneWidget);

    await tester.tap(find.text('Gửi lỗi · Gửi lại'));
    await tester.pumpAndSettle();
    final ids = api.sent.map((s) => s.split(' ').first).toSet();
    expect(ids, hasLength(1));
    expect(api.sent.first, endsWith('Nhờ cô duyệt đề tài question=true'));
    expect(find.text('Gửi lỗi · Gửi lại'), findsNothing);
    expect(find.text('Nhờ cô duyệt đề tài'), findsOneWidget);
  });

  testWidgets('C03: Bộ môn chỉ xem, không có khung soạn tin', (tester) async {
    await _pump(tester, const ChannelView(channel: _channel, readOnly: true, isStudent: false), _FakeApi(items: [_msg('m1', 'Chào cô')]));
    expect(find.text('Bộ môn chỉ xem kênh này.'), findsOneWidget);
    expect(find.byType(TextField), findsNothing);
  });

  testWidgets('C02 cố vấn: mỗi nhóm một dòng, số chưa đọc; lọc Cần trả lời chỉ còn nhóm có câu hỏi chờ', (tester) async {
    final api = _FakeApi(
      channelsOf: {
        'w1': [Channel(id: 'c1', workspaceId: 'w1', kind: 'ADVISORS', lastMessage: _msg('m1', 'Cô ơi'))],
        'w2': const [Channel(id: 'c2', workspaceId: 'w2', kind: 'ADVISORS', unreadCount: 2)],
      },
      waiting: {'c1'},
    );
    await _pump(tester, const AdvisorConversationsScreen(basePath: '/gv/tin-nhan'), api, overrides: [
      myWorkspacesProvider.overrideWith((ref) async => const [
            WorkspaceOverview(id: 'w1', teamName: 'Nhóm A', courseCode: 'EXE201', myRole: WorkspaceRole.instructor),
            WorkspaceOverview(id: 'w2', teamName: 'Nhóm B', courseCode: 'EXE201', myRole: WorkspaceRole.instructor),
          ]),
    ]);

    expect(find.text('Nhóm A · Cố vấn'), findsOneWidget);
    expect(find.text('Nhóm B · Cố vấn'), findsOneWidget);
    expect(find.text('2'), findsOneWidget);
    await tester.tap(find.text('Cần trả lời (1)'));
    await tester.pumpAndSettle();
    expect(find.text('Nhóm B · Cố vấn'), findsNothing);
    expect(find.text('Cần trả lời'), findsOneWidget);
  });
}
