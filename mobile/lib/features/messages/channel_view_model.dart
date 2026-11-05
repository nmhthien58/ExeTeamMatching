import 'dart:math';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/view_state.dart';
import '../files/file_services.dart' show openUrlProvider;
import '../realtime/collab_hub.dart';
import '../workspace/workspace_api.dart' show collabApiProvider;
import 'messages_api.dart';

/// Nội dung khung soạn tin khi bấm Gửi.
typedef MessageDraft = ({String body, bool asQuestion, List<String> fileIds, List<String> mentionIds});

/// clientMessageId dạng UUID v4 – gửi lại cùng id thì server không tạo tin trùng.
String newClientId() {
  final r = Random.secure();
  final b = List<int>.generate(16, (_) => r.nextInt(256));
  b[6] = (b[6] & 0x0f) | 0x40;
  b[8] = (b[8] & 0x3f) | 0x80;
  final h = b.map((x) => x.toRadixString(16).padLeft(2, '0')).join();
  return '${h.substring(0, 8)}-${h.substring(8, 12)}-${h.substring(12, 16)}-${h.substring(16, 20)}-${h.substring(20)}';
}

/// Tin đang gửi (chưa có id server); [failed] thì hiện "Gửi lỗi · Gửi lại".
class PendingMessage {
  const PendingMessage({required this.clientId, required this.draft, this.failed = false});

  final String clientId;
  final MessageDraft draft;
  final bool failed;
}

/// Trạng thái C03: tin đã tải (cũ → mới), tin đang gửi, con trỏ trang cũ hơn, đang tải, lỗi tải.
class ChannelState {
  const ChannelState({this.items = const [], this.pending = const [], this.cursor, this.loading = true, this.error, this.hidden = const {}});

  final List<Message> items;

  /// Tin vừa bấm Xóa, đang chờ 5 giây Hoàn tác: ẩn khỏi danh sách.
  final Set<String> hidden;
  final List<PendingMessage> pending;
  final String? cursor;
  final bool loading;
  final Object? error;

  bool get hasMore => cursor != null;

  /// Trạng thái hiển thị: lỗi / đang tải chỉ chiếm màn khi chưa có tin; còn tin đang gửi thì không phải màn trống.
  ViewState<List<Message>> get view => error != null && items.isEmpty
      ? ViewError(error!)
      : loading && items.isEmpty
      ? const ViewLoading()
      : items.isEmpty && pending.isEmpty
      ? const ViewEmpty()
      : ViewData(items);

  ChannelState copyWith({
    List<Message>? items,
    List<PendingMessage>? pending,
    String? Function()? cursor,
    bool? loading,
    Object? Function()? error,
    Set<String>? hidden,
  }) =>
      ChannelState(
        hidden: hidden ?? this.hidden,
        items: items ?? this.items,
        pending: pending ?? this.pending,
        cursor: cursor == null ? this.cursor : cursor(),
        loading: loading ?? this.loading,
        error: error == null ? this.error : error(),
      );
}

/// Tham số của [ChannelViewModel]: kênh và quyền chỉ xem (Bộ môn không đánh dấu đã đọc).
typedef ChannelArgs = ({String channelId, bool readOnly});

/// ViewModel của C03 – Kênh tin nhắn (MB-39, MVVM MB-49): tải lịch sử, tin cũ hơn, gửi / gửi lại, cập nhật realtime từ hub,
/// đánh dấu đã đọc. Khung soạn tin (chữ, @nhắc, file) là trạng thái giao diện ở View.
class ChannelViewModel extends Notifier<ChannelState> {
  ChannelViewModel(this.args);

  final ChannelArgs args;
  final Set<String> _countedReplies = {};
  String? _lastRead;

  String get _channelId => args.channelId;

  MessagesApi get _api => ref.read(messagesApiProvider);

