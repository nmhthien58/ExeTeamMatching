import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../workspace/format.dart';
import '../workspace/workspace_api.dart';
import '../workspace/workspace_widgets.dart';
import 'meetings_api.dart';
import 'meetings_view_model.dart';

/// C07 – Lịch họp (MB-42, quy tắc 25): Chờ chốt · Sắp tới · Đã họp, số buổi mentor; trưởng nhóm, giảng viên, mentor đề xuất họp.
/// Trạng thái: có dữ liệu · trống · đang tải · lỗi.
class MeetingsScreen extends ConsumerWidget {
  const MeetingsScreen({super.key, required this.basePath, this.workspaceId});

  final String basePath;

  /// Null với sinh viên (workspace của mình).
  final String? workspaceId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return ref.watch(currentWorkspaceProvider(workspaceId)).when(
          loading: () => Scaffold(appBar: AppBar(title: const Text('Lịch họp')), body: const SkeletonList(count: 3)),
          error: (e, _) => Scaffold(
            appBar: AppBar(title: const Text('Lịch họp')),
            body: ErrorState(error: e, onRetry: () => ref.invalidate(currentWorkspaceProvider(workspaceId))),
          ),
          data: (workspace) => workspace == null
              ? Scaffold(
                  appBar: AppBar(title: const Text('Lịch họp')),
                  body: const EmptyState(icon: Icons.group_off_outlined, title: 'Bạn chưa có nhóm', description: 'Lịch họp mở khi bạn đã có nhóm.'),
                )
              : _MeetingsView(workspaceId: workspace.id, basePath: basePath, canPropose: canPropose(workspace.myRole)),
        );
  }
}

class _MeetingsView extends ConsumerWidget {
  const _MeetingsView({required this.workspaceId, required this.basePath, required this.canPropose});

  final String workspaceId;
  final String basePath;
  final bool canPropose;

  Future<void> _propose(BuildContext context) async {
    final id = await showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      builder: (_) => ProposeMeetingSheet(workspaceId: workspaceId),
    );
    if (id != null && context.mounted) unawaited(context.push('$basePath/lich-hop/$id'));
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final list = ref.watch(meetingListProvider(workspaceId));
    return Scaffold(
      appBar: AppBar(title: const Text('Lịch họp')),
      floatingActionButton: canPropose
          ? FloatingActionButton.extended(onPressed: () => _propose(context), icon: const Icon(Icons.add), label: const Text('Đề xuất họp'))
          : null,
      body: list.when(
        loading: () => const SkeletonList(count: 3),
        error: (e, _) => ErrorState(error: e, onRetry: () => ref.invalidate(meetingListProvider(workspaceId))),
        data: (data) {
          final groups = [
            ('Chờ chốt', data.items.where((m) => m.status == MeetingStatus.proposed).toList()),
            ('Sắp tới', data.items.where((m) => m.status == MeetingStatus.scheduled).toList()),
            ('Đã họp', data.items.where((m) => m.status == MeetingStatus.done || m.status == MeetingStatus.cancelled).toList()),
          ];
          return RefreshIndicator(
            onRefresh: () async {
              ref.invalidate(meetingListProvider(workspaceId));
              await ref.read(meetingListProvider(workspaceId).future);
            },
            child: ListView(
              padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s4, AppSpacing.s4, 96),
              children: [
                if (data.expectedMentorSessions > 0) ...[
                  Text('Buổi mentor: ${data.mentorSessionsDone}/${data.expectedMentorSessions}', style: theme.textTheme.titleSmall),
                  const SizedBox(height: AppSpacing.s3),
                ],
                if (data.items.isEmpty)
                  const EmptyState(
                    icon: Icons.event_outlined,
                    title: 'Chưa có cuộc họp',
                    description: 'Trưởng nhóm, giảng viên hoặc mentor đề xuất 1–3 khung giờ để cả nhóm chọn.',
                  ),
                for (final (title, items) in groups)
                  if (items.isNotEmpty) ...[
                    Text('$title (${items.length})', style: theme.textTheme.titleMedium),
                    const SizedBox(height: AppSpacing.s2),
                    for (final m in items) _MeetingTile(meeting: m, onTap: () => context.push('$basePath/lich-hop/${m.id}')),
                    const SizedBox(height: AppSpacing.s4),
                  ],
              ],
            ),
          );
        },
      ),
    );
  }
}

class _MeetingTile extends StatelessWidget {
  const _MeetingTile({required this.meeting, required this.onTap});

