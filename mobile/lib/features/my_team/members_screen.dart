import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../workspace/workspace_widgets.dart';
import 'leader_vote.dart';
import 'members_view_model.dart';
import 'my_team_api.dart';

/// S17 – Thành viên nhóm (MB-22): trưởng nhóm xóa / chuyển quyền qua menu, thành viên rời nhóm; mọi thao tác có AlertDialog.
/// Nhóm EXE201 kế thừa: thay đổi chỉ ghi nhận, chờ Bộ môn duyệt (quy tắc 7).
/// Thành viên thường của nhóm EXE101 còn mở đề nghị được người làm nhóm trưởng (MB-63), nhóm bỏ phiếu ở S15.
class MembersScreen extends ConsumerStatefulWidget {
  const MembersScreen({super.key});

  @override
  ConsumerState<MembersScreen> createState() => _MembersScreenState();
}

class _MembersScreenState extends ConsumerState<MembersScreen> {
  MembersViewModel get _vm => ref.read(membersViewModelProvider.notifier);

  Future<void> _run(String confirmTitle, String confirmBody, String action, Future<MemberOutcome> Function() call) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(confirmTitle),
        content: Text(confirmBody),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy')),
          TextButton(
            onPressed: () => Navigator.pop(context, true),
            style: TextButton.styleFrom(foregroundColor: Theme.of(context).colorScheme.error),
            child: Text(action),
          ),
        ],
      ),
    );
    if (ok != true || !mounted) return;
    final outcome = await call();
    if (outcome is! MemberChanged || !mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(outcome.message)));
    if (outcome.leaving && outcome.applied) context.go('/sv/nhom');
  }

  Future<void> _propose(MyTeam team, Member nominee) async {
    final message = await showProposeLeaderSheet(context, team: team, nominee: nominee);
    if (message == null || !mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    final team = ref.watch(myTeamDetailProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Thành viên')),
      body: team.when(
        loading: () => const SkeletonList(),
        error: (e, _) => ErrorState(error: e, onRetry: () => refreshMyTeam(ref)),
        data: (t) => t == null ? const EmptyState(icon: Icons.group_off_outlined, title: 'Bạn chưa có nhóm') : _list(t),
      ),
    );
  }

  Widget _list(MyTeam team) {
    final error = ref.watch(membersViewModelProvider);
    final canChange = team.isOpen || team.isInherited;
    final canPropose = !team.isLeader && team.isOpen && !team.isInherited && team.courseCode != 'EXE201' && team.memberCount >= 2;
    return ListView(
      padding: const EdgeInsets.all(AppSpacing.s4),
      children: [
        if (error != null) ...[ErrorBanner(message: error), const SizedBox(height: AppSpacing.s3)],
        if (team.hasPendingChanges) ...[
          const StatusChip(label: 'Có thay đổi chờ Bộ môn duyệt', tone: StatusTone.warning, icon: Icons.hourglass_top_outlined),
          const SizedBox(height: AppSpacing.s3),
        ],
        Card.outlined(
          child: Column(
            children: [
              for (final m in team.members)
                ListTile(
                  leading: Avatar(name: m.fullName),
                  title: Text(m.fullName),
                  subtitle: Text([m.studentCode, if (m.specialization?.name.isNotEmpty ?? false) m.specialization!.name].join(' · ')),
                  trailing: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      if (m.isLeader) const StatusChip(label: 'Trưởng nhóm', tone: StatusTone.info),
                      if (canPropose && !m.isLeader)
                        PopupMenuButton<String>(
                          tooltip: 'Thao tác với ${m.fullName}',
                          onSelected: (_) => _propose(team, m),
                          itemBuilder: (_) => const [PopupMenuItem(value: 'propose', child: Text('Đề nghị làm nhóm trưởng'))],
                        ),
                      if (team.isLeader && !m.isLeader && canChange)
                        PopupMenuButton<String>(
                          tooltip: 'Thao tác với ${m.fullName}',
                          onSelected: (action) => action == 'leader'
                              ? _run('Chuyển trưởng nhóm?', '${m.fullName} sẽ là trưởng nhóm; bạn thành thành viên.', 'Chuyển',
                                  () => _vm.transferLeader(team.id, m.id))
                              : _run('Xóa khỏi nhóm?', 'Xóa ${m.fullName} khỏi nhóm.', 'Xóa', () => _vm.remove(team.id, m.id)),
                          itemBuilder: (_) => const [
                            PopupMenuItem(value: 'leader', child: Text('Chuyển trưởng nhóm')),
                            PopupMenuItem(value: 'remove', child: Text('Xóa khỏi nhóm')),
                          ],
                        ),
                    ],
                  ),
                ),
            ],
          ),
        ),
        if (!team.isLeader && canChange) ...[
          const SizedBox(height: AppSpacing.s4),
          OutlinedButton.icon(
            onPressed: () => _run('Rời nhóm?', 'Bạn sẽ rời ${team.name}.', 'Rời nhóm', () => _vm.leave(team.id)),
            icon: const Icon(Icons.logout),
            label: const Text('Rời nhóm'),
          ),
        ],
        if (team.isLeader)
          Padding(
            padding: const EdgeInsets.only(top: AppSpacing.s3),
            child: Text('Muốn rời nhóm, hãy chuyển quyền trưởng nhóm trước.', style: Theme.of(context).textTheme.bodyMedium),
          ),
      ],
    );
  }
}
