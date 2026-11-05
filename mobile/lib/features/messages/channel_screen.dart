import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../files/file_services.dart' show fileIcon;
import '../realtime/collab_hub.dart';
import '../workspace/format.dart';
import '../workspace/workspace_api.dart' show folderProvider, workspaceProvider;
import '../workspace/workspace_models.dart' show PersonRef, ProjectFile, WorkspaceRole;
import '../workspace/workspace_widgets.dart' show Avatar, RoleChip;
import '../../core/network/api_client.dart';
import '../../core/view_state.dart';
import 'channel_view_model.dart';
import 'messages_api.dart';

export 'channel_view_model.dart' show MessageDraft, newClientId;

const maxMessageLength = 2000;
const maxAttachments = 5;

/// Người được @nhắc: trừ mình; kênh Nhóm chỉ có sinh viên (quy tắc 22).
List<PersonRef> mentionable(Channel channel, List<PersonRef> people, Set<String> mine) => [
      for (final p in people)
        if (!mine.contains(p.personId) && (!channel.isTeam || p.role == WorkspaceRole.leader || p.role == WorkspaceRole.member)) p,
    ];

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
  ChannelArgs get _args => (channelId: widget.channel.id, readOnly: widget.readOnly);

  ChannelViewModel get _vm => ref.read(channelViewModelProvider(_args).notifier);

  /// Tin của mình: Trả lời / Sửa (trong 15 phút) / Xóa.
  Future<void> _actions(Message m) async {
    final canEdit = DateTime.now().difference(m.createdAt) < const Duration(minutes: 15);
    final choice = await showModalBottomSheet<String>(
      context: context,
      showDragHandle: true,
      builder: (context) => SafeArea(
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          ListTile(leading: const Icon(Icons.reply), title: const Text('Trả lời trong luồng'), onTap: () => Navigator.pop(context, 'thread')),
          if (canEdit) ListTile(leading: const Icon(Icons.edit_outlined), title: const Text('Sửa'), onTap: () => Navigator.pop(context, 'edit')),
          ListTile(leading: const Icon(Icons.delete_outline), title: const Text('Xóa'), onTap: () => Navigator.pop(context, 'delete')),
        ]),
      ),
    );
    if (!mounted) return;
    switch (choice) {
      case 'thread':
        await _openThread(m);
      case 'edit':
        await _edit(m);
      case 'delete':
        _delete(m);
    }
  }

  Future<void> _edit(Message m) async {
    final body = await showDialog<String>(context: context, builder: (_) => _EditMessageDialog(initial: m.body));
    if (body == null || body.isEmpty || body == m.body || !mounted) return;
    final messenger = ScaffoldMessenger.of(context);
    final error = await _vm.edit(m, body);
    if (error != null) messenger.showSnackBar(SnackBar(content: Text(error)));
  }

  void _delete(Message m) {
    _vm.hide(m.id);
    final messenger = ScaffoldMessenger.of(context);
    final vm = _vm;
    messenger
        .showSnackBar(SnackBar(
          content: const Text('Đã xóa tin'),
          duration: const Duration(seconds: 5),
          persist: false, // có nút Hoàn tác vẫn phải tự đóng sau 5 giây, rồi mới xóa thật
          action: SnackBarAction(label: 'Hoàn tác', onPressed: () {})))
        .closed
        .then((reason) async {
      if (reason == SnackBarClosedReason.action) return vm.unhide(m.id);
      final error = await vm.delete(m);
      if (error != null) messenger.showSnackBar(SnackBar(content: Text(error)));
    });
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
    final state = ref.watch(channelViewModelProvider(_args));
    final items = [for (final m in state.items) if (!state.hidden.contains(m.id)) m];
    return Column(
      children: [
        const RealtimeStatusBanner(),
        Expanded(
          child: switch (state.view) {
            ViewError(:final error) => ErrorState(error: error, onRetry: _vm.load),
            ViewLoading() => const SkeletonList(count: 3),
            ViewEmpty() => EmptyState(icon: Icons.forum_outlined, title: 'Chưa có tin nhắn.', description: 'Bắt đầu trò chuyện ở kênh ${widget.channel.label}.'),
            ViewData() => ListView(
                reverse: true,
                padding: const EdgeInsets.all(AppSpacing.s3),
                children: [
                  for (final p in state.pending.reversed) _PendingBubble(pending: p, onRetry: () => _vm.retry(p)),
                  for (var i = items.length - 1; i >= 0; i--) ...[
                    MessageBubble(
                      message: items[i],
                      mine: mine.contains(items[i].author.personId),
                      onThread: () => _openThread(items[i]),
                      onActions: widget.readOnly || !mine.contains(items[i].author.personId) ? null : () => _actions(items[i]),
                    ),
                    if (i == 0 || !_sameDay(items[i - 1].createdAt, items[i].createdAt)) _DayDivider(date: items[i].createdAt),
                  ],
                  if (state.hasMore)
                    Center(
                      child: state.loading
                          ? const Padding(padding: EdgeInsets.all(AppSpacing.s2), child: CircularProgressIndicator())
                          : TextButton(onPressed: () => _vm.load(older: true), child: const Text('Tải tin cũ hơn')),
                    ),
                ],
              ),
          },
        ),
        if (widget.readOnly)
          const Padding(padding: EdgeInsets.all(AppSpacing.s4), child: Text('Bộ môn chỉ xem kênh này.'))
        else
          MessageComposer(
            workspaceId: widget.channel.workspaceId,
            people: mentionable(widget.channel, widget.people, mine),
            allowQuestion: !widget.channel.isTeam && widget.isStudent,
            onSend: _vm.send,
          ),
      ],
    );
  }
}

