import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_sign_in/google_sign_in.dart';

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

  /// A03: Google Sign-In → gửi ID token cho Identity → lưu phiên. Trả `null` khi người dùng hủy (không báo lỗi).
  /// Ném [ApiException] (ví dụ `not-in-list` → A04).
  Future<UserInfo?> signInWithGoogle() async {
    final google = ref.read(googleSignInProvider);
    final account = await google.signIn();
    if (account == null) return null;
    final idToken = (await account.authentication).idToken;
    if (idToken == null) {
      throw StateError('Google không trả ID token – kiểm tra GOOGLE_SERVER_CLIENT_ID.');
    }
    return signInWithIdToken(idToken);
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
      await ref.read(googleSignInProvider).signOut();
    } catch (_) {}
    state = const AsyncData(null);
  }
}
