import 'dart:ui' show VoidCallback;

import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/core/view_state.dart';
import 'package:exe_team_matching/features/messages/channel_view_model.dart';
import 'package:exe_team_matching/features/messages/conversations_view_model.dart';
import 'package:exe_team_matching/features/messages/messages_api.dart';
import 'package:exe_team_matching/features/realtime/collab_hub.dart';
import 'package:exe_team_matching/features/workspace/workspace_api.dart';
import 'package:exe_team_matching/features/workspace/workspace_models.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/workspace_fakes.dart';

const _author = PersonRef(personType: 'STUDENT', personId: 's1', displayName: 'An', role: WorkspaceRole.leader);
const _error = ApiException(status: 0, code: 'network', title: 'Mất kết nối');

Message _msg(String id, {int minute = 0, String? parent}) =>
    Message(id: id, channelId: 'c1', author: _author, body: 'Tin $id', createdAt: DateTime.utc(2026, 11, 1, 3, minute), parentMessageId: parent);

class _FakeApi extends MessagesApi {
  _FakeApi() : super(Dio());

  List<Message> history_ = const [];
  String? next;
  Object? error;
  int failSend = 0;
  final read = <String>[];
  final sent = <String>[];
  Map<String, List<Channel>> channelsOf = const {};
  Set<String> waiting = const {};

  @override
  Future<(List<Message>, String?)> history(String channelId, {String? before}) async {
    if (error != null) throw error!;
    return (history_, before == null ? next : null);
  }

  @override
  Future<(Message, List<Message>)> thread(String messageId) async {
    if (error != null) throw error!;
    return (_msg(messageId), [_msg('r1', parent: messageId)]);
  }

  @override
  Future<void> markRead(String channelId, String lastReadMessageId) async => read.add(lastReadMessageId);

  @override
  Future<List<Channel>> channels(String workspaceId) async => channelsOf[workspaceId] ?? const [];

  @override
  Future<Set<String>> channelsWithOpenQuestions() async => waiting;

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
    sent.add('$clientMessageId $body');
    if (failSend-- > 0) throw _error;
    return _msg('new-${sent.length}', minute: 9, parent: parentMessageId);
  }
}

