import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../workspace/format.dart';
import '../workspace/workspace_widgets.dart';
import 'meetings_api.dart';
import 'meetings_screen.dart';
import 'meetings_view_model.dart';

/// C08 – Chi tiết cuộc họp (MB-42, quy tắc 25): trả lời Có / Không từng khung; người tổ chức chốt khung, thêm khung, hủy có lý do;
/// ghi biên bản và chuyển dòng thành việc. Trạng thái: chờ trả lời · đã chốt · đã hủy · đã họp (có / chưa có biên bản) · lỗi lưu.
class MeetingDetailScreen extends ConsumerStatefulWidget {
  const MeetingDetailScreen({super.key, required this.meetingId, this.pickTime = pickSlotTime});

  final String meetingId;
  final Future<DateTime?> Function(BuildContext) pickTime;

  @override
  ConsumerState<MeetingDetailScreen> createState() => _MeetingDetailScreenState();
}

class _MeetingDetailScreenState extends ConsumerState<MeetingDetailScreen> {
  MeetingDetailViewModel get _vm => ref.read(meetingDetailViewModelProvider(widget.meetingId).notifier);

  /// Hiện câu SnackBar của thao tác thành công; trả true khi thành công.
  Future<bool> _show(Future<MeetingActionOutcome> pending) async {
    final outcome = await pending;
    if (outcome case MeetingActionDone(:final message?) when mounted) {
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(message)));
    }
    return outcome.ok;
  }

  Future<void> _cancel() async {
    final reason = await showDialog<String>(context: context, builder: (_) => const _CancelDialog());
    if (reason != null) await _show(_vm.cancel(reason));
  }

  Future<void> _addSlot() async {
    final at = await widget.pickTime(context);
    if (at == null) return;
    await _show(_vm.addSlot(at));
  }

  @override
  Widget build(BuildContext context) {
    final meeting = ref.watch(meetingDetailProvider(widget.meetingId));
    return Scaffold(
      appBar: AppBar(title: Text(meeting.value?.card.title ?? 'Cuộc họp')),
      body: meeting.when(
        skipLoadingOnRefresh: true,
        loading: () => const SkeletonList(count: 2),
        error: (e, _) => ErrorState(error: e, onRetry: _vm.refresh),
        data: (m) => _details(context, m),
      ),
    );
  }

  Widget _details(BuildContext context, MeetingDetail m) {
    final theme = Theme.of(context);
    final c = m.card;
    final organizer = isOrganizer(ref, c.organizer);
    final action = ref.watch(meetingDetailViewModelProvider(widget.meetingId));
    final busy = action.busy;
    final open = c.status == MeetingStatus.proposed || c.status == MeetingStatus.scheduled;
    final (label, tone, icon) = switch (c.status) {
      MeetingStatus.proposed => ('Chờ chốt', StatusTone.warning, Icons.how_to_vote_outlined),
      MeetingStatus.scheduled => ('Đã chốt', StatusTone.success, Icons.event_available_outlined),
      MeetingStatus.cancelled => ('Đã hủy', StatusTone.neutral, Icons.event_busy_outlined),
      MeetingStatus.done => ('Đã họp', StatusTone.info, Icons.task_alt),
    };
    return ListView(
      padding: const EdgeInsets.all(AppSpacing.s4),
      children: [
        Wrap(
          spacing: AppSpacing.s2,
          children: [
            StatusChip(label: label, tone: tone, icon: icon),
            if (c.isMentorSession) const StatusChip(label: 'Buổi mentor', tone: StatusTone.info, icon: Icons.support_agent),
            if (c.isWeekly) const StatusChip(label: 'Hằng tuần', tone: StatusTone.info, icon: Icons.repeat),
          ],
        ),
        const SizedBox(height: AppSpacing.s3),
        Text('Tổ chức: ${c.organizer.displayName}', style: theme.textTheme.bodyMedium),
        if (c.startsAt != null) Text('Thời gian: ${formatDateTime(c.startsAt!)}', style: theme.textTheme.bodyMedium),
        Text('Nơi họp: ${c.location?.isNotEmpty == true ? c.location : 'Chưa có'}', style: theme.textTheme.bodyMedium),
        if (m.agenda?.isNotEmpty == true) ...[const SizedBox(height: AppSpacing.s2), Text(m.agenda!, style: theme.textTheme.bodyMedium)],
        const SizedBox(height: AppSpacing.s3),
        if (action.error != null) ...[ErrorBanner(message: action.error!), const SizedBox(height: AppSpacing.s3)],
        if (c.status == MeetingStatus.cancelled) ErrorBanner(message: 'Cuộc họp đã hủy. ${m.cancelReason ?? ''}'),
        if (c.status == MeetingStatus.proposed) ...[
          Text('Khung giờ – chọn Có / Không', style: theme.textTheme.titleMedium),
          const SizedBox(height: AppSpacing.s2),
          for (final s in m.slots) _SlotCard(slot: s, suggested: s.id == m.suggestedSlotId, organizer: organizer, busy: busy, onAnswer: (a) => _show(_vm.answer(s.id, a)), onConfirm: () => _show(_vm.confirm(s.id))),
          if (organizer && m.slots.length < maxSlots) TextButton.icon(onPressed: busy ? null : _addSlot, icon: const Icon(Icons.add), label: const Text('Thêm khung giờ')),
        ],
        if ((c.status == MeetingStatus.scheduled || c.status == MeetingStatus.done) && organizer)
          _MinutesEditor(key: ValueKey(m.minutes), initial: m.minutes ?? '', busy: busy, onSave: (text) => _show(_vm.saveMinutes(text))),
        if (c.status == MeetingStatus.done && !organizer) ...[
          Text('Biên bản', style: theme.textTheme.titleMedium),
          const SizedBox(height: AppSpacing.s2),
          Text(m.minutes ?? 'Chưa có biên bản.', style: theme.textTheme.bodyMedium),
        ],
        if (c.status == MeetingStatus.done) ...[
          const SizedBox(height: AppSpacing.s4),
          _TasksFromMinutes(
            key: ValueKey(m.createdTasks.length),
            created: m.createdTasks,
            lines: organizer && m.minutes != null ? minutesLines(m.minutes!).take(20).toList() : const [],
            busy: busy,
            onCreate: (titles) => _show(_vm.createTasks(titles)),
          ),
        ],
        if (organizer && open) ...[
          const SizedBox(height: AppSpacing.s4),
          TextButton.icon(
            onPressed: busy ? null : _cancel,
            icon: Icon(Icons.event_busy_outlined, color: theme.colorScheme.error),
            label: Text('Hủy cuộc họp', style: TextStyle(color: theme.colorScheme.error)),
          ),
        ],
      ],
    );
  }
}

