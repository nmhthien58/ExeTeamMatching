import 'dart:math';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../realtime/collab_hub.dart';
import '../workspace/format.dart';
import '../workspace/workspace_api.dart' show workspaceProvider;
import '../workspace/workspace_models.dart' show WorkspaceRole;
import '../workspace/workspace_widgets.dart' show Avatar, RoleChip;
import 'messages_api.dart';

const maxMessageLength = 2000;

/// clientMessageId dạng UUID v4 – gửi lại cùng id thì server không tạo tin trùng.
String newClientId() {
  final r = Random.secure();
  final b = List<int>.generate(16, (_) => r.nextInt(256));
  b[6] = (b[6] & 0x0f) | 0x40;
  b[8] = (b[8] & 0x3f) | 0x80;
  final h = b.map((x) => x.toRadixString(16).padLeft(2, '0')).join();
  return '${h.substring(0, 8)}-${h.substring(8, 12)}-${h.substring(12, 16)}-${h.substring(16, 20)}-${h.substring(20)}';
}

/// C03 – Kênh tin nhắn (MB-39): nạp workspace và kênh rồi mở [ChannelView]. Mở từ C02, C01 hoặc thông báo câu hỏi.
class ChannelScreen extends ConsumerWidget {
  const ChannelScreen({super.key, required this.workspaceId, required this.channelId});

  final String workspaceId;
  final String channelId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final workspace = ref.watch(workspaceProvider(workspaceId));
    final channels = ref.watch(channelsProvider(workspaceId));
    final error = workspace.error ?? channels.error;
    final channel = channels.value?.where((c) => c.id == channelId).firstOrNull;
    final ws = workspace.value;
    final isStudent = ws?.myRole == WorkspaceRole.leader || ws?.myRole == WorkspaceRole.member;
    return Scaffold(
      appBar: AppBar(title: Text(channel == null || ws == null ? 'Tin nhắn' : isStudent ? channel.label : '${ws.teamName} · ${channel.label}')),
      body: error != null
          ? ErrorState(error: error, onRetry: () => ref.invalidate(channelsProvider(workspaceId)))
          : ws == null || channels.isLoading
              ? const SkeletonList(count: 3)
              : channel == null
                  ? const EmptyState(icon: Icons.forum_outlined, title: 'Không tìm thấy kênh')
                  : ChannelView(channel: channel, readOnly: ws.myRole == WorkspaceRole.admin, isStudent: isStudent),
    );
  }
}

class _Pending {
  _Pending({required this.clientId, required this.body, this.asQuestion = false});

  final String clientId;
  final String body;
  final bool asQuestion;
  bool failed = false;
}

/// Danh sách tin (mới nhất ở dưới, chia theo ngày), khung soạn tin, cập nhật realtime, đánh dấu đã đọc.
// shortcut: chưa có đính kèm file (API chưa nhận), @nhắc tên, sửa / xóa tin trên giao diện; bổ sung khi cần.
class ChannelView extends ConsumerStatefulWidget {
  const ChannelView({super.key, required this.channel, required this.readOnly, required this.isStudent});

  final Channel channel;
  final bool readOnly;
  final bool isStudent;

  @override
  ConsumerState<ChannelView> createState() => _ChannelViewState();
}

class _ChannelViewState extends ConsumerState<ChannelView> {
  final List<Message> _items = [];
  final List<_Pending> _pending = [];
  final List<VoidCallback> _unsubscribe = [];
  final Set<String> _countedReplies = {};
  String? _cursor;
  bool _hasMore = false;
  bool _loading = true;
  Object? _error;
  String? _lastRead;

  String get _channelId => widget.channel.id;

  @override
  void initState() {
    super.initState();
    final hub = ref.read(collabHubProvider);
    _unsubscribe.addAll([
      hub.join(HubGroup.channel, _channelId),
      hub.on('messageCreated', (p) => _onMessage(p, created: true)),
      hub.on('messageUpdated', (p) => _onMessage(p, created: false)),
      hub.on('messageDeleted', (p) => _updateRoot(p, (m) => m.copyWith(isDeleted: true))),
      hub.on('questionStatusChanged', (p) => _updateRoot(p, (m) => m.copyWith(questionStatus: (p as Map)['status'] as String?))),
    ]);
    _load();
  }

  @override
  void dispose() {
    for (final u in _unsubscribe) {
      u();
    }
    super.dispose();
  }

