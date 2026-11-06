import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/view_state.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../lecturers/match_reason.dart';
import '../meeting_schedule/meeting_schedule_api.dart'
    show ScheduleSlot, dayNames;
import '../workspace/workspace_widgets.dart';
import 'advised_teams_view_model.dart';
import 'lecturer_home_api.dart';
import 'lecturer_home_models.dart';
import '../../shared/widgets/brand_app_bar.dart';

class AdvisedTeamsScreen extends ConsumerStatefulWidget {
  const AdvisedTeamsScreen({super.key, required this.advisorKind});

  final String advisorKind;

  bool get isMentor => advisorKind == 'MENTOR';

  @override
  ConsumerState<AdvisedTeamsScreen> createState() => _AdvisedTeamsScreenState();
}

class _AdvisedTeamsScreenState extends ConsumerState<AdvisedTeamsScreen> {
  AdvisedTeamsViewModel get _vm =>
      ref.read(advisedTeamsViewModelProvider(widget.advisorKind).notifier);

  @override
  Widget build(BuildContext context) {
    final filter = ref.watch(advisedTeamsViewModelProvider(widget.advisorKind));
    final teams = ref.watch(advisedTeamsListProvider(widget.advisorKind));

    return Scaffold(
      appBar: BrandAppBar(
        title: widget.isMentor ? 'Nhóm đang mentor' : 'Nhóm tôi hướng dẫn',
      ),
      body: RefreshIndicator(
        onRefresh: _vm.refresh,
        child: ListView(
          padding: const EdgeInsets.all(AppSpacing.s4),
          children: [
            if (!widget.isMentor) ...[
              Row(
                children: [
                  for (final course in const ['EXE101', 'EXE201']) ...[
                    Expanded(
                      child: course == filter.course
                          ? FilledButton(onPressed: () {}, child: Text(course))
                          : OutlinedButton(
                              onPressed: () => _vm.selectCourse(course),
                              child: Text(course),
                            ),
                    ),
                    if (course == 'EXE101')
                      const SizedBox(width: AppSpacing.s2),
                  ],
                ],
              ),
              const SizedBox(height: AppSpacing.s3),
            ],
            TextField(
              decoration: const InputDecoration(
                labelText: 'Tìm nhóm hoặc mã lớp',
                prefixIcon: Icon(Icons.search),
              ),
              onChanged: _vm.search,
            ),
            if (!widget.isMentor && filter.course == 'EXE101') ...[
              const SizedBox(height: AppSpacing.s3),
              DropdownButtonFormField<int>(
                isExpanded: true,
                initialValue: filter.missingCheckpoint,
                decoration: const InputDecoration(labelText: 'Lọc Checkpoint'),
                items: [
                  const DropdownMenuItem(value: 0, child: Text('Tất cả nhóm')),
                  for (var no = 1; no <= 4; no++)
                    DropdownMenuItem(
                      value: no,
                      child: Text('Chưa trình bày CP$no'),
                    ),
                ],
                onChanged: _vm.filterMissingCheckpoint,
              ),
            ],
            const SizedBox(height: AppSpacing.s4),
            switch (teams) {
              ViewLoading() => const SkeletonList(),
              ViewError(:final error) => ErrorState(
                error: error,
                onRetry: _vm.retry,
              ),
              ViewEmpty() => EmptyState(
                icon: Icons.groups_outlined,
                title: filter.isFiltering
                    ? 'Không tìm thấy nhóm'
                    : 'Chưa có nhóm được ghép',
              ),
              ViewData(value: final rows) => Column(
                children: [
                  for (final row in rows) ...[
                    _AdvisedTeamCard(
                      entry: row.entry,
                      advisorKind: widget.advisorKind,
                      checkpoint: row.checkpoint,
                    ),
                    const SizedBox(height: AppSpacing.s3),
                  ],
                ],
              ),
            },
          ],
        ),
      ),
    );
  }
}

class _AdvisedTeamCard extends StatelessWidget {
  const _AdvisedTeamCard({
    required this.entry,
    required this.advisorKind,
    required this.checkpoint,
  });

