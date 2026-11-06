import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/firebase/push_notifications.dart';
import '../../core/network/api_exception.dart';
import '../../core/providers.dart';
import 'settings_screen.dart' show emailEnabledProvider, settingsApiProvider;

/// ViewModel của A07 – Cài đặt (MB-10, MVVM MB-49). State là giá trị công tắc email đang gửi (null khi không gửi):
/// công tắc đổi ngay, lỗi thì trả lại. Giá trị đã lưu đọc từ `emailEnabledProvider`.
class SettingsViewModel extends Notifier<bool?> {
  @override
  bool? build() => null;

  void retryEmail() => ref.invalidate(emailEnabledProvider);

  /// Trả false khi chưa lưu được (View hiện SnackBar).
  Future<bool> setEmailEnabled(bool value) async {
    state = value;
    try {
      await ref.read(settingsApiProvider).setEmailEnabled(value);
      if (!ref.mounted) return true;
      ref.invalidate(emailEnabledProvider);
      await ref.read(emailEnabledProvider.future);
      return true;
    } on ApiException {
      return false;
    } finally {
      if (ref.mounted) state = null;
    }
  }

  /// Đăng xuất (thu hồi refresh token); `main.dart` dựng lại ProviderScope.
  Future<void> signOut() async {
    await ref.read(pushNotificationsProvider.notifier).unregister();
    await ref.read(authControllerProvider.notifier).signOut();
  }
}

final settingsViewModelProvider =
    NotifierProvider.autoDispose<SettingsViewModel, bool?>(
      SettingsViewModel.new,
    );