  Future<void> _load({bool older = false}) async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final (items, next) = await ref.read(messagesApiProvider).history(_channelId, before: older ? _cursor : null);
      if (!mounted) return;
      setState(() {
        if (!older) _items.clear();
        _items.insertAll(0, items.reversed.where((m) => _items.every((x) => x.id != m.id)));
        _cursor = next;
        _hasMore = next != null;
      });
      if (!older) _markRead();
    } catch (e) {
      if (mounted) setState(() => _error = e);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _onMessage(Object? payload, {required bool created}) {
    if (payload is! Map || !mounted) return;
    _apply(Message.fromJson(Map<String, dynamic>.from(payload)), created: created);
  }

  /// Tin mới / đã sửa từ REST hoặc hub; bỏ trùng theo id (REST và hub cùng báo một tin).
  void _apply(Message m, {required bool created}) {
    if (m.channelId != _channelId) return;
    if (created && m.parentMessageId != null && !_countedReplies.add(m.id)) return;
    setState(() {
      final i = _items.indexWhere((x) => x.id == m.id);
      if (m.parentMessageId == null) {
        i >= 0 ? _items[i] = m : _items.add(m);
      } else if (created) {
        final root = _items.indexWhere((x) => x.id == m.parentMessageId);
        if (root >= 0) _items[root] = _items[root].copyWith(replyCount: _items[root].replyCount + 1);
      }
    });
    _markRead();
  }

  void _updateRoot(Object? payload, Message Function(Message) update) {
    if (payload is! Map || payload['channelId'] != _channelId || !mounted) return;
    setState(() {
      final i = _items.indexWhere((x) => x.id == payload['messageId']);
      if (i >= 0) _items[i] = update(_items[i]);
    });
  }

  void _markRead() {
    final newest = _items.lastOrNull;
    if (widget.readOnly || newest == null || newest.id == _lastRead) return;
    _lastRead = newest.id;
    ref.read(messagesApiProvider).markRead(_channelId, newest.id).ignore();
  }

  Future<void> _send(_Pending p) async {
    setState(() {
      p.failed = false;
      if (!_pending.contains(p)) _pending.add(p);
    });
    try {
      final m = await ref
          .read(messagesApiProvider)
          .send(_channelId, clientMessageId: p.clientId, body: p.body, isAdvisorQuestion: p.asQuestion);
      if (!mounted) return;
      setState(() => _pending.remove(p));
      _apply(m, created: true);
    } catch (_) {
      if (mounted) setState(() => p.failed = true);
    }
  }

  Future<void> _openThread(Message root) => showModalBottomSheet<void>(
        context: context,
        isScrollControlled: true,
        useSafeArea: true,
        showDragHandle: true,
        builder: (_) => ThreadSheet(root: root, channel: widget.channel, readOnly: widget.readOnly),
      );

  @override
  Widget build(BuildContext context) {
    final mine = ref.watch(myPersonIdsProvider);
    return Column(
      children: [
        const RealtimeStatusBanner(),
        Expanded(
          child: _error != null && _items.isEmpty
              ? ErrorState(error: _error!, onRetry: _load)
              : _loading && _items.isEmpty
                  ? const SkeletonList(count: 3)
                  : _items.isEmpty && _pending.isEmpty
                      ? EmptyState(icon: Icons.forum_outlined, title: 'Chưa có tin nhắn.', description: 'Bắt đầu trò chuyện ở kênh ${widget.channel.label}.')
                      : ListView(
                          reverse: true,
                          padding: const EdgeInsets.all(AppSpacing.s3),
                          children: [
                            for (final p in _pending.reversed) _PendingBubble(pending: p, onRetry: () => _send(p)),
                            for (var i = _items.length - 1; i >= 0; i--) ...[
                              MessageBubble(message: _items[i], mine: mine.contains(_items[i].author.personId), onThread: () => _openThread(_items[i])),
                              if (i == 0 || !_sameDay(_items[i - 1].createdAt, _items[i].createdAt)) _DayDivider(date: _items[i].createdAt),
                            ],
                            if (_hasMore)
                              Center(
                                child: _loading
                                    ? const Padding(padding: EdgeInsets.all(AppSpacing.s2), child: CircularProgressIndicator())
                                    : TextButton(onPressed: () => _load(older: true), child: const Text('Tải tin cũ hơn')),
                              ),
                          ],
                        ),
        ),
        if (widget.readOnly)
          const Padding(padding: EdgeInsets.all(AppSpacing.s4), child: Text('Bộ môn chỉ xem kênh này.'))
        else
          MessageComposer(
            allowQuestion: !widget.channel.isTeam && widget.isStudent,
            onSend: (body, asQuestion) => _send(_Pending(clientId: newClientId(), body: body, asQuestion: asQuestion)),
          ),
      ],
    );
  }
}

bool _sameDay(DateTime a, DateTime b) {
  final x = a.toLocal(), y = b.toLocal();
  return x.year == y.year && x.month == y.month && x.day == y.day;
}


