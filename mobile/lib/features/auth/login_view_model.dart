import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_sign_in/google_sign_in.dart';

import '../../core/auth/firebase_auth_service.dart';
import '../../core/network/api_client.dart';
import '../../core/providers.dart';

/// Trạng thái A03: chờ bấm / đang đăng nhập / lỗi có câu thông báo (banner + Thử lại).
sealed class LoginState {
  const LoginState();
}

final class LoginIdle extends LoginState {
  const LoginIdle();
}

final class LoginPending extends LoginState {
  const LoginPending();
}

final class LoginFailed extends LoginState {
  const LoginFailed(this.message);

  final String message;
}

/// Kết quả một lần của lệnh đăng nhập; View tự điều hướng / hiện SnackBar, không lưu vào state.
sealed class LoginOutcome {
  const LoginOutcome();
}

/// Router tự chuyển vào đúng khung khi phiên có người dùng.
final class LoginSignedIn extends LoginOutcome {
  const LoginSignedIn();
}

/// Người dùng hủy chọn tài khoản (Lab 3 §6: báo nhẹ bằng SnackBar).
final class LoginCancelled extends LoginOutcome {
  const LoginCancelled();
}

/// Email không có trong danh sách học kỳ → A04, hiện email vừa dùng.
final class LoginNotInList extends LoginOutcome {
  const LoginNotInList(this.email);

  final String? email;
}

final class LoginError extends LoginOutcome {
  const LoginError();
}

/// ViewModel của A03 (MB-07, MB-47; mẫu MVVM MB-48). Đăng nhập thật nằm ở [AuthController];
/// ở đây chỉ giữ trạng thái màn và đổi lỗi (Google, Firebase, Identity) thành câu thông báo.
class LoginViewModel extends Notifier<LoginState> {
  static const networkMessage = 'Mất kết nối. Kiểm tra mạng rồi thử lại.';
  static const googleMessage = 'Không đăng nhập được với Google. Thử lại sau.';

  @override
  LoginState build() => const LoginIdle();

  Future<LoginOutcome> signIn() async {
    if (state is LoginPending) return const LoginError();
    state = const LoginPending();
    try {
      final user = await ref.read(authControllerProvider.notifier).signInWithGoogle();
      if (!ref.mounted) return const LoginError();
      state = const LoginIdle();
      return user == null ? const LoginCancelled() : const LoginSignedIn();
    } on PlatformException catch (e) {
      // Lỗi từ Google Sign-In (không phải từ Identity).
      if (e.code == GoogleSignIn.kSignInCanceledError) {
        state = const LoginIdle();
        return const LoginCancelled();
      }
      return _fail(e.code == GoogleSignIn.kNetworkError ? networkMessage : googleMessage);
    } on SignInFailure catch (e) {
      // Firebase Authentication từ chối credential hoặc mất mạng.
      return _fail(e.isNetwork ? networkMessage : googleMessage);
    } catch (e) {
      final error = e.apiError;
      if (error.code == 'not-in-list') {
        state = const LoginIdle();
        return LoginNotInList(ref.read(googleSignInProvider).currentUser?.email);
      }
      return _fail(error.isNetwork ? networkMessage : error.title);
    }
  }

  LoginOutcome _fail(String message) {
    if (ref.mounted) state = LoginFailed(message);
    return const LoginError();
  }
}

final loginViewModelProvider = NotifierProvider.autoDispose<LoginViewModel, LoginState>(LoginViewModel.new);
