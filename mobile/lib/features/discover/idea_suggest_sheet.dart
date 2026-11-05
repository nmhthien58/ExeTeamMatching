import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_tokens.dart';
import 'idea_ai_contract.dart';
import 'idea_suggest_view_model.dart';

/// Nhãn đánh dấu nội dung do AI đề xuất.
class AiSuggestionLabel extends StatelessWidget {
  const AiSuggestionLabel({super.key});

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(Icons.auto_awesome, size: 16, color: theme.colorScheme.primary),
        const SizedBox(width: AppSpacing.s1),
        Text('Gợi ý của AI', style: theme.textTheme.labelMedium?.copyWith(color: theme.colorScheme.primary)),
      ],
    );
  }
}

/// S09 – "Gợi ý bằng AI" (MB-56): sinh viên gõ vài câu, xem gợi ý rồi chọn dùng. Trả (tiêu đề, mô tả) hoặc null khi đóng.
class IdeaSuggestSheet extends ConsumerStatefulWidget {
  const IdeaSuggestSheet({super.key});

  @override
  ConsumerState<IdeaSuggestSheet> createState() => _IdeaSuggestSheetState();
}

class _IdeaSuggestSheetState extends ConsumerState<IdeaSuggestSheet> {
  final _description = TextEditingController();

  @override
  void dispose() {
    _description.dispose();
    super.dispose();
  }

  IdeaSuggestViewModel get _vm => ref.read(ideaSuggestViewModelProvider.notifier);

  Future<void> _accept() async {
    final fields = await _vm.accept();
    if (mounted) Navigator.pop(context, fields);
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final state = ref.watch(ideaSuggestViewModelProvider);
    final loading = state is IdeaSuggestLoading;
    return SafeArea(
      child: Padding(
        padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(context).bottom),
        child: ListView(
          shrinkWrap: true,
          padding: const EdgeInsets.all(AppSpacing.s4),
          children: [
            Text('Gợi ý bằng AI', style: theme.textTheme.titleLarge),
            const SizedBox(height: AppSpacing.s1),
            Text(
              'Mô tả ý tưởng trong vài câu. Không nhập tên, email hay MSSV.',
              style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            const SizedBox(height: AppSpacing.s3),
            TextField(
              controller: _description,
              enabled: !loading,
              maxLength: 500,
              minLines: 3,
              maxLines: 5,
              decoration: InputDecoration(
                labelText: 'Mô tả ý tưởng',
                alignLabelWithHint: true,
                errorText: state is IdeaSuggestIdle ? state.inputError : null,
              ),
            ),
            if (state is IdeaSuggestFailed)
              Semantics(
                liveRegion: true,
                child: Padding(
                  padding: const EdgeInsets.only(bottom: AppSpacing.s2),
                  child: Text(state.message, style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.error)),
                ),
              ),
            if (state is IdeaSuggestReady) _DraftPreview(draft: state.draft),
            const SizedBox(height: AppSpacing.s2),
            Row(
              children: [
                TextButton(onPressed: () => Navigator.pop(context), child: const Text('Tự nhập')),
                const Spacer(),
                if (state is IdeaSuggestReady) ...[
                  TextButton(onPressed: () => _vm.suggest(_description.text), child: const Text('Gợi ý khác')),
                  const SizedBox(width: AppSpacing.s2),
                  FilledButton(onPressed: _accept, child: const Text('Dùng gợi ý')),
                ] else
                  FilledButton(
                    onPressed: loading ? null : () => _vm.suggest(_description.text),
                    child: Text(loading ? 'Đang tạo…' : 'Tạo gợi ý'),
                  ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _DraftPreview extends StatelessWidget {
  const _DraftPreview({required this.draft});

  final IdeaDraft draft;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card.outlined(
      margin: const EdgeInsets.only(bottom: AppSpacing.s2),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.s3),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const AiSuggestionLabel(),
            const SizedBox(height: AppSpacing.s2),
            Text(draft.title, style: theme.textTheme.titleMedium),
            const SizedBox(height: AppSpacing.s1),
            Text('Vấn đề: ${draft.problem}', style: theme.textTheme.bodyMedium),
            Text('Giải pháp: ${draft.solution}', style: theme.textTheme.bodyMedium),
            Text('Khách hàng: ${draft.targetCustomer}', style: theme.textTheme.bodyMedium),
          ],
        ),
      ),
    );
  }
}