class _DayDivider extends StatelessWidget {
  const _DayDivider({required this.date});

  final DateTime date;

  @override
  Widget build(BuildContext context) {
    final l = date.toLocal();
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: AppSpacing.s2),
      child: Center(child: Text('${l.day.toString().padLeft(2, '0')}/${l.month.toString().padLeft(2, '0')}/${l.year}', style: Theme.of(context).textTheme.bodySmall)),
    );
  }
}

/// Bong bóng tin (DESIGN.md 6.3): của mình bên phải nền Surface viền Border; người khác bên trái nền Info Container, avatar,
/// tên và nhãn vai trò. Câu hỏi cho cố vấn có chip Chờ / Đã trả lời. Nhấn giữ → Trả lời trong luồng.
class MessageBubble extends StatelessWidget {
  const MessageBubble({super.key, required this.message, required this.mine, this.onThread});

  final Message message;
  final bool mine;
  final VoidCallback? onThread;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final m = message;
    final bubble = Container(
      padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s3, vertical: AppSpacing.s2),
      decoration: BoxDecoration(
        color: mine ? colors.surface : context.appColors.infoContainer,
        border: mine ? Border.all(color: colors.outlineVariant) : null,
        borderRadius: BorderRadius.circular(AppRadius.lg),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          m.isDeleted
              ? Text('Tin nhắn đã bị xóa', style: theme.textTheme.bodyMedium?.copyWith(fontStyle: FontStyle.italic))
              : Text(m.body, style: theme.textTheme.bodyLarge),
          Text('${formatDateTime(m.createdAt)}${m.editedAt != null ? ' · đã sửa' : ''}', style: theme.textTheme.bodySmall),
        ],
      ),
    );
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: AppSpacing.s1),
      child: Row(
        mainAxisAlignment: mine ? MainAxisAlignment.end : MainAxisAlignment.start,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (!mine) ...[Avatar(name: m.author.displayName, size: 32, isMentor: m.author.role == WorkspaceRole.mentor), const SizedBox(width: AppSpacing.s2)],
          Flexible(
            child: Column(
              crossAxisAlignment: mine ? CrossAxisAlignment.end : CrossAxisAlignment.start,
              children: [
                if (!mine)
                  Wrap(spacing: AppSpacing.s2, crossAxisAlignment: WrapCrossAlignment.center, children: [
                    Text(m.author.displayName, style: theme.textTheme.labelLarge),
                    RoleChip(role: m.author.role),
                  ]),
                GestureDetector(onLongPress: m.isDeleted ? null : onThread, child: bubble),
                Wrap(spacing: AppSpacing.s2, crossAxisAlignment: WrapCrossAlignment.center, children: [
                  if (m.isAdvisorQuestion)
                    Chip(
                      avatar: Icon(m.questionStatus == 'ANSWERED' ? Icons.check_circle_outline : Icons.schedule, size: 18),
                      label: Text(m.questionStatus == 'ANSWERED' ? 'Đã trả lời' : 'Chờ trả lời'),
                    ),
                  if (onThread != null && !m.isDeleted)
                    TextButton.icon(onPressed: onThread, icon: const Icon(Icons.reply, size: 18), label: Text(m.replyCount > 0 ? '${m.replyCount} trả lời' : 'Trả lời')),
                ]),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _PendingBubble extends StatelessWidget {
  const _PendingBubble({required this.pending, required this.onRetry});

  final _Pending pending;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Align(
      alignment: Alignment.centerRight,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          Container(
            margin: const EdgeInsets.only(top: AppSpacing.s1),
            padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s3, vertical: AppSpacing.s2),
            decoration: BoxDecoration(border: Border.all(color: theme.colorScheme.outlineVariant), borderRadius: BorderRadius.circular(AppRadius.lg)),
            child: Text(pending.body),
          ),
          pending.failed
              ? TextButton.icon(onPressed: onRetry, icon: const Icon(Icons.refresh, size: 18), label: const Text('Gửi lỗi · Gửi lại'))
              : Text('Đang gửi…', style: theme.textTheme.bodySmall),
        ],
      ),
    );
  }
}

/// Khung soạn tin dính đáy: tối đa 2.000 ký tự, công tắc "Hỏi cố vấn" ở kênh Cố vấn (chỉ sinh viên, chỉ tin gốc).
class MessageComposer extends StatefulWidget {
  const MessageComposer({super.key, required this.onSend, this.allowQuestion = false, this.hint = 'Nhập tin nhắn'});

  final void Function(String body, bool asQuestion) onSend;
  final bool allowQuestion;
  final String hint;

