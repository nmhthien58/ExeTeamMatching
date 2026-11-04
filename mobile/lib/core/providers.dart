import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_sign_in/google_sign_in.dart';

import 'auth/firebase_auth_service.dart';
import 'auth/session.dart';
import 'config/env.dart';
import 'network/api_client.dart';

/// Phiên đăng nhập (secure storage). Ghi đè trong test bằng `sessionStoreProvider.overrideWithValue(...)`.
final sessionStoreProvider = Provider<SessionStore>((ref) => SessionStore());

/// Dio dùng chung cho mọi màn hình (MB-04). Hết phiên (refresh lỗi) thì đưa về màn đăng nhập.
final dioProvider = Provider<Dio>((ref) {
  return createDio(
    session: ref.watch(sessionStoreProvider),
    onSessionExpired: () => ref.invalidate(authControllerProvider),
  );
});

final googleSignInProvider = Provider<GoogleSignIn>(
  (ref) => GoogleSignIn(
    scopes: const ['email'],
    serverClientId: Env.googleServerClientId.isEmpty ? null : Env.googleServerClientId,
  ),
);

/// Firebase Authentication (MB-47). Ghi đè trong test bằng một [FirebaseAuthGateway] giả.
final firebaseAuthProvider = Provider<FirebaseAuthGateway>((ref) => FirebaseAuthService());

/// Gọi sau khi đăng xuất để bỏ mọi dữ liệu đã cache của người dùng cũ. `main()` ghi đè bằng hàm dựng lại
/// ProviderScope; mặc định không làm gì (test).
final signedOutProvider = Provider<void Function()>((ref) => () {});

/// Trạng thái đăng nhập: `null` = chưa đăng nhập.
final authControllerProvider = AsyncNotifierProvider<AuthController, UserInfo?>(AuthController.new);

class AuthController extends AsyncNotifier<UserInfo?> {
  @override
  Future<UserInfo?> build() async {
    final session = ref.read(sessionStoreProvider);
    final user = await session.readUser();
    // Có user nhưng không có refresh token = phiên hỏng.
    if (user != null && await session.readRefreshToken() == null) return null;
    return user;
  }

  /// A03: Google Sign-In → Firebase Auth → gửi ID token cho Identity → lưu phiên. Trả `null` khi người dùng hủy.
  /// Ném [SignInFailure] (Firebase) hoặc [ApiException] (Identity, ví dụ `not-in-list` → A04).
  Future<UserInfo?> signInWithGoogle() async {
    final google = ref.read(googleSignInProvider);
    final account = await google.signIn();
    if (account == null) return null;
    final authentication = await account.authentication;
    final idToken = authentication.idToken;
    if (idToken == null) {
      throw StateError('Google không trả ID token – kiểm tra GOOGLE_SERVER_CLIENT_ID.');
    }
    final firebase = ref.read(firebaseAuthProvider);
    await firebase.signInWithGoogle(idToken: idToken, accessToken: authentication.accessToken);
    try {
      return await signInWithIdToken(idToken);
    } catch (_) {
      // Identity từ chối (ví dụ không có trong danh sách): không giữ phiên Firebase của tài khoản đó.
      try {
        await firebase.signOut();
      } catch (_) {}
      rethrow;
    }
  }

  Future<UserInfo> signInWithIdToken(String idToken) async {
    try {
      final response = await ref
          .read(dioProvider)
          .post<Map<String, dynamic>>('/api/auth/google', data: {'idToken': idToken, 'client': 'mobile'});
      final tokens = TokenResponse.fromJson(response.data!);
      await ref.read(sessionStoreProvider).save(tokens);
      state = AsyncData(tokens.user);
      return tokens.user;
    } on DioException catch (e) {
      throw e.apiError;
    }
  }

  Future<void> signOut() async {
    final session = ref.read(sessionStoreProvider);
    final refreshToken = await session.readRefreshToken();
    try {
      if (refreshToken != null) {
        await ref.read(dioProvider).post<void>('/api/auth/logout', data: {'refreshToken': refreshToken});
      }
    } catch (_) {
      // Đăng xuất trên máy vẫn tiếp tục.
    }
    await session.clear();
    try {
      await ref.read(firebaseAuthProvider).signOut();
    } catch (_) {}
    try {
      await ref.read(googleSignInProvider).signOut();
    } catch (_) {}
    state = const AsyncData(null);
    ref.read(signedOutProvider)();
  }
}
