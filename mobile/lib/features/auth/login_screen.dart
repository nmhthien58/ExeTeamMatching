import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/network/api_client.dart';
import '../../core/providers.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';

/// A03 – Đăng nhập Google (flutter-handoff mục 4, F1). Email không có trong danh sách → A04.
/// Router tự chuyển vào đúng khung khi đăng nhập xong.
class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  bool _pending = false;
  String? _error;

  Future<void> _signIn() async {
    setState(() {
      _pending = true;
      _error = null;
    });
    try {
      await ref.read(authControllerProvider.notifier).signInWithGoogle();
    } catch (e) {
      final error = e.apiError;
      if (!mounted) return;
      if (error.code == 'not-in-list') {
        context.go('/tai-khoan-chua-co');
        return;
      }
      setState(() => _error = error.isNetwork ? 'Mất kết nối. Kiểm tra mạng rồi thử lại.' : error.title);
    } finally {
      if (mounted) setState(() => _pending = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.s4),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Spacer(),
              Container(
                width: 56,
                height: 6,
                decoration: BoxDecoration(color: context.appColors.brandOrange, borderRadius: BorderRadius.circular(AppRadius.full)),
                alignment: Alignment.centerLeft,
              ),
              const SizedBox(height: AppSpacing.s6),
              Text('EXE Team Matching', style: theme.textTheme.headlineSmall),
              const SizedBox(height: AppSpacing.s2),
              Text('Tìm nhóm, làm việc cùng giảng viên và mentor.',
                  style: theme.textTheme.bodyLarge?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
              const Spacer(),
              if (_error != null) ...[ErrorBanner(message: _error!, onRetry: _signIn), const SizedBox(height: AppSpacing.s4)],
              LoadingButton(label: 'Đăng nhập với Google', loading: _pending, onPressed: _signIn),
              const SizedBox(height: AppSpacing.s3),
              Text('Dùng email đã đăng ký với bộ môn.',
                  textAlign: TextAlign.center,
                  style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
              const SizedBox(height: AppSpacing.s4),
            ],
          ),
        ),
      ),
    );
  }
}

/// A04 – Tài khoản chưa có trong danh sách (nhánh lỗi E1). Nút đăng nhập bằng tài khoản khác → A03.
class NotInListScreen extends ConsumerWidget {
  const NotInListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) => Scaffold(
        body: SafeArea(
          child: EmptyState(
            icon: Icons.person_off_outlined,
            title: 'Tài khoản chưa có trong danh sách',
            description: 'Email này không có trong danh sách học kỳ. Hãy dùng email trường đã đăng ký, hoặc liên hệ Bộ môn EXE.',
            action: FilledButton(
              onPressed: () async {
                await ref.read(authControllerProvider.notifier).signOut();
                if (context.mounted) context.go('/dang-nhap');
              },
              child: const Text('Dùng tài khoản khác'),
            ),
          ),
        ),
      );
}
