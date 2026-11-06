import 'dart:convert';

import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/onboarding/onboarding_screen.dart';
import 'package:exe_team_matching/features/onboarding/onboarding_store.dart';
import 'package:exe_team_matching/features/onboarding/splash_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';

class FakeOnboardingStore extends OnboardingStore {
  FakeOnboardingStore({
    this.seen = false,
    this.failRead = false,
    this.failWrite = false,
  });

  bool seen;
  bool failRead;
  bool failWrite;
  int markSeenCalls = 0;

  @override
  Future<bool> hasSeen() async {
    if (failRead) throw StateError('Không đọc được secure storage');
    return seen;
  }

  @override
  Future<void> markSeen() async {
    if (failWrite) throw StateError('Không ghi được secure storage');
    seen = true;
    markSeenCalls++;
  }
}

void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  test('A02 lưu trạng thái đã xem trên thiết bị qua nhiều instance', () async {
    FlutterSecureStorage.setMockInitialValues({});
    final first = OnboardingStore();
    expect(await first.hasSeen(), isFalse);
    await first.markSeen();
    expect(await OnboardingStore().hasSeen(), isTrue);
  });

  testWidgets('A02 báo lỗi lưu và cho thử hoàn tất lại', (tester) async {
    final store = FakeOnboardingStore(failWrite: true);
    var finished = 0;
    await tester.pumpWidget(
      ProviderScope(
        child: MaterialApp(
          theme: AppTheme.light(),
          home: OnboardingScreen(
            onboardingStore: store,
            onFinished: () => finished++,
          ),
        ),
      ),
    );
    await tester.tap(find.text('Bỏ qua'));
    await tester.pumpAndSettle();
    expect(finished, 0);
    expect(
      find.text('Không lưu được trạng thái giới thiệu. Vui lòng thử lại.'),
      findsOneWidget,
    );

    store.failWrite = false;
    await tester.tap(find.text('Bỏ qua'));
    await tester.pump();
    expect(store.markSeenCalls, 1);
    expect(finished, 1);
  });

  testWidgets('A02 bỏ qua ghi nhận đã xem và chỉ hoàn tất một lần', (
    tester,
  ) async {
    final store = FakeOnboardingStore();
    var finished = 0;
    await tester.pumpWidget(
      ProviderScope(
        child: MaterialApp(
          theme: AppTheme.light(),
          home: OnboardingScreen(
            onboardingStore: store,
            onFinished: () => finished++,
          ),
        ),
      ),
    );
    expect(find.text('Lập nhóm'), findsOneWidget);
    await tester.tap(find.text('Bỏ qua'));
    await tester.pump();
    expect(store.markSeenCalls, 1);
    expect(finished, 1);
  });

  testWidgets('A02 đi qua ba trang rồi bắt đầu', (tester) async {
    final store = FakeOnboardingStore();
    var finished = 0;
    await tester.pumpWidget(
      ProviderScope(
        child: MaterialApp(
          theme: AppTheme.light(),
          home: OnboardingScreen(
            onboardingStore: store,
            onFinished: () => finished++,
          ),
        ),
      ),
    );
    await tester.tap(find.text('Tiếp tục'));
    await tester.pumpAndSettle();
    expect(find.text('Tìm thành viên'), findsOneWidget);
    await tester.tap(find.text('Tiếp tục'));
    await tester.pumpAndSettle();
    expect(find.text('Theo dõi hạn chót'), findsOneWidget);
    await tester.tap(find.text('Bắt đầu'));
    await tester.pump();
    expect(store.markSeenCalls, 1);
    expect(finished, 1);
  });

  for (final seen in [false, true]) {
    testWidgets('A01 chưa đăng nhập, đã xem onboarding: $seen', (tester) async {
      FlutterSecureStorage.setMockInitialValues({});
      String? route;
      await tester.pumpWidget(
        ProviderScope(
          child: MaterialApp(
            theme: AppTheme.light(),
            home: SplashScreen(
              onboardingStore: FakeOnboardingStore(seen: seen),
              onResolved: (value) => route = value,
            ),
          ),
        ),
      );
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 100));
      expect(route, seen ? '/dang-nhap' : '/gioi-thieu');
    });
  }

  testWidgets('A01 lỗi đọc onboarding chuyển tới đăng nhập', (tester) async {
    FlutterSecureStorage.setMockInitialValues({});
    String? route;
    await tester.pumpWidget(
      ProviderScope(
        child: MaterialApp(
          theme: AppTheme.light(),
          home: SplashScreen(
            onboardingStore: FakeOnboardingStore(failRead: true),
            onResolved: (value) => route = value,
          ),
        ),
      ),
    );
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 100));
    expect(route, '/dang-nhap');
  });

  testWidgets('A01 phiên sinh viên hợp lệ đi thẳng vào khung sinh viên', (
    tester,
  ) async {
    FlutterSecureStorage.setMockInitialValues({
      'exe.refreshToken': 'demo-token',
      'exe.user': jsonEncode({
        'id': 'student-1',
        'email': 'student@example.test',
        'fullName': 'Sinh viên thử',
        'roles': ['Student'],
        'studentId': 'student-1',
      }),
    });
    String? route;
    await tester.pumpWidget(
      ProviderScope(
        child: MaterialApp(
          theme: AppTheme.light(),
          home: SplashScreen(
            onboardingStore: FakeOnboardingStore(),
            onResolved: (value) => route = value,
          ),
        ),
      ),
    );
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 100));
    expect(route, '/sv');
  });

  for (final (role, expected) in [
    ('Lecturer', '/gv'),
    ('Mentor', '/mentor'),
    ('Admin', '/chi-dung-web'),
  ]) {
    testWidgets('A01 phiên $role vào đúng màn $expected', (tester) async {
      FlutterSecureStorage.setMockInitialValues({
        'exe.refreshToken': 'demo-token',
        'exe.user': jsonEncode({
          'id': 'person-1',
          'email': 'person@example.test',
          'fullName': 'Người dùng thử',
          'roles': [role],
        }),
      });
      String? route;
      await tester.pumpWidget(
        ProviderScope(
          child: MaterialApp(
            theme: AppTheme.light(),
            home: SplashScreen(
              onboardingStore: FakeOnboardingStore(),
              onResolved: (value) => route = value,
            ),
          ),
        ),
      );
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 100));
      expect(route, expected);
    });
  }

  testWidgets(
    'A01 thông tin người dùng thiếu refresh token không được coi là phiên hợp lệ',
    (tester) async {
      FlutterSecureStorage.setMockInitialValues({
        'exe.user': jsonEncode({
          'id': 'student-1',
          'email': 'student@example.test',
          'fullName': 'Sinh viên thử',
          'roles': ['Student'],
        }),
      });
      String? route;
      await tester.pumpWidget(
        ProviderScope(
          child: MaterialApp(
            theme: AppTheme.light(),
            home: SplashScreen(
              onboardingStore: FakeOnboardingStore(seen: true),
              onResolved: (value) => route = value,
            ),
          ),
        ),
      );
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 100));
      expect(route, '/dang-nhap');
    },
  );
}
