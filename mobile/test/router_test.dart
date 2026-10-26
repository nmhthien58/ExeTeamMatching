import 'dart:convert';

import 'package:exe_team_matching/app/app.dart';
import 'package:exe_team_matching/core/phase.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

Map<String, String> signedIn(List<String> roles, {String? studentId}) => {
      'exe.refreshToken': 'r',
      'exe.user': jsonEncode({'id': 'u', 'email': 'a@fpt.edu.vn', 'fullName': 'A', 'roles': roles, 'studentId': studentId}),
    };

Future<void> pumpApp(WidgetTester tester, {required List<CoursePhase> phases, String? course}) async {
  await tester.pumpWidget(ProviderScope(
    overrides: [
      phasesProvider.overrideWith((ref) async => phases),
      myCourseProvider.overrideWith((ref) async => course),
    ],
    child: const ExeTeamMatchingApp(),
  ));
  await tester.pumpAndSettle();
}

void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  testWidgets('đã xem giới thiệu, chưa đăng nhập → A03', (tester) async {
    FlutterSecureStorage.setMockInitialValues({'exe.onboardingSeen': 'true'});
    await pumpApp(tester, phases: const []);
    expect(find.text('Đăng nhập với Google'), findsOneWidget);
  });

  testWidgets('lần đầu mở app → giới thiệu → bỏ qua → đăng nhập', (tester) async {
    FlutterSecureStorage.setMockInitialValues({});
    await pumpApp(tester, phases: const []);
    expect(find.text('Lập nhóm'), findsOneWidget);
    await tester.tap(find.text('Bỏ qua'));
    await tester.pumpAndSettle();
    expect(find.text('Đăng nhập với Google'), findsOneWidget);
    expect(await const FlutterSecureStorage().read(key: 'exe.onboardingSeen'), 'true');
    GoRouterHelper.go(tester, '/');
    await tester.pumpAndSettle();
    expect(find.text('Đăng nhập với Google'), findsOneWidget);
  });

  testWidgets('sinh viên trong đợt lập nhóm: tab Khám phá', (tester) async {
    FlutterSecureStorage.setMockInitialValues(signedIn(['Student'], studentId: 's1'));
    await pumpApp(tester, phases: const [CoursePhase(courseCode: 'EXE101', isTeamFormation: true)], course: 'EXE101');

    expect(find.text('Khám phá'), findsOneWidget);
    expect(find.text('Dự án'), findsNothing);
    expect(find.text('S01 · Trang chủ'), findsOneWidget);
  });

  testWidgets('sinh viên sau khi đợt đóng: tab Dự án, mở Khám phá từ link cũ thì hiện hộp nhắc', (tester) async {
    FlutterSecureStorage.setMockInitialValues(signedIn(['Student'], studentId: 's1'));
    await pumpApp(tester, phases: const [CoursePhase(courseCode: 'EXE101', isTeamFormation: false)], course: 'EXE101');

    expect(find.text('Dự án'), findsOneWidget);
    expect(find.text('Khám phá'), findsNothing);

    // Mở đường dẫn cũ /sv/kham-pha.
    GoRouterHelper.go(tester, '/sv/kham-pha');
    await tester.pumpAndSettle();
    expect(find.text('Đợt lập nhóm đã đóng'), findsOneWidget);

    await tester.tap(find.text('Mở dự án'));
    await tester.pumpAndSettle();
    expect(find.widgetWithText(AppBar, 'Dự án'), findsOneWidget);
  });

  testWidgets('giảng viên làm mentor dùng khung giảng viên; ngoài đợt lập nhóm có tab Lịch', (tester) async {
    FlutterSecureStorage.setMockInitialValues(signedIn(['Lecturer', 'Mentor']));
    await pumpApp(tester, phases: const [CoursePhase(courseCode: 'EXE201', isTeamFormation: false)]);

    expect(find.text('L01 · Trang chủ'), findsOneWidget);
    expect(find.text('Lịch'), findsOneWidget);
    expect(find.text('Đề tài'), findsNothing);
  });
}

/// Điều hướng qua GoRouter của ứng dụng đang chạy trong test.
abstract final class GoRouterHelper {
  static void go(WidgetTester tester, String location) {
    final context = tester.element(find.byType(Scaffold).first);
    GoRouter.of(context).go(location);
  }
}