  final MeetingCard meeting;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final m = meeting;
    final when = m.startsAt != null ? formatDateTime(m.startsAt!) : '${m.slotCount} khung giờ đề xuất';
    return Card(
      margin: const EdgeInsets.only(bottom: AppSpacing.s2),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppRadius.md),
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.s3),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(m.title, style: Theme.of(context).textTheme.titleSmall),
              const SizedBox(height: AppSpacing.s1),
              Text('$when · ${m.organizer.displayName}', style: Theme.of(context).textTheme.bodySmall),
              if (m.isWeekly || m.isMentorSession || (m.status == MeetingStatus.proposed && m.myAnswerPending) || m.status == MeetingStatus.cancelled) ...[
                const SizedBox(height: AppSpacing.s2),
                Wrap(
                  spacing: AppSpacing.s2,
                  runSpacing: AppSpacing.s1,
                  children: [
                    if (m.isWeekly) StatusChip(label: m.weekNo == null ? 'Hằng tuần' : 'Hằng tuần · tuần ${m.weekNo}', tone: StatusTone.info, icon: Icons.repeat),
                    if (m.isMentorSession) const StatusChip(label: 'Buổi mentor', tone: StatusTone.info, icon: Icons.support_agent),
                    if (m.status == MeetingStatus.proposed && m.myAnswerPending) const StatusChip(label: 'Chưa trả lời', tone: StatusTone.warning, icon: Icons.how_to_vote_outlined),
                    if (m.status == MeetingStatus.cancelled) const StatusChip(label: 'Đã hủy', tone: StatusTone.neutral, icon: Icons.event_busy_outlined),
                  ],
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

/// Chọn ngày rồi giờ cho một khung; null nếu người dùng hủy.
Future<DateTime?> pickSlotTime(BuildContext context) async {
  final now = DateTime.now();
  final date = await showDatePicker(context: context, firstDate: now, lastDate: now.add(const Duration(days: 180)), initialDate: now);
  if (date == null || !context.mounted) return null;
  final time = await showTimePicker(context: context, initialTime: const TimeOfDay(hour: 9, minute: 0));
  if (time == null) return null;
  return DateTime(date.year, date.month, date.day, time.hour, time.minute);
}

/// C08 đang tạo: tiêu đề, nơi họp, nội dung, 1–3 khung giờ (60 phút). Một khung = họp đã chốt luôn. Trả id cuộc họp mới.
class ProposeMeetingSheet extends ConsumerStatefulWidget {
  const ProposeMeetingSheet({super.key, required this.workspaceId, this.pickTime = pickSlotTime});

  final String workspaceId;

  /// Thay được trong test.
  final Future<DateTime?> Function(BuildContext) pickTime;

  @override
  ConsumerState<ProposeMeetingSheet> createState() => _ProposeMeetingSheetState();
}

class _ProposeMeetingSheetState extends ConsumerState<ProposeMeetingSheet> {
  final _title = TextEditingController();
  final _location = TextEditingController();
  final _agenda = TextEditingController();

  ProposeMeetingViewModel get _vm => ref.read(proposeMeetingViewModelProvider(widget.workspaceId).notifier);

  @override
  void dispose() {
    _title.dispose();
    _location.dispose();
    _agenda.dispose();
    super.dispose();
  }

  Future<void> _addSlot() async {
    final at = await widget.pickTime(context);
    if (at != null) _vm.addSlot(at);
  }

  Future<void> _submit() async {
    final id = await _vm.submit(title: _title.text, location: _location.text, agenda: _agenda.text);
    if (id != null && mounted) Navigator.pop(context, id);
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final state = ref.watch(proposeMeetingViewModelProvider(widget.workspaceId));
    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(context).bottom),
      child: SafeArea(
        child: ListView(
          shrinkWrap: true,
          padding: const EdgeInsets.all(AppSpacing.s4),
          children: [
            Text('Đề xuất họp', style: theme.textTheme.titleLarge),
            const SizedBox(height: AppSpacing.s3),
            if (state.error != null) ...[ErrorBanner(message: state.error!), const SizedBox(height: AppSpacing.s3)],
            TextField(controller: _title, maxLength: 200, decoration: InputDecoration(labelText: 'Tiêu đề', errorText: state.titleError)),
            TextField(controller: _location, maxLength: 500, decoration: const InputDecoration(labelText: 'Nơi họp – phòng hoặc link (không bắt buộc)')),
            TextField(controller: _agenda, maxLength: 2000, maxLines: 3, decoration: const InputDecoration(labelText: 'Nội dung (không bắt buộc)')),
            Text('Khung giờ (60 phút)', style: theme.textTheme.titleSmall),
            for (final (i, s) in state.slots.indexed)
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: const Icon(Icons.schedule),
                title: Text(formatDateTime(s)),
                trailing: IconButton(tooltip: 'Bỏ khung ${i + 1}', icon: const Icon(Icons.close), onPressed: () => _vm.removeSlot(i)),
              ),
            if (state.canAddSlot) TextButton.icon(onPressed: _addSlot, icon: const Icon(Icons.add), label: const Text('Thêm khung giờ')),
            if (state.slotError != null) Text(state.slotError!, style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.error)),
            Text(
              'Một khung giờ thì cuộc họp được chốt luôn; nhiều khung thì mọi người chọn Có / Không rồi người tổ chức chốt.',
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            const SizedBox(height: AppSpacing.s4),
            LoadingButton(label: state.submitLabel, loading: state.saving, onPressed: _submit),
          ],
        ),
      ),
    );
  }
}
