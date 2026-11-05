import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../profile/skill_bars.dart';
import '../workspace/workspace_widgets.dart';
import 'my_team_api.dart';
import 'requests_view_model.dart';

/// S20 – Yêu cầu tham gia và lời mời đã gửi (MB-24, trưởng nhóm). Tab 1: yêu cầu kèm ý tưởng hoặc "Chưa có ý tưởng" (quy tắc 17),
/// Nhận / Từ chối. Tab 2: lời mời đã gửi, thu hồi khi còn chờ.
class RequestsScreen extends ConsumerWidget {
  const RequestsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final team = ref.watch(myTeamDetailProvider);
    return DefaultTabController(
      length: 2,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Yêu cầu và lời mời'),
          bottom: const TabBar(tabs: [Tab(text: 'Yêu cầu'), Tab(text: 'Lời mời đã gửi')]),
        ),
        body: team.when(
          loading: () => const SkeletonList(),
          error: (e, _) => ErrorState(error: e, onRetry: () => refreshMyTeam(ref)),
          data: (t) => t == null || !t.isLeader
              ? const EmptyState(icon: Icons.lock_outline, title: 'Chỉ trưởng nhóm xem được')
              : TabBarView(children: [_JoinRequests(team: t), _SentInvitations(team: t)]),
        ),
      ),
    );
  }
}

class _JoinRequests extends ConsumerWidget {
  const _JoinRequests({required this.team});

  final MyTeam team;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final vm = ref.read(joinRequestsViewModelProvider(team.id).notifier);
    final state = ref.watch(joinRequestsViewModelProvider(team.id));
    final busyId = state.busyId;
    final requests = ref.watch(joinRequestsProvider(team.id));
    Future<void> decide(JoinRequestItem request, bool accept) async {
      final outcome = await vm.decide(request, accept: accept);
      if (outcome case DecisionDone(:final message) when context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
      }
    }

    return RefreshIndicator(
      onRefresh: () async => vm.refresh(),
      child: requests.when(
        loading: () => const SkeletonList(),
        error: (e, _) => ErrorState(error: e, onRetry: vm.refresh),
        data: (items) => ListView(
          padding: const EdgeInsets.all(AppSpacing.s4),
          children: [
            if (state.error != null) ...[ErrorBanner(message: state.error!), const SizedBox(height: AppSpacing.s3)],
            if (items.isEmpty)
              const EmptyState(icon: Icons.inbox_outlined, title: 'Chưa có yêu cầu', description: 'Yêu cầu tham gia sẽ hiện ở đây.'),
            for (final r in items)
              Card.outlined(
                margin: const EdgeInsets.only(bottom: AppSpacing.s3),
                child: Padding(
                  padding: const EdgeInsets.all(AppSpacing.s4),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(r.student.fullName, style: theme.textTheme.titleMedium),
                      Text([r.student.studentCode, if (r.student.specialization?.name.isNotEmpty ?? false) r.student.specialization!.name].join(' · ')),
                      if (r.description != null) ...[const SizedBox(height: AppSpacing.s2), Text(r.description!)],
                      // shortcut: chưa đánh dấu kỹ năng nhóm đang thiếu – API thành viên nhóm chưa trả kỹ năng; thêm highlight khi contract có.
                      const SizedBox(height: AppSpacing.s2),
                      SkillBars(levels: r.skills),
                      if (r.message != null) ...[const SizedBox(height: AppSpacing.s2), Text('“${r.message}”')],
                      const SizedBox(height: AppSpacing.s2),
                      if (r.hasNoIdea)
                        const StatusChip(label: 'Chưa có ý tưởng', tone: StatusTone.neutral, icon: Icons.lightbulb_outline)
                      else
                        for (final idea in r.ideas)
                          ListTile(
                            dense: true,
                            contentPadding: EdgeInsets.zero,
                            leading: const Icon(Icons.lightbulb_outline),
                            title: Text(idea.title),
                            subtitle: idea.description == null ? null : Text(idea.description!),
                          ),
                      const SizedBox(height: AppSpacing.s2),
                      Row(
                        mainAxisAlignment: MainAxisAlignment.end,
                        children: [
                          TextButton(onPressed: busyId == null ? () => decide(r, false) : null, child: const Text('Từ chối')),
                          const SizedBox(width: AppSpacing.s2),
                          if (team.isFull)
                            const Text('Nhóm đã đủ')
                          else
                            LoadingButton(label: 'Nhận', expand: false, loading: busyId == r.id, onPressed: busyId == null ? () => decide(r, true) : null),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _SentInvitations extends ConsumerWidget {
  const _SentInvitations({required this.team});

  final MyTeam team;

  static (String, StatusTone) _status(String s) => switch (s) {
        'PENDING' => ('Đang chờ', StatusTone.warning),
        'ACCEPTED' => ('Đã nhận', StatusTone.success),
        'DECLINED' => ('Đã từ chối', StatusTone.error),
        'EXPIRED' => ('Hết hạn', StatusTone.neutral),
        _ => ('Đã thu hồi', StatusTone.neutral),
      };

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final vm = ref.read(sentInvitationsViewModelProvider(team.id).notifier);
    final error = ref.watch(sentInvitationsViewModelProvider(team.id));
    final invitations = ref.watch(sentInvitationsProvider(team.id));
    return RefreshIndicator(
      onRefresh: () async => vm.refresh(),
      child: invitations.when(
        loading: () => const SkeletonList(),
        error: (e, _) => ErrorState(error: e, onRetry: vm.refresh),
        data: (items) => items.isEmpty
            ? ListView(children: const [EmptyState(icon: Icons.outgoing_mail, title: 'Chưa gửi lời mời nào')])
            : ListView(
                padding: const EdgeInsets.all(AppSpacing.s4),
                children: [
                  if (error != null) ...[ErrorBanner(message: error), const SizedBox(height: AppSpacing.s3)],
                  for (final i in items)
                    Card.outlined(
                      margin: const EdgeInsets.only(bottom: AppSpacing.s3),
                      child: ListTile(
                        title: Text(i.invitee.fullName),
                        subtitle: Text(i.invitee.studentCode),
                        trailing: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            StatusChip(label: _status(i.status).$1, tone: _status(i.status).$2),
                            if (i.status == 'PENDING')
                              IconButton(
                                tooltip: 'Thu hồi lời mời',
                                icon: const Icon(Icons.undo),
                                onPressed: () => vm.cancel(i),
                              ),
                          ],
                        ),
                      ),
                    ),
                ],
              ),
      ),
    );
  }
}
