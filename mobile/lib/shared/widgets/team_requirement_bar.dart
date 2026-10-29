import 'package:flutter/material.dart';

import '../../core/theme/app_tokens.dart';

/// Thanh điều kiện nhóm (DESIGN.md 6.1, quy tắc 1–2): 6 chấm thành viên kèm chữ "4/6 thành viên".
/// Dạng đầy đủ thêm 2 dòng kiểm tra "4–6 thành viên" và "Ít nhất 2 chuyên ngành".
class TeamRequirementBar extends StatelessWidget {
  const TeamRequirementBar({
    super.key,
    required this.memberCount,
    this.maxMembers = 6,
    this.minMembers = 4,
    this.specializationCount,
    this.minSpecializations = 2,
    this.full = false,
  });

  final int memberCount;
  final int maxMembers;
  final int minMembers;
  final int? specializationCount;
  final int minSpecializations;
  final bool full;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final dots = Semantics(
      label: '$memberCount trên $maxMembers thành viên',
      child: ExcludeSemantics(
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            for (var i = 0; i < maxMembers; i++)
              Container(
                width: 12,
                height: 12,
                margin: const EdgeInsets.only(right: AppSpacing.s1),
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: i < memberCount ? theme.colorScheme.primary : null,
                  border: i < memberCount ? null : Border.all(color: theme.colorScheme.outline),
                ),
              ),
            const SizedBox(width: AppSpacing.s2),
            // Flexible: chữ 130% trong thẻ hẹp (S01, S05, S08, S12) xuống dòng thay vì tràn (flutter-handoff §5).
            Flexible(child: Text('$memberCount/$maxMembers thành viên', style: theme.textTheme.bodyMedium)),
          ],
        ),
      ),
    );
    if (!full) return dots;

    final sizeOk = memberCount >= minMembers && memberCount <= maxMembers;
    final specOk = (specializationCount ?? 0) >= minSpecializations;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        dots,
        const SizedBox(height: AppSpacing.s2),
        _Check(ok: sizeOk, label: '$minMembers–$maxMembers thành viên'),
        _Check(ok: specOk, label: 'Ít nhất $minSpecializations chuyên ngành'),
      ],
    );
  }
}

class _Check extends StatelessWidget {
  const _Check({required this.ok, required this.label});

  final bool ok;
  final String label;

  @override
  Widget build(BuildContext context) {
    final colors = context.appColors;
    return Semantics(
      label: '$label: ${ok ? 'đạt' : 'chưa đạt'}',
      child: ExcludeSemantics(
        child: Padding(
          padding: const EdgeInsets.only(top: AppSpacing.s1),
          child: Row(
            children: [
              Icon(ok ? Icons.check_circle : Icons.error, size: 20, color: ok ? colors.success : colors.warning),
              const SizedBox(width: AppSpacing.s2),
              Expanded(child: Text(label, style: Theme.of(context).textTheme.bodyMedium)),
            ],
          ),
        ),
      ),
    );
  }
}
