import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/theme/app_theme.dart';
import 'router.dart';

/// Gốc ứng dụng: theme bản 4 (MB-02, MB-37) và router theo vai trò (MB-05).
class ExeTeamMatchingApp extends ConsumerWidget {
  const ExeTeamMatchingApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) => MaterialApp.router(
        title: 'EXE Team Matching',
        debugShowCheckedModeBanner: false,
        theme: AppTheme.light(),
        routerConfig: ref.watch(routerProvider),
      );
}
