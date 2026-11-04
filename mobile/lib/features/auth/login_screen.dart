import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:google_sign_in/google_sign_in.dart';

import '../../core/auth/firebase_auth_service.dart';
import '../../core/network/api_client.dart';
import '../../core/providers.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/app_logo.dart';
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
      // null = người dùng hủy chọn tài khoản: về trạng thái mặc định, báo nhẹ bằng SnackBar (Lab 3 §6).
      final user = await ref.read(authControllerProvider.notifier).signInWithGoogle();
      if (user == null) _showCancelled();
    } on PlatformException catch (e) {
      // Lỗi từ Google Sign-In (không phải từ Identity).
      if (!mounted) return;
      if (e.code == GoogleSignIn.kSignInCanceledError) {
        _showCancelled();
        return;
      }
      setState(() => _error = e.code == GoogleSignIn.kNetworkError
          ? 'Mất kết nối. Kiểm tra mạng rồi thử lại.'
          : 'Không đăng nhập được với Google. Thử lại sau.');
    } on SignInFailure catch (e) {
      // Firebase Authentication từ chối credential hoặc mất mạng.
      if (!mounted) return;
      setState(() => _error = e.isNetwork
          ? 'Mất kết nối. Kiểm tra mạng rồi thử lại.'
          : 'Không đăng nhập được với Google. Thử lại sau.');
    } catch (e) {
      final error = e.apiError;
      if (!mounted) return;
      if (error.code == 'not-in-list') {
        // A04 hiện email vừa dùng để người dùng nhận ra nhầm tài khoản.
        context.go('/tai-khoan-chua-co', extra: ref.read(googleSignInProvider).currentUser?.email);
        return;
      }
      setState(() => _error = error.isNetwork ? 'Mất kết nối. Kiểm tra mạng rồi thử lại.' : error.title);
    } finally {
      if (mounted) setState(() => _pending = false);
    }
  }

  void _showCancelled() {
    if (!mounted) return;
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(const SnackBar(content: Text('Bạn đã hủy đăng nhập. Bấm "Đăng nhập với Google" để thử lại.')));
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
              // flutter-handoff A03: logo và tên ứng dụng ở nửa trên.
              const Align(alignment: Alignment.centerLeft, child: AppLogo()),
              const SizedBox(height: AppSpacing.s6),
              Text('EXE Team Matching', style: theme.textTheme.headlineSmall),
              const SizedBox(height: AppSpacing.s2),
              Text('Tìm nhóm, làm việc cùng giảng viên và mentor.',
                  style: theme.textTheme.bodyLarge?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
              const Spacer(),
              if (_error != null) ...[ErrorBanner(message: _error!, onRetry: _signIn), const SizedBox(height: AppSpacing.s4)],
              LoadingButton(
                label: 'Đăng nhập với Google',
                loading: _pending,
                onPressed: _signIn,
                // Logo G chính thức của Google trên nền tròn trắng (Sign in with Google branding guidelines).
                leading: Image.asset('assets/images/google_g.png', width: 28, height: 28, excludeFromSemantics: true),
              ),
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

/// A04 – Tài khoản chưa có trong danh sách (nhánh lỗi E1). Hiện email vừa dùng; nút "Dùng tài khoản khác" đăng xuất Google → A03.
class NotInListScreen extends ConsumerWidget {
  const NotInListScreen({super.key, this.email});

  final String? email;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    return Scaffold(
      body: SafeArea(
        child: EmptyState(
          icon: Icons.person_off_outlined,
          title: 'Tài khoản chưa có trong danh sách',
          description: 'Email này không có trong danh sách học kỳ. Hãy dùng email đã đăng ký với bộ môn, hoặc liên hệ Bộ môn EXE.',
          action: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (email != null) ...[
                Chip(
                  avatar: const Icon(Icons.mail_outline, size: 18),
                  label: Text(email!, style: theme.textTheme.bodyMedium),
                ),
                const SizedBox(height: AppSpacing.s4),
              ],
              FilledButton(
                onPressed: () async {
                  await ref.read(authControllerProvider.notifier).signOut();
                  if (context.mounted) context.go('/dang-nhap');
                },
                child: const Text('Dùng tài khoản khác'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
