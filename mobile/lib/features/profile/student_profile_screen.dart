import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_exception.dart';
import '../../core/phase.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../suggestions/suggestion_widgets.dart';
import '../suggestions/suggestions_api.dart';
import '../workspace/workspace_widgets.dart';
import 'profile_api.dart';
import 'skill_bars.dart';

/// S04 – Hồ sơ sinh viên khác (MB-13): kỹ năng, giới thiệu; không có SĐT (quy tắc 11, 16). Email chỉ hiện với người trong nhóm.
/// Trạng thái: trưởng nhóm xem (có nút mời) · người khác xem · đã mời · sinh viên đã có nhóm · đang tải · không tìm thấy.
class StudentProfileScreen extends ConsumerWidget {
  const StudentProfileScreen({super.key, required this.studentId});

  final String studentId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      appBar: AppBar(title: const Text('Hồ sơ sinh viên')),
      body: ref.watch(studentProfileProvider(studentId)).when(
            loading: () => const SkeletonList(count: 3),
            error: (e, _) => e is ApiException && e.status == 404
                ? const EmptyState(icon: Icons.person_off_outlined, title: 'Không tìm thấy sinh viên', description: 'Sinh viên này không còn trong danh sách.')
                : ErrorState(error: e, onRetry: () => ref.invalidate(studentProfileProvider(studentId))),
            data: (student) => _Details(student: student),
          ),
    );
  }
}

class _Details extends ConsumerWidget {
  const _Details({required this.student});

  final StudentProfile student;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final team = ref.watch(myTeamProvider).value;
    final teammate = team?.members.where((m) => m.id == student.id).firstOrNull;
    // Team trả email của đồng đội; Academic chưa biết quan hệ nhóm nên có thể để trống (quy tắc 11).
    final email = student.email ?? teammate?.email;
    final subtitle = [student.studentCode, student.majorName, student.specialization, student.classCode].whereType<String>().where((s) => s.isNotEmpty).join(' · ');

    return ListView(
      padding: const EdgeInsets.all(AppSpacing.s4),
      children: [
        Text(student.fullName, style: theme.textTheme.titleLarge),
        const SizedBox(height: AppSpacing.s1),
        Text(subtitle, style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
        if (email != null) ...[const SizedBox(height: AppSpacing.s1), Text(email, style: theme.textTheme.bodyMedium)],
        const SizedBox(height: AppSpacing.s3),
        if (teammate != null) const StatusChip(label: 'Cùng nhóm với bạn', tone: StatusTone.success, icon: Icons.groups_outlined) else _InviteAction(student: student, team: team),
        const SizedBox(height: AppSpacing.s4),
        Text('Kỹ năng', style: theme.textTheme.titleMedium),
        const SizedBox(height: AppSpacing.s2),
        SkillBars(levels: student.skills),
        const SizedBox(height: AppSpacing.s4),
        Text('Giới thiệu', style: theme.textTheme.titleMedium),
        const SizedBox(height: AppSpacing.s2),
        Text(student.description?.isNotEmpty == true ? student.description! : 'Chưa có giới thiệu.', style: theme.textTheme.bodyMedium),
      ],
    );
  }
}

class _InviteAction extends ConsumerStatefulWidget {
  const _InviteAction({required this.student, required this.team});

  final StudentProfile student;
  final TeamDetail? team;

  @override
  ConsumerState<_InviteAction> createState() => _InviteActionState();
}

class _InviteActionState extends ConsumerState<_InviteAction> {
  bool _sent = false;

  Future<void> _invite(TeamDetail team) async {
    final student = widget.student;
    final sent = await showInviteSheet(context, teamId: team.card.id, student: StudentRef(id: student.id, studentCode: student.studentCode, fullName: student.fullName));
    if (!sent || !mounted) return;
    setState(() => _sent = true);
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(const SnackBar(content: Text('Đã gửi lời mời')));
  }

  @override
  Widget build(BuildContext context) {
    final team = widget.team;
    if (widget.student.hasTeam) return const StatusChip(label: 'Đã có nhóm', tone: StatusTone.neutral, icon: Icons.group_outlined);
    if (team == null || !team.isLeader || !ref.watch(shellPhaseProvider).isTeamFormation) return const SizedBox.shrink();

    final invited = ref.watch(invitedStudentIdsProvider(team.card.id));
    if (_sent || (invited.value?.contains(widget.student.id) ?? false)) {
      return const StatusChip(label: 'Đã mời', tone: StatusTone.info, icon: Icons.send_outlined);
    }
    if (invited.isLoading) return const SizedBox.shrink();
    if (team.isFull) return Text('Nhóm đã đủ ${team.card.maxSize} người', style: Theme.of(context).textTheme.bodyMedium);
    return FilledButton.icon(onPressed: () => _invite(team), icon: const Icon(Icons.person_add_alt_1_outlined), label: const Text('Gửi lời mời'));
  }
}
