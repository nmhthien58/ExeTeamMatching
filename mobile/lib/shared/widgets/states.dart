import 'package:flutter/material.dart';

import '../../core/network/api_exception.dart';
import '../../core/theme/app_tokens.dart';

/// A08 – trạng thái dùng chung (DESIGN.md 6.1, 6.2). Không có màn hình ngõ cụt: luôn có Thử lại hoặc lối ra.

/// Màn trống: icon 48, một dòng tiêu đề, một dòng mô tả, tối đa một nút.
class EmptyState extends StatelessWidget {
  const EmptyState({super.key, this.icon = Icons.inbox_outlined, required this.title, this.description, this.action});

  final IconData icon;
  final String title;
  final String? description;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.s6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            // DESIGN.md 6.1: icon 48 màu Primary trong vòng 96 nền Primary Container; bản 6 thêm bóng nổi và hiện dần.
            TweenAnimationBuilder<double>(
              tween: Tween(begin: 0.85, end: 1),
              duration: AppMotion.of(context, const Duration(milliseconds: 420)),
              curve: Curves.easeOutBack,
              builder: (context, scale, child) => Transform.scale(scale: scale, child: child),
              child: Container(
                width: 96,
                height: 96,
                decoration: BoxDecoration(
                  color: theme.colorScheme.primaryContainer,
                  shape: BoxShape.circle,
                  boxShadow: AppShadows.raised,
                ),
                child: Icon(icon, size: 48, color: theme.colorScheme.primary),
              ),
            ),
            const SizedBox(height: AppSpacing.s4),
            Text(title, style: theme.textTheme.titleMedium, textAlign: TextAlign.center),
            if (description != null) ...[
              const SizedBox(height: AppSpacing.s1),
              Text(
                description!,
                style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                textAlign: TextAlign.center,
              ),
            ],
            if (action != null) ...[const SizedBox(height: AppSpacing.s4), action!],
          ],
        ),
      ),
    );
  }
}

/// Lỗi cả màn hình: nói điều gì xảy ra, cách xử lý, nút chính "Thử lại". Mất mạng và không có quyền có câu riêng.
class ErrorState extends StatelessWidget {
  const ErrorState({super.key, required this.error, required this.onRetry});

  final Object error;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final e = error is ApiException ? error as ApiException : null;
    if (e?.isNetwork ?? false) {
      return EmptyState(
        icon: Icons.wifi_off,
        title: 'Mất kết nối',
        description: 'Kiểm tra mạng rồi thử lại.',
        action: FilledButton(onPressed: onRetry, child: const Text('Thử lại')),
      );
    }
    if (e?.status == 403) {
      return const ForbiddenState();
    }
    return EmptyState(
      icon: Icons.error_outline,
      title: e?.title ?? 'Không tải được dữ liệu',
      description: [e?.detail, if (e?.traceId != null) 'Mã tra cứu: ${e!.traceId}'].whereType<String>().join('\n'),
      action: FilledButton(onPressed: onRetry, child: const Text('Thử lại')),
    );
  }
}

class ForbiddenState extends StatelessWidget {
  const ForbiddenState({super.key});

  @override
  Widget build(BuildContext context) => const EmptyState(
        icon: Icons.lock_outline,
        title: 'Bạn không có quyền xem mục này',
        description: 'Nếu bạn nghĩ đây là nhầm lẫn, hãy liên hệ Bộ môn EXE.',
      );
}

/// Banner lỗi khi gửi / mất mạng: nền Error Container, chữ Error, nút chữ "Thử lại" bên phải.
/// Nội dung người dùng đã nhập phải được giữ nguyên.
class ErrorBanner extends StatelessWidget {
  const ErrorBanner({super.key, required this.message, this.onRetry});

  final String message;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Semantics(
      liveRegion: true,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s4, vertical: AppSpacing.s3),
        decoration: BoxDecoration(color: scheme.errorContainer, borderRadius: BorderRadius.circular(AppRadius.md)),
        child: Row(
          children: [
            Icon(Icons.error_outline, size: 20, color: scheme.error),
            const SizedBox(width: AppSpacing.s3),
            Expanded(
              child: Text(message, style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: scheme.error)),
            ),
            if (onRetry != null)
              TextButton(
                onPressed: onRetry,
                style: TextButton.styleFrom(foregroundColor: scheme.error),
                child: const Text('Thử lại'),
              ),
          ],
        ),
      ),
    );
  }
}

/// Khối skeleton nền Neutral Container, đúng hình nội dung; không nhấp nháy.
class Skeleton extends StatelessWidget {
  const Skeleton({super.key, this.width, this.height = 16, this.radius = AppRadius.sm});

  final double? width;
  final double height;
  final double radius;

  @override
  Widget build(BuildContext context) => ExcludeSemantics(
        child: Container(
          width: width,
          height: height,
          decoration: BoxDecoration(color: context.appColors.neutralContainer, borderRadius: BorderRadius.circular(radius)),
        ),
      );
}

/// Danh sách thẻ skeleton dùng khi đang tải.
class SkeletonList extends StatelessWidget {
  const SkeletonList({super.key, this.count = 4});

  final int count;

  @override
  Widget build(BuildContext context) => Semantics(
        label: 'Đang tải',
        child: ListView.separated(
          physics: const NeverScrollableScrollPhysics(),
          shrinkWrap: true,
          padding: const EdgeInsets.all(AppSpacing.s4),
          itemCount: count,
          separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.s3),
          itemBuilder: (_, _) => const Card(
            child: Padding(
              padding: EdgeInsets.all(AppSpacing.s4),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Skeleton(width: 160, height: 20),
                  SizedBox(height: AppSpacing.s2),
                  Skeleton(width: 220),
                  SizedBox(height: AppSpacing.s2),
                  Skeleton(width: 120),
                ],
              ),
            ),
          ),
        ),
      );
}

/// Nút chính khi đang gửi: vòng xoay 20 px thay chữ, giữ chiều rộng, khóa bấm lần hai (DESIGN.md 6.1).
class LoadingButton extends StatelessWidget {
  const LoadingButton({
    super.key,
    required this.label,
    required this.onPressed,
    this.loading = false,
    this.expand = true,
    this.leading,
  });

  final String label;
  final VoidCallback? onPressed;
  final bool loading;
  final bool expand;

  /// Icon đứng trước nhãn (ví dụ logo Google ở A03); ẩn cùng nhãn khi đang gửi.
  final Widget? leading;

  @override
  Widget build(BuildContext context) {
    final content = leading == null
        ? Text(label)
        : Row(
            mainAxisSize: MainAxisSize.min,
            children: [leading!, const SizedBox(width: AppSpacing.s3), Flexible(child: Text(label))],
          );
    final button = FilledButton(
      onPressed: loading ? null : onPressed,
      child: Stack(
        alignment: Alignment.center,
        children: [
          Opacity(opacity: loading ? 0 : 1, child: content),
          if (loading)
            SizedBox.square(
              dimension: 20,
              child: CircularProgressIndicator(
                strokeWidth: 2,
                color: Theme.of(context).colorScheme.onSurfaceVariant,
                semanticsLabel: 'Đang gửi',
              ),
            ),
        ],
      ),
    );
    return expand ? SizedBox(width: double.infinity, child: button) : button;
  }
}
