import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/network/api_exception.dart';
import '../../core/providers.dart';
import '../../core/theme/app_tokens.dart';

/// Phiên bản hiển thị ở A07; giữ khớp `version` trong pubspec.yaml.
const appVersion = '1.0.0';

/// notification.yaml – Preferences (A07).
class SettingsApi {
  SettingsApi(this._dio);

  final Dio _dio;

  Future<bool> emailEnabled() async {
    try {
      final response = await _dio.get<Map<String, dynamic>>('/api/notifications/preferences');
      return response.data!['emailEnabled'] as bool? ?? true;
    } on DioException catch (e) {
      throw e.apiError;
    }
  }

  Future<void> setEmailEnabled(bool value) async {
    try {
      await _dio.put<void>('/api/notifications/preferences', data: {'emailEnabled': value});
    } on DioException catch (e) {
      throw e.apiError;
    }
  }
}

final settingsApiProvider = Provider<SettingsApi>((ref) => SettingsApi(ref.watch(dioProvider)));

final emailEnabledProvider = FutureProvider.autoDispose<bool>((ref) => ref.watch(settingsApiProvider).emailEnabled(), retry: (_, _) => null);

/// A07 – Cài đặt và tài khoản (MB-10): bật/tắt nhận email thông báo, thông tin ứng dụng, đăng xuất có xác nhận
/// (thu hồi refresh token). Dùng chung cho sinh viên, giảng viên, mentor.
class SettingsScreen extends ConsumerStatefulWidget {
  const SettingsScreen({super.key});

  @override
  ConsumerState<SettingsScreen> createState() => _SettingsScreenState();
}

class _SettingsScreenState extends ConsumerState<SettingsScreen> {
  /// Giá trị đang gửi: công tắc đổi ngay, lỗi thì trả lại.
  bool? _pending;

  Future<void> _toggleEmail(bool value) async {
    setState(() => _pending = value);
    try {
      await ref.read(settingsApiProvider).setEmailEnabled(value);
      ref.invalidate(emailEnabledProvider);
      await ref.read(emailEnabledProvider.future);
    } on ApiException {
      if (!mounted) return;
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(const SnackBar(content: Text('Chưa lưu được cài đặt email. Thử lại sau.')));
    } finally {
      if (mounted) setState(() => _pending = null);
    }
  }

  Future<void> _signOut() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Đăng xuất?'),
        content: const Text('Bạn cần đăng nhập lại bằng Google để tiếp tục dùng ứng dụng.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy')),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Đăng xuất')),
        ],
      ),
    );
    if (confirmed == true) await ref.read(authControllerProvider.notifier).signOut();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final user = ref.watch(authControllerProvider).value;
    final email = ref.watch(emailEnabledProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Cài đặt')),
      body: ListView(
        padding: const EdgeInsets.symmetric(vertical: AppSpacing.s2),
        children: [
          if (user != null)
            ListTile(
              leading: const Icon(Icons.account_circle_outlined),
              title: Text(user.fullName),
              subtitle: Text(user.email),
            ),
          const Divider(),
          email.when(
            loading: () => const SwitchListTile(title: Text('Nhận email thông báo'), value: false, onChanged: null),
            error: (_, _) => ListTile(
              leading: Icon(Icons.error_outline, color: theme.colorScheme.error),
              title: const Text('Không tải được cài đặt email'),
              trailing: TextButton(onPressed: () => ref.invalidate(emailEnabledProvider), child: const Text('Thử lại')),
            ),
            data: (enabled) => SwitchListTile(
              secondary: const Icon(Icons.mail_outline),
              title: const Text('Nhận email thông báo'),
              subtitle: const Text('Lời mời, hạn chót, kết quả ghép nhóm. Thông báo trong ứng dụng vẫn nhận đủ.'),
              value: _pending ?? enabled,
              onChanged: _pending == null ? _toggleEmail : null,
            ),
          ),
          const Divider(),
          const ListTile(leading: Icon(Icons.info_outline), title: Text('EXE Team Matching'), subtitle: Text('Phiên bản $appVersion')),
          const Divider(),
          ListTile(
            leading: Icon(Icons.logout, color: theme.colorScheme.error),
            title: Text('Đăng xuất', style: TextStyle(color: theme.colorScheme.error)),
            onTap: _signOut,
          ),
        ],
      ),
    );
  }
}
