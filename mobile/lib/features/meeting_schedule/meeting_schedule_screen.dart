import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../my_team/my_team_api.dart' show myTeamDetailProvider;
import '../realtime/collab_hub.dart';
import '../workspace/format.dart';
import 'meeting_schedule_api.dart';
import 'meeting_schedule_view_model.dart';

/// WeeklySlotGrid (flutter-handoff C09): slot theo ngày, mỗi ô là nút có nhãn đọc được "Thứ Ba 09:15, còn trống";
/// trạng thái có chữ + icon, không chỉ màu. `canPick` quyết định ô nào bấm được.
class WeeklySlotGrid extends StatelessWidget {
  const WeeklySlotGrid({super.key, required this.slots, required this.canPick, required this.onPick, this.errorSlotId});

  final List<ScheduleSlot> slots;
  final bool Function(ScheduleSlot slot) canPick;
  final void Function(ScheduleSlot slot) onPick;
  final String? errorSlotId;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final days = {for (final s in slots) s.dayOfWeek}.toList()..sort();
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (final day in days) ...[
          Padding(
            padding: const EdgeInsets.symmetric(vertical: AppSpacing.s2),
            child: Text(dayNames[day], style: theme.textTheme.titleSmall),
          ),
          Wrap(
            spacing: AppSpacing.s2,
            runSpacing: AppSpacing.s2,
            children: [
              for (final s in (slots.where((s) => s.dayOfWeek == day).toList()..sort((a, b) => a.startTime.compareTo(b.startTime))))
                Builder(builder: (_) {
                  final (text, icon) = switch (s.state) {
                    'MINE' => ('Nhóm bạn', Icons.check_circle),
                    'TAKEN' => (s.teamName ?? 'Đã có nhóm', Icons.event_busy_outlined),
                    _ => ('Còn trống', Icons.event_available_outlined),
                  };
                  final enabled = canPick(s);
                  return Semantics(
                    button: true,
                    enabled: enabled,
                    label: '${dayNames[day]} ${s.startTime}, ${s.teamName ?? text.toLowerCase()}',
                    excludeSemantics: true,
                    child: SizedBox(
                      width: 148,
                      child: OutlinedButton(
                        onPressed: enabled ? () => onPick(s) : null,
                        style: OutlinedButton.styleFrom(
                          minimumSize: const Size(0, 56),
                          alignment: Alignment.centerLeft,
                          backgroundColor: s.state == 'MINE' ? context.appColors.infoContainer : null,
                          side: BorderSide(color: errorSlotId == s.id ? colors.error : s.state == 'MINE' ? colors.primary : colors.outlineVariant),
                        ),
                        child: Row(
                          children: [
                            Icon(icon, size: 20),
                            const SizedBox(width: AppSpacing.s2),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(s.startTime, style: theme.textTheme.labelLarge),
                                  Text(text, style: theme.textTheme.bodySmall, maxLines: 1, overflow: TextOverflow.ellipsis),
                                ],
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  );
                }),
            ],
          ),
        ],
      ],
    );
  }
}

/// C09 – Lịch họp hằng tuần của nhóm (MB-46, quy tắc 25): trưởng nhóm chọn / đổi một slot cố định tới hạn, ai chọn trước được trước;
/// slot vừa bị nhóm khác chọn thì báo tại ô và lưới tự tải lại. Thành viên chỉ xem slot của nhóm.
class TeamScheduleScreen extends ConsumerWidget {
  const TeamScheduleScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final team = ref.watch(myTeamDetailProvider);
    final workspaceId = team.value == null ? null : ref.watch(teamWorkspaceIdProvider(team.value!.id));
    final error = team.error ?? workspaceId?.error;
    return Scaffold(
      appBar: AppBar(title: const Text('Lịch họp hằng tuần')),
      body: error != null
          ? ErrorState(error: error, onRetry: () => ref.invalidate(myTeamDetailProvider))
          : team.isLoading || (workspaceId?.isLoading ?? false)
              ? const SkeletonList(count: 3)
              : workspaceId?.value == null
                  ? const EmptyState(icon: Icons.event_busy_outlined, title: 'Nhóm chưa có dự án', description: 'Lịch họp hằng tuần có sau khi nhóm được chốt và có dự án EXE201.')
                  : _TeamSchedule(workspaceId: workspaceId!.value!, isLeader: team.value!.isLeader),
    );
  }
}

