import 'package:flutter/material.dart';

import '../../core/theme/app_tokens.dart';

/// Banner hạn chót 4 mức (DESIGN.md 6.1): &gt; 72 giờ · ≤ 72 giờ · ≤ 24 giờ · đã khóa. Thông tin tĩnh, không bấm được.
class DeadlineBanner extends StatelessWidget {
  const DeadlineBanner({super.key, required this.deadline, this.title, this.now});

  final DateTime deadline;

  /// Mặc định "Hạn lập nhóm".
  final String? title;

  /// Cho test; mặc định thời điểm hiện tại.
  final DateTime? now;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = context.appColors;
    final left = deadline.difference(now ?? DateTime.now());

    final (Color bg, Color fg, IconData icon, String line) = switch (left) {
      Duration d when d <= Duration.zero => (colors.neutralContainer, theme.colorScheme.onSurfaceVariant, Icons.lock_outline, 'Đợt đăng ký đã đóng'),
      Duration d when d <= const Duration(hours: 24) =>
        (theme.colorScheme.errorContainer, theme.colorScheme.error, Icons.warning_amber_rounded, 'Còn ${_hours(d)}'),
      Duration d when d <= const Duration(hours: 72) => (colors.warningContainer, colors.warning, Icons.schedule, 'Còn ${_daysHours(d)}'),
      _ => (theme.colorScheme.primaryContainer, theme.colorScheme.onPrimaryContainer, Icons.event, 'Hạn chót ${_format(deadline.toLocal())}'),
    };

    return Semantics(
      container: true,
      label: '${title ?? 'Hạn lập nhóm'}. $line',
      child: ExcludeSemantics(
        child: Container(
          width: double.infinity,
          padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s4, vertical: AppSpacing.s3),
          decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(AppRadius.md)),
          child: Row(
            children: [
              Icon(icon, size: 20, color: fg),
              const SizedBox(width: AppSpacing.s3),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(title ?? 'Hạn lập nhóm', style: theme.textTheme.titleMedium?.copyWith(color: fg)),
                    Text(line, style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurface)),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  static String _hours(Duration d) => d.inHours >= 1 ? '${d.inHours} giờ' : '${d.inMinutes.clamp(1, 59)} phút';

  static String _daysHours(Duration d) {
    final days = d.inDays;
    final hours = d.inHours - days * 24;
    return days > 0 ? '$days ngày $hours giờ' : '$hours giờ';
  }

  static String _format(DateTime t) {
    String two(int v) => v.toString().padLeft(2, '0');
    return '${two(t.hour)}:${two(t.minute)} · ${two(t.day)}/${two(t.month)}/${t.year}';
  }
}
