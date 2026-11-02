import 'dart:math';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../files/file_services.dart' show fileIcon, openUrlProvider;
import '../realtime/collab_hub.dart';
import '../workspace/format.dart';
import '../workspace/workspace_api.dart' show collabApiProvider, folderProvider, workspaceProvider;
import '../workspace/workspace_models.dart' show PersonRef, ProjectFile, WorkspaceRole;
import '../workspace/workspace_widgets.dart' show Avatar, RoleChip;
import 'messages_api.dart';

const maxMessageLength = 2000;
const maxAttachments = 5;

/// Nội dung khung soạn tin khi bấm Gửi.
typedef MessageDraft = ({String body, bool asQuestion, List<String> fileIds, List<String> mentionIds});

/// Người được @nhắc: trừ mình; kênh Nhóm chỉ có sinh viên (quy tắc 22).
List<PersonRef> mentionable(Channel channel, List<PersonRef> people, Set<String> mine) => [
      for (final p in people)
        if (!mine.contains(p.personId) && (!channel.isTeam || p.role == WorkspaceRole.leader || p.role == WorkspaceRole.member)) p,
    ];

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
                  : ChannelView(channel: channel, readOnly: ws.myRole == WorkspaceRole.admin, isStudent: isStudent, people: ws.people),
    );
  }
}

class _Pending {
  _Pending({required this.clientId, required this.draft});

  final String clientId;
  final MessageDraft draft;
  bool failed = false;
}

/// Danh sách tin (mới nhất ở dưới, chia theo ngày), khung soạn tin, cập nhật realtime, đánh dấu đã đọc.
// shortcut: chưa có sửa / xóa tin trên giao diện; bổ sung khi cần.
class ChannelView extends ConsumerStatefulWidget {
  const ChannelView({super.key, required this.channel, required this.readOnly, required this.isStudent, this.people = const []});

  final Channel channel;
  final bool readOnly;
  final bool isStudent;
  final List<PersonRef> people;

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
      final m = await ref.read(messagesApiProvider).send(
            _channelId,
            clientMessageId: p.clientId,
            body: p.draft.body,
            isAdvisorQuestion: p.draft.asQuestion,
            attachmentFileIds: p.draft.fileIds,
            mentionPersonIds: p.draft.mentionIds,
          );
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
        builder: (_) => ThreadSheet(root: root, channel: widget.channel, readOnly: widget.readOnly, people: widget.people),
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
            workspaceId: widget.channel.workspaceId,
            people: mentionable(widget.channel, widget.people, mine),
            allowQuestion: !widget.channel.isTeam && widget.isStudent,
            onSend: (draft) => _send(_Pending(clientId: newClientId(), draft: draft)),
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
          for (final f in m.attachments) _Attachment(file: f),
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

/// File đính kèm trong tin: nhấn để tải.
class _Attachment extends ConsumerWidget {
  const _Attachment({required this.file});

  final ProjectFile file;

  @override
  Widget build(BuildContext context, WidgetRef ref) => Material(
        type: MaterialType.transparency, // bong bóng có nền nên ListTile cần Material riêng
        child: ListTile(
          contentPadding: EdgeInsets.zero,
          leading: Icon(fileIcon(file.name)),
          title: Text(file.name, maxLines: 1, overflow: TextOverflow.ellipsis),
          subtitle: Text(formatSize(file.sizeBytes)),
          onTap: () async {
            final messenger = ScaffoldMessenger.of(context);
            try {
              await ref.read(openUrlProvider)(await ref.read(collabApiProvider).downloadUrl(file.id));
            } catch (e) {
              messenger.showSnackBar(SnackBar(content: Text(e.apiError.title)));
            }
          },
        ),
      );
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
            child: Text(pending.draft.body),
          ),
          pending.failed
              ? TextButton.icon(onPressed: onRetry, icon: const Icon(Icons.refresh, size: 18), label: const Text('Gửi lỗi · Gửi lại'))
              : Text('Đang gửi…', style: theme.textTheme.bodySmall),
        ],
      ),
    );
  }
}

