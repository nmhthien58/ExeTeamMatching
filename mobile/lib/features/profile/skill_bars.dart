import 'package:flutter/material.dart';

import '../../core/theme/app_tokens.dart';
import 'profile_api.dart';

/// Biểu đồ kỹ năng: 5 hàng × 5 ô (flutter-handoff.md mục 2). [highlight]: kỹ năng cần nhấn mạnh.
class SkillBars extends StatelessWidget {
  const SkillBars({super.key, required this.levels, this.highlight = const {}});

  final Map<String, int> levels;
  final Set<String> highlight;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    if (levels.isEmpty) {
      return Text('Chưa đánh giá kỹ năng.', style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant));
    }
    return Column(
      children: [
        for (final MapEntry(key: skill, value: label) in skillLabels.entries)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: AppSpacing.s1),
            child: _SkillRow(label: label, level: levels[skill] ?? 0, emphasized: highlight.contains(skill)),
          ),
      ],
    );
  }
}

class _SkillRow extends StatelessWidget {
  const _SkillRow({required this.label, required this.level, required this.emphasized});

  final String label;
  final int level;
  final bool emphasized;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Semantics(
      container: true,
      label: '$label, $level trên 5',
      excludeSemantics: true,
      child: Row(
        children: [
          Expanded(
            flex: 2,
            child: Text(label, style: emphasized ? theme.textTheme.bodyMedium?.copyWith(fontWeight: FontWeight.w600) : theme.textTheme.bodyMedium),
          ),
          const SizedBox(width: AppSpacing.s2),
          Expanded(
            flex: 3,
            child: Row(
              children: [
                for (var n = 1; n <= 5; n++)
                  Expanded(
                    child: Container(
                      height: 12,
                      margin: const EdgeInsets.only(right: AppSpacing.s1),
                      decoration: BoxDecoration(
                        color: n <= level ? theme.colorScheme.primary : context.appColors.neutralContainer,
                        borderRadius: BorderRadius.circular(AppRadius.sm),
                      ),
                    ),
                  ),
              ],
            ),
          ),
          Text('$level/5', style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
        ],
      ),
    );
  }
}
