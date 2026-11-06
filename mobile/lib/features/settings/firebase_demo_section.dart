import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/ai/ai_service.dart';
import '../../core/firebase/push_notifications.dart';
import '../../core/theme/app_tokens.dart';
import '../../core/view_state.dart';
import 'firebase_demo_view_model.dart';

/// Mục "Demo Firebase" ở A07 (Lab 3 §3): giá trị Remote Config đang dùng, gửi lỗi thử và crash thử
/// (đều hỏi xác nhận), lối vào xuất / tải file và thông báo, gửi lại device token. Chỉ hiện khi `Env.firebaseDemo`.
class FirebaseDemoSection extends ConsumerWidget {
  const FirebaseDemoSection({super.key});

  Future<bool> _confirm(BuildContext context, String title, String content) async =>
      await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: Text(title),
          content: Text(content),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy')),
            FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Tiếp tục')),
          ],
        ),
      ) ??
      false;

  Future<void> _nonFatal(BuildContext context, WidgetRef ref) async {
    if (!await _confirm(context, 'Gửi lỗi thử?', 'Ghi một lỗi non-fatal lên Crashlytics. Ứng dụng vẫn chạy bình thường.')) return;
    await ref.read(firebaseDemoViewModelProvider.notifier).sendNonFatal();
    if (!context.mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Đã gửi lỗi thử. Vài phút sau sẽ thấy trong Firebase Console → Crashlytics.')),
    );
  }

  Future<void> _crash(BuildContext context, WidgetRef ref) async {
    if (!await _confirm(context, 'Crash thử?', 'Ứng dụng sẽ đóng ngay. Mở lại ứng dụng để báo cáo được gửi lên Crashlytics.')) return;
    await ref.read(firebaseDemoViewModelProvider.notifier).crash();
  }

  Future<void> _resendToken(BuildContext context, WidgetRef ref) async {
    final result = await ref.read(firebaseDemoViewModelProvider.notifier).resendDeviceToken();
    if (!context.mounted) return;
    final message = switch (result) {
      DeviceTokenResend.sent => 'Đã gửi lại device token.',
      DeviceTokenResend.noToken => 'Máy chưa có device token. Hãy cho phép thông báo rồi thử lại.',
      DeviceTokenResend.failed => 'Không gửi được device token. Kiểm tra kết nối rồi thử lại.',
    };
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final state = ref.watch(firebaseDemoViewModelProvider);
    final config = state.config;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s4, AppSpacing.s4, AppSpacing.s2),
          child: Text('Demo Firebase', style: theme.textTheme.titleSmall),
        ),
        ListTile(
          leading: const Icon(Icons.tune),
          title: const Text('Remote Config đang dùng'),
          subtitle: Text(
            'Số nhóm gợi ý tối đa: ${config.suggestionsMaxItems}\n'
            'Gợi ý ý tưởng bằng AI: ${config.aiIdeaEnabled ? 'bật' : 'tắt'} · ${config.aiModel}',
          ),
          isThreeLine: true,
        ),
        if (!state.crashlyticsEnabled)
          ListTile(
            leading: Icon(Icons.info_outline, color: theme.colorScheme.onSurfaceVariant),
            title: const Text('Crashlytics đang tắt ở bản debug'),
            subtitle: const Text('Build với --dart-define=CRASHLYTICS_DEBUG=true để gửi báo cáo.'),
          ),
        if (state.links case final links?) ...[
          ListTile(
            leading: const Icon(Icons.picture_as_pdf_outlined),
            title: const Text('Xuất và tải file'),
            subtitle: const Text('Xuất báo cáo PDF ở Dự án, tải file ở mục File.'),
            trailing: const Icon(Icons.chevron_right),
            onTap: () => context.go(links.files),
          ),
          ListTile(
            leading: const Icon(Icons.notifications_outlined),
            title: const Text('Thông báo'),
            trailing: const Icon(Icons.chevron_right),
            onTap: () => context.go(links.notifications),
          ),
          ListTile(
            leading: const Icon(Icons.sync_outlined),
            title: const Text('Gửi lại device token'),
            subtitle: const Text('Đăng ký lại máy này để nhận thông báo đẩy.'),
            onTap: () => _resendToken(context, ref),
          ),
        ],
        ListTile(
          leading: const Icon(Icons.auto_awesome_outlined),
          title: const Text('Thử AI (gợi ý ý tưởng)'),
          subtitle: const Text('Gemini qua Firebase AI Logic, có App Check'),
          onTap: () => showDialog<void>(context: context, builder: (_) => const _AiDemoDialog()),
        ),
        ListTile(
          leading: const Icon(Icons.bug_report_outlined),
          title: const Text('Gửi lỗi thử (non-fatal)'),
          onTap: () => _nonFatal(context, ref),
        ),
        ListTile(
          leading: Icon(Icons.warning_amber_rounded, color: theme.colorScheme.error),
          title: Text('Crash thử', style: TextStyle(color: theme.colorScheme.error)),
          onTap: () => _crash(context, ref),
        ),
      ],
    );
  }
}

/// Hộp thử AI (MB-55): nhập mô tả → hiện bản nháp hoặc mã lỗi. Mọi lời gọi đi qua [AiDemoViewModel].
class _AiDemoDialog extends ConsumerStatefulWidget {
  const _AiDemoDialog();

  @override
  ConsumerState<_AiDemoDialog> createState() => _AiDemoDialogState();
}

class _AiDemoDialogState extends ConsumerState<_AiDemoDialog> {
  final _description = TextEditingController();

  @override
  void dispose() {
    _description.dispose();
    super.dispose();
  }

  void _send() => ref.read(aiDemoViewModelProvider.notifier).suggest(_description.text);

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final state = ref.watch(aiDemoViewModelProvider);
    final loading = state is ViewLoading;
    return AlertDialog(
      title: const Text('Thử AI'),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            TextField(
              controller: _description,
              minLines: 2,
              maxLines: 5,
              enabled: !loading,
              decoration: const InputDecoration(
                labelText: 'Mô tả ý tưởng',
                hintText: 'Ví dụ: app giúp sinh viên tìm phòng trọ gần trường, có đánh giá của người thuê cũ',
              ),
            ),
            const SizedBox(height: AppSpacing.s3),
            switch (state) {
              null || ViewEmpty() => const SizedBox.shrink(),
              ViewLoading() => const LinearProgressIndicator(),
              ViewError(:final error) => Text(
                'Lỗi: ${error is AiException ? error.code : error}',
                style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.error),
              ),
              ViewData(value: final d) => SelectableText(
                'Tên: ${d.title}\n\nVấn đề: ${d.problem}\n\nGiải pháp: ${d.solution}\n\n'
                'Khách hàng: ${d.targetCustomer}\n\nTag: ${d.tags.isEmpty ? '(không có)' : d.tags.join(', ')}',
                style: theme.textTheme.bodyMedium,
              ),
            },
          ],
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: const Text('Đóng')),
        FilledButton(onPressed: loading ? null : _send, child: const Text('Gửi')),
      ],
    );
  }
}
