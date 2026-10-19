import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../core/config/env.dart';

/// MB-01 – khung ban đầu. Theme (MB-02), widget dùng chung (MB-03), lớp mạng (MB-04)
/// và điều hướng theo vai trò (MB-05) được thêm ở các task sau.
final _router = GoRouter(
  routes: [
    GoRoute(path: '/', builder: (context, state) => const _PlaceholderScreen()),
  ],
);

class ExeTeamMatchingApp extends StatelessWidget {
  const ExeTeamMatchingApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp.router(
      title: 'EXE Team Matching',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(useMaterial3: true),
      routerConfig: _router,
    );
  }
}

class _PlaceholderScreen extends StatelessWidget {
  const _PlaceholderScreen();

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('EXE Team Matching', style: Theme.of(context).textTheme.headlineSmall),
              const SizedBox(height: 8),
              Text('API Gateway: ${Env.apiBaseUrl}'),
            ],
          ),
        ),
      ),
    );
  }
}