/// Khung soạn tin dính đáy: tối đa 2.000 ký tự, công tắc "Hỏi cố vấn" ở kênh Cố vấn (chỉ sinh viên, chỉ tin gốc),
/// đính kèm file có sẵn ở C06 và @nhắc tên.
class MessageComposer extends StatefulWidget {
  const MessageComposer({super.key, required this.workspaceId, required this.onSend, this.people = const [], this.allowQuestion = false, this.hint = 'Nhập tin nhắn'});

  final String workspaceId;
  final void Function(MessageDraft draft) onSend;
  final List<PersonRef> people;
  final bool allowQuestion;
  final String hint;

  @override
  State<MessageComposer> createState() => _MessageComposerState();
}

class _MessageComposerState extends State<MessageComposer> {
  final _text = TextEditingController();
  bool _asQuestion = false;
  List<ProjectFile> _files = const [];
  final List<PersonRef> _mentions = [];

  @override
  void dispose() {
    _text.dispose();
    super.dispose();
  }

  // shortcut: chỉ gợi ý khi đang gõ @ ở cuối ô soạn tin; cần thì theo vị trí con trỏ.
  List<PersonRef> get _suggestions {
    final query = RegExp(r'(?:^|\s)@([^@\n]*)$').firstMatch(_text.text)?.group(1)?.toLowerCase();
    return query == null ? const [] : widget.people.where((p) => p.displayName.toLowerCase().contains(query)).toList();
  }

  void _mention(PersonRef person) {
    final text = _text.text.replaceFirst(RegExp(r'@[^@\n]*$'), '@${person.displayName} ');
    _text.value = TextEditingValue(text: text, selection: TextSelection.collapsed(offset: text.length));
    setState(() => _mentions.add(person));
  }

