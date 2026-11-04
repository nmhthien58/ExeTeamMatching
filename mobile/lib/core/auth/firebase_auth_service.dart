import 'package:firebase_auth/firebase_auth.dart';

/// Lỗi đăng nhập Firebase đã chuyển sang dạng màn hình hiểu được, để widget không phụ thuộc SDK.
class SignInFailure implements Exception {
  const SignInFailure({required this.isNetwork, this.code});

  final bool isNetwork;
  final String? code;

  @override
  String toString() => 'SignInFailure(${code ?? '-'})';
}

/// Firebase Authentication (MB-47). Hệ thống vẫn dùng JWT của Identity; Firebase giữ danh tính Google
/// để Console có người dùng, và cho App Check / AI Logic / Analytics dùng sau này.
abstract interface class FirebaseAuthGateway {
  /// Ném [SignInFailure] khi Firebase từ chối hoặc mất mạng.
  Future<void> signInWithGoogle({required String idToken, String? accessToken});

  Future<void> signOut();
}

class FirebaseAuthService implements FirebaseAuthGateway {
  // Lấy instance khi dùng, không lấy lúc tạo, để provider tạo được cả khi chưa khởi tạo Firebase (test).
  FirebaseAuth get _auth => FirebaseAuth.instance;

  @override
  Future<void> signInWithGoogle({required String idToken, String? accessToken}) async {
    try {
      await _auth.signInWithCredential(GoogleAuthProvider.credential(idToken: idToken, accessToken: accessToken));
    } on FirebaseAuthException catch (e) {
      throw SignInFailure(isNetwork: e.code == 'network-request-failed', code: e.code);
    }
  }

  @override
  Future<void> signOut() => _auth.signOut();
}
