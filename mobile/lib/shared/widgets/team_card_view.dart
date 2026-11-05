import 'package:flutter/material.dart';

import '../../core/theme/app_tokens.dart';

/// Thẻ nhóm dùng chung (bản 6, 06/11/2026 – MB-64) cho S01, S05, S07: ô chữ tắt dải màu, tên + dự án, chip "Phù hợp"
/// (tuỳ chọn) ở góc phải, chip domain chỉ ghi **tên** (không mã D01…), thanh chỗ trống chạy dần, dòng "n/6 thành viên ·
/// còn k chỗ", phần thêm của từng màn ([extra]: lý do gợi ý, chuyên ngành đang thiếu).
class TeamCardView extends StatelessWidget {
  const TeamCardView({
    super.key,
    required this.seed,
    required this.name,
    this.projectName,
    this.domains = const [],
    required this.memberCount,
    required this.maxSize,
    this.minSize = 4,
    this.badge,
    this.highlighted = false,
    this.extra = const [],
    required this.onTap,
  });

  /// Chuỗi cố định của nhóm (id) để chọn màu ô chữ tắt.
  final String seed;
  final String name;
  final String? projectName;

  /// Tên domain, tối đa 2 chip rồi "+n".
  final List<String> domains;
  final int memberCount;
  final int maxSize;

  /// Số người tối thiểu (quy tắc 1): chỗ trống còn thiếu để đủ mức này viền cam.
  final int minSize;
  final Widget? badge;

