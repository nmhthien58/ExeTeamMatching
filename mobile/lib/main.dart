import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'app/app.dart';
import 'core/firebase/firebase_bootstrap.dart';
import 'core/providers.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await initFirebase();
  runApp(const AppRoot());
}

/// Đăng xuất thì dựng lại ProviderScope mới: mọi provider đã cache dữ liệu của người dùng cũ bị bỏ (MB-47).
/// Phiên trong secure storage đã xóa trước đó nên scope mới mở màn đăng nhập.
class AppRoot extends StatefulWidget {
  const AppRoot({super.key});

  @override
  State<AppRoot> createState() => _AppRootState();
}

class _AppRootState extends State<AppRoot> {
  int _generation = 0;

  void _restart() {
    if (mounted) setState(() => _generation++);
  }

  @override
  Widget build(BuildContext context) => ProviderScope(
        key: ValueKey(_generation),
        overrides: [signedOutProvider.overrideWithValue(_restart)],
        child: const ExeTeamMatchingApp(),
      );
}
