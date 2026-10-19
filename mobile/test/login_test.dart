import 'package:dio/dio.dart';
import 'package:exe_team_matching/app/app.dart';
import 'package:exe_team_matching/core/phase.dart';
import 'package:exe_team_matching/core/providers.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:google_sign_in/google_sign_in.dart';
import 'package:mocktail/mocktail.dart';

class _MockGoogle extends Mock implements GoogleSignIn {}

class _MockAccount extends Mock implements GoogleSignInAccount {}

class _MockAuth extends Mock implements GoogleSignInAuthentication {}

/// Dio trả lời cố định cho POST /api/auth/google.
Dio _dio(void Function(RequestOptions, RequestInterceptorHandler) onRequest) =>
    Dio(BaseOptions(baseUrl: 'http://test'))..interceptors.add(InterceptorsWrapper(onRequest: onRequest));

void _problem(RequestOptions o, RequestInterceptorHandler h, int status, String code, String title) => h.reject(DioException(
      requestOptions: o,
      response: Response(
        requestOptions: o,
        statusCode: status,
        data: {'type': 'https://exe-team-matching/errors/$code', 'title': title, 'status': status},
      ),
    ));

/// MB-07 – A03 đăng nhập Google, A04 tài khoản chưa có trong danh sách.
void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);
  late _MockGoogle google;

  setUp(() {
    FlutterSecureStorage.setMockInitialValues({});
    google = _MockGoogle();
    when(() => google.signOut()).thenAnswer((_) async => null);
  });

  void signsInAs(String email) {
    final account = _MockAccount();
    final auth = _MockAuth();
    when(() => auth.idToken).thenReturn('id-token');
    when(() => account.authentication).thenAnswer((_) async => auth);
    when(() => account.email).thenReturn(email);
    when(() => google.signIn()).thenAnswer((_) async => account);
    when(() => google.currentUser).thenReturn(account);
  }

  Future<void> pump(WidgetTester tester, Dio dio) async {
    await tester.pumpWidget(ProviderScope(
      overrides: [
        googleSignInProvider.overrideWithValue(google),
        dioProvider.overrideWithValue(dio),
        phasesProvider.overrideWith((ref) async => const [CoursePhase(courseCode: 'EXE101', isTeamFormation: true)]),
        myCourseProvider.overrideWith((ref) async => 'EXE101'),
      ],
      child: const ExeTeamMatchingApp(),
    ));
    await tester.pumpAndSettle();
  }

  testWidgets('hủy chọn tài khoản: về mặc định, không báo lỗi', (tester) async {
    when(() => google.signIn()).thenAnswer((_) async => null);
    await pump(tester, _dio((o, h) => fail('Không được gọi Identity khi đã hủy')));

    await tester.tap(find.text('Đăng nhập với Google'));
    await tester.pumpAndSettle();

    expect(find.text('Đăng nhập với Google'), findsOneWidget);
    expect(find.textContaining('Mất kết nối'), findsNothing);
  });

  testWidgets('Google báo hủy bằng PlatformException: không báo lỗi', (tester) async {
    when(() => google.signIn()).thenThrow(PlatformException(code: GoogleSignIn.kSignInCanceledError));
    await pump(tester, _dio((o, h) => fail('Không được gọi Identity')));

    await tester.tap(find.text('Đăng nhập với Google'));
    await tester.pumpAndSettle();

    expect(find.textContaining('Mất kết nối'), findsNothing);
    expect(find.textContaining('Không đăng nhập được'), findsNothing);
  });

  testWidgets('mất mạng: banner lỗi kèm Thử lại', (tester) async {
    signsInAs('vy@fpt.edu.vn');
    await pump(tester, _dio((o, h) => h.reject(DioException(requestOptions: o, type: DioExceptionType.connectionError))));

    await tester.tap(find.text('Đăng nhập với Google'));
    await tester.pumpAndSettle();

    expect(find.text('Mất kết nối. Kiểm tra mạng rồi thử lại.'), findsOneWidget);
    expect(find.text('Thử lại'), findsOneWidget);
  });

  testWidgets('email không có trong danh sách → A04 hiện email, nút dùng tài khoản khác về A03', (tester) async {
    signsInAs('nham@gmail.com');
    await pump(tester, _dio((o, h) => _problem(o, h, 403, 'not-in-list', 'Email không có trong danh sách')));

    await tester.tap(find.text('Đăng nhập với Google'));
    await tester.pumpAndSettle();

    expect(find.text('Tài khoản chưa có trong danh sách'), findsOneWidget);
    expect(find.text('nham@gmail.com'), findsOneWidget);

    await tester.tap(find.text('Dùng tài khoản khác'));
    await tester.pumpAndSettle();

    verify(() => google.signOut()).called(1);
    expect(find.text('Đăng nhập với Google'), findsOneWidget);
  });

  testWidgets('đăng nhập thành công: lưu phiên và vào khung sinh viên', (tester) async {
    signsInAs('vy@fpt.edu.vn');
    late Map<String, dynamic> sent;
    await pump(tester, _dio((o, h) {
      sent = Map<String, dynamic>.from(o.data as Map);
      h.resolve(Response(requestOptions: o, statusCode: 200, data: {
        'accessToken': 'a',
        'accessTokenExpiresAt': '2030-01-01T00:00:00Z',
        'refreshToken': 'r',
        'refreshTokenExpiresAt': '2030-01-08T00:00:00Z',
        'user': {'id': 'u', 'email': 'vy@fpt.edu.vn', 'fullName': 'Vy', 'roles': ['Student'], 'studentId': 's1'},
      }));
    }));

    await tester.tap(find.text('Đăng nhập với Google'));
    await tester.pumpAndSettle();

    expect(sent, {'idToken': 'id-token', 'client': 'mobile'});
    expect(find.text('Đăng nhập với Google'), findsNothing);
    expect(find.text('Khám phá'), findsOneWidget);
  });
}