  /// Nhóm được đề xuất: viền cam nhạt.
  final bool highlighted;
  final List<Widget> extra;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final open = (maxSize - memberCount).clamp(0, maxSize);
    return Card(
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.lg),
        side: highlighted ? BorderSide(color: context.appColors.primaryBorder) : BorderSide.none,
      ),
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.s4),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  TeamMonogram(seed: seed, name: name),
                  const SizedBox(width: AppSpacing.s3),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        // Nhãn đọc màn hình cho cả thẻ (MB-58): TalkBack và ARTEMIS tìm thẻ theo tên nhóm.
                        Semantics(
                          header: true,
                          label: '$name, $memberCount trên $maxSize thành viên',
                          child: ExcludeSemantics(
                            child: Text(name, style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700), maxLines: 2, overflow: TextOverflow.ellipsis),
                          ),
                        ),
                        if (projectName != null)
                          Text(
                            projectName!,
                            style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                      ],
                    ),
                  ),
                  if (badge != null) ...[const SizedBox(width: AppSpacing.s2), badge!],
                ],
              ),
              if (domains.isNotEmpty) ...[
                const SizedBox(height: AppSpacing.s3),
                Wrap(
                  spacing: AppSpacing.s2,
                  runSpacing: AppSpacing.s2,
                  children: [
                    for (final d in domains.take(2)) _InfoPill(label: d),
                    if (domains.length > 2) _InfoPill(label: '+${domains.length - 2}'),
                  ],
                ),
              ],
              ...extra,
              // Chân thẻ như thẻ web (FE-52): vạch kẻ mờ, thanh chỗ trống + số chỗ, "Xem nhóm →".
              const SizedBox(height: AppSpacing.s3),
              Divider(color: theme.colorScheme.outlineVariant.withValues(alpha: .7)),
              const SizedBox(height: AppSpacing.s3),
              Row(
                children: [
                  Expanded(
                    child: Semantics(
                      label: '$memberCount trên $maxSize thành viên, còn $open chỗ',
                      child: ExcludeSemantics(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            MemberSlots(memberCount: memberCount, maxSize: maxSize, minSize: minSize),
                            const SizedBox(height: AppSpacing.s1 + 2),
                            Text('$memberCount/$maxSize thành viên · còn $open chỗ', style: theme.textTheme.bodySmall?.copyWith(fontWeight: FontWeight.w500)),
                          ],
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(width: AppSpacing.s4),
                  ExcludeSemantics(
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text('Xem nhóm', style: theme.textTheme.labelSmall?.copyWith(color: context.appColors.onInfoContainer)),
                        const SizedBox(width: AppSpacing.s1),
                        Icon(Icons.arrow_forward, size: 18, color: context.appColors.onInfoContainer),
                      ],
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Ô chữ tắt của nhóm: hai chữ đầu của tên (bỏ chữ "Nhóm"), nền dải một trong ba màu thương hiệu FPT (trang trí).
class TeamMonogram extends StatelessWidget {
  const TeamMonogram({super.key, required this.seed, required this.name, this.size = 44});

  final String seed;
  final String name;
  final double size;

  static String initials(String name) {
    final words = name.trim().split(RegExp(r'\s+')).where((w) => w.isNotEmpty && w.toLowerCase() != 'nhóm').toList();
    if (words.isEmpty) return '?';
    final first = words.first.characters.first;
    final second = words.length > 1 ? words[1].characters.first : '';
    return (first + second).toUpperCase();
  }

  @override
  Widget build(BuildContext context) {
    const all = AppGradients.monograms;
    // Băm FNV-1a (ổn định giữa các lần chạy, không như String.hashCode) để các nhóm cạnh nhau ít trùng màu.
    final hash = seed.codeUnits.fold<int>(0x811c9dc5, (h, c) => ((h ^ c) * 0x01000193) & 0xffffffff);
    final colors = all[hash % all.length];
    return ExcludeSemantics(
      child: Container(
        width: size,
        height: size,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(AppRadius.md + 2),
          gradient: LinearGradient(begin: Alignment.topLeft, end: Alignment.bottomRight, colors: colors),
        ),
        child: Text(
          initials(name),
          style: Theme.of(context).textTheme.titleMedium?.copyWith(color: Theme.of(context).colorScheme.onPrimary, fontWeight: FontWeight.w800),
        ),
      ),
    );
  }
}

class _InfoPill extends StatelessWidget {
  const _InfoPill({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s3, vertical: AppSpacing.s1),
        decoration: BoxDecoration(color: context.appColors.infoContainer, borderRadius: BorderRadius.circular(AppRadius.full)),
        child: Text(
          label,
          style: Theme.of(context).textTheme.labelLarge?.copyWith(color: context.appColors.onInfoContainer, fontWeight: FontWeight.w600),
        ),
      );
}

/// Sáu chỗ của nhóm thay cho thanh ngang (06/11/2026): chỗ đã có là vòng cam có hình người, xếp chồng nhẹ như nhóm avatar;
/// chỗ trống còn thiếu để đủ [minSize] viền cam có "+", chỗ trống còn lại viền xám. Các ô bật ra lần lượt khi hiện
/// (tắt khi giảm chuyển động).
class MemberSlots extends StatelessWidget {
  const MemberSlots({super.key, required this.memberCount, required this.maxSize, this.minSize = 4, this.size = 30});

  final int memberCount;
  final int maxSize;
  final int minSize;
  final double size;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final overlap = size * 0.22;
    return SizedBox(
      height: size,
      width: maxSize * (size - overlap) + overlap,
      child: Stack(
        children: [
          for (var i = maxSize - 1; i >= 0; i--)
            Positioned(
              left: i * (size - overlap),
              child: TweenAnimationBuilder<double>(
                tween: Tween(begin: 0, end: 1),
                duration: AppMotion.of(context, Duration(milliseconds: 260 + i * 70)),
                curve: Curves.easeOutBack,
                builder: (context, t, child) => Transform.scale(scale: t.clamp(0, 1.2), child: child),
                child: _Slot(
                  size: size,
                  filled: i < memberCount,
                  needed: i >= memberCount && i < minSize,
                  surface: scheme.surface,
                ),
              ),
            ),
        ],
      ),
    );
  }
}

class _Slot extends StatelessWidget {
  const _Slot({required this.size, required this.filled, required this.needed, required this.surface});

  final double size;
  final bool filled;
  final bool needed;
  final Color surface;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    if (filled) {
      return Container(
        width: size,
        height: size,
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          gradient: LinearGradient(begin: Alignment.topLeft, end: Alignment.bottomRight, colors: AppGradients.monograms.first),
          border: Border.all(color: surface, width: 2),
        ),
        child: Icon(Icons.person, size: size * 0.6, color: scheme.onPrimary),
      );
    }
    final color = needed ? scheme.primary : scheme.outline.withValues(alpha: .55);
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: needed ? scheme.primaryContainer : surface,
        border: Border.all(color: color, width: 1.5),
      ),
      child: Icon(Icons.add, size: size * 0.5, color: color),
    );
  }
}
