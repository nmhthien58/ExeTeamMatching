import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/navigation.dart';
import '../../core/providers.dart';

/// Avatar chữ cái ở góc phải BrandAppBar của các tab (MB-62, 06/11/2026): thay tab Hồ sơ của sinh viên,
/// mở S02 (sinh viên), L06 (giảng viên) hoặc Cài đặt (mentor). Chưa đăng nhập thì không hiện gì.
class ProfileAvatarButton extends ConsumerWidget {
  const ProfileAvatarButton({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final user = ref.watch(authControllerProvider).value;
    final shell = user == null ? null : shellOf(user);
    if (user == null || shell == null) return const SizedBox.shrink();
    final theme = Theme.of(context);
    return Semantics(
      button: true,
      label: 'Hồ sơ của tôi',
      excludeSemantics: true,
      child: IconButton(
        tooltip: 'Hồ sơ của tôi',
        onPressed: () => context.push(shellProfile[shell]!),
        icon: CircleAvatar(
          radius: 18,
          backgroundColor: theme.colorScheme.primaryContainer,
          child: Text(initialsOf(user.fullName), style: theme.textTheme.labelLarge?.copyWith(color: theme.colorScheme.onPrimaryContainer)),
        ),
      ),
    );
  }
}

/// "Nguyễn Minh Thiên" → "T" (tên gọi là chữ cuối, theo cách gọi tên tiếng Việt).
String initialsOf(String fullName) {
  final parts = fullName.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty);
  return parts.isEmpty ? '?' : parts.last.characters.first.toUpperCase();
}