class _TeamSchedule extends ConsumerStatefulWidget {
  const _TeamSchedule({required this.workspaceId, required this.isLeader});

  final String workspaceId;
  final bool isLeader;

  @override
  ConsumerState<_TeamSchedule> createState() => _TeamScheduleState();
}

class _TeamScheduleState extends ConsumerState<_TeamSchedule> {
  TeamScheduleViewModel get _vm => ref.read(teamScheduleViewModelProvider(widget.workspaceId).notifier);

  Future<void> _pick(ScheduleSlot slot, bool change) async {
    final ok = await showModalBottomSheet<bool>(
      context: context,
      showDragHandle: true,
      builder: (context) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(AppSpacing.s4, 0, AppSpacing.s4, AppSpacing.s4),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text('Chọn ${slot.label}?', style: Theme.of(context).textTheme.titleLarge),
              const SizedBox(height: AppSpacing.s2),
              Text('Nhóm họp cố định ${slot.label}–${slot.endTime} mỗi tuần. Bạn đổi được tới hạn chọn slot.'),
              const SizedBox(height: AppSpacing.s4),
              FilledButton(onPressed: () => Navigator.pop(context, true), child: Text(change ? 'Đổi slot' : 'Chọn slot')),
            ],
          ),
        ),
      ),
    );
    if (ok != true || !mounted) return;
    final messenger = ScaffoldMessenger.of(context);
    if (await _vm.book(slot)) messenger.showSnackBar(SnackBar(content: Text('Đã chọn ${slot.label}')));
  }

  @override
  Widget build(BuildContext context) {
    final schedule = ref.watch(teamScheduleProvider(widget.workspaceId));
    final action = ref.watch(teamScheduleViewModelProvider(widget.workspaceId));
    final theme = Theme.of(context);
    return Column(
      children: [
        const RealtimeStatusBanner(),
        Expanded(
          child: schedule.when(
            loading: () => const SkeletonList(count: 3),
            error: (e, _) => ErrorState(error: e, onRetry: _vm.refresh),
            data: (data) => data == null
                ? const EmptyState(icon: Icons.event_note_outlined, title: 'Giảng viên chưa đăng lịch', description: 'Bạn sẽ nhận thông báo khi giảng viên đăng lịch họp hằng tuần.')
                : RefreshIndicator(
                    onRefresh: () => ref.refresh(teamScheduleProvider(widget.workspaceId).future),
                    child: ListView(
                      padding: const EdgeInsets.all(AppSpacing.s4),
                      children: [
                        Text(data.mySlot == null ? 'Nhóm chưa chọn slot' : 'Họp hằng tuần · ${data.mySlot!.label}', style: theme.textTheme.titleMedium),
                        Text('Giảng viên: ${data.lecturerName}', style: theme.textTheme.bodyMedium),
                        if (data.location.isNotEmpty)
                          Align(
                            alignment: Alignment.centerLeft,
                            child: TextButton.icon(
                              onPressed: () => _vm.openLink(data.location),
                              icon: const Icon(Icons.videocam_outlined),
                              label: const Text('Link họp'),
                            ),
                          ),
                        Text(
                          data.canBook
                              ? 'Chọn trước ${formatDateTime(data.bookingDeadline)}'
                              : data.bookingDeadline.isBefore(DateTime.now())
                                  ? 'Đã hết hạn chọn slot; nhóm chưa chọn được hệ thống xếp vào slot trống.'
                                  : 'Trưởng nhóm chọn slot cho nhóm.',
                          style: theme.textTheme.bodyMedium,
                        ),
                        if (action.error != null) ...[const SizedBox(height: AppSpacing.s3), ErrorBanner(message: action.error!)],
                        if (widget.isLeader) ...[
                          const SizedBox(height: AppSpacing.s3),
                          WeeklySlotGrid(
                            slots: data.slots,
                            canPick: (s) => data.canBook && s.state == 'FREE',
                            onPick: (s) => _pick(s, data.mySlot != null),
                            errorSlotId: action.failedSlot,
                          ),
                        ],
                      ],
                    ),
                  ),
          ),
        ),
      ],
    );
  }
}

