import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/navigation.dart';
import '../../core/providers.dart';
import 'onboarding_store.dart';

/// ViewModel của A01 – Splash (MB-49): đọc phiên rồi chọn màn kế tiếp.
class SplashViewModel extends Notifier<void> {
  @override
  void build() {}

  /// Route kế tiếp: khung theo vai trò khi đã đăng nhập, A02 lần đầu mở, còn lại A03.
  /// [store] thay service (màn nhận từ tham số để test).
  Future<String> resolve({OnboardingStore? store}) async {
    try {
      final user = await ref.read(authControllerProvider.future);
      if (user != null) {
        final shell = shellOf(user);
        return shell == null ? '/chi-dung-web' : shellHome[shell]!;
      }
      final seen = await (store ?? ref.read<OnboardingStore>(onboardingStoreProvider)).hasSeen();
      return seen ? '/dang-nhap' : '/gioi-thieu';
    } catch (_) {
      return '/dang-nhap';
    }
  }
}

final splashViewModelProvider = NotifierProvider.autoDispose<SplashViewModel, void>(SplashViewModel.new);

/// ViewModel của A02 – Giới thiệu (MB-49): ghi nhận đã xem, chặn bấm lần hai. State = đang lưu.
class OnboardingViewModel extends Notifier<bool> {
  static const saveFailed = 'Không lưu được trạng thái giới thiệu. Vui lòng thử lại.';

  @override
  bool build() => false;

  /// Trả `true` khi đã lưu (View chuyển sang A03); `false` khi đang lưu dở hoặc lỗi.
  Future<bool> finish({OnboardingStore? store}) async {
    if (state) return false;
    state = true;
    try {
      await (store ?? ref.read<OnboardingStore>(onboardingStoreProvider)).markSeen();
      return true;
    } catch (_) {
      return false;
    } finally {
      if (ref.mounted) state = false;
    }
  }
}

final onboardingViewModelProvider = NotifierProvider.autoDispose<OnboardingViewModel, bool>(OnboardingViewModel.new);
