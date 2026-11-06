import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../../shared/widgets/team_card_view.dart';
import '../workspace/workspace_widgets.dart';
import 'suggestions_api.dart';
import 'suggestions_view_model.dart';

/// Mức phù hợp (bản 6, MB-64): "Rất phù hợp" nền cam chữ trắng, "Phù hợp" nền cam nhạt chữ cam đậm, "Có thể phù hợp"
/// nền xám nhạt chữ phụ – cùng một màu nhấn thay cho ba màu xanh lá / xanh dương / xám của bản 5.
class FitChip extends StatelessWidget {
  const FitChip({super.key, required this.score});

  final double score;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final (bg, fg) = score >= 0.6
        ? (scheme.primary, scheme.onPrimary)
        : score >= 0.3
            ? (scheme.primaryContainer, scheme.onPrimaryContainer)
            : (context.appColors.neutralContainer, scheme.onSurfaceVariant);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s3, vertical: AppSpacing.s1 + 2),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(AppRadius.full)),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.auto_awesome, size: 16, color: fg),
          const SizedBox(width: AppSpacing.s1),
          Text(fitLabel(score), style: theme.textTheme.labelLarge?.copyWith(color: fg, fontWeight: FontWeight.w600)),
        ],
      ),
    );
  }
}

/// Lý do gợi ý do Matching trả về – mỗi dòng một ý.
class Reasons extends StatelessWidget {
  const Reasons({super.key, required this.reasons});

  final List<String> reasons;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (final r in reasons)
          Padding(
            padding: const EdgeInsets.only(top: AppSpacing.s1),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Icon(Icons.check, size: 20, color: context.appColors.success),
                const SizedBox(width: AppSpacing.s2),
                Expanded(child: Text(r, style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant))),
              ],
            ),
          ),
      ],
    );
  }
}

/// Tối đa 2 chip domain rồi "+n".
class DomainChips extends StatelessWidget {
  const DomainChips({super.key, required this.domains});

  final List<CatalogRef> domains;

  @override
  Widget build(BuildContext context) => Wrap(
        spacing: AppSpacing.s2,
        runSpacing: AppSpacing.s2,
        children: [
          for (final d in domains.take(2)) Chip(label: Text(d.name)), // chỉ tên, không mã D01… (06/11/2026)
          if (domains.length > 2) Chip(label: Text('+${domains.length - 2}')),
        ],
      );
}

/// Thẻ nhóm do người ngoài nhóm xem (S07): không chip trạng thái, dòng thành viên có số chỗ còn lại.
class TeamSuggestionCard extends StatelessWidget {
  const TeamSuggestionCard({super.key, required this.suggestion, required this.onTap});

  final TeamSuggestion suggestion;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final team = suggestion.team;
    return TeamCardView(
      seed: team.id,
      name: team.name,
      projectName: team.projectName,
      domains: [for (final d in team.domains) d.name],
      memberCount: team.memberCount,
      maxSize: team.maxSize,
      badge: FitChip(score: suggestion.score),
      highlighted: suggestion.score >= 0.6,
      onTap: onTap,
      extra: [
        if (team.missingSpecializations.isNotEmpty) ...[
          const SizedBox(height: AppSpacing.s2),
          Text('Đang thiếu: ${team.missingSpecializations.map((s) => s.name).join(', ')}', style: theme.textTheme.bodyMedium),
        ],
        if (suggestion.reasons.isNotEmpty) ...[const SizedBox(height: AppSpacing.s1), Reasons(reasons: suggestion.reasons)],
      ],
    );
  }
}

/// Thẻ sinh viên (DESIGN.md 6.1): avatar 40, họ tên, "MSSV · chuyên ngành" (không cắt chuyên ngành), tối đa 3 chip kỹ năng,
/// nút ở cuối căn phải.
class StudentCard extends StatelessWidget {
  const StudentCard({super.key, required this.student, this.skills = const [], this.details = const [], this.action});

  final StudentRef student;
  final List<String> skills;
  /// Phần thêm dưới thẻ (mức phù hợp, lý do gợi ý).
  final List<Widget> details;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card.outlined(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.s4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Avatar(name: student.fullName),
                const SizedBox(width: AppSpacing.s3),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(student.fullName, style: theme.textTheme.titleMedium, maxLines: 2, overflow: TextOverflow.ellipsis),
                      Text(
                        [student.studentCode, ?student.specialization?.name].join(' · '),
                        style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                        maxLines: 2,
                      ),
                    ],
                  ),
                ),
              ],
            ),
            if (skills.isNotEmpty) ...[
              const SizedBox(height: AppSpacing.s2),
              Wrap(spacing: AppSpacing.s2, runSpacing: AppSpacing.s2, children: [for (final s in skills.take(3)) Chip(label: Text(s))]),
            ],
            ...details,
            if (action != null) Align(alignment: Alignment.centerRight, child: action),
          ],
        ),
      ),
    );
  }
}


/// Bottom sheet "Gửi lời mời" kèm lời nhắn (S18, S19). Trả `true` khi đã gửi; lỗi nghiệp vụ hiện đúng câu,
/// lời nhắn đã nhập giữ nguyên.
Future<bool> showInviteSheet(BuildContext context, {required String teamId, required StudentRef student}) async {
  final sent = await showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    showDragHandle: true,
    builder: (_) => _InviteSheet(teamId: teamId, student: student),
  );
  return sent ?? false;
}

class _InviteSheet extends ConsumerStatefulWidget {
  const _InviteSheet({required this.teamId, required this.student});

  final String teamId;
  final StudentRef student;

  @override
  ConsumerState<_InviteSheet> createState() => _InviteSheetState();
}

class _InviteSheetState extends ConsumerState<_InviteSheet> {
  final _message = TextEditingController();

  InviteKey get _key => (teamId: widget.teamId, studentId: widget.student.id);

  @override
  void dispose() {
    _message.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final sent = await ref.read(inviteViewModelProvider(_key).notifier).send(_message.text);
    if (sent && mounted) Navigator.pop(context, true);
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final invite = ref.watch(inviteViewModelProvider(_key));
    return Padding(
      padding: EdgeInsets.fromLTRB(AppSpacing.s6, 0, AppSpacing.s6, AppSpacing.s4 + MediaQuery.viewInsetsOf(context).bottom),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Gửi lời mời?', style: theme.textTheme.titleLarge),
          const SizedBox(height: AppSpacing.s2),
          Text('${widget.student.fullName} có 72 giờ để chấp nhận lời mời.', style: theme.textTheme.bodyMedium),
          const SizedBox(height: AppSpacing.s4),
          if (invite.error != null) ...[ErrorBanner(message: invite.error!), const SizedBox(height: AppSpacing.s3)],
          TextField(controller: _message, minLines: 2, maxLines: 4, maxLength: 500, decoration: const InputDecoration(labelText: 'Lời nhắn (không bắt buộc)')),
          const SizedBox(height: AppSpacing.s3),
          LoadingButton(label: 'Gửi lời mời', loading: invite.sending, onPressed: _send),
        ],
      ),
    );
  }
}
