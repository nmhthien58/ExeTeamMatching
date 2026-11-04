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
            color: context.appColors.brandOrange,
            borderRadius: BorderRadius.circular(AppRadius.lg),
          ),
          child: Icon(Icons.groups_rounded, size: size * 0.6, color: Theme.of(context).colorScheme.onPrimary),
        ),
      );
}