/// C09 – Lịch họp hằng tuần của giảng viên (MB-46): khung giờ, độ dài slot, link họp, xem trước số slot; lưu thì cắt slot, "Đăng lịch"
/// cho các nhóm EXE201 (thiếu slot vẫn đăng, có cảnh báo). Chạm ô có nhóm → bottom sheet đổi slot cho nhóm đó.
class LecturerScheduleScreen extends ConsumerWidget {
  const LecturerScheduleScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    ref.watch(lecturerScheduleSyncProvider);
    final schedule = ref.watch(myScheduleProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Lịch họp hằng tuần')),
      body: Column(
        children: [
          const RealtimeStatusBanner(),
          Expanded(
            child: schedule.when(
              loading: () => const SkeletonList(count: 3),
              error: (e, _) => ErrorState(error: e, onRetry: () => ref.invalidate(myScheduleProvider)),
              data: (data) => _LecturerSchedule(key: ValueKey(data?.id), schedule: data),
            ),
          ),
        ],
      ),
    );
  }
}

class _LecturerSchedule extends ConsumerStatefulWidget {
  const _LecturerSchedule({super.key, required this.schedule});

  final MeetingSchedule? schedule;

  @override
  ConsumerState<_LecturerSchedule> createState() => _LecturerScheduleState();
}

class _LecturerScheduleState extends ConsumerState<_LecturerSchedule> {
  late final _minutes = TextEditingController(text: '${widget.schedule?.slotMinutes ?? 15}');
  late final _location = TextEditingController(text: widget.schedule?.location ?? '');

  LecturerScheduleViewModel get _vm => ref.read(lecturerScheduleViewModelProvider(widget.schedule?.id).notifier);

  @override
  void dispose() {
    _minutes.dispose();
    _location.dispose();
    super.dispose();
  }

  Future<void> _run(Future<String?> pending) async {
    final messenger = ScaffoldMessenger.of(context);
    final done = await pending;
    if (done != null) messenger.showSnackBar(SnackBar(content: Text(done)));
  }

  Future<void> _pickTime(ScheduleWindow window, int i, bool start) async {
    final current = start ? window.startTime : window.endTime;
    final parts = current.split(':');
    final picked = await showTimePicker(context: context, initialTime: TimeOfDay(hour: int.parse(parts[0]), minute: int.parse(parts[1])));
    if (picked == null) return;
    final text = '${picked.hour.toString().padLeft(2, '0')}:${picked.minute.toString().padLeft(2, '0')}';
    _vm.setWindow(i, start ? window.copyWith(startTime: text) : window.copyWith(endTime: text));
  }