  final AdvisorEntry entry;
  final String advisorKind;
  final CheckpointProgress? checkpoint;

  @override
  Widget build(BuildContext context) {
    final item = entry.item;
    final reason = entry.isMentorRole
        ? mentorMatchReason(item.reason)
        : lecturerMatchReason(item.reason, item.preferenceRank);
    return Card.outlined(
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.lg),
        onTap: () => context.push(
          advisorKind == 'MENTOR'
              ? '/mentor/nhom/${item.teamId}'
              : '/gv/nhom/${item.teamId}',
        ),
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.s4),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Text(
                      item.teamName,
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                  ),
                  if (entry.isMentorRole)
                    const StatusChip(
                      label: 'Mentor',
                      tone: StatusTone.info,
                      icon: Icons.support_agent,
                    ),
                  const Icon(Icons.chevron_right),
                ],
              ),
              const SizedBox(height: AppSpacing.s1),
              Text(
                [
                  if (item.classCode?.isNotEmpty ?? false) item.classCode!,
                  item.projectName ?? 'Chưa đặt tên đề tài',
                  '${item.memberCount} thành viên',
                ].join(' · '),
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: Theme.of(context).colorScheme.onSurfaceVariant,
                ),
              ),
              if (reason != null) ...[
                const SizedBox(height: AppSpacing.s2),
                StatusChip(
                  label: reason,
                  tone: StatusTone.info,
                  icon: Icons.verified_outlined,
                ),
              ],
              if (checkpoint != null) ...[
                const SizedBox(height: AppSpacing.s3),
                Text('Checkpoint ${checkpoint!.doneCount}/4'),
                const SizedBox(height: AppSpacing.s1),
                LinearProgressIndicator(
                  value: checkpoint!.doneCount / 4,
                  semanticsLabel:
                      'Đã trình bày ${checkpoint!.doneCount} trên 4 checkpoint',
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class AdvisedTeamDetailScreen extends ConsumerWidget {
  const AdvisedTeamDetailScreen({
    super.key,
    required this.teamId,
    required this.advisorKind,
  });

  final String teamId;
  final String advisorKind;

  bool get isMentor => advisorKind == 'MENTOR';

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final key = (teamId: teamId, advisorKind: advisorKind);
    final state = ref.watch(advisedTeamDetailViewModelProvider(key));
    return Scaffold(
      appBar: AppBar(title: const Text('Chi tiết nhóm')),
      body: switch (state) {
        ViewLoading() || ViewEmpty() => const SkeletonList(),
        ViewError(:final error) => ErrorState(
          error: error,
          onRetry: ref
              .read(advisedTeamDetailViewModelProvider(key).notifier)
              .retry,
        ),
        ViewData(value: final data) => _TeamDetailBody(
          teamId: teamId,
          advisorKind: advisorKind,
          data: data,
        ),
      },
    );
  }
}

class _TeamDetailBody extends ConsumerWidget {
  const _TeamDetailBody({
    required this.teamId,
    required this.advisorKind,
    required this.data,
  });

  final String teamId;
  final String advisorKind;
  final AdvisedTeamDetailData data;

  AdvisorTeamDetail get detail => data.detail;
  AdvisorEntry? get assignment => data.assignment;
  AdvisorWorkspaceRef? get workspace => data.workspace;
  String get courseCode => data.courseCode;

  bool get isMentorRole =>
      AdvisedTeamDetailViewModel.isMentorRole(advisorKind, data);

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final activityQuery = workspace == null
        ? null
        : AdvisedTeamDetailViewModel.activityQuery(
            advisorKind,
            workspace!,
            courseCode,
          );
    final activity = activityQuery == null
        ? null
        : ref.watch(advisorTeamActivityProvider(activityQuery));
    final showsCheckpoint = AdvisedTeamDetailViewModel.showsCheckpoint(
      advisorKind,
      data,
    );
    final checkpoint = showsCheckpoint
        ? ref.watch(advisorCheckpointProvider(workspace!.id))
        : null;
    // Giữ ViewModel Checkpoint sống suốt màn để lệnh "Hoàn tác" trên SnackBar còn chạy được.
    if (showsCheckpoint) ref.watch(checkpointViewModelProvider(workspace!.id));
    final reason = assignment == null
        ? null
        : isMentorRole
        ? mentorMatchReason(assignment!.item.reason)
        : lecturerMatchReason(
            assignment!.item.reason,
            assignment!.item.preferenceRank,
          );
    return RefreshIndicator(
      onRefresh: () async => ref
          .read(
            advisedTeamDetailViewModelProvider((
              teamId: teamId,
              advisorKind: advisorKind,
            )).notifier,
          )
          .refresh(data),
      child: ListView(
        padding: const EdgeInsets.all(AppSpacing.s4),
        children: [
          Card.outlined(
            child: Padding(
              padding: const EdgeInsets.all(AppSpacing.s4),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          detail.team.card.name,
                          style: Theme.of(context).textTheme.titleLarge,
                        ),
                      ),
                      if (isMentorRole)
                        const StatusChip(
                          label: 'Mentor',
                          tone: StatusTone.info,
                          icon: Icons.support_agent,
                        ),
                    ],
                  ),
                  const SizedBox(height: AppSpacing.s1),
                  Text(
                    '${detail.team.card.projectName ?? 'Chưa đặt tên đề tài'} · $courseCode',
                  ),
                  if (reason != null) ...[
                    const SizedBox(height: AppSpacing.s2),
                    StatusChip(
                      label: reason,
                      tone: StatusTone.info,
                      icon: Icons.verified_outlined,
                    ),
                  ],
                  const SizedBox(height: AppSpacing.s3),
                  Text(detail.ideaDescription ?? 'Nhóm chưa mô tả ý tưởng.'),
                  if (detail.team.card.domains.isNotEmpty) ...[
                    const SizedBox(height: AppSpacing.s3),
                    Wrap(
                      spacing: AppSpacing.s2,
                      runSpacing: AppSpacing.s2,
                      children: [
                        for (final domain in detail.team.card.domains)
                          Chip(label: Text(domain.name)),
                      ],
                    ),
                  ],
                  const SizedBox(height: AppSpacing.s4),
                  Text(
                    'Thành viên',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  const SizedBox(height: AppSpacing.s2),
                  for (final member in detail.team.members)
                    ListTile(
                      contentPadding: EdgeInsets.zero,
                      leading: CircleAvatar(
                        child: Text(_initials(member.fullName)),
                      ),
                      title: Text(member.fullName),
                      subtitle: Text(
                        [
                          member.studentCode,
                          if (member.specialization != null)
                            member.specialization!.name,
                          if (member.email?.isNotEmpty ?? false) member.email!,
                          if (member.phone?.isNotEmpty ?? false) member.phone!,
                        ].join(' · '),
                      ),
                      trailing: member.isLeader
                          ? const StatusChip(
                              label: 'Trưởng nhóm',
                              tone: StatusTone.neutral,
                            )
                          : null,
                    ),
                ],
              ),
            ),
          ),
          if (workspace != null) ...[
            const SizedBox(height: AppSpacing.s3),
            Card.outlined(
              child: Padding(
                padding: const EdgeInsets.all(AppSpacing.s4),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Dự án',
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                    if (activity != null)
                      activity.when(
                        loading: () => const Padding(
                          padding: EdgeInsets.symmetric(
                            vertical: AppSpacing.s3,
                          ),
                          child: Skeleton(height: 18),
                        ),
                        error: (error, _) => ErrorState(
                          error: error,
                          onRetry: () => ref.invalidate(
                            advisorTeamActivityProvider(activityQuery!),
                          ),
                        ),
                        data: (value) => Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            if (advisorKind == 'LECTURER')
                              Padding(
                                padding: const EdgeInsets.only(
                                  top: AppSpacing.s2,
                                ),
                                child: Text(
                                  'Việc hoàn thành: ${value.completedTasks}/${value.tasks.length} · '
                                  'Bài nộp chờ duyệt: ${value.pendingSubmissions}',
                                ),
                              ),
                            if (isMentorRole)
                              Padding(
                                padding: const EdgeInsets.only(
                                  top: AppSpacing.s2,
                                ),
                                child: Text(
                                  'Buổi mentor ${value.meetings.mentorSessionsDone}/'
                                  '${value.meetings.expectedMentorSessions}',
                                ),
                              ),
                            if (courseCode == 'EXE201') ...[
                              const SizedBox(height: AppSpacing.s2),
                              Text(
                                value.schedule?.mySlot == null
                                    ? 'Nhóm chưa chốt lịch họp hằng tuần.'
                                    : _slotLabel(value.schedule!.mySlot!),
                              ),
                            ],
                          ],
                        ),
                      ),
                    const SizedBox(height: AppSpacing.s3),
                    FilledButton.icon(
                      onPressed: () => context.push(
                        advisorKind == 'MENTOR'
                            ? '/mentor/du-an/${workspace!.id}'
                            : '/gv/du-an/${workspace!.id}',
                      ),
                      icon: const Icon(Icons.open_in_new),
                      label: const Text('Mở không gian làm việc'),
                    ),
                  ],
                ),
              ),
            ),
          ],
          if (checkpoint != null) ...[
            const SizedBox(height: AppSpacing.s3),
            checkpoint.when(
              loading: () => const Skeleton(height: 180),
              error: (error, _) => ErrorState(
                error: error,
                onRetry: () =>
                    ref.invalidate(advisorCheckpointProvider(workspace!.id)),
              ),
              data: (progress) => _CheckpointCard(
                progress: progress,
                onMark: () =>
                    _showCheckpointSheet(context, ref, workspace!.id, progress),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _CheckpointCard extends StatelessWidget {
  const _CheckpointCard({required this.progress, required this.onMark});

  final CheckpointProgress progress;
  final VoidCallback onMark;

  @override
  Widget build(BuildContext context) => Card.outlined(
    child: Padding(
      padding: const EdgeInsets.all(AppSpacing.s4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Checkpoint ${progress.doneCount}/4',
            style: Theme.of(context).textTheme.titleMedium,
          ),
          const SizedBox(height: AppSpacing.s2),
          for (final checkpoint in progress.items) ...[
            StatusChip(
              label:
                  'CP${checkpoint.checkpointNo} · '
                  '${checkpoint.presented ? 'Đã trình bày' : 'Chưa trình bày'}',
              tone: checkpoint.presented
                  ? StatusTone.success
                  : checkpoint.overdue
                  ? StatusTone.warning
                  : StatusTone.neutral,
            ),
            if (checkpoint.comment?.isNotEmpty ?? false)
              Padding(
                padding: const EdgeInsets.fromLTRB(
                  AppSpacing.s2,
                  AppSpacing.s1,
                  AppSpacing.s2,
                  AppSpacing.s2,
                ),
                child: Text(checkpoint.comment!),
              )
            else
              const SizedBox(height: AppSpacing.s2),
          ],
          SizedBox(
            width: double.infinity,
            child: FilledButton(
              onPressed: progress.items.any((item) => !item.presented)
                  ? onMark
                  : null,
              child: const Text('Đánh dấu Checkpoint'),
            ),
          ),
        ],
      ),
    ),
  );
}

Future<void> _showCheckpointSheet(
  BuildContext context,
  WidgetRef ref,
  String workspaceId,
  CheckpointProgress progress,
) async {
  var checkpointNo =
      progress.items
          .where((item) => !item.presented)
          .firstOrNull
          ?.checkpointNo ??
      1;
  var presentedOn = DateTime.now();
  var comment = '';
  final vm = ref.read(checkpointViewModelProvider(workspaceId).notifier)
    ..reset();
  final marked = await showModalBottomSheet<int>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    builder: (sheetContext) => Consumer(
      builder: (context, ref, _) {
        final markState = ref.watch(checkpointViewModelProvider(workspaceId));
        final saving = markState.saving;
        final error = markState.error;
        return StatefulBuilder(
      builder: (context, setState) => Padding(
        padding: EdgeInsets.fromLTRB(
          AppSpacing.s4,
          0,
          AppSpacing.s4,
          MediaQuery.viewInsetsOf(context).bottom + AppSpacing.s4,
        ),
        child: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Đánh dấu Checkpoint',
                style: Theme.of(context).textTheme.titleLarge,
              ),
              const SizedBox(height: AppSpacing.s4),
              DropdownButtonFormField<int>(
                isExpanded: true,
                initialValue: checkpointNo,
                decoration: const InputDecoration(labelText: 'Checkpoint'),
                items: [
                  for (final item in progress.items.where(
                    (item) => !item.presented,
                  ))
                    DropdownMenuItem(
                      value: item.checkpointNo,
                      child: Text('CP${item.checkpointNo}'),
                    ),
                ],
                onChanged: saving
                    ? null
                    : (value) => setState(() => checkpointNo = value ?? 1),
              ),
              const SizedBox(height: AppSpacing.s3),
              ListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Ngày trình bày'),
                subtitle: Text(_dateLabel(presentedOn)),
                trailing: const Icon(Icons.calendar_today_outlined),
                onTap: saving
                    ? null
                    : () async {
                        final selected = await showDatePicker(
                          context: context,
                          initialDate: presentedOn,
                          firstDate: DateTime(presentedOn.year - 1),
                          lastDate: DateTime.now(),
                        );
                        if (selected != null) {
                          setState(() => presentedOn = selected);
                        }
                      },
              ),
              TextFormField(
                maxLength: 500,
                minLines: 3,
                maxLines: 5,
                onChanged: (value) => comment = value,
                decoration: const InputDecoration(
                  labelText: 'Nhận xét (không bắt buộc)',
                  alignLabelWithHint: true,
                ),
              ),
              if (error != null) ...[
                const SizedBox(height: AppSpacing.s2),
                ErrorBanner(message: error),
              ],
              const SizedBox(height: AppSpacing.s3),
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton(
                      onPressed: saving ? null : () => Navigator.pop(context),
                      child: const Text('Hủy'),
                    ),
                  ),
                  const SizedBox(width: AppSpacing.s3),
                  Expanded(
                    child: FilledButton(
                      onPressed: saving
                          ? null
                          : () async {
                              final outcome = await vm.mark(
                                checkpointNo: checkpointNo,
                                presentedOn: presentedOn,
                                comment: comment,
                              );
                              if (outcome is CheckpointDone &&
                                  context.mounted) {
                                Navigator.pop(context, checkpointNo);
                              }
                            },
                      child: saving
                          ? const SizedBox.square(
                              dimension: 20,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Text('Lưu'),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
      },
    ),
  );
  if (marked == null || !context.mounted) return;
  ScaffoldMessenger.of(context).showSnackBar(
    SnackBar(
      content: Text('Đã ghi nhận Checkpoint $marked'),
      duration: const Duration(seconds: 5),
      action: SnackBarAction(
        label: 'Hoàn tác',
        onPressed: () async {
          final outcome = await vm.undo(marked);
          if (outcome is CheckpointFailed && context.mounted) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(content: Text(outcome.message)),
            );
          }
        },
      ),
    ),
  );
}

String _slotLabel(ScheduleSlot slot) {
  final day = (slot.dayOfWeek >= 1 && slot.dayOfWeek <= 7)
      ? dayNames[slot.dayOfWeek]
      : 'Thứ ${slot.dayOfWeek}';
  return 'Lịch hằng tuần: $day · ${slot.startTime}–${slot.endTime}';
}

String _dateLabel(DateTime date) =>
    '${date.day.toString().padLeft(2, '0')}/${date.month.toString().padLeft(2, '0')}/${date.year}';

String _initials(String name) {
  final words = name.trim().split(RegExp(r'\s+'));
  return words
      .where((word) => word.isNotEmpty)
      .take(2)
      .map((word) => word[0])
      .join()
      .toUpperCase();
}
