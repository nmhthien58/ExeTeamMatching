import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../../shared/widgets/team_requirement_bar.dart';
import '../suggestions/suggestion_widgets.dart';
import '../suggestions/suggestions_api.dart';
import '../workspace/workspace_models.dart';
import '../workspace/workspace_widgets.dart';

/// S31 – Bạn đã được ghép nhóm tự động (MB-31, quy tắc 14): nhóm do hệ thống lập, thành viên kèm liên hệ
/// (chỉ đồng đội thấy – quy tắc 11), các bước tiếp theo.
class AutoAssignedScreen extends ConsumerWidget {
  const AutoAssignedScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final team = ref.watch(myTeamProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Nhóm của bạn')),
      body: team.when(
        loading: () => const SkeletonList(count: 2),
        error: (e, _) => ErrorState(error: e, onRetry: () => ref.invalidate(myTeamProvider)),
        data: (t) => t == null
            ? const EmptyState(icon: Icons.hourglass_top, title: 'Chưa có kết quả ghép', description: 'Hệ thống đang ghép nhóm. Bạn sẽ nhận thông báo khi xong.')
            : _Content(team: t),
      ),
    );
  }
}

class _Content extends StatelessWidget {
  const _Content({required this.team});

  final TeamDetail team;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return ListView(
      padding: const EdgeInsets.all(AppSpacing.s4),
      children: [
        Card(
          elevation: 0,
          color: theme.colorScheme.primaryContainer,
          child: Padding(
            padding: const EdgeInsets.all(AppSpacing.s4),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(children: [
                  Icon(Icons.auto_awesome, size: 20, color: theme.colorScheme.onPrimaryContainer),
                  const SizedBox(width: AppSpacing.s2),
                  Expanded(
                    child: Text('Bạn đã được ghép vào ${team.card.name}',
                        style: theme.textTheme.titleMedium?.copyWith(color: theme.colorScheme.onPrimaryContainer)),
                  ),
                ]),
                const SizedBox(height: AppSpacing.s2),
                DomainChips(domains: team.card.domains),
                const SizedBox(height: AppSpacing.s2),
                TeamRequirementBar(memberCount: team.card.memberCount, maxMembers: team.card.maxSize),
              ],
            ),
          ),
        ),
        const SizedBox(height: AppSpacing.s4),
        Text('Thành viên', style: theme.textTheme.titleMedium),
        for (final m in team.members)
          Card.outlined(
            child: Padding(
              padding: const EdgeInsets.all(AppSpacing.s3),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Avatar(name: m.fullName),
                  const SizedBox(width: AppSpacing.s3),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Wrap(spacing: AppSpacing.s2, crossAxisAlignment: WrapCrossAlignment.center, children: [
                          Text(m.fullName, style: theme.textTheme.titleMedium),
                          if (m.isLeader) const RoleChip(role: WorkspaceRole.leader),
                        ]),
                        Text([m.studentCode, ?m.specialization?.name].join(' · '),
                            style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant), maxLines: 2),
                        if (m.email != null)
                          TextButton(onPressed: () => launchUrl(Uri(scheme: 'mailto', path: m.email)), child: Text(m.email!)),
                        if (m.phone != null)
                          TextButton(onPressed: () => launchUrl(Uri(scheme: 'tel', path: m.phone)), child: Text(m.phone!)),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
        const SizedBox(height: AppSpacing.s4),
        Text('Bước tiếp theo', style: theme.textTheme.titleMedium),
        Card.outlined(
          child: Column(
            children: [
              _NextStep(icon: Icons.forum_outlined, title: 'Chào nhóm', subtitle: 'Nhắn kênh Nhóm để làm quen và hẹn buổi đầu.', path: '/sv/du-an/tin-nhan'),
              _NextStep(icon: Icons.rocket_launch_outlined, title: 'Mở dự án', subtitle: 'Việc, file và lịch họp của nhóm.', path: '/sv/du-an', go: true),
              _NextStep(icon: Icons.groups_outlined, title: 'Xem nhóm', subtitle: 'Đề tài, giảng viên hướng dẫn và mentor.', path: '/sv/nhom', go: true),
            ],
          ),
        ),
      ],
    );
  }
}

/// Dòng điều hướng (DESIGN.md 6.1): icon, tiêu đề, dòng phụ, mũi tên; cả dòng bấm được.
class _NextStep extends StatelessWidget {
  const _NextStep({required this.icon, required this.title, required this.subtitle, required this.path, this.go = false});

  final IconData icon;
  final String title;
  final String subtitle;
  final String path;

  /// Tab cấp 1 thì `go`, màn cấp 2 thì `push`.
  final bool go;

  @override
  Widget build(BuildContext context) => ListTile(
        leading: Icon(icon, color: Theme.of(context).colorScheme.secondary),
        title: Text(title),
        subtitle: Text(subtitle),
        trailing: const Icon(Icons.chevron_right),
        onTap: () => go ? context.go(path) : context.push(path),
      );
}