/// Hộp sửa tin; controller sống theo hộp thoại để không bị dùng sau khi dispose lúc hộp đang đóng.
class _EditMessageDialog extends StatefulWidget {
  const _EditMessageDialog({required this.initial});

  final String initial;

  @override
  State<_EditMessageDialog> createState() => _EditMessageDialogState();
}

class _EditMessageDialogState extends State<_EditMessageDialog> {
  late final _text = TextEditingController(text: widget.initial);

  @override
  void dispose() {
    _text.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
        title: const Text('Sửa tin nhắn'),
        content: TextField(controller: _text, autofocus: true, minLines: 1, maxLines: 5, maxLength: maxMessageLength),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Hủy')),
          FilledButton(onPressed: () => Navigator.pop(context, _text.text.trim()), child: const Text('Lưu')),
        ],
      );
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
/// tên và nhãn vai trò. Câu hỏi cho cố vấn có chip Chờ / Đã trả lời. Nhấn giữ → Trả lời trong luồng; tin của mình có
/// [onActions] (nhấn giữ hoặc nút "Tùy chọn tin") → Trả lời / Sửa / Xóa.
// shortcut: sửa / xóa chỉ ở tin gốc trong kênh, chưa có cho trả lời trong luồng; thêm vào ThreadSheet khi cần.
class MessageBubble extends StatelessWidget {
  const MessageBubble({super.key, required this.message, required this.mine, this.onThread, this.onActions});

  final Message message;
  final bool mine;
  final VoidCallback? onThread;
  final VoidCallback? onActions;

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
                GestureDetector(onLongPress: m.isDeleted ? null : onActions ?? onThread, child: bubble),
                Wrap(spacing: AppSpacing.s2, crossAxisAlignment: WrapCrossAlignment.center, children: [
                  if (m.isAdvisorQuestion)
                    Chip(
                      avatar: Icon(m.questionStatus == 'ANSWERED' ? Icons.check_circle_outline : Icons.schedule, size: 18),
                      label: Text(m.questionStatus == 'ANSWERED' ? 'Đã trả lời' : 'Chờ trả lời'),
                    ),
                  if (onActions != null && !m.isDeleted)
                    IconButton(tooltip: 'Tùy chọn tin', onPressed: onActions, icon: const Icon(Icons.more_horiz)),
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
              await ref.read(openAttachmentProvider)(file.id);
            } catch (e) {
              messenger.showSnackBar(SnackBar(content: Text(e.apiError.title)));
            }
          },
        ),
      );
}

class _PendingBubble extends StatelessWidget {
  const _PendingBubble({required this.pending, required this.onRetry});

  final PendingMessage pending;
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
  ThreadArgs get _args => (rootId: widget.root.id, channelId: widget.channel.id);

  @override
  Widget build(BuildContext context) {
    final mine = ref.watch(myPersonIdsProvider);
    final vm = ref.read(threadViewModelProvider(_args).notifier);
    final state = ref.watch(threadViewModelProvider(_args));
    final root = state.root ?? widget.root;
    final replies = state.replies;
    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(context).bottom),
      child: ConstrainedBox(
        constraints: BoxConstraints(maxHeight: MediaQuery.sizeOf(context).height * 0.85),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text('Luồng trả lời', style: Theme.of(context).textTheme.titleMedium),
            Flexible(
              child: state.error != null
                  ? ErrorState(error: state.error!, onRetry: vm.load)
                  : ListView(
                      shrinkWrap: true,
                      padding: const EdgeInsets.all(AppSpacing.s3),
                      children: [
                        MessageBubble(message: root, mine: mine.contains(root.author.personId)),
                        const Divider(),
                        Text('${replies.length} trả lời', style: Theme.of(context).textTheme.bodySmall),
                        for (final r in replies) MessageBubble(message: r, mine: mine.contains(r.author.personId)),
                      ],
                    ),
            ),
            if (state.sendError != null) ErrorBanner(message: state.sendError!),
            if (!widget.readOnly)
              MessageComposer(
                workspaceId: widget.channel.workspaceId,
                people: mentionable(widget.channel, widget.people, mine),
                hint: 'Trả lời trong luồng',
                onSend: vm.reply,
              ),
          ],
        ),
      ),
    );
  }
}
