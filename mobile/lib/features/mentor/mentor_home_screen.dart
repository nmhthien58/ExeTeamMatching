import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/phase.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../lecturer/lecturer_home_api.dart';
import '../lecturer/lecturer_home_models.dart';
import '../my_team/team_advisors.dart';
import '../workspace/workspace_widgets.dart';
import 'mentor_home_view_model.dart';

class MentorHomeScreen extends ConsumerStatefulWidget {
  const MentorHomeScreen({super.key});

  @override
  ConsumerState<MentorHomeScreen> createState() => _MentorHomeScreenState();
}

class _MentorHomeScreenState extends ConsumerState<MentorHomeScreen> {
  MentorHomeViewModel get _vm => ref.read(mentorHomeViewModelProvider.notifier);

  @override
  Widget build(BuildContext context) {
    final edit = ref.watch(mentorHomeViewModelProvider);
    final profile = ref.watch(mentorProfileProvider);
    final teams = ref.watch(mentorTeamsMeProvider);
    final workspaces = ref.watch(advisorWorkspacesProvider);
    final questions = ref.watch(advisorOpenQuestionCountProvider);
    final topics = ref.watch(advisorTopicRecommendationsProvider('MENTOR'));
    final beforeWeekSix = !ref.watch(shellPhaseProvider).isMentoringWindow;

    return Scaffold(
      appBar: AppBar(title: const Text('Trang chủ mentor')),
      body: RefreshIndicator(
        onRefresh: _vm.refresh,
        child: ListView(
          padding: const EdgeInsets.all(AppSpacing.s4),
          children: [
            profile.when(
              loading: () => const Skeleton(height: 150),
              error: (error, _) => ErrorState(
                error: error,
                onRetry: () => ref.invalidate(mentorProfileProvider),
              ),
              data: (value) => Card.outlined(
                child: Padding(
                  padding: const EdgeInsets.all(AppSpacing.s4),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Xin chào, ${value.fullName}',
                        style: Theme.of(context).textTheme.titleLarge,
                      ),
                      const SizedBox(height: AppSpacing.s2),
                      Text(
                        'Đang nhận ${value.currentTeamCount} nhóm · '
                        'mong muốn ${value.preferredTeams ?? value.maxTeams} · '
                        'tối đa ${value.maxTeams}',
                      ),
                      if (edit.saveError != null) ...[
                        const SizedBox(height: AppSpacing.s2),
                        ErrorBanner(message: edit.saveError!),
                      ],
                      const SizedBox(height: AppSpacing.s2),
                      if (!edit.editing)
                        TextButton.icon(
                          onPressed: () => _vm.startEdit(value),
                          icon: const Icon(Icons.edit_outlined),
                          label: const Text('Sửa số nhóm mong muốn'),
                        )
                      else
                        Row(
                          crossAxisAlignment: CrossAxisAlignment.end,
                          children: [
                            Expanded(
                              child: DropdownButtonFormField<int>(
                                initialValue: edit.preferredTeams,
                                decoration: const InputDecoration(
                                  labelText: 'Số nhóm mong muốn',
                                ),
                                items: [
                                  for (
                                    var count = 1;
                                    count <= value.maxTeams;
                                    count++
                                  )
                                    DropdownMenuItem(
                                      value: count,
                                      child: Text('$count'),
                                    ),
                                ],
                                onChanged: edit.saving ? null : _vm.choose,
                              ),
                            ),
                            const SizedBox(width: AppSpacing.s2),
                            FilledButton(
                              onPressed: edit.saving ? null : _vm.save,
                              child: edit.saving
                                  ? const SizedBox.square(
                                      dimension: 20,
                                      child: CircularProgressIndicator(
                                        strokeWidth: 2,
                                      ),
                                    )
                                  : const Text('Lưu'),
                            ),
                            IconButton(
                              tooltip: 'Hủy',
                              onPressed: edit.saving ? null : _vm.cancelEdit,
                              icon: const Icon(Icons.close),
                            ),
                          ],
                        ),
                    ],
                  ),
                ),
              ),
            ),
            const SizedBox(height: AppSpacing.s4),
            if (beforeWeekSix)
              topics.when(
                loading: () => const Skeleton(height: 120),
                error: (error, _) => ErrorState(
                  error: error,
                  onRetry: () => ref.invalidate(
                    advisorTopicRecommendationsProvider('MENTOR'),
                  ),
                ),
                data: (value) => Card.outlined(
                  child: Padding(
                    padding: const EdgeInsets.all(AppSpacing.s4),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Đang quan tâm ${value.openInterestCount} đề tài · '
                          'Ghép vào tuần 6',
                          style: Theme.of(context).textTheme.titleMedium,
                        ),
                        const SizedBox(height: AppSpacing.s2),
                        TextButton.icon(
                          onPressed: () => context.push('/mentor/de-tai'),
                          icon: const Icon(Icons.lightbulb_outline),
                          label: const Text('Xem đề tài'),
                        ),
                      ],
                    ),
                  ),
                ),
              )
            else
              _MentoringWindowContent(
                teams: teams,
                workspaces: workspaces,
                questions: questions,
              ),
          ],
        ),
      ),
    );
  }
}

class _MentoringWindowContent extends ConsumerWidget {
  const _MentoringWindowContent({
    required this.teams,
    required this.workspaces,
    required this.questions,
  });

