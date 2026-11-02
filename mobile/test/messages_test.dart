import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/messages/channel_screen.dart';
import 'package:exe_team_matching/features/messages/conversations_screen.dart';
import 'package:exe_team_matching/features/messages/messages_api.dart';
import 'package:exe_team_matching/features/files/file_services.dart';
import 'package:exe_team_matching/features/realtime/collab_hub.dart';
import 'package:exe_team_matching/features/workspace/workspace_api.dart';
import 'package:exe_team_matching/features/workspace/workspace_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';

import 'support/workspace_fakes.dart';

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
  List<String> lastFiles = const [], lastMentions = const [];
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
  Future<Message> send(
    String channelId, {
    required String clientMessageId,
    required String body,
    String? parentMessageId,
    bool isAdvisorQuestion = false,
    List<String> attachmentFileIds = const [],
    List<String> mentionPersonIds = const [],
  }) async {
    sent.add('$clientMessageId $body question=$isAdvisorQuestion');
    lastFiles = attachmentFileIds;
    lastMentions = mentionPersonIds;
    if (failNext-- > 0) throw const ApiException(status: 0, code: 'network', title: 'Mất kết nối');
    return _msg('new', body, minute: 9);
  }
}

class _FilesApi extends FakeCollabApi {
  _FilesApi({super.folderContent});

  @override
  Future<String> downloadUrl(String fileId, {int? version}) async => 'https://s3/$fileId';
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

  testWidgets('C03: chọn file C06 và @nhắc tên rồi gửi; tin có file nhấn để tải', (tester) async {
    tester.view.physicalSize = const Size(800, 1600);
    addTearDown(tester.view.resetPhysicalSize);
    final file = ProjectFile(id: 'f1', name: 'ke-hoach.pdf', currentVersion: 1, sizeBytes: 2048, updatedByName: 'Vy', updatedAt: DateTime.utc(2026, 10, 27));
    final archived = ProjectFile(id: 'f2', name: 'exe101.pdf', currentVersion: 1, sizeBytes: 1, updatedByName: 'Vy', updatedAt: DateTime.utc(2026, 10, 27), isArchive: true);
    const lecturer = PersonRef(personType: 'LECTURER', personId: 'l1', displayName: 'Cô Hạnh', role: WorkspaceRole.instructor);
    final opened = <String>[];
    final api = _FakeApi(items: [
      Message(id: 'm1', channelId: 'c1', author: lecturer, body: 'Xem file', createdAt: DateTime.utc(2026, 10, 27), attachments: [file]),
    ]);
    await _pump(tester, const ChannelView(channel: _channel, readOnly: false, isStudent: true, people: [_leader, lecturer]), api, overrides: [
      collabApiOverride(_FilesApi(folderContent: FolderContent(files: [file, archived]))),
      openUrlProvider.overrideWithValue((url) async {
        opened.add(url);
        return true;
      }),
    ]);

    await tester.tap(find.text('ke-hoach.pdf'));
    await tester.pumpAndSettle();
    expect(opened, ['https://s3/f1']);

    await tester.tap(find.byTooltip('Đính kèm file'));
    await tester.pumpAndSettle();
    expect(find.text('exe101.pdf'), findsNothing);
    await tester.tap(find.widgetWithText(CheckboxListTile, 'ke-hoach.pdf'));
    await tester.pump();
    await tester.tap(find.text('Đính kèm (1)'));
    await tester.pumpAndSettle();
    expect(find.widgetWithText(InputChip, 'ke-hoach.pdf'), findsOneWidget);

    await tester.enterText(find.byType(TextField), 'Cô @Tr');
    await tester.pump();
    expect(find.widgetWithText(ListTile, 'Trưởng Nhóm'), findsNothing, reason: 'không tự nhắc mình');
    await tester.enterText(find.byType(TextField), 'Cô @h');
    await tester.pump();
    await tester.tap(find.widgetWithText(ListTile, 'Cô Hạnh'));
    await tester.pump();
    await tester.tap(find.byTooltip('Gửi'));
    await tester.pumpAndSettle();

    expect(api.sent.single, endsWith('Cô @Cô Hạnh question=false'));
    expect(api.lastFiles, ['f1']);
    expect(api.lastMentions, ['l1']);
    expect(find.byType(InputChip), findsNothing);
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
