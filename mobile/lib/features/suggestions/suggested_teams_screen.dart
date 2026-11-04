import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import 'suggestion_widgets.dart';
import 'suggestions_api.dart';
import 'suggestions_view_model.dart';

/// S07 – Nhóm gợi ý cho bạn (MB-15): danh sách từ Matching kèm mức phù hợp và lý do.
/// Trạng thái: có gợi ý · hồ sơ chưa đủ dữ liệu để gợi ý · đang tải · lỗi.
class SuggestedTeamsScreen extends ConsumerWidget {
  const SuggestedTeamsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final suggestions = ref.watch(suggestedTeamsProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Gợi ý cho bạn')),
      body: suggestions.when(
        loading: () => const SkeletonList(),
        error: (e, _) => ErrorState(error: e, onRetry: () => ref.invalidate(teamsForMeProvider)),
        data: (items) => items.isEmpty
            ? EmptyState(
                icon: Icons.person_search_outlined,
                title: 'Chưa đủ dữ liệu để gợi ý',
                description: 'Đánh giá đủ 5 kỹ năng trong hồ sơ để hệ thống tìm nhóm cần đúng thế mạnh của bạn.',
                action: FilledButton(onPressed: () => context.go('/sv/ho-so'), child: const Text('Hoàn thiện hồ sơ')),
              )
            : RefreshIndicator(
                onRefresh: () async => ref.invalidate(teamsForMeProvider),
                child: ListView.separated(
                  padding: const EdgeInsets.all(AppSpacing.s4),
                  itemCount: items.length,
                  separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.s3),
                  itemBuilder: (_, i) => TeamSuggestionCard(
                    suggestion: items[i],
                    onTap: () => context.push('/sv/kham-pha/nhom/${items[i].team.id}'),
                  ),
                ),
              ),
      ),
    );
  }
}