class _SlotCard extends StatelessWidget {
  const _SlotCard({required this.slot, required this.suggested, required this.organizer, required this.busy, required this.onAnswer, required this.onConfirm});

  final MeetingSlot slot;
  final bool suggested;
  final bool organizer;
  final bool busy;
  final ValueChanged<String> onAnswer;
  final VoidCallback onConfirm;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final s = slot;
    return Card(
      margin: const EdgeInsets.only(bottom: AppSpacing.s2),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.s3),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('${formatDateTime(s.startsAt)} · ${s.durationMinutes} phút', style: theme.textTheme.titleSmall),
            Text('${s.yesCount}/${s.inviteeCount} người có thể', style: theme.textTheme.bodySmall),
            if (s.yesPeople.isNotEmpty) Text('Có: ${s.yesPeople.join(', ')}', style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
            if (suggested) ...[
              const SizedBox(height: AppSpacing.s1),
              const StatusChip(label: 'Nhiều người chọn nhất', tone: StatusTone.success, icon: Icons.star_outline),
            ],
            const SizedBox(height: AppSpacing.s2),
            Wrap(
              spacing: AppSpacing.s2,
              runSpacing: AppSpacing.s2,
              children: [
                for (final (value, text) in [('YES', 'Có'), ('NO', 'Không')])
                  ChoiceChip(label: Text(text), selected: s.myAnswer == value, onSelected: busy ? null : (_) => onAnswer(value)),
                if (organizer) OutlinedButton.icon(onPressed: busy ? null : onConfirm, icon: const Icon(Icons.event_available_outlined), label: const Text('Chốt khung này')),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _MinutesEditor extends StatefulWidget {
  const _MinutesEditor({super.key, required this.initial, required this.busy, required this.onSave});

  final String initial;
  final bool busy;
  final Future<bool> Function(String) onSave;

  @override
  State<_MinutesEditor> createState() => _MinutesEditorState();
}

class _MinutesEditorState extends State<_MinutesEditor> {
  late final _text = TextEditingController(text: widget.initial);

  @override
  void dispose() {
    _text.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text('Biên bản', style: Theme.of(context).textTheme.titleMedium),
        const SizedBox(height: AppSpacing.s2),
        TextField(
          controller: _text,
          maxLines: 6,
          maxLength: 8000,
          decoration: const InputDecoration(hintText: 'Mỗi dòng một ý, ví dụ:\n- Hoàn thành landing page\n- Phỏng vấn 5 khách hàng'),
          onChanged: (_) => setState(() {}),
        ),
        Align(
          alignment: Alignment.centerRight,
          child: LoadingButton(
            label: 'Lưu biên bản',
            expand: false,
            loading: widget.busy,
            onPressed: _text.text.trim().isEmpty || _text.text.trim() == widget.initial ? null : () => widget.onSave(_text.text.trim()),
          ),
        ),
      ],
    );
  }
}

class _TasksFromMinutes extends StatefulWidget {
  const _TasksFromMinutes({super.key, required this.created, required this.lines, required this.busy, required this.onCreate});

  final List<String> created;
  final List<String> lines;
  final bool busy;
  final Future<bool> Function(List<String>) onCreate;

  @override
  State<_TasksFromMinutes> createState() => _TasksFromMinutesState();
}

class _TasksFromMinutesState extends State<_TasksFromMinutes> {
  final _picked = <int>{};

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Việc từ biên bản', style: theme.textTheme.titleMedium),
        for (final t in widget.created) ListTile(contentPadding: EdgeInsets.zero, dense: true, leading: const Icon(Icons.task_alt), title: Text(t)),
        if (widget.lines.isNotEmpty) ...[
          Text('Chọn các dòng biên bản để tạo việc trong một bước.', style: theme.textTheme.bodySmall),
          for (final (i, line) in widget.lines.indexed)
            CheckboxListTile(
              contentPadding: EdgeInsets.zero,
              controlAffinity: ListTileControlAffinity.leading,
              title: Text(line),
              value: _picked.contains(i),
              onChanged: (v) => setState(() => v == true ? _picked.add(i) : _picked.remove(i)),
            ),
          Align(
            alignment: Alignment.centerRight,
            child: LoadingButton(
              label: _picked.isEmpty ? 'Tạo việc' : 'Tạo ${_picked.length} việc',
              expand: false,
              loading: widget.busy,
              onPressed: _picked.isEmpty ? null : () => widget.onCreate([for (final i in _picked.toList()..sort()) widget.lines[i]]),
            ),
          ),
        ] else if (widget.created.isEmpty)
          Text('Chưa có việc nào.', style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
      ],
    );
  }
}

class _CancelDialog extends StatefulWidget {
  const _CancelDialog();

  @override
  State<_CancelDialog> createState() => _CancelDialogState();
}

class _CancelDialogState extends State<_CancelDialog> {
  final _reason = TextEditingController();

  @override
  void dispose() {
    _reason.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Hủy cuộc họp?'),
      content: TextField(
        controller: _reason,
        maxLength: 500,
        maxLines: 3,
        decoration: const InputDecoration(labelText: 'Lý do (mọi người sẽ thấy)'),
        onChanged: (_) => setState(() {}),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: const Text('Không')),
        FilledButton(
          style: FilledButton.styleFrom(backgroundColor: Theme.of(context).colorScheme.error),
          onPressed: _reason.text.trim().isEmpty ? null : () => Navigator.pop(context, _reason.text.trim()),
          child: const Text('Hủy họp'),
        ),
      ],
    );
  }
}