  @override
  ChannelState build() {
    final hub = ref.read(collabHubProvider);
    final unsubscribe = [
      hub.join(HubGroup.channel, _channelId),
      hub.on('messageCreated', (p) => _onMessage(p, created: true)),
      hub.on('messageUpdated', (p) => _onMessage(p, created: false)),
      hub.on('messageDeleted', (p) => _updateRoot(p, (m) => m.copyWith(isDeleted: true))),
      hub.on('questionStatusChanged', (p) => _updateRoot(p, (m) => m.copyWith(questionStatus: (p as Map)['status'] as String?))),
    ];
    ref.onDispose(() {
      for (final u in unsubscribe) {
        u();
      }
    });
    Future.microtask(load);
    return const ChannelState();
  }

  /// Tải trang mới nhất ([older] = false) hoặc trang cũ hơn.
  Future<void> load({bool older = false}) async {
    state = state.copyWith(loading: true, error: () => null);
    try {
      final (items, next) = await _api.history(_channelId, before: older ? state.cursor : null);
      if (!ref.mounted) return;
      final kept = older ? state.items : const <Message>[];
      state = state.copyWith(items: [...items.reversed.where((m) => kept.every((x) => x.id != m.id)), ...kept], cursor: () => next, loading: false);
      if (!older) _markRead();
    } catch (e) {
      if (ref.mounted) state = state.copyWith(error: () => e, loading: false);
    }
  }

  void send(MessageDraft draft) => retry(PendingMessage(clientId: newClientId(), draft: draft));

  /// Gửi (lại) một tin đang chờ; cùng clientId nên server không tạo trùng.
  Future<void> retry(PendingMessage p) async {
    final sending = PendingMessage(clientId: p.clientId, draft: p.draft);
    _replacePending(p.clientId, sending);
    try {
      final m = await _api.send(
        _channelId,
        clientMessageId: p.clientId,
        body: p.draft.body,
        isAdvisorQuestion: p.draft.asQuestion,
        attachmentFileIds: p.draft.fileIds,
        mentionPersonIds: p.draft.mentionIds,
      );
      if (!ref.mounted) return;
      state = state.copyWith(pending: [...state.pending.where((x) => x.clientId != p.clientId)]);
      _apply(m, created: true);
    } catch (_) {
      if (ref.mounted) _replacePending(p.clientId, PendingMessage(clientId: p.clientId, draft: p.draft, failed: true));
    }
  }

  /// Sửa tin của mình; trả câu lỗi để View hiện, null khi thành công.
  Future<String?> edit(Message m, String body) async {
    try {
      final updated = await _api.edit(m.id, body);
      if (ref.mounted) _apply(updated, created: false);
      return null;
    } catch (e) {
      return e.apiError.detail ?? e.apiError.title;
    }
  }

  void hide(String messageId) => state = state.copyWith(hidden: {...state.hidden, messageId});

  void unhide(String messageId) => state = state.copyWith(hidden: {...state.hidden}..remove(messageId));

  /// Gọi sau 5 giây Hoàn tác; trả câu lỗi khi xóa không được (tin hiện lại).
  Future<String?> delete(Message m) async {
    try {
      await _api.delete(m.id);
      if (!ref.mounted) return null;
      final i = state.items.indexWhere((x) => x.id == m.id);
      if (i >= 0) state = state.copyWith(items: [...state.items]..[i] = state.items[i].copyWith(isDeleted: true));
      return null;
    } catch (e) {
      return e.apiError.detail ?? e.apiError.title;
    } finally {
      if (ref.mounted) unhide(m.id);
    }
  }

  void _replacePending(String clientId, PendingMessage p) {
    final i = state.pending.indexWhere((x) => x.clientId == clientId);
    state = state.copyWith(pending: i < 0 ? [...state.pending, p] : ([...state.pending]..[i] = p));
  }

  void _onMessage(Object? payload, {required bool created}) {
    if (payload is! Map || !ref.mounted) return;
    _apply(Message.fromJson(Map<String, dynamic>.from(payload)), created: created);
  }