  @override
  State<MessageComposer> createState() => _MessageComposerState();
}

class _MessageComposerState extends State<MessageComposer> {
  final _text = TextEditingController();
  bool _asQuestion = false;

  @override
  void dispose() {
    _text.dispose();
    super.dispose();
  }

  void _send() {
    final body = _text.text.trim();
    if (body.isEmpty) return;
    widget.onSend(body, _asQuestion);
    _text.clear();
    setState(() => _asQuestion = false);
  }

  @override
  Widget build(BuildContext context) => SafeArea(
        top: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(AppSpacing.s3, AppSpacing.s2, AppSpacing.s3, AppSpacing.s2),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (widget.allowQuestion)
                SwitchListTile(
                  dense: true,
                  contentPadding: EdgeInsets.zero,
                  title: const Text('Hỏi cố vấn'),
                  value: _asQuestion,
                  onChanged: (v) => setState(() => _asQuestion = v),
                ),
              Row(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  Expanded(
                    child: TextField(
                      controller: _text,
                      minLines: 1,
                      maxLines: 5,
                      maxLength: maxMessageLength,
                      textInputAction: TextInputAction.newline,
                      decoration: InputDecoration(hintText: widget.hint, counterText: ''),
                      onChanged: (_) => setState(() {}),
                    ),
                  ),
                  IconButton.filled(tooltip: 'Gửi', onPressed: _text.text.trim().isEmpty ? null : _send, icon: const Icon(Icons.send)),
                ],
              ),
            ],
          ),
        ),
      );
}

/// Luồng trả lời (bottom sheet): tin gốc, các trả lời, khung trả lời. Cố vấn trả lời câu hỏi → "Đã trả lời".
class ThreadSheet extends ConsumerStatefulWidget {
  const ThreadSheet({super.key, required this.root, required this.channel, required this.readOnly});

  final Message root;
  final Channel channel;
  final bool readOnly;

  @override
  ConsumerState<ThreadSheet> createState() => _ThreadSheetState();
}

class _ThreadSheetState extends ConsumerState<ThreadSheet> {
  Message? _root;
  final List<Message> _replies = [];
  Object? _error;
  String? _sendError;
  late final VoidCallback _off;

  @override
  void initState() {
    super.initState();
    _off = ref.read(collabHubProvider).on('messageCreated', (p) {
      if (p is! Map || p['parentMessageId'] != widget.root.id || !mounted) return;
      final m = Message.fromJson(Map<String, dynamic>.from(p));
      if (_replies.every((r) => r.id != m.id)) setState(() => _replies.add(m));
    });
    _load();
  }

  @override
  void dispose() {
    _off();
    super.dispose();
  }

  Future<void> _load() async {
    try {
      final (root, replies) = await ref.read(messagesApiProvider).thread(widget.root.id);
      if (!mounted) return;
      setState(() {
        _root = root;
        _replies
          ..clear()
          ..addAll(replies);
      });
    } catch (e) {
      if (mounted) setState(() => _error = e);
    }
  }

  Future<void> _reply(String body) async {
    setState(() => _sendError = null);
    try {
      final m = await ref.read(messagesApiProvider).send(widget.channel.id, clientMessageId: newClientId(), body: body, parentMessageId: widget.root.id);
      if (mounted && _replies.every((r) => r.id != m.id)) setState(() => _replies.add(m));
    } catch (e) {
      if (mounted) setState(() => _sendError = e.apiError.detail ?? e.apiError.title);
    }
  }

  @override
  Widget build(BuildContext context) {
    final mine = ref.watch(myPersonIdsProvider);
    final root = _root ?? widget.root;
    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(context).bottom),
      child: ConstrainedBox(
        constraints: BoxConstraints(maxHeight: MediaQuery.sizeOf(context).height * 0.85),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text('Luồng trả lời', style: Theme.of(context).textTheme.titleMedium),
            Flexible(
              child: _error != null
                  ? ErrorState(error: _error!, onRetry: _load)
                  : ListView(
                      shrinkWrap: true,
                      padding: const EdgeInsets.all(AppSpacing.s3),
                      children: [
                        MessageBubble(message: root, mine: mine.contains(root.author.personId)),
                        const Divider(),
                        Text('${_replies.length} trả lời', style: Theme.of(context).textTheme.bodySmall),
                        for (final r in _replies) MessageBubble(message: r, mine: mine.contains(r.author.personId)),
                      ],
                    ),
            ),
            if (_sendError != null) ErrorBanner(message: _sendError!),
            if (!widget.readOnly) MessageComposer(hint: 'Trả lời trong luồng', onSend: (body, _) => _reply(body)),
          ],
        ),
      ),
    );
  }
}
