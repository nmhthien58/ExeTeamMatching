import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/phase.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/deadline_banner.dart';
import '../../shared/widgets/states.dart';
import '../suggestions/suggestions_api.dart';
import '../workspace/workspace_widgets.dart';

/// S30 – Đợt đăng ký đã khóa (MB-31, quy tắc 13, 14): không sửa nhóm được nữa.
/// Đã có nhóm → trạng thái chốt và lối vào dự án; chưa có nhóm → hệ thống sẽ ghép tự động.
class RoundClosedScreen extends ConsumerWidget {
  const RoundClosedScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final team = ref.watch(myTeamProvider);
    final closedAt = ref.watch(shellPhaseProvider).studentPhase?.teamFormationDeadlineAt;
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: const Text('Đợt đã khóa')),
      body: team.when(
        loading: () => const SkeletonList(count: 1),
        error: (e, _) => ErrorState(error: e, onRetry: () => ref.invalidate(myTeamProvider)),
        data: (t) => ListView(
          padding: const EdgeInsets.all(AppSpacing.s4),
          children: [
            // Banner hạn chót ở mức "đã khóa" (deadline đã qua).
            DeadlineBanner(deadline: closedAt ?? DateTime.now().subtract(const Duration(minutes: 1))),
            const SizedBox(height: AppSpacing.s6),
            if (t != null) ...[
              Icon(Icons.groups_outlined, size: 48, color: theme.colorScheme.primary),
              const SizedBox(height: AppSpacing.s3),
              Text(t.card.name, style: theme.textTheme.titleMedium, textAlign: TextAlign.center),
              const SizedBox(height: AppSpacing.s2),
              Center(
                child: t.card.status == 'FINALIZED'
                    ? const StatusChip(label: 'Đã chốt', tone: StatusTone.info)
                    : const StatusChip(label: 'Bộ môn đang xem lại', tone: StatusTone.warning, icon: Icons.error_outline),
              ),
              const SizedBox(height: AppSpacing.s3),
              Text('Không thêm, bớt hay đổi thành viên được nữa. Mọi việc tiếp theo nằm trong dự án.',
                  style: theme.textTheme.bodyMedium, textAlign: TextAlign.center),
              const SizedBox(height: AppSpacing.s6),
              FilledButton(onPressed: () => context.go('/sv/du-an'), child: const Text('Mở dự án')),
            ] else ...[
              Icon(Icons.auto_awesome, size: 48, color: theme.colorScheme.primary),
              const SizedBox(height: AppSpacing.s3),
              Text('Bạn sẽ được ghép nhóm tự động', style: theme.textTheme.titleMedium, textAlign: TextAlign.center),
              const SizedBox(height: AppSpacing.s2),
              Text(
                'Hệ thống đưa bạn vào nhóm còn thiếu người hoặc lập nhóm mới đủ sĩ số và số chuyên ngành theo quy định của Bộ môn. Bạn sẽ nhận thông báo khi có kết quả.',
                style: theme.textTheme.bodyMedium,
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: AppSpacing.s6),
              OutlinedButton(onPressed: () => context.go('/sv/thong-bao'), child: const Text('Xem thông báo')),
            ],
          ],
        ),
      ),
    );
  }
}
