import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/auth/firebase_auth_service.dart';
import 'package:exe_team_matching/core/providers.dart';
import 'package:exe_team_matching/features/auth/login_view_model.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:google_sign_in/google_sign_in.dart';
import 'package:mocktail/mocktail.dart';

class _MockGoogle extends Mock implements GoogleSignIn {}

class _MockAccount extends Mock implements GoogleSignInAccount {}

class _MockAuth extends Mock implements GoogleSignInAuthentication {}

class _FakeFirebase implements FirebaseAuthGateway {
  SignInFailure? failure;

  @override
  Future<void> signInWithGoogle({required String idToken, String? accessToken}) async {
    if (failure != null) throw failure!;
  }

  @override
  Future<void> signOut() async {}
}

/// MB-48 – ViewModel của A03 test riêng: đổi lỗi Google / Firebase / Identity thành state và kết quả.
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late _MockGoogle google;
  late _FakeFirebase firebase;

  setUp(() {
    FlutterSecureStorage.setMockInitialValues({});
    google = _MockGoogle();
    firebase = _FakeFirebase();
    when(() => google.signOut()).thenAnswer((_) async => null);
    final account = _MockAccount();
    final auth = _MockAuth();
    when(() => auth.idToken).thenReturn('id-token');
    when(() => account.authentication).thenAnswer((_) async => auth);
    when(() => account.email).thenReturn('nham@gmail.com');
    when(() => google.signIn()).thenAnswer((_) async => account);
    when(() => google.currentUser).thenReturn(account);
  });

  ProviderContainer create(void Function(RequestOptions, RequestInterceptorHandler) identity) {
    final dio = Dio(BaseOptions(baseUrl: 'http://test'))..interceptors.add(InterceptorsWrapper(onRequest: identity));
    final c = ProviderContainer(overrides: [
      googleSignInProvider.overrideWithValue(google),
      firebaseAuthProvider.overrideWithValue(firebase),
      dioProvider.overrideWithValue(dio),
    ]);
    addTearDown(c.dispose);
    c.listen(loginViewModelProvider, (_, _) {});
    return c;
  }

  test('hủy chọn tài khoản: LoginCancelled, state về Idle', () async {
    when(() => google.signIn()).thenAnswer((_) async => null);
    final c = create((o, h) => fail('Không gọi Identity khi đã hủy'));

    expect(await c.read(loginViewModelProvider.notifier).signIn(), isA<LoginCancelled>());
    expect(c.read(loginViewModelProvider), isA<LoginIdle>());
  });

  test('Google báo hủy bằng PlatformException: cũng là LoginCancelled', () async {
    when(() => google.signIn()).thenThrow(PlatformException(code: GoogleSignIn.kSignInCanceledError));
    final c = create((o, h) => fail('Không gọi Identity'));

    expect(await c.read(loginViewModelProvider.notifier).signIn(), isA<LoginCancelled>());
  });

  test('Firebase mất mạng: LoginFailed với câu mất kết nối', () async {
    firebase.failure = const SignInFailure(isNetwork: true);
    final c = create((o, h) => fail('Không gọi Identity khi Firebase lỗi'));

    expect(await c.read(loginViewModelProvider.notifier).signIn(), isA<LoginError>());
    expect((c.read(loginViewModelProvider) as LoginFailed).message, LoginViewModel.networkMessage);
  });

  test('Identity trả not-in-list: LoginNotInList mang email vừa dùng', () async {
    final c = create((o, h) => h.reject(DioException(
          requestOptions: o,
          response: Response(requestOptions: o, statusCode: 403, data: {
            'type': 'https://exe-team-matching/errors/not-in-list',
            'title': 'Email không có trong danh sách',
            'status': 403,
          }),
        )));

    final outcome = await c.read(loginViewModelProvider.notifier).signIn();
    expect((outcome as LoginNotInList).email, 'nham@gmail.com');
    expect(c.read(loginViewModelProvider), isA<LoginIdle>());
  });
}
