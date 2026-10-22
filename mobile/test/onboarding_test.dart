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
  FakeOnboardingStore({this.seen = false});

  bool seen;
  int markSeenCalls = 0;

  @override
  Future<bool> hasSeen() async => seen;

  @override
  Future<void> markSeen() async {
    seen = true;
    markSeenCalls++;
  }
}

void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  testWidgets('A02 bỏ qua ghi nhận đã xem và chỉ hoàn tất một lần', (
    tester,
  ) async {
    final store = FakeOnboardingStore();
    var finished = 0;
    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.light(),
        home: OnboardingScreen(
          onboardingStore: store,
          onFinished: () => finished++,
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
      MaterialApp(
        theme: AppTheme.light(),
        home: OnboardingScreen(
          onboardingStore: store,
          onFinished: () => finished++,
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
}
