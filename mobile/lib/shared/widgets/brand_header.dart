import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_tokens.dart';
import 'brand_header_view_model.dart';

/// Phần chào của trang chủ, ngay dưới [BrandAppBar] (đổi 05/11/2026 so với Lab 2, xem docs/design-traceability.md).
/// Bản 6 (06/11/2026, MB-64): khối dải cam FPT có bóng nổi – lời chào, ngày + tuần học, vòng tiến độ học kỳ chạy dần,
/// chip giai đoạn kiểu kính mờ ("Còn 12 ngày"). Chữ trắng đậm trên dải; vòng sáng trang trí ở góc.
class BrandHeader extends ConsumerWidget {
  const BrandHeader({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final data = ref.watch(brandHeaderProvider);
    final onHero = theme.colorScheme.onPrimary;
    final progress = data.semesterProgress;
    return Container(
      decoration: BoxDecoration(
        gradient: AppGradients.hero,
        borderRadius: BorderRadius.circular(AppRadius.xl - 4),
        boxShadow: AppShadows.raised,
      ),
      clipBehavior: Clip.antiAlias,
      child: Stack(
        children: [
          // Vòng sáng trang trí ở góc phải trên.
          Positioned(
            top: -90,
            right: -70,
            child: ExcludeSemantics(
              child: Container(
                width: 220,
                height: 220,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  gradient: RadialGradient(colors: [onHero.withValues(alpha: .28), onHero.withValues(alpha: 0)]),
                ),
              ),
            ),
          ),
          Padding(
            padding: const EdgeInsets.all(AppSpacing.s4 + AppSpacing.s1),
            child: Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        data.greeting,
                        style: theme.textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w800, color: onHero),
                      ),
                      const SizedBox(height: AppSpacing.s1),
                      Text(data.dateLine, style: theme.textTheme.bodyMedium?.copyWith(color: onHero.withValues(alpha: .92))),
                      if (data.chip != null) ...[
                        const SizedBox(height: AppSpacing.s3),
                        _GlassChip(label: data.chip!),
                      ],
                    ],
                  ),
                ),
                if (progress != null) ...[
                  const SizedBox(width: AppSpacing.s3),
                  Semantics(label: data.progressLabel, child: ExcludeSemantics(child: _ProgressRing(value: progress))),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Chip trên dải cam: nền trắng mờ, viền trắng mờ, chữ trắng.
class _GlassChip extends StatelessWidget {
  const _GlassChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final onHero = theme.colorScheme.onPrimary;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s3, vertical: AppSpacing.s1 + 2),
      decoration: BoxDecoration(
        color: onHero.withValues(alpha: .18),
        borderRadius: BorderRadius.circular(AppRadius.full),
        border: Border.all(color: onHero.withValues(alpha: .32)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.event_outlined, size: 16, color: onHero),
          const SizedBox(width: AppSpacing.s1),
          Flexible(child: Text(label, style: theme.textTheme.labelLarge?.copyWith(color: onHero, fontWeight: FontWeight.w600))),
        ],
      ),
    );
  }
}

/// Vòng tiến độ học kỳ: chạy từ 0 tới giá trị khi mở màn (tắt khi máy bật giảm chuyển động), số % ở giữa.
class _ProgressRing extends StatelessWidget {
  const _ProgressRing({required this.value});

  final double value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final onHero = theme.colorScheme.onPrimary;
    return TweenAnimationBuilder<double>(
      tween: Tween(begin: 0, end: value.clamp(0, 1)),
      duration: AppMotion.of(context, const Duration(milliseconds: 900)),
      curve: Curves.easeOutCubic,
      builder: (context, v, _) => SizedBox(
        width: 76,
        height: 76,
        child: CustomPaint(
          painter: _RingPainter(value: v, track: onHero.withValues(alpha: .24), color: onHero),
          child: Center(
            child: Text(
              '${(v * 100).round()}%',
              style: theme.textTheme.titleMedium?.copyWith(color: onHero, fontWeight: FontWeight.w800),
            ),
          ),
        ),
      ),
    );
  }
}

class _RingPainter extends CustomPainter {
  _RingPainter({required this.value, required this.track, required this.color});

  final double value;
  final Color track;
  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    const stroke = 7.0;
    final rect = Offset.zero & size;
    final arc = rect.deflate(stroke / 2);
    final paint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = stroke
      ..strokeCap = StrokeCap.round;
    canvas.drawArc(arc, 0, math.pi * 2, false, paint..color = track);
    if (value > 0) canvas.drawArc(arc, -math.pi / 2, math.pi * 2 * value, false, paint..color = color);
  }

  @override
  bool shouldRepaint(_RingPainter old) => old.value != value || old.color != color || old.track != track;
}
