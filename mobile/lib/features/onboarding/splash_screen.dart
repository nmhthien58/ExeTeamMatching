import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_tokens.dart';
import 'onboarding_store.dart';
import 'onboarding_view_model.dart';

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
    final route = await ref
        .read(splashViewModelProvider.notifier)
        .resolve(store: widget.onboardingStore);
    if (mounted) widget.onResolved(route);
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    ref.watch(splashViewModelProvider);
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