  Future<void> _move(MeetingSchedule schedule, ScheduleSlot slot) async {
    final free = schedule.slots.where((s) => s.workspaceId == null).toList();
    final target = await showModalBottomSheet<ScheduleSlot>(
      context: context,
      showDragHandle: true,
      isScrollControlled: true,
      builder: (context) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s4),
              child: Text('Đổi slot cho ${slot.teamName}', style: Theme.of(context).textTheme.titleLarge),
            ),
            Padding(
              padding: const EdgeInsets.all(AppSpacing.s4),
              child: Text('Hiện tại: ${slot.label}. Các buổi họp chưa diễn ra dời theo slot mới.'),
            ),
            if (free.isEmpty)
              const Padding(padding: EdgeInsets.all(AppSpacing.s4), child: Text('Không còn slot trống. Thêm khung giờ trước.'))
            else
              Flexible(
                child: ListView(
                  shrinkWrap: true,
                  children: [for (final s in free) ListTile(title: Text('${s.label}–${s.endTime}'), onTap: () => Navigator.pop(context, s))],
                ),
              ),
          ],
        ),
      ),
    );
    if (target != null) await _run(_vm.move(slot, target));
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final schedule = widget.schedule;
    final state = ref.watch(lecturerScheduleViewModelProvider(schedule?.id));
    final windows = state.windows;
    final busy = state.busy;
    return ListView(
      padding: const EdgeInsets.all(AppSpacing.s4),
      children: [
        Row(
          children: [
            Expanded(child: Text('Khung giờ họp', style: theme.textTheme.titleMedium)),
            if (schedule != null) Chip(label: Text(switch (schedule.status) { 'DRAFT' => 'Chưa đăng', 'PUBLISHED' => 'Đã đăng', _ => 'Đã chốt slot' })),
          ],
        ),
        if (state.error != null) ErrorBanner(message: state.error!),
        for (var i = 0; i < windows.length; i++)
          Padding(
            padding: const EdgeInsets.only(top: AppSpacing.s2),
            child: Wrap(
              crossAxisAlignment: WrapCrossAlignment.center,
              spacing: AppSpacing.s2,
              children: [
                DropdownButton<int>(
                  value: windows[i].dayOfWeek,
                  items: [for (var d = 1; d <= 7; d++) DropdownMenuItem(value: d, child: Text(dayNames[d]))],
                  onChanged: (d) => _vm.setWindow(i, windows[i].copyWith(dayOfWeek: d)),
                ),
                OutlinedButton(onPressed: () => _pickTime(windows[i], i, true), child: Text('Từ ${windows[i].startTime}')),
                OutlinedButton(onPressed: () => _pickTime(windows[i], i, false), child: Text('Đến ${windows[i].endTime}')),
                if (windows.length > 1)
                  IconButton(tooltip: 'Xóa khung ${i + 1}', onPressed: () => _vm.removeWindow(i), icon: const Icon(Icons.delete_outline)),
                if (state.shownBadWindow == i) Text('Giờ kết thúc phải sau giờ bắt đầu.', style: TextStyle(color: theme.colorScheme.error)),
              ],
            ),
          ),
        Align(
          alignment: Alignment.centerLeft,
          child: TextButton.icon(onPressed: _vm.addWindow, icon: const Icon(Icons.add), label: const Text('Thêm khung giờ')),
        ),
        TextField(
          controller: _minutes,
          keyboardType: TextInputType.number,
          decoration: const InputDecoration(labelText: 'Độ dài slot (phút)', helperText: '5–60 phút'),
          onChanged: _vm.minutesChanged,
        ),
        const SizedBox(height: AppSpacing.s3),
        TextField(
          controller: _location,
          keyboardType: TextInputType.url,
          decoration: InputDecoration(labelText: 'Link họp', errorText: state.locationError),
          onChanged: _vm.locationChanged,
        ),
        const SizedBox(height: AppSpacing.s2),
        Text('Xem trước: ${state.previewCount} slot', style: theme.textTheme.bodyMedium),
        const SizedBox(height: AppSpacing.s3),
        Wrap(
          spacing: AppSpacing.s2,
          children: [
            OutlinedButton(onPressed: busy ? null : () => _run(_vm.save()), child: const Text('Lưu khung giờ')),
            if (schedule?.status == 'DRAFT')
              FilledButton.icon(
                onPressed: busy ? null : () => _run(_vm.publish()),
                icon: const Icon(Icons.campaign_outlined),
                label: const Text('Đăng lịch'),
              ),
          ],
        ),
        if (schedule != null) ...[
          const Divider(height: AppSpacing.s6),
          Text('Các slot', style: theme.textTheme.titleMedium),
          Text('${schedule.slots.length} slot · ${schedule.teamCount} nhóm · còn ${schedule.teamsWithoutSlot.length} nhóm chưa chọn', style: theme.textTheme.bodyMedium),
          if (schedule.slots.length < schedule.teamCount)
            Padding(
              padding: const EdgeInsets.only(top: AppSpacing.s2),
              child: Card(
                color: context.appColors.warningContainer,
                margin: EdgeInsets.zero,
                child: ListTile(
                  leading: Icon(Icons.warning_amber_outlined, color: context.appColors.warning),
                  title: const Text('Thiếu slot so với số nhóm'),
                  subtitle: Text('${schedule.slots.length} slot cho ${schedule.teamCount} nhóm. Vẫn đăng được; thêm khung giờ để mọi nhóm có slot.'),
                ),
              ),
            ),
          if (schedule.status != 'DRAFT') Text('Hạn chọn slot của nhóm: ${formatDateTime(schedule.bookingDeadline)}', style: theme.textTheme.bodyMedium),
          if (schedule.slots.isEmpty)
            const Padding(padding: EdgeInsets.only(top: AppSpacing.s2), child: Text('Chưa có slot. Lưu khung giờ để cắt slot.'))
          else
            WeeklySlotGrid(slots: schedule.slots, canPick: (s) => s.workspaceId != null, onPick: (s) => _move(schedule, s)),
          if (schedule.teamsWithoutSlot.isNotEmpty)
            Padding(
              padding: const EdgeInsets.only(top: AppSpacing.s3),
              child: Text('Chưa chọn: ${schedule.teamsWithoutSlot.join(', ')}', style: theme.textTheme.bodyMedium),
            ),
        ],
      ],
    );
  }
}
