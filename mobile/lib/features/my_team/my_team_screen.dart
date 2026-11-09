import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/phase.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../../shared/widgets/team_requirement_bar.dart';
import '../workspace/workspace_api.dart' show myWorkspacesProvider;
import '../workspace/workspace_widgets.dart';
import '../lecturers/paths.dart';
import 'leader_vote.dart';
import 'my_team_api.dart';
import 'project_card.dart';
import 'team_advisors.dart';
import '../meeting_schedule/meeting_schedule_api.dart' show teamScheduleProvider, teamWorkspaceIdProvider;

/// Nhãn và màu chip trạng thái nhóm (quy tắc 15) – luôn có chữ, không chỉ màu.
(String, StatusTone, IconData) teamStatusChip(String status) => switch (status) {
      'ACTIVE' => ('Hoạt động', StatusTone.success, Icons.check_circle_outline),
      'NEEDS_REVIEW' => ('Cần xem lại', StatusTone.warning, Icons.error_outline),
      'FINALIZED' => ('Đã chốt', StatusTone.info, Icons.verified_outlined),
      'DISBANDED' => ('Đã giải tán', StatusTone.neutral, Icons.block),
      _ => ('Chưa đủ người', StatusTone.neutral, Icons.edit_note),
    };

/// S15 – Nhóm của tôi (MB-20, flutter-handoff S15). Tab cấp 1: tên nhóm + trạng thái, thanh điều kiện, cố vấn,
/// thay đổi chờ duyệt (EXE201 kế thừa), lối tắt Thành viên / Yêu cầu / Gợi ý / Sửa. Chưa có nhóm → EmptyState.
class MyTeamScreen extends ConsumerWidget {
  const MyTeamScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final team = ref.watch(myTeamDetailProvider);
    return RefreshIndicator(
      onRefresh: () async {
        refreshMyTeam(ref);
        ref.invalidate(teamProjectProvider);
        ref.invalidate(teamLecturerProvider);
        ref.invalidate(teamMentorProvider);
        ref.invalidate(checkpointsDoneProvider);
      },
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
        LeaderVoteCard(teamId: team.id),
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
        const SizedBox(height: AppSpacing.s3),
        ProjectCard(team: team),
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
                trailing: const Icon(Icons.chevron_right),
                onTap: () => context.push(teamLecturerPath),
              ),
              ListTile(
                leading: const Icon(Icons.support_agent_outlined),
                title: const Text('Mentor'),
                subtitle: Text(team.mentorName ?? 'Ghép từ tuần 6'),
              ),
            ],
          ),
        ),
        AdvisorNotes(team: team),
        CheckpointCard(team: team),
        if (team.hasPendingChanges) ...[
          const SizedBox(height: AppSpacing.s3),
          Card.outlined(
            child: ListTile(
              leading: const Icon(Icons.hourglass_top_outlined),
              title: const Text('Thay đổi chờ duyệt'),
              subtitle: const Text('Có hiệu lực khi Bộ môn duyệt yêu cầu thay đổi.'),
              trailing: const Icon(Icons.chevron_right),
              onTap: () => context.push('/sv/yeu-cau-doi-nhom'),
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
              if (canEdit)
                _Shortcut(
                  icon: Icons.campaign_outlined,
                  label: 'Bài tuyển thành viên',
                  detail: team.recruitmentPost == null ? null : 'Đang mở',
                  path: '/sv/nhom/bai-tuyen',
                ),
              if (canEdit) const _Shortcut(icon: Icons.edit_outlined, label: 'Sửa thông tin', path: '/sv/nhom/sua'),
              if (team.isInherited && team.isLeader)
                const _Shortcut(
                  icon: Icons.published_with_changes_outlined,
                  label: 'Yêu cầu thay đổi',
                  path: '/sv/yeu-cau-doi-nhom',
                ),
              const _OpenProject(),
              if (team.courseCode == 'EXE201') _WeeklyMeeting(teamId: team.id),
              const _Shortcut(icon: Icons.history, label: 'Lịch sử hoạt động', path: '/sv/nhom/lich-su'),
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

/// Lối tắt sang tab Dự án (C01); ẩn khi nhóm chưa có dự án.
class _OpenProject extends ConsumerWidget {
  const _OpenProject();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (ref.watch(myWorkspacesProvider).value?.isNotEmpty != true) return const SizedBox.shrink();
    return ListTile(
      leading: const Icon(Icons.rocket_launch_outlined),
      title: const Text('Mở dự án'),
      trailing: const Icon(Icons.chevron_right),
      onTap: () => context.go('/sv/du-an'),
    );
  }
}

/// Lối vào C09 của nhóm EXE201: "Thứ Ba 09:15" khi đã chọn slot, chưa chọn thì "Chọn lịch họp"; ẩn khi nhóm chưa có dự án.
class _WeeklyMeeting extends ConsumerWidget {
  const _WeeklyMeeting({required this.teamId});

  final String teamId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final workspaceId = ref.watch(teamWorkspaceIdProvider(teamId)).value;
    if (workspaceId == null) return const SizedBox.shrink();
    final mySlot = ref.watch(teamScheduleProvider(workspaceId)).value?.mySlot;
    return _Shortcut(icon: Icons.event_repeat_outlined, label: 'Lịch họp hằng tuần', detail: mySlot?.label ?? 'Chọn lịch họp', path: '/sv/nhom/lich-hop');
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
