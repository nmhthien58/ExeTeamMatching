import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/phase.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../../shared/widgets/team_requirement_bar.dart';
import '../suggestions/suggestion_widgets.dart' show DomainChips;
import '../workspace/workspace_widgets.dart';
import 'my_team_api.dart';

/// Nhãn và màu chip trạng thái nhóm (quy tắc 15) – luôn có chữ, không chỉ màu.
(String, StatusTone, IconData) teamStatusChip(String status) => switch (status) {
      'ACTIVE' => ('Hoạt động', StatusTone.success, Icons.check_circle_outline),
      'NEEDS_REVIEW' => ('Cần xem lại', StatusTone.warning, Icons.error_outline),
      'FINALIZED' => ('Đã chốt', StatusTone.info, Icons.verified_outlined),
      'DISBANDED' => ('Đã giải tán', StatusTone.neutral, Icons.block),
      _ => ('Nháp', StatusTone.neutral, Icons.edit_note),
    };

/// S15 – Nhóm của tôi (MB-20, flutter-handoff S15). Tab cấp 1: tên nhóm + trạng thái, thanh điều kiện, cố vấn,
/// thay đổi chờ duyệt (EXE201 kế thừa), lối tắt Thành viên / Yêu cầu / Gợi ý / Sửa. Chưa có nhóm → EmptyState.
class MyTeamScreen extends ConsumerWidget {
  const MyTeamScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final team = ref.watch(myTeamDetailProvider);
    return RefreshIndicator(
      onRefresh: () async => refreshMyTeam(ref),
      child: team.when(
        loading: () => const SkeletonList(),
        error: (e, _) => ErrorState(error: e, onRetry: () => refreshMyTeam(ref)),
        data: (t) => t == null ? _NoTeam(isTeamFormation: ref.watch(shellPhaseProvider).isTeamFormation) : _TeamOverview(team: t),
      ),
    );
  }
}

class _NoTeam extends StatelessWidget {
  const _NoTeam({required this.isTeamFormation});

  final bool isTeamFormation;

  @override
  Widget build(BuildContext context) => ListView(
        children: [
          EmptyState(
            icon: Icons.group_add_outlined,
            title: 'Bạn chưa có nhóm',
            description: isTeamFormation ? 'Tạo nhóm mới hoặc xin vào nhóm ở Khám phá.' : 'Đợt lập nhóm đã đóng; Bộ môn sẽ ghép bạn vào nhóm.',
            action: isTeamFormation
                ? Wrap(
                    spacing: AppSpacing.s2,
                    alignment: WrapAlignment.center,
                    children: [
                      FilledButton(onPressed: () => context.push('/sv/nhom/tao'), child: const Text('Tạo nhóm')),
                      OutlinedButton(onPressed: () => context.go('/sv/kham-pha'), child: const Text('Khám phá')),
                    ],
                  )
                : null,
          ),
        ],
      );
}

class _TeamOverview extends StatelessWidget {
  const _TeamOverview({required this.team});

  final MyTeam team;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final (label, tone, icon) = teamStatusChip(team.status);
    final canEdit = team.isLeader && team.isOpen;
    return ListView(
      padding: const EdgeInsets.all(AppSpacing.s4),
      children: [
        Wrap(
          alignment: WrapAlignment.spaceBetween,
          crossAxisAlignment: WrapCrossAlignment.center,
          spacing: AppSpacing.s2,
          runSpacing: AppSpacing.s2,
          children: [
            Text(team.name, style: theme.textTheme.headlineSmall),
            StatusChip(label: label, tone: tone, icon: icon),
          ],
        ),
        if (team.projectName != null) ...[
          const SizedBox(height: AppSpacing.s2),
          Text(team.projectName!, style: theme.textTheme.titleMedium),
        ],
        if (team.domains.isNotEmpty) ...[const SizedBox(height: AppSpacing.s2), DomainChips(domains: team.domains)],
        const SizedBox(height: AppSpacing.s4),
        Card.outlined(
          child: Padding(
            padding: const EdgeInsets.all(AppSpacing.s4),
            child: TeamRequirementBar(
              memberCount: team.memberCount,
              maxMembers: team.maxSize,
              minMembers: team.minSize,
              specializationCount: team.specializationCount,
              minSpecializations: team.minSpecializations,
              full: true,
            ),
          ),
        ),
        const SizedBox(height: AppSpacing.s3),
        Card.outlined(
          child: Column(
            children: [
              ListTile(
                leading: const Icon(Icons.school_outlined),
                title: const Text('Giảng viên'),
                subtitle: Text(team.lecturerName ?? 'Chưa ghép'),
              ),
              ListTile(
                leading: const Icon(Icons.support_agent_outlined),
                title: const Text('Mentor'),
                subtitle: Text(team.mentorName ?? 'Ghép từ tuần 6'),
              ),
            ],
          ),
        ),
        if (team.hasPendingChanges) ...[
          const SizedBox(height: AppSpacing.s3),
          Card.outlined(
            child: ListTile(
              leading: const Icon(Icons.hourglass_top_outlined),
              title: const Text('Thay đổi chờ duyệt'),
              subtitle: const Text('Có hiệu lực khi Bộ môn duyệt yêu cầu đổi nhóm.'),
              trailing: const Icon(Icons.chevron_right),
              onTap: () => context.push('/sv/nhom/thanh-vien'),
            ),
          ),
        ],
        const SizedBox(height: AppSpacing.s3),
        Card.outlined(
          child: Column(
            children: [
              _Shortcut(icon: Icons.groups_outlined, label: 'Thành viên', detail: '${team.memberCount}/${team.maxSize}', path: '/sv/nhom/thanh-vien'),
              if (team.isLeader) const _Shortcut(icon: Icons.mark_email_unread_outlined, label: 'Yêu cầu và lời mời', path: '/sv/nhom/yeu-cau'),
              if (canEdit && !team.isFull) const _Shortcut(icon: Icons.person_search_outlined, label: 'Gợi ý thành viên', path: '/sv/nhom/goi-y'),
              if (canEdit) const _Shortcut(icon: Icons.edit_outlined, label: 'Sửa thông tin', path: '/sv/nhom/sua'),
            ],
          ),
        ),
        if (team.isLeader && !team.isOpen && !team.isInherited)
          Padding(
            padding: const EdgeInsets.only(top: AppSpacing.s3),
            child: Text('Nhóm đã khóa sau hạn chót, không sửa được.', style: theme.textTheme.bodyMedium),
          ),
      ],
    );
  }
}

class _Shortcut extends StatelessWidget {
  const _Shortcut({required this.icon, required this.label, required this.path, this.detail});

  final IconData icon;
  final String label;
  final String path;
  final String? detail;

  @override
  Widget build(BuildContext context) => ListTile(
        leading: Icon(icon),
        title: Text(label),
        trailing: Row(mainAxisSize: MainAxisSize.min, children: [if (detail != null) Text(detail!), const Icon(Icons.chevron_right)]),
        onTap: () => context.push(path),
      );
}
