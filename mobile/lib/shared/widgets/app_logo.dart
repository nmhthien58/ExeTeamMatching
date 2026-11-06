import 'package:flutter/material.dart';

import '../../core/theme/app_tokens.dart';

/// Logo ứng dụng: ô bo góc nền FPT Orange (màu trang trí, DESIGN.md 2.1) với icon nhóm của A01.
/// Chỉ để trang trí nên không có nhãn đọc màn hình; tên ứng dụng đứng cạnh đã đủ nghĩa.
class AppLogo extends StatelessWidget {
  const AppLogo({super.key, this.size = 64});

  final double size;

  @override
  Widget build(BuildContext context) => ExcludeSemantics(
        child: Container(
          width: size,
          height: size,
          decoration: BoxDecoration(
            // Bản 6: dải cam FPT và bóng nổi ngả cam.
            gradient: AppGradients.hero,
            // Logo nhỏ nằm trong app bar (bị cắt bóng) nên chỉ logo lớn ở A03 có bóng nổi.
            boxShadow: size >= 56 ? AppShadows.raised : null,
            // Logo nhỏ (header) bo góc vừa, logo lớn (A03) bo góc thẻ.
            borderRadius: BorderRadius.circular(size >= 56 ? AppRadius.lg : AppRadius.md),
          ),
          child: Icon(Icons.groups_rounded, size: size * 0.6, color: Theme.of(context).colorScheme.onPrimary),
        ),
      );
}
