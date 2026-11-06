import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_tokens.dart';
import 'onboarding_store.dart';
import 'onboarding_view_model.dart';

/// A02. Ba nội dung giới thiệu; Bỏ qua và Hoàn tất đều ghi nhận đã xem.
class OnboardingScreen extends ConsumerStatefulWidget {
  const OnboardingScreen({
    super.key,
    required this.onFinished,
    this.onboardingStore,
  });

  final VoidCallback onFinished;
  final OnboardingStore? onboardingStore;

  @override
  ConsumerState<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends ConsumerState<OnboardingScreen> {
  final _controller = PageController();
  int _index = 0;

  static const _slides = [
    (
      Icons.groups_outlined,
      'Lập nhóm',
      'Tạo nhóm và cùng bạn học bắt đầu dự án EXE.',
    ),
    (
      Icons.person_search_outlined,
      'Tìm thành viên',
      'Khám phá hồ sơ và mời người phù hợp vào nhóm.',
    ),
    (
      Icons.schedule_outlined,
      'Theo dõi hạn chót',
      'Nắm các mốc quan trọng để hoàn thành đúng hạn.',
    ),
  ];

  Future<void> _finish() async {
    if (ref.read(onboardingViewModelProvider)) return;
    final done = await ref
        .read(onboardingViewModelProvider.notifier)
        .finish(store: widget.onboardingStore);
    if (!mounted) return;
    if (done) {
      widget.onFinished();
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text(OnboardingViewModel.saveFailed)),
      );
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final finishing = ref.watch(onboardingViewModelProvider);
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.s4),
          child: Column(
            children: [
              Align(
                alignment: Alignment.centerRight,
                child: TextButton(
                  onPressed: finishing ? null : _finish,
                  child: const Text('Bỏ qua'),
                ),
              ),
              Expanded(
                child: PageView.builder(
                  controller: _controller,
                  itemCount: _slides.length,
                  onPageChanged: (index) => setState(() => _index = index),
                  itemBuilder: (context, index) {
                    final slide = _slides[index];
                    return Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(
                          slide.$1,
                          size: AppSpacing.s8 * 3,
                          color: theme.colorScheme.primary,
                        ),
                        const SizedBox(height: AppSpacing.s8),
                        Text(
                          slide.$2,
                          style: theme.textTheme.headlineSmall,
                          textAlign: TextAlign.center,
                        ),
                        const SizedBox(height: AppSpacing.s3),
                        Text(
                          slide.$3,
                          style: theme.textTheme.bodyLarge,
                          textAlign: TextAlign.center,
                        ),
                      ],
                    );
                  },
                ),
              ),
              Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  for (var i = 0; i < _slides.length; i++)
                    Padding(
                      padding: const EdgeInsets.symmetric(
                        horizontal: AppSpacing.s1,
                      ),
                      child: Icon(
                        i == _index ? Icons.circle : Icons.circle_outlined,
                        size: AppSpacing.s3,
                        color: theme.colorScheme.primary,
                      ),
                    ),
                ],
              ),
              const SizedBox(height: AppSpacing.s6),
              SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: finishing
                      ? null
                      : _index == _slides.length - 1
                      ? _finish
                      : () => _controller.nextPage(
                          duration: AppMotion.of(context, AppMotion.slow),
                          curve: Curves.easeOut,
                        ),
                  child: Text(
                    _index == _slides.length - 1 ? 'Bắt đầu' : 'Tiếp tục',
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
