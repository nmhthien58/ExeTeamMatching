import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_sign_in/google_sign_in.dart';

import 'auth/firebase_auth_service.dart';
import 'auth/session.dart';
import 'config/env.dart';
import 'firebase/analytics_service.dart';
import 'firebase/crash_reporter.dart';
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
    // Mở lại app với phiên cũ: gắn lại người dùng cho Crashlytics / Analytics (không log `login`).
    if (user != null) Future.microtask(() => _identify(user));
    return user;
  }

  /// MB-50/51: gắn báo cáo với id nội bộ (không phải email), custom key `role`, `course`.
  /// Analytics / Crashlytics lỗi (hoặc chưa khởi tạo Firebase, như trong test) không ảnh hưởng đăng nhập.
  Future<void> _identify(UserInfo user) async {
    try {
      final analytics = ref.read(analyticsServiceProvider);
      final crash = ref.read(crashReporterProvider);
      await analytics.setUserId(user.id);
      await crash.setUser(user.id);
      await crash.setKey('role', (user.roles.map((r) => r.name).toList()..sort()).join(','));
      if (user.studentId == null) return;
      // Môn của sinh viên lấy thẳng từ hồ sơ qua Dio: myCourseProvider watch AuthController, nên đọc nó từ đây
      // là phụ thuộc vòng (Riverpod báo CircularDependencyError, key `course` không được gắn).
      final profile = await ref.read(dioProvider).get<Map<String, dynamic>>('/api/students/me');
      final course = profile.data?['courseCode'] as String?;
      if (ref.mounted && course != null) await crash.setKey('course', course);
    } catch (e) {
      debugPrint('identify user for Firebase failed: $e');
    }
  }

  Future<void> _forget() async {
    try {
      await ref.read(analyticsServiceProvider).logLogout();
      await ref.read(analyticsServiceProvider).setUserId(null);
      await ref.read(crashReporterProvider).clearUser();
    } catch (e) {
      debugPrint('clear Firebase user failed: $e');
    }
  }

  /// A03: Google Sign-In → Firebase Auth → gửi ID token cho Identity → lưu phiên. Trả `null` khi người dùng hủy.
  /// Ném [SignInFailure] (Firebase) hoặc [ApiException] (Identity, ví dụ `not-in-list` → A04).
  /// Email Google của lần đăng nhập gần nhất – A04 cần nó sau khi đã đăng xuất Google vì bị từ chối.
  String? lastGoogleEmail;

  Future<UserInfo?> signInWithGoogle() async {
    final google = ref.read(googleSignInProvider);
    final account = await google.signIn();
    if (account == null) return null;
    lastGoogleEmail = account.email;
    final authentication = await account.authentication;
    final idToken = authentication.idToken;
    if (idToken == null) {
      throw StateError('Google không trả ID token – kiểm tra GOOGLE_SERVER_CLIENT_ID.');
    }
    final firebase = ref.read(firebaseAuthProvider);
    try {
      await firebase.signInWithGoogle(idToken: idToken, accessToken: authentication.accessToken);
      return await signInWithIdToken(idToken);
    } catch (_) {
      // Firebase hoặc Identity từ chối (không có trong danh sách, tài khoản Bộ môn trên mobile…): bỏ phiên Firebase VÀ
      // Google của tài khoản đó. Không bỏ Google thì lần bấm sau signIn() trả lại ngay tài khoản cũ, không mở hộp chọn,
      // và bị chặn mãi tới khi xóa dữ liệu app (lỗi tìm thấy khi chạy ARTEMIS TC-17, 06/11/2026).
      try {
        await firebase.signOut();
      } catch (_) {}
      try {
        await google.signOut();
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
      unawaited(ref.read(analyticsServiceProvider).logLogin(method: 'google'));
      unawaited(_identify(tokens.user));
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
    await _forget();
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