class _Connection implements HubConnectionLike {
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

Map<String, Object?> _json(String id, {String? parent}) => {
  'id': id,
  'channelId': 'c1',
  'author': {'personType': 'STUDENT', 'personId': 's2', 'displayName': 'Bình'},
  'body': 'Tin $id',
  'createdAt': '2026-11-01T03:10:00Z',
  'parentMessageId': parent,
};

Future<void> _settle() => Future<void>.delayed(Duration.zero);

/// MB-49 – ViewModel của C02 (cố vấn), C03 và luồng trả lời, test riêng không dựng widget.
void main() {
  late _FakeApi api;
  late _Connection connection;
  late FakeCollabApi collab;
  late ProviderContainer c;

  setUp(() {
    api = _FakeApi();
    connection = _Connection();
    collab = FakeCollabApi();
    c = ProviderContainer(
      overrides: [
        messagesApiProvider.overrideWithValue(api),
        collabHubProvider.overrideWithValue(CollabHub(() => connection)),
        collabApiProvider.overrideWithValue(collab),
      ],
    );
    addTearDown(c.dispose);
  });

  group('ChannelViewModel (C03)', () {
    const args = (channelId: 'c1', readOnly: false);
    ChannelState state() => c.read(channelViewModelProvider(args));
    ChannelViewModel vm() => c.read(channelViewModelProvider(args).notifier);

    test('tải được: cũ → mới, đánh dấu đã đọc tin mới nhất; Tải tin cũ hơn', () async {
      api
        ..history_ = [_msg('m2', minute: 2), _msg('m1', minute: 1)]
        ..next = 'cursor';
      c.listen(channelViewModelProvider(args), (_, _) {});
      expect(state().view, isA<ViewLoading<List<Message>>>());
      await _settle();
      expect(state().items.map((m) => m.id), ['m1', 'm2']);
      expect(state().hasMore, isTrue);
      expect(api.read, ['m2']);

      api.history_ = [_msg('m0')];
      await vm().load(older: true);
      expect(state().items.map((m) => m.id), ['m0', 'm1', 'm2']);
      expect(state().hasMore, isFalse);
    });

    test('trống: ViewEmpty; lỗi: ViewError', () async {
      c.listen(channelViewModelProvider(args), (_, _) {});
      await _settle();
      expect(state().view, isA<ViewEmpty<List<Message>>>());
      api.error = _error;
      await vm().load();
      expect((state().view as ViewError).error, _error);
    });

    test('gửi lỗi giữ tin chờ (failed), gửi lại cùng clientId thì thành tin thật', () async {
      c.listen(channelViewModelProvider(args), (_, _) {});
      await _settle();
      api.failSend = 1;
      vm().send((body: 'Chào', asQuestion: false, fileIds: const [], mentionIds: const []));
      await _settle();
      final pending = state().pending.single;
      expect(pending.failed, isTrue);
      expect(state().view, isA<ViewData<List<Message>>>()); // có tin chờ thì không phải màn trống

      await vm().retry(pending);
      expect(state().pending, isEmpty);
      expect(state().items.single.id, 'new-2');
      expect(api.sent.map((s) => s.split(' ').first).toSet(), hasLength(1));
    });

    test('realtime: tin mới, bỏ trùng, trả lời tăng replyCount, xóa tin', () async {
      api.history_ = [_msg('m1')];
      c.listen(channelViewModelProvider(args), (_, _) {});
      await _settle();
      connection.emit('messageCreated', _json('m2'));
      connection.emit('messageCreated', _json('m2'));
      connection.emit('messageCreated', _json('r1', parent: 'm1'));
      connection.emit('messageCreated', _json('r1', parent: 'm1'));
      connection.emit('messageDeleted', {'channelId': 'c1', 'messageId': 'm2'});
      expect(state().items.map((m) => m.id), ['m1', 'm2']);
      expect(state().items.first.replyCount, 1);
      expect(state().items.last.isDeleted, isTrue);
    });

    test('Bộ môn chỉ xem: không đánh dấu đã đọc', () async {
      api.history_ = [_msg('m1')];
      const readOnly = (channelId: 'c1', readOnly: true);
      c.listen(channelViewModelProvider(readOnly), (_, _) {});
      await _settle();
      expect(api.read, isEmpty);
    });
  });

  group('ThreadViewModel', () {
    const args = (rootId: 'm1', channelId: 'c1');

    test('tải tin gốc và trả lời; trả lời mới qua hub; gửi trả lời', () async {
      c.listen(threadViewModelProvider(args), (_, _) {});
      await _settle();
      expect(c.read(threadViewModelProvider(args)).replies.map((m) => m.id), ['r1']);
      connection.emit('messageCreated', _json('r2', parent: 'm1'));
      await c.read(threadViewModelProvider(args).notifier).reply((body: 'Ok', asQuestion: false, fileIds: const [], mentionIds: const []));
      expect(c.read(threadViewModelProvider(args)).replies.map((m) => m.id), ['r1', 'r2', 'new-1']);
    });

    test('lỗi tải và lỗi gửi', () async {
      api
        ..error = _error
        ..failSend = 1;
      c.listen(threadViewModelProvider(args), (_, _) {});
      await _settle();
      expect(c.read(threadViewModelProvider(args)).error, _error);
      await c.read(threadViewModelProvider(args).notifier).reply((body: 'Ok', asQuestion: false, fileIds: const [], mentionIds: const []));
      expect(c.read(threadViewModelProvider(args)).sendError, 'Mất kết nối');
    });
  });

  group('AdvisorConversationsViewModel (C02 cố vấn)', () {
    const ws = [
      WorkspaceOverview(id: 'w1', teamName: 'Nhóm A', courseCode: 'EXE101', myRole: WorkspaceRole.instructor),
      WorkspaceOverview(id: 'w2', teamName: 'Nhóm B', courseCode: 'EXE101', myRole: WorkspaceRole.instructor),
    ];
    AdvisorConversationsState state() => c.read(advisorConversationsViewModelProvider);

    Future<void> load() async {
      c.listen(advisorConversationsViewModelProvider, (_, _) {});
      for (var i = 0; i < 5; i++) {
        await _settle();
      }
    }

    test('gộp kênh mọi dự án, sắp theo tin mới nhất; lọc "Cần trả lời" giữ khi dữ liệu đổi', () async {
      collab.workspaces = ws;
      api
        ..channelsOf = {
          'w1': [Channel(id: 'a', workspaceId: 'w1', kind: 'ADVISORS', lastMessage: _msg('x', minute: 1))],
          'w2': [Channel(id: 'b', workspaceId: 'w2', kind: 'ADVISORS', lastMessage: _msg('y', minute: 5))],
        }
        ..waiting = {'a'};
      await load();
      final inbox = state().inbox.valueOrNull!;
      final channels = inbox.channels.valueOrNull!;
      expect(channels.map((c) => c.id), ['b', 'a']);
      expect(inbox.titleOf(channels.first), 'Nhóm B · Cố vấn');

      c.read(advisorConversationsViewModelProvider.notifier).setNeedsReplyOnly(true);
      await c.read(advisorConversationsViewModelProvider.notifier).refresh();
      await load();
      expect(state().needsReplyOnly, isTrue);
      expect(state().inbox.valueOrNull!.channels.valueOrNull!.map((c) => c.id), ['a']);
    });

    test('chưa được ghép nhóm nào: kênh trống, câu trống theo bộ lọc', () async {
      await load();
      expect(state().inbox.valueOrNull!.channels, isA<ViewEmpty<List<Channel>>>());
      expect(state().emptyTitle, 'Bạn chưa được ghép với nhóm nào');
    });

    test('lỗi tải dự án: ViewError', () async {
      c = ProviderContainer(overrides: [messagesApiProvider.overrideWithValue(api), myWorkspacesProvider.overrideWith((ref) async => throw _error)]);
      addTearDown(c.dispose);
      await load();
      expect((state().inbox as ViewError).error, _error);
    });
  });
}