  Future<void> _attach() async {
    final picked = await showModalBottomSheet<List<ProjectFile>>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      showDragHandle: true,
      builder: (_) => _AttachSheet(workspaceId: widget.workspaceId, selected: _files),
    );
    if (picked != null && mounted) setState(() => _files = picked);
  }

  void _send() {
    final body = _text.text.trim();
    if (body.isEmpty) return;
    widget.onSend((
      body: body,
      asQuestion: _asQuestion,
      fileIds: [for (final f in _files) f.id],
      mentionIds: {for (final p in _mentions) if (body.contains('@${p.displayName}')) p.personId}.toList(),
    ));
    _text.clear();
    _mentions.clear();
    setState(() {
      _asQuestion = false;
      _files = const [];
    });
  }

  @override
  Widget build(BuildContext context) => SafeArea(
        top: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(AppSpacing.s3, AppSpacing.s2, AppSpacing.s3, AppSpacing.s2),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (_suggestions.isNotEmpty)
                ConstrainedBox(
                  constraints: const BoxConstraints(maxHeight: 168),
                  child: ListView(
                    shrinkWrap: true,
                    children: [
                      for (final p in _suggestions) ListTile(dense: true, title: Text(p.displayName), trailing: RoleChip(role: p.role), onTap: () => _mention(p)),
                    ],
                  ),
                ),
              if (_files.isNotEmpty)
                Align(
                  alignment: Alignment.centerLeft,
                  child: Wrap(spacing: AppSpacing.s2, children: [
                    for (final f in _files)
                      InputChip(
                        label: ConstrainedBox(constraints: const BoxConstraints(maxWidth: 200), child: Text(f.name, overflow: TextOverflow.ellipsis)),
                        deleteButtonTooltipMessage: 'Bỏ ${f.name}',
                        onDeleted: () => setState(() => _files = [..._files]..remove(f)),
                      ),
                  ]),
                ),
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
                  IconButton(tooltip: 'Đính kèm file', onPressed: _attach, icon: const Icon(Icons.attach_file)),
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

/// Chọn tối đa 5 file có sẵn của dự án (C06). File EXE101 không đính kèm được.
class _AttachSheet extends ConsumerStatefulWidget {
  const _AttachSheet({required this.workspaceId, required this.selected});

  final String workspaceId;
  final List<ProjectFile> selected;

  @override
  ConsumerState<_AttachSheet> createState() => _AttachSheetState();
}

class _AttachSheetState extends ConsumerState<_AttachSheet> {
  final List<String> _path = [];
  late final List<ProjectFile> _selected = [...widget.selected];

  void _toggle(ProjectFile file) => setState(() {
        final had = _selected.any((f) => f.id == file.id);
        had ? _selected.removeWhere((f) => f.id == file.id) : _selected.add(file);
      });

  @override
  Widget build(BuildContext context) {
    final folder = ref.watch(folderProvider((workspaceId: widget.workspaceId, folderId: _path.lastOrNull)));
    final theme = Theme.of(context);
    return SizedBox(
      height: MediaQuery.sizeOf(context).height * 0.7,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s4),
            child: Text('Chọn tối đa $maxAttachments file', style: theme.textTheme.titleLarge),
          ),
          if (_path.isNotEmpty)
            TextButton.icon(onPressed: () => setState(() => _path.removeLast()), icon: const Icon(Icons.arrow_back), label: const Text('Quay lại')),
          Expanded(
            child: folder.when(
              loading: () => const SkeletonList(count: 3),
              error: (e, _) => ErrorState(error: e, onRetry: () => ref.invalidate(folderProvider)),
              data: (content) {
                final folders = content.folders.where((f) => !f.isArchive).toList();
                final files = content.files.where((f) => !f.isArchive).toList();
                if (folders.isEmpty && files.isEmpty) {
                  return const EmptyState(icon: Icons.folder_open, title: 'Chưa có file.', description: 'Tải lên ở mục File trước.');
                }
                return ListView(
                  children: [
                    for (final f in folders)
                      ListTile(
                        leading: Icon(Icons.folder_outlined, color: theme.colorScheme.primary),
                        title: Text(f.name),
                        trailing: const Icon(Icons.chevron_right),
                        onTap: () => setState(() => _path.add(f.id)),
                      ),
                    for (final f in files)
                      Builder(builder: (_) {
                        final checked = _selected.any((s) => s.id == f.id);
                        return CheckboxListTile(
                          value: checked,
                          secondary: Icon(fileIcon(f.name)),
                          title: Text(f.name, maxLines: 2, overflow: TextOverflow.ellipsis),
                          subtitle: Text(formatSize(f.sizeBytes)),
                          onChanged: checked || _selected.length < maxAttachments ? (_) => _toggle(f) : null,
                        );
                      }),
                  ],
                );
              },
            ),
          ),
          Padding(
            padding: const EdgeInsets.all(AppSpacing.s4),
            child: SizedBox(
              width: double.infinity,
              child: FilledButton(onPressed: () => Navigator.pop(context, _selected), child: Text('Đính kèm (${_selected.length})')),
            ),
          ),
        ],
      ),
    );
  }
}

/// Luồng trả lời (bottom sheet): tin gốc, các trả lời, khung trả lời. Cố vấn trả lời câu hỏi → "Đã trả lời".
class ThreadSheet extends ConsumerStatefulWidget {
  const ThreadSheet({super.key, required this.root, required this.channel, required this.readOnly, this.people = const []});

  final Message root;
  final Channel channel;
  final bool readOnly;
  final List<PersonRef> people;

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

  Future<void> _reply(MessageDraft draft) async {
    setState(() => _sendError = null);
    try {
      final m = await ref.read(messagesApiProvider).send(
            widget.channel.id,
            clientMessageId: newClientId(),
            body: draft.body,
            parentMessageId: widget.root.id,
            attachmentFileIds: draft.fileIds,
            mentionPersonIds: draft.mentionIds,
          );
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
            if (!widget.readOnly)
              MessageComposer(
                workspaceId: widget.channel.workspaceId,
                people: mentionable(widget.channel, widget.people, mine),
                hint: 'Trả lời trong luồng',
                onSend: _reply,
              ),
          ],
        ),
      ),
    );
  }
}
