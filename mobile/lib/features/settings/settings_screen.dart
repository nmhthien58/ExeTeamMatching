import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/navigation.dart';
import '../../core/auth/session.dart' show Role;

import '../../core/network/api_client.dart';
import '../../core/providers.dart';
import '../../core/theme/app_tokens.dart';
import '../../core/config/env.dart';
import '../../shared/widgets/profile_avatar_button.dart' show initialsOf;
import '../workspace/workspace_widgets.dart' show StatusChip, StatusTone;
import 'firebase_demo_section.dart';
import 'settings_view_model.dart';

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

/// A07 – Cài đặt và tài khoản (MB-10): thẻ tài khoản, lối vào "Quản lý hồ sơ" (S02 / L06), bật/tắt nhận email thông báo,
/// thông tin ứng dụng, đăng xuất có xác nhận (thu hồi refresh token). Mở từ avatar ở góc phải BrandAppBar. Dùng chung cho sinh viên, giảng viên, mentor.
class SettingsScreen extends ConsumerStatefulWidget {
  const SettingsScreen({super.key});

  @override
  ConsumerState<SettingsScreen> createState() => _SettingsScreenState();
}

class _SettingsScreenState extends ConsumerState<SettingsScreen> {
  SettingsViewModel get _vm => ref.read(settingsViewModelProvider.notifier);

  Future<void> _toggleEmail(bool value) async {
    if (await _vm.setEmailEnabled(value) || !mounted) return;
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(const SnackBar(content: Text('Chưa lưu được cài đặt email. Thử lại sau.')));
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
    if (confirmed == true) await _vm.signOut();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final user = ref.watch(authControllerProvider).value;
    final shell = user == null ? null : shellOf(user);
    final email = ref.watch(emailEnabledProvider);
    final pending = ref.watch(settingsViewModelProvider);
    final isStudent = user?.roles.contains(Role.student) ?? false;
    return Scaffold(
      appBar: AppBar(title: const Text('Cài đặt và tài khoản')),
      body: ListView(
        padding: const EdgeInsets.all(AppSpacing.s4),
        children: [
          if (user != null) ...[
            _AccountCard(name: user.fullName, email: user.email, roleLabel: _roleLabel(user.roles)),
            const SizedBox(height: AppSpacing.s4),
            _Section(
              title: 'Tài khoản',
              children: [
                // Mentor chưa có màn hồ sơ riêng (shellProfile trỏ về chính A07).
                if (shell != null && shell != Shell.mentor)
                  ListTile(
                    leading: const Icon(Icons.manage_accounts_outlined),
                    title: const Text('Quản lý hồ sơ'),
                    subtitle: Text(isStudent ? 'Kỹ năng, chuyên ngành, số điện thoại' : 'Chuyên môn, số nhóm, số điện thoại'),
                    trailing: const Icon(Icons.chevron_right),
                    onTap: () => context.push(shellProfile[shell]!),
                  ),
                // Lối vào S12 tab Yêu cầu đã gửi (mảng Cường): xem và hủy yêu cầu xin vào nhóm.
                if (isStudent)
                  ListTile(
                    leading: const Icon(Icons.outgoing_mail),
                    title: const Text('Yêu cầu đã gửi'),
                    subtitle: const Text('Yêu cầu xin vào nhóm và trạng thái'),
                    trailing: const Icon(Icons.chevron_right),
                    onTap: () => context.push('/sv/loi-moi?tab=yeu-cau'),
                  ),
              ],
            ),
          ],
          _Section(
            title: 'Thông báo',
            children: [
              email.when(
                loading: () => const SwitchListTile(title: Text('Nhận email thông báo'), value: false, onChanged: null),
                error: (_, _) => ListTile(
                  leading: Icon(Icons.error_outline, color: theme.colorScheme.error),
                  title: const Text('Không tải được cài đặt email'),
                  trailing: TextButton(onPressed: _vm.retryEmail, child: const Text('Thử lại')),
                ),
                data: (enabled) => SwitchListTile(
                  title: const Text('Nhận email thông báo'),
                  subtitle: const Text('Lời mời, hạn chót, kết quả ghép nhóm. Thông báo trong ứng dụng vẫn nhận đủ.'),
                  value: pending ?? enabled,
                  onChanged: pending == null ? _toggleEmail : null,
                ),
              ),
            ],
          ),
          _Section(
            title: 'Ứng dụng',
            children: [
              ListTile(
                leading: const Icon(Icons.info_outline),
                title: const Text('Phiên bản'),
                trailing: Text(appVersion, style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
              ),
            ],
          ),
          // design-decisions B11: nút viền, màu Error chỉ trong dialog xác nhận.
          OutlinedButton.icon(onPressed: _signOut, icon: const Icon(Icons.logout), label: const Text('Đăng xuất')),
          // Lab 3 §3: khu demo Firebase ở cuối để không đẩy các mục thường dùng xuống.
          if (Env.firebaseDemo) ...[const SizedBox(height: AppSpacing.s4), const FirebaseDemoSection()],
        ],
      ),
    );
  }
}

String _roleLabel(Set<Role> roles) => [
      if (roles.contains(Role.student)) 'Sinh viên',
      if (roles.contains(Role.lecturer)) 'Giảng viên',
      if (roles.contains(Role.mentor)) 'Mentor',
    ].join(' · ');

/// Thẻ đầu A07: avatar chữ cái, họ tên, email, chip vai trò.
class _AccountCard extends StatelessWidget {
  const _AccountCard({required this.name, required this.email, required this.roleLabel});

  final String name;
  final String email;
  final String roleLabel;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card.outlined(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.s4),
        child: Row(
          children: [
            CircleAvatar(
              radius: 28,
              backgroundColor: theme.colorScheme.primaryContainer,
              child: Text(initialsOf(name), style: theme.textTheme.titleMedium?.copyWith(color: theme.colorScheme.onPrimaryContainer)),
            ),
            const SizedBox(width: AppSpacing.s4),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(name, style: theme.textTheme.titleMedium),
                  const SizedBox(height: AppSpacing.s1),
                  Text(email, style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
                  if (roleLabel.isNotEmpty) ...[
                    const SizedBox(height: AppSpacing.s2),
                    StatusChip(label: roleLabel, tone: StatusTone.info),
                  ],
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Nhóm mục có tiêu đề và thẻ viền như Figma A07; các dòng cách nhau bằng Divider.
class _Section extends StatelessWidget {
  const _Section({required this.title, required this.children});

  final String title;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    if (children.isEmpty) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.s4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(title, style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: AppSpacing.s2),
          Card.outlined(
            margin: EdgeInsets.zero,
            clipBehavior: Clip.antiAlias,
            child: Column(children: [for (final (i, c) in children.indexed) ...[if (i > 0) const Divider(height: 1), c]]),
          ),
        ],
      ),
    );
  }
}
