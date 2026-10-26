import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/network/api_exception.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../workspace/workspace_widgets.dart';
import 'my_team_api.dart';

/// S17 – Thành viên nhóm (MB-22): trưởng nhóm xóa / chuyển quyền qua menu, thành viên rời nhóm; mọi thao tác có AlertDialog.
/// Nhóm EXE201 kế thừa: thay đổi chỉ ghi nhận, chờ Bộ môn duyệt (quy tắc 7).
class MembersScreen extends ConsumerStatefulWidget {
  const MembersScreen({super.key});

  @override
  ConsumerState<MembersScreen> createState() => _MembersScreenState();
}

class _MembersScreenState extends ConsumerState<MembersScreen> {
  String? _error;

  Future<void> _run(String confirmTitle, String confirmBody, String action, Future<MemberChange> Function() call, {bool leaving = false}) async {
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
    try {
      final result = await call();
      if (!mounted) return;
      setState(() => _error = null);
      refreshMyTeam(ref);
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(result.applied ? 'Đã cập nhật' : 'Đã ghi nhận, chờ Bộ môn duyệt')));
      if (leaving && result.applied) context.go('/sv/nhom');
    } on ApiException catch (e) {
      setState(() => _error = e.detail ?? e.title);
    }
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
    final api = ref.read(myTeamApiProvider);
    final canChange = team.isOpen || team.isInherited;
    return ListView(
      padding: const EdgeInsets.all(AppSpacing.s4),
      children: [
        if (_error != null) ...[ErrorBanner(message: _error!), const SizedBox(height: AppSpacing.s3)],
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
                      if (team.isLeader && !m.isLeader && canChange)
                        PopupMenuButton<String>(
                          tooltip: 'Thao tác với ${m.fullName}',
                          onSelected: (action) => action == 'leader'
                              ? _run('Chuyển trưởng nhóm?', '${m.fullName} sẽ là trưởng nhóm; bạn thành thành viên.', 'Chuyển',
                                  () => api.transferLeader(team.id, m.id))
                              : _run('Xóa khỏi nhóm?', 'Xóa ${m.fullName} khỏi nhóm.', 'Xóa', () => api.removeMember(team.id, m.id)),
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
            onPressed: () => _run('Rời nhóm?', 'Bạn sẽ rời ${team.name}.', 'Rời nhóm', () => api.leave(team.id), leaving: true),
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
