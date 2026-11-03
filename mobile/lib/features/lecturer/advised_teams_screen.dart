import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../lecturers/match_reason.dart';
import '../meeting_schedule/meeting_schedule_api.dart' show ScheduleSlot, dayNames;
import '../workspace/workspace_widgets.dart';
import 'lecturer_home_api.dart';
import 'lecturer_home_models.dart';

class AdvisedTeamsScreen extends ConsumerStatefulWidget {
  const AdvisedTeamsScreen({super.key, required this.advisorKind});

  final String advisorKind;

  bool get isMentor => advisorKind == 'MENTOR';

  @override
  ConsumerState<AdvisedTeamsScreen> createState() => _AdvisedTeamsScreenState();
}

class _AdvisedTeamsScreenState extends ConsumerState<AdvisedTeamsScreen> {
  String _course = 'EXE101';
  int _missingCheckpoint = 0;
  String _search = '';

  @override
  Widget build(BuildContext context) {
    final workspaces = ref.watch(advisorWorkspacesProvider);
    final checkpoints = ref.watch(advisorCheckpointsProvider);
    final teams = widget.isMentor
        ? ref
              .watch(mentorTeamsMeProvider)
              .whenData(
                (items) => [
                  for (final item in items) _AdvisorEntry(item, true),
                ],
              )
        : ref
              .watch(lecturerTeamsMeProvider(_course))
              .whenData(
                (response) => [
                  for (final item in response.instructing)
                    _AdvisorEntry(item, false),
                  for (final item in response.mentoring)
                    _AdvisorEntry(item, true),
                ],
              );

    return Scaffold(
      appBar: AppBar(
        title: Text(
          widget.isMentor ? 'Nhóm đang mentor' : 'Nhóm tôi hướng dẫn',
        ),
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(advisorWorkspacesProvider);
          ref.invalidate(advisorCheckpointsProvider);
          if (widget.isMentor) {
            ref.invalidate(mentorTeamsMeProvider);
          } else {
            ref.invalidate(lecturerTeamsMeProvider(_course));
          }
          await Future.wait([
            ref.read(advisorWorkspacesProvider.future),
            if (widget.isMentor)
              ref.read(mentorTeamsMeProvider.future)
            else
              ref.read(lecturerTeamsMeProvider(_course).future),
          ]);
        },
        child: ListView(
          padding: const EdgeInsets.all(AppSpacing.s4),
          children: [
            if (!widget.isMentor) ...[
              Row(
                children: [
                  for (final course in const ['EXE101', 'EXE201']) ...[
                    Expanded(
                      child: course == _course
                          ? FilledButton(onPressed: () {}, child: Text(course))
                          : OutlinedButton(
                              onPressed: () => setState(() {
                                _course = course;
                                _missingCheckpoint = 0;
                              }),
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
              onChanged: (value) => setState(() => _search = value.trim()),
            ),
            if (!widget.isMentor && _course == 'EXE101') ...[
              const SizedBox(height: AppSpacing.s3),
              DropdownButtonFormField<int>(
                isExpanded: true,
                initialValue: _missingCheckpoint,
                decoration: const InputDecoration(labelText: 'Lọc Checkpoint'),
                items: [
                  const DropdownMenuItem(value: 0, child: Text('Tất cả nhóm')),
                  for (var no = 1; no <= 4; no++)
                    DropdownMenuItem(
                      value: no,
                      child: Text('Chưa trình bày CP$no'),
                    ),
                ],
                onChanged: (value) =>
                    setState(() => _missingCheckpoint = value ?? 0),
              ),
            ],
            const SizedBox(height: AppSpacing.s4),
            teams.when(
              loading: () => const SkeletonList(),
              error: (error, _) => ErrorState(
                error: error,
                onRetry: () => widget.isMentor
                    ? ref.invalidate(mentorTeamsMeProvider)
                    : ref.invalidate(lecturerTeamsMeProvider(_course)),
              ),
              data: (entries) => workspaces.when(
                loading: () => const SkeletonList(),
                error: (error, _) => ErrorState(
                  error: error,
                  onRetry: () => ref.invalidate(advisorWorkspacesProvider),
                ),
                data: (workspaceItems) {
                  if (_missingCheckpoint > 0 && checkpoints.hasError) {
                    return ErrorState(
                      error: checkpoints.error!,
                      onRetry: () => ref.invalidate(advisorCheckpointsProvider),
                    );
                  }
                  final checkpointItems = checkpoints.value ?? const [];
                  final query = _search.toLowerCase();
                  final displayed = entries.where((entry) {
                    final item = entry.item;
                    final matchesSearch =
                        query.isEmpty ||
                        item.teamName.toLowerCase().contains(query) ||
                        (item.classCode?.toLowerCase().contains(query) ??
                            false);
                    if (!matchesSearch || _missingCheckpoint == 0) {
                      return matchesSearch;
                    }
                    final workspace = workspaceItems
                        .where((value) => value.teamId == item.teamId)
                        .firstOrNull;
                    final progress = checkpointItems
                        .where((value) => value.workspaceId == workspace?.id)
                        .firstOrNull;
                    return progress?.items.any(
                          (cp) =>
                              cp.checkpointNo == _missingCheckpoint &&
                              !cp.presented,
                        ) ??
                        false;
                  }).toList();
                  if (displayed.isEmpty) {
                    return EmptyState(
                      icon: Icons.groups_outlined,
                      title: query.isNotEmpty || _missingCheckpoint > 0
                          ? 'Không tìm thấy nhóm'
                          : 'Chưa có nhóm được ghép',
                    );
                  }
                  return Column(
                    children: [
                      for (final entry in displayed) ...[
                        _AdvisedTeamCard(
                          entry: entry,
                          advisorKind: widget.advisorKind,
                          workspace: workspaceItems
                              .where(
                                (value) => value.teamId == entry.item.teamId,
                              )
                              .firstOrNull,
                          checkpoint: checkpointItems
                              .where(
                                (value) =>
                                    value.workspaceId ==
                                    workspaceItems
                                        .where(
                                          (workspace) =>
                                              workspace.teamId ==
                                              entry.item.teamId,
                                        )
                                        .firstOrNull
                                        ?.id,
                              )
                              .firstOrNull,
                        ),
                        const SizedBox(height: AppSpacing.s3),
                      ],
                    ],
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _AdvisorEntry {
  const _AdvisorEntry(this.item, this.isMentorRole);

  final AdvisedTeamItem item;
  final bool isMentorRole;
}

class _AdvisedTeamCard extends ConsumerWidget {
  const _AdvisedTeamCard({
    required this.entry,
    required this.advisorKind,
    required this.workspace,
    required this.checkpoint,
  });

  final _AdvisorEntry entry;
  final String advisorKind;
  final AdvisorWorkspaceRef? workspace;
  final CheckpointProgress? checkpoint;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final item = entry.item;
    final activity = workspace == null
        ? null
        : ref.watch(
            advisorTeamActivityProvider((
              workspace: workspace!,
              includeTasks: advisorKind == 'LECTURER',
              includeSchedule:
                  advisorKind == 'LECTURER' && item.courseCode == 'EXE201',
            )),
          );
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
              if (activity != null)
                activity.when(
                  loading: () => const Padding(
                    padding: EdgeInsets.only(top: AppSpacing.s3),
                    child: Skeleton(height: 16),
                  ),
                  error: (_, _) => const SizedBox.shrink(),
                  data: (value) => Padding(
                    padding: const EdgeInsets.only(top: AppSpacing.s3),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        if (advisorKind == 'LECTURER')
                          Text(
                            'Tiến độ việc: ${value.completedTasks}/${value.tasks.length} · '
                            '${value.pendingSubmissions} bài nộp chờ duyệt',
                          ),
                        if (item.courseCode == 'EXE201')
                          Text(
                            value.schedule?.mySlot == null
                                ? 'Chưa chốt lịch họp hằng tuần'
                                : _slotLabel(value.schedule!.mySlot!),
                          ),
                        if (entry.isMentorRole)
                          Text(
                            'Buổi mentor ${value.meetings.mentorSessionsDone}/'
                            '${value.meetings.expectedMentorSessions}',
                          ),
                      ],
                    ),
                  ),
                ),
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
    final detail = ref.watch(advisorTypedTeamDetailProvider(teamId));
    final workspaces = ref.watch(advisorWorkspacesProvider);
    final assignments = isMentor
        ? ref
              .watch(mentorTeamsMeProvider)
              .whenData(
                (items) => [
                  for (final item in items) _AdvisorEntry(item, true),
                ],
              )
        : ref
              .watch(lecturerTeamsMeProvider(null))
              .whenData(
                (response) => [
                  for (final item in response.instructing)
                    _AdvisorEntry(item, false),
                  for (final item in response.mentoring)
                    _AdvisorEntry(item, true),
                ],
              );
    return Scaffold(
      appBar: AppBar(title: const Text('Chi tiết nhóm')),
      body: detail.when(
        loading: () => const SkeletonList(),
        error: (error, _) => ErrorState(
          error: error,
          onRetry: () => ref.invalidate(advisorTypedTeamDetailProvider(teamId)),
        ),
        data: (teamDetail) => workspaces.when(
          loading: () => const SkeletonList(),
          error: (error, _) => ErrorState(
            error: error,
            onRetry: () => ref.invalidate(advisorWorkspacesProvider),
          ),
          data: (workspaceItems) => assignments.when(
            loading: () => const SkeletonList(),
            error: (error, _) => ErrorState(
              error: error,
              onRetry: () => isMentor
                  ? ref.invalidate(mentorTeamsMeProvider)
                  : ref.invalidate(lecturerTeamsMeProvider(null)),
            ),
            data: (entries) {
              final assignment = entries
                  .where((entry) => entry.item.teamId == teamId)
                  .firstOrNull;
              final workspace = workspaceItems
                  .where((item) => item.teamId == teamId)
                  .firstOrNull;
              final courseCode =
                  assignment?.item.courseCode ??
                  workspace?.courseCode ??
                  'EXE101';
              return _TeamDetailBody(
                teamId: teamId,
                advisorKind: advisorKind,
                detail: teamDetail,
                assignment: assignment,
                workspace: workspace,
                courseCode: courseCode,
              );
            },
          ),
        ),
      ),
    );
  }
}

class _TeamDetailBody extends ConsumerWidget {
  const _TeamDetailBody({
    required this.teamId,
    required this.advisorKind,
    required this.detail,
    required this.assignment,
    required this.workspace,
    required this.courseCode,
  });

  final String teamId;
  final String advisorKind;
  final AdvisorTeamDetail detail;
  final _AdvisorEntry? assignment;
  final AdvisorWorkspaceRef? workspace;
  final String courseCode;

  bool get isMentorRole =>
      advisorKind == 'MENTOR' || (assignment?.isMentorRole ?? false);

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final activity = workspace == null
        ? null
        : ref.watch(
            advisorTeamActivityProvider((
              workspace: workspace!,
              includeTasks: advisorKind == 'LECTURER',
              includeSchedule:
                  advisorKind == 'LECTURER' && courseCode == 'EXE201',
            )),
          );
    final checkpoint =
        workspace != null &&
            courseCode == 'EXE101' &&
            advisorKind == 'LECTURER' &&
            !isMentorRole
        ? ref.watch(advisorCheckpointProvider(workspace!.id))
        : null;
    final reason = assignment == null
        ? null
        : isMentorRole
        ? mentorMatchReason(assignment!.item.reason)
        : lecturerMatchReason(
            assignment!.item.reason,
            assignment!.item.preferenceRank,
          );
    return RefreshIndicator(
      onRefresh: () async {
        ref.invalidate(advisorTypedTeamDetailProvider(teamId));
        ref.invalidate(advisorWorkspacesProvider);
        if (workspace != null) {
          ref.invalidate(
            advisorTeamActivityProvider((
              workspace: workspace!,
              includeTasks: advisorKind == 'LECTURER',
              includeSchedule:
                  advisorKind == 'LECTURER' && courseCode == 'EXE201',
            )),
          );
          ref.invalidate(advisorCheckpointProvider(workspace!.id));
        }
      },
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
                            advisorTeamActivityProvider((
                              workspace: workspace!,
                              includeTasks: advisorKind == 'LECTURER',
                              includeSchedule:
                                  advisorKind == 'LECTURER' &&
                                  courseCode == 'EXE201',
                            )),
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
  var saving = false;
  String? error;
  final marked = await showModalBottomSheet<int>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    builder: (sheetContext) => StatefulBuilder(
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
                          lastDate: DateTime(presentedOn.year + 1),
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
                ErrorBanner(message: error!),
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
                              setState(() {
                                saving = true;
                                error = null;
                              });
                              try {
                                await ref
                                    .read(lecturerHomeApiProvider)
                                    .markCheckpoint(
                                      workspaceId,
                                      checkpointNo: checkpointNo,
                                      presentedOn: presentedOn,
                                      comment: comment,
                                    );
                                ref.invalidate(
                                  advisorCheckpointProvider(workspaceId),
                                );
                                ref.invalidate(advisorCheckpointsProvider);
                                if (context.mounted) {
                                  Navigator.pop(context, checkpointNo);
                                }
                              } catch (cause) {
                                setState(() {
                                  saving = false;
                                  error =
                                      'Không thể ghi nhận Checkpoint. Hãy thử lại.';
                                });
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
          try {
            await ref
                .read(lecturerHomeApiProvider)
                .undoCheckpoint(workspaceId, marked);
            ref.invalidate(advisorCheckpointProvider(workspaceId));
            ref.invalidate(advisorCheckpointsProvider);
          } catch (_) {
            if (!context.mounted) return;
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(content: Text('Không thể hoàn tác. Hãy thử lại.')),
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
