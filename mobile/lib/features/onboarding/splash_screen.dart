import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/navigation.dart';
import '../../core/providers.dart';
import '../../core/theme/app_tokens.dart';
import 'onboarding_store.dart';

/// A01. Chờ đọc phiên trước khi quyết định màn tiếp theo.
class SplashScreen extends ConsumerStatefulWidget {
  const SplashScreen({
    super.key,
    required this.onResolved,
    this.onboardingStore,
  });

  final void Function(String route) onResolved;
  final OnboardingStore? onboardingStore;

  @override
  ConsumerState<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends ConsumerState<SplashScreen> {
  @override
  void initState() {
    super.initState();
    Future<void>.microtask(_resolve);
  }

  Future<void> _resolve() async {
    try {
      final user = await ref.read(authControllerProvider.future);
      if (!mounted) return;
      if (user != null) {
        final shell = shellOf(user);
        widget.onResolved(shell == null ? '/chi-dung-web' : shellHome[shell]!);
        return;
      }
      final seen = await (widget.onboardingStore ?? OnboardingStore())
          .hasSeen();
      if (mounted) widget.onResolved(seen ? '/dang-nhap' : '/gioi-thieu');
    } catch (_) {
      if (mounted) widget.onResolved('/dang-nhap');
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(
                Icons.groups_rounded,
                size: AppSpacing.s8 * 2,
                color: theme.colorScheme.primary,
              ),
              const SizedBox(height: AppSpacing.s4),
              Text('EXE Team Matching', style: theme.textTheme.headlineSmall),
              const SizedBox(height: AppSpacing.s6),
              const CircularProgressIndicator(),
            ],
          ),
        ),
      ),
    );
  }
}
