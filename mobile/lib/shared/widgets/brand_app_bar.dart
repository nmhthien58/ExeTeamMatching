import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_tokens.dart';
import 'app_logo.dart';
import 'brand_header_view_model.dart';
import 'profile_avatar_button.dart';

/// AppBar chung cho mọi tab cấp 1 (đổi 05/11/2026 so với Lab 2, xem docs/design-traceability.md):
/// logo ứng dụng, tên màn, dòng "EXE101 · Học kỳ SP90", nút riêng của màn ở bên phải.
/// Màn được mở bằng `push` (có thể quay lại) thì hiện nút Back thay cho logo, nên dùng lại được ở màn cấp 2.
/// Tab cấp 1 có avatar ở góc phải để mở hồ sơ (MB-62 – tab Hồ sơ của sinh viên đã nhường chỗ cho Tin nhắn).
class BrandAppBar extends ConsumerWidget implements PreferredSizeWidget {
  const BrandAppBar({super.key, required this.title, this.actions = const [], this.showPhaseChip = false, this.bottom});

  final String title;
  final List<Widget> actions;

  /// Trang chủ hiện chip giai đoạn ("Còn 20 ngày") ở góc phải.
  final bool showPhaseChip;
  final PreferredSizeWidget? bottom;

  static const _height = 64.0;

  @override
  Size get preferredSize => Size.fromHeight(_height + (bottom?.preferredSize.height ?? 0));

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final data = ref.watch(brandHeaderProvider);
    final canPop = ModalRoute.of(context)?.canPop ?? false;
    return AppBar(
      toolbarHeight: _height,
      automaticallyImplyLeading: false,
      titleSpacing: canPop ? 0 : AppSpacing.s4,
      leading: canPop ? const BackButton() : null,
      title: Row(
        children: [
          if (!canPop) ...[const AppLogo(size: 40), const SizedBox(width: AppSpacing.s3)],
          Expanded(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, maxLines: 1, overflow: TextOverflow.ellipsis, style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700)),
                if (data.context != null)
                  Text(
                    data.context!,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                  ),
              ],
            ),
          ),
        ],
      ),
      actions: [
        if (showPhaseChip && data.chip != null) Padding(padding: const EdgeInsets.only(left: AppSpacing.s2), child: PhaseChip(label: data.chip!)),
        ...actions,
        if (!canPop) const ProfileAvatarButton(),
        const SizedBox(width: AppSpacing.s3),
      ],
      bottom: bottom,
    );
  }
}

/// Chip giai đoạn ở góc phải header trang chủ.
class PhaseChip extends StatelessWidget {
  const PhaseChip({super.key, required this.label});

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
          Text(label, style: theme.textTheme.labelLarge),
        ],
      ),
    );
  }
}