  final AsyncValue<List<AdvisedTeamItem>> teams;
  final AsyncValue<List<AdvisorWorkspaceRef>> workspaces;
  final AsyncValue<int> questions;

  @override
  Widget build(BuildContext context, WidgetRef ref) => teams.when(
    loading: () => const SkeletonList(),
    error: (error, _) => ErrorState(
      error: error,
      onRetry: () => ref.invalidate(mentorTeamsMeProvider),
    ),
    data: (teamItems) {
      if (teamItems.isEmpty) {
        return const EmptyState(
          icon: Icons.groups_outlined,
          title: 'Bộ môn đang hoàn tất ghép mentor',
          description: 'Danh sách nhóm sẽ xuất hiện sau khi ghép xong.',
        );
      }
      return workspaces.when(
        loading: () => const SkeletonList(),
        error: (error, _) => ErrorState(
          error: error,
          onRetry: () => ref.invalidate(advisorWorkspacesProvider),
        ),
        data: (workspaceItems) {
          final summary = ref.watch(mentorActivitySummaryProvider);
          final activityLoading = summary.loading;
          final activityError = summary.error;
          final upcomingMeetings = summary.upcomingMeetings;
          final pendingSchedules = summary.pendingSchedules;
          return Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              if (activityError != null)
                ErrorBanner(
                  message: 'Chưa tải được số liệu lịch họp.',
                  onRetry: ref
                      .read(mentorHomeViewModelProvider.notifier)
                      .retryActivities,
                )
              else
                Row(
                  children: [
                    Expanded(
                      child: _MetricCard(
                        label: 'Cần trả lời',
                        value: questions.value,
                        loading: questions.isLoading,
                        onTap: () => context.push('/mentor/tin-nhan'),
                      ),
                    ),
                    const SizedBox(width: AppSpacing.s2),
                    Expanded(
                      child: _MetricCard(
                        label: 'Họp sắp tới',
                        value: upcomingMeetings,
                        loading: activityLoading,
                        onTap: () => context.push('/mentor/nhom'),
                      ),
                    ),
                    const SizedBox(width: AppSpacing.s2),
                    Expanded(
                      child: _MetricCard(
                        label: 'Chờ chốt lịch',
                        value: pendingSchedules,
                        loading: activityLoading,
                        onTap: () => context.push('/mentor/nhom'),
                      ),
                    ),
                  ],
                ),
              const SizedBox(height: AppSpacing.s4),
              Text(
                'Nhóm đang mentor',
                style: Theme.of(context).textTheme.titleMedium,
              ),
              const SizedBox(height: AppSpacing.s2),
              for (final team in teamItems) ...[
                _MentorTeamCard(
                  team: team,
                  workspace: workspaceOfTeam(workspaceItems, team.teamId),
                  activity: summary.activityOf(team.teamId),
                ),
                const SizedBox(height: AppSpacing.s3),
              ],
            ],
          );
        },
      );
    },
  );
}

class _MetricCard extends StatelessWidget {
  const _MetricCard({
    required this.label,
    required this.value,
    required this.loading,
    required this.onTap,
  });

  final String label;
  final int? value;
  final bool loading;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => Card.outlined(
    child: InkWell(
      borderRadius: BorderRadius.circular(AppRadius.lg),
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.s3),
        child: Column(
          children: [
            Text(
              loading ? '—' : '${value ?? 0}',
              style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                color: Theme.of(context).colorScheme.primary,
              ),
            ),
            Text(label, textAlign: TextAlign.center),
          ],
        ),
      ),
    ),
  );
}

class _MentorTeamCard extends ConsumerWidget {
  const _MentorTeamCard({
    required this.team,
    required this.workspace,
    required this.activity,
  });

  final AdvisedTeamItem team;
  final AdvisorWorkspaceRef? workspace;
  final AdvisorTeamActivity? activity;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final lecturer = ref.watch(teamLecturerProvider(team.teamId));
    return Card.outlined(
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.lg),
        onTap: () => context.push('/mentor/nhom/${team.teamId}'),
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.s4),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      team.teamName,
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                  ),
                  const StatusChip(
                    label: 'Mentor',
                    tone: StatusTone.info,
                    icon: Icons.support_agent,
                  ),
                  const Icon(Icons.chevron_right),
                ],
              ),
              const SizedBox(height: AppSpacing.s1),
              Text(team.projectName ?? 'Chưa đặt tên đề tài'),
              if (activity != null)
                Padding(
                  padding: const EdgeInsets.only(top: AppSpacing.s2),
                  child: Text(
                    'Buổi mentor ${activity!.meetings.mentorSessionsDone}/'
                    '${activity!.meetings.expectedMentorSessions}',
                  ),
                ),
              lecturer.when(
                data: (value) => value == null
                    ? const SizedBox.shrink()
                    : Padding(
                        padding: const EdgeInsets.only(top: AppSpacing.s1),
                        child: Text('Giảng viên: ${value.name}'),
                      ),
                loading: () => const SizedBox.shrink(),
                error: (_, _) => const SizedBox.shrink(),
              ),
              if (workspace != null) ...[
                const SizedBox(height: AppSpacing.s2),
                TextButton.icon(
                  onPressed: () =>
                      context.push('/mentor/du-an/${workspace!.id}'),
                  icon: const Icon(Icons.open_in_new),
                  label: const Text('Mở dự án'),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
