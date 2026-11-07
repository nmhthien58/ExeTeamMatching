import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../workspace/workspace_widgets.dart';
import 'suggestion_widgets.dart';
import 'suggestions_api.dart';
import 'suggestions_view_model.dart';

/// S18 – Gợi ý thành viên (MB-23, flutter-handoff F2): "Nhóm bạn đang thiếu: …", thẻ sinh viên do Matching gợi ý kèm lý do,
/// nút phụ "Gửi lời mời" (chỉ trưởng nhóm). Trạng thái: có gợi ý · không có gợi ý · đã mời · đang tải; nhóm đủ 6.
class SuggestedMembersScreen extends ConsumerStatefulWidget {
  const SuggestedMembersScreen({super.key});

  @override
  ConsumerState<SuggestedMembersScreen> createState() => _SuggestedMembersScreenState();
}

class _SuggestedMembersScreenState extends ConsumerState<SuggestedMembersScreen> {
  SuggestedMembersViewModel get _vm => ref.read(suggestedMembersViewModelProvider.notifier);

  Future<void> _invite(TeamDetail team, StudentRef student) async {
    if (!await showInviteSheet(context, teamId: team.card.id, student: student) || !mounted) return;
    _vm.markInvited(student.id);
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(const SnackBar(content: Text('Đã gửi lời mời')));
  }

  @override
  Widget build(BuildContext context) {
    final team = ref.watch(myTeamProvider);
    ref.watch(suggestedMembersViewModelProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Gợi ý thành viên')),
      body: team.when(
        loading: () => const SkeletonList(),
        error: (e, _) => ErrorState(error: e, onRetry: _vm.retryTeam),
        data: (t) => t == null
            ? const EmptyState(icon: Icons.group_off_outlined, title: 'Bạn chưa có nhóm', description: 'Tạo nhóm hoặc xin vào một nhóm trước.')
            : _suggestions(t),
      ),
    );
  }

  Widget _suggestions(TeamDetail team) {
    final theme = Theme.of(context);
    final suggestions = ref.watch(memberSuggestionsProvider(team.card.id));
    final header = <Widget>[
      if (team.isFull) Padding(padding: const EdgeInsets.only(bottom: AppSpacing.s3), child: StatusChip(label: 'Nhóm đã đủ ${team.card.maxSize} người', tone: StatusTone.success)),
      if (!team.isLeader)
        Padding(padding: const EdgeInsets.only(bottom: AppSpacing.s3), child: Text('Chỉ trưởng nhóm gửi được lời mời.', style: theme.textTheme.bodyMedium)),
      if ((suggestions.value?.missingSpecializations ?? const []).isNotEmpty)
        Padding(
          padding: const EdgeInsets.only(bottom: AppSpacing.s3),
          child: Text('Nhóm bạn đang thiếu: ${suggestions.value!.missingSpecializations.map((s) => s.name).join(', ')}', style: theme.textTheme.titleMedium),
        ),
    ];

    return RefreshIndicator(
      onRefresh: () async => _vm.refresh(team.card.id),
      child: suggestions.when(
        loading: () => ListView(padding: const EdgeInsets.all(AppSpacing.s4), children: [...header, const SkeletonList(count: 3)]),
        error: (e, _) => ErrorState(error: e, onRetry: () => _vm.refresh(team.card.id)),
        data: (data) => ListView(
          padding: const EdgeInsets.all(AppSpacing.s4),
          children: [
            ...header,
            if (data.items.isEmpty)
              EmptyState(
                icon: Icons.person_search_outlined,
                title: 'Chưa có gợi ý phù hợp',
                description: 'Tìm theo tên hoặc MSSV để mời người bạn đã biết.',
                action: team.isLeader && !team.isFull ? FilledButton(onPressed: () => context.push('/sv/nhom/moi'), child: const Text('Tìm sinh viên')) : null,
              )
            else
              for (final item in data.items)
                Padding(
                  padding: const EdgeInsets.only(bottom: AppSpacing.s3),
                  child: StudentCard(
                    student: item.student,
                    skills: item.skills,
                    details: [
                      const SizedBox(height: AppSpacing.s2),
                      FitChip(score: item.score),
                      Reasons(reasons: item.reasons),
                    ],
                    action: item.alreadyInvited || ref.read(suggestedMembersViewModelProvider).contains(item.student.id)
                        ? const Padding(padding: EdgeInsets.only(top: AppSpacing.s2), child: StatusChip(label: 'Đã mời', tone: StatusTone.warning))
                        : team.isLeader
                            ? Padding(
                                padding: const EdgeInsets.only(top: AppSpacing.s2),
                                child: OutlinedButton.icon(
                                  onPressed: team.isFull ? null : () => _invite(team, item.student),
                                  icon: const Icon(Icons.person_add_alt, size: 20),
                                  label: const Text('Gửi lời mời'),
                                ),
                              )
                            : null,
                  ),
                ),
            if (team.isLeader && !team.isFull && data.items.isNotEmpty)
              TextButton(onPressed: () => context.push('/sv/nhom/moi'), child: const Text('Tìm theo tên hoặc MSSV')),
          ],
        ),
      ),
    );
  }
}
