import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_tokens.dart';
import 'brand_header_view_model.dart';

/// Phần chào của trang chủ, ngay dưới [BrandAppBar]: lời chào, ngày + tuần học, vạch nhấn tiến độ học kỳ
/// (đổi 05/11/2026 so với Lab 2, xem docs/design-traceability.md).
class BrandHeader extends ConsumerWidget {
  const BrandHeader({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final data = ref.watch(brandHeaderProvider);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(data.greeting, style: theme.textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w700)),
        const SizedBox(height: AppSpacing.s1),
        Text(data.dateLine, style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
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
