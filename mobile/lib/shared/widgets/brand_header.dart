import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_tokens.dart';
import 'app_logo.dart';
import 'brand_header_view_model.dart';

/// Header trang chủ: logo + tên ứng dụng + môn / học kỳ, chip giai đoạn, lời chào, ngày + tuần,
/// vạch nhấn tiến độ học kỳ (đổi 05/11/2026 so với Lab 2, xem docs/design-traceability.md).
class BrandHeader extends ConsumerWidget {
  const BrandHeader({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final muted = theme.colorScheme.onSurfaceVariant;
    final data = ref.watch(brandHeaderProvider);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            const AppLogo(size: 44),
            const SizedBox(width: AppSpacing.s3),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'EXE Team Matching',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700),
                  ),
                  if (data.context != null)
                    Text(data.context!, maxLines: 1, overflow: TextOverflow.ellipsis, style: theme.textTheme.bodySmall?.copyWith(color: muted)),
                ],
              ),
            ),
            if (data.chip != null) ...[
              const SizedBox(width: AppSpacing.s2),
              _PhaseChip(label: data.chip!),
            ],
          ],
        ),
        const SizedBox(height: AppSpacing.s6),
        Text(data.greeting, style: theme.textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w700)),
        const SizedBox(height: AppSpacing.s1),
        Text(data.dateLine, style: theme.textTheme.bodyMedium?.copyWith(color: muted)),
        if (data.semesterProgress != null) ...[
          const SizedBox(height: AppSpacing.s3),
          Semantics(
            label: data.progressLabel,
            child: ExcludeSemantics(
              child: ClipRRect(
                borderRadius: BorderRadius.circular(AppRadius.full),
                child: LinearProgressIndicator(
                  value: data.semesterProgress,
                  minHeight: 3,
                  backgroundColor: context.appColors.neutralContainer,
                  color: context.appColors.brandOrange,
                ),
              ),
            ),
          ),
        ],
      ],
    );
  }
}

class _PhaseChip extends StatelessWidget {
  const _PhaseChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s3, vertical: AppSpacing.s2),
      decoration: BoxDecoration(
        color: theme.colorScheme.surface,
        borderRadius: BorderRadius.circular(AppRadius.full),
        border: Border.all(color: theme.colorScheme.outlineVariant),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.event_outlined, size: 18, color: theme.colorScheme.primary),
          const SizedBox(width: AppSpacing.s1),
          Flexible(child: Text(label, style: theme.textTheme.labelLarge, overflow: TextOverflow.ellipsis)),
        ],
      ),
    );
  }
}