  /// Tin mới / đã sửa từ REST hoặc hub; bỏ trùng theo id (REST và hub cùng báo một tin).
  void _apply(Message m, {required bool created}) {
    if (m.channelId != _channelId) return;
    if (created && m.parentMessageId != null && !_countedReplies.add(m.id)) return;
    final items = [...state.items];
    final i = items.indexWhere((x) => x.id == m.id);
    if (m.parentMessageId == null) {
      i >= 0 ? items[i] = m : items.add(m);
    } else if (created) {
      final root = items.indexWhere((x) => x.id == m.parentMessageId);
      if (root >= 0) items[root] = items[root].copyWith(replyCount: items[root].replyCount + 1);
    }
    state = state.copyWith(items: items);
    _markRead();
  }

  void _updateRoot(Object? payload, Message Function(Message) update) {
    if (payload is! Map || payload['channelId'] != _channelId || !ref.mounted) return;
    final i = state.items.indexWhere((x) => x.id == payload['messageId']);
    if (i >= 0) state = state.copyWith(items: [...state.items]..[i] = update(state.items[i]));
  }

  void _markRead() {
    final newest = state.items.lastOrNull;
    if (args.readOnly || newest == null || newest.id == _lastRead) return;
    _lastRead = newest.id;
    _api.markRead(_channelId, newest.id).ignore();
  }
}

final channelViewModelProvider = NotifierProvider.autoDispose.family<ChannelViewModel, ChannelState, ChannelArgs>(ChannelViewModel.new);

/// Trạng thái luồng trả lời: tin gốc mới nhất từ server, các trả lời, lỗi tải, lỗi gửi trả lời.
class ThreadState {
  const ThreadState({this.root, this.replies = const [], this.error, this.sendError});

  final Message? root;
  final List<Message> replies;
  final Object? error;
  final String? sendError;

  ThreadState copyWith({Message? root, List<Message>? replies, Object? Function()? error, String? Function()? sendError}) => ThreadState(
    root: root ?? this.root,
    replies: replies ?? this.replies,
    error: error == null ? this.error : error(),
    sendError: sendError == null ? this.sendError : sendError(),
  );
}

typedef ThreadArgs = ({String rootId, String channelId});

/// ViewModel của luồng trả lời (bottom sheet trong C03): tải tin gốc + trả lời, nhận trả lời mới qua hub, gửi trả lời.
class ThreadViewModel extends Notifier<ThreadState> {
  ThreadViewModel(this.args);

  final ThreadArgs args;

  @override
  ThreadState build() {
    final off = ref.read(collabHubProvider).on('messageCreated', (p) {
      if (p is! Map || p['parentMessageId'] != args.rootId || !ref.mounted) return;
      _add(Message.fromJson(Map<String, dynamic>.from(p)));
    });
    ref.onDispose(off);
    Future.microtask(load);
    return const ThreadState();
  }

  Future<void> load() async {
    try {
      final (root, replies) = await ref.read(messagesApiProvider).thread(args.rootId);
      if (ref.mounted) state = state.copyWith(root: root, replies: replies, error: () => null);
    } catch (e) {
      if (ref.mounted) state = state.copyWith(error: () => e);
    }
  }

  Future<void> reply(MessageDraft draft) async {
    state = state.copyWith(sendError: () => null);
    try {
      final m = await ref
          .read(messagesApiProvider)
          .send(
            args.channelId,
            clientMessageId: newClientId(),
            body: draft.body,
            parentMessageId: args.rootId,
            attachmentFileIds: draft.fileIds,
            mentionPersonIds: draft.mentionIds,
          );
      if (ref.mounted) _add(m);
    } catch (e) {
      if (ref.mounted) state = state.copyWith(sendError: () => e.apiError.detail ?? e.apiError.title);
    }
  }

  void _add(Message m) {
    if (state.replies.every((r) => r.id != m.id)) state = state.copyWith(replies: [...state.replies, m]);
  }
}

final threadViewModelProvider = NotifierProvider.autoDispose.family<ThreadViewModel, ThreadState, ThreadArgs>(ThreadViewModel.new);

/// Lệnh mở file đính kèm trong tin: lấy link tải từ Collaboration Service rồi mở. Lỗi ném `ApiException`.
final openAttachmentProvider = Provider<Future<void> Function(String fileId)>(
  (ref) =>
      (fileId) async => ref.read(openUrlProvider)(await ref.read(collabApiProvider).downloadUrl(fileId)),
);
