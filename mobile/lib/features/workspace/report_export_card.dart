import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_tokens.dart';
import 'report_pdf.dart';
import 'report_view_model.dart';

/// Thẻ "Báo cáo dự án" ở C01 (MB-53): nút xuất, tiến độ, link tải, kết quả thành công hoặc thất bại.
class ReportExportCard extends ConsumerWidget {
  const ReportExportCard({super.key, required this.workspaceId, required this.data});

  final String workspaceId;

  /// Dựng lúc bấm để báo cáo lấy đúng số liệu đang hiện.
  final ProjectReportData Function() data;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final provider = reportExportViewModelProvider(workspaceId);
    final state = ref.watch(provider);
    final viewModel = ref.read(provider.notifier);
    final theme = Theme.of(context);
    final busy = state is ReportGenerating || state is ReportUploading;

    return Card.outlined(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.s4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Báo cáo dự án', style: theme.textTheme.titleMedium),
            const SizedBox(height: AppSpacing.s1),
            Text(
              'Tạo file PDF từ thông tin trên màn này. Chỉ bạn mở được file.',
              style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            const SizedBox(height: AppSpacing.s3),
            switch (state) {
              ReportIdle() => const SizedBox.shrink(),
              ReportGenerating() => const _Progress(label: 'Đang tạo PDF…'),
              ReportUploading(:final progress) => _Progress(label: 'Đang tải lên ${(progress * 100).round()}%', value: progress),
              ReportFailed(:final message) => _Result(icon: Icons.error_outline, color: theme.colorScheme.error, text: message),
              ReportSaved(:final url, :final openError) => _Saved(url: url, openError: openError, onOpen: viewModel.open),
            },
            if (state is! ReportIdle) const SizedBox(height: AppSpacing.s3),
            SizedBox(
              width: double.infinity,
              child: OutlinedButton.icon(
                onPressed: busy ? null : () => viewModel.export(data()),
                icon: const Icon(Icons.picture_as_pdf_outlined),
                label: Text(state is ReportFailed ? 'Thử lại' : 'Xuất báo cáo PDF'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _Progress extends StatelessWidget {
  const _Progress({required this.label, this.value});

  final String label;
  final double? value;

  @override
  Widget build(BuildContext context) => Semantics(
    liveRegion: true,
    label: label,
    child: ExcludeSemantics(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: Theme.of(context).textTheme.bodyMedium),
          const SizedBox(height: AppSpacing.s2),
          LinearProgressIndicator(value: value),
        ],
      ),
    ),
  );
}

class _Result extends StatelessWidget {
  const _Result({required this.icon, required this.color, required this.text});

  final IconData icon;
  final Color color;
  final String text;

  @override
  Widget build(BuildContext context) => Semantics(
    liveRegion: true,
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, color: color),
        const SizedBox(width: AppSpacing.s2),
        Expanded(child: Text(text, style: Theme.of(context).textTheme.bodyMedium)),
      ],
    ),
  );
}

class _Saved extends StatelessWidget {
  const _Saved({required this.url, required this.openError, required this.onOpen});

  final String url;
  final String? openError;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _Result(icon: Icons.check_circle_outline, color: context.appColors.success, text: 'Đã lưu báo cáo'),
        const SizedBox(height: AppSpacing.s2),
        Text('Link tải (có hạn vài phút):', style: theme.textTheme.bodySmall),
        Text(url, maxLines: 2, overflow: TextOverflow.ellipsis, style: theme.textTheme.bodySmall),
        const SizedBox(height: AppSpacing.s2),
        Wrap(
          spacing: AppSpacing.s2,
          children: [
            FilledButton(onPressed: onOpen, child: const Text('Mở báo cáo')),
            TextButton(onPressed: () => Clipboard.setData(ClipboardData(text: url)), child: const Text('Sao chép link')),
          ],
        ),
        if (openError != null) ...[
          const SizedBox(height: AppSpacing.s2),
          _Result(icon: Icons.error_outline, color: theme.colorScheme.error, text: openError!),
        ],
      ],
    );
  }
}
