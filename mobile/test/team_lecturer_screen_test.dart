import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/app/feature_routes.dart';
import 'package:exe_team_matching/app/navigation.dart';
import 'package:exe_team_matching/features/lecturers/team_lecturer_screen.dart';
import 'package:exe_team_matching/features/my_team/my_team_api.dart' show MyTeam, myTeamDetailProvider;
import 'package:exe_team_matching/features/my_team/my_team_screen.dart';
import 'package:exe_team_matching/features/my_team/project_card.dart' show teamProjectProvider;
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);
  const team = TeamDetail(
    card: TeamCard(id: 'team-1', name: 'Nhóm A', status: 'ACTIVE', memberCount: 4),
    members: [], isLeader: true,
  );

  Widget app(TeamLecturerData data, {TeamDetail? selectedTeam = team}) => ProviderScope(
        overrides: [
          myTeamProvider.overrideWith((ref) async => selectedTeam),
          teamLecturerProvider('team-1').overrideWith((ref) async => data),
        ],
        child: MaterialApp(theme: AppTheme.light(), home: const TeamLecturerScreen()),
      );

  testWidgets('trước ghép: hiện nguyện vọng và giảng viên quan tâm', (tester) async {
    await tester.pumpWidget(app(const TeamLecturerData(preferences: {
      'locked': false,
      'preferences': [
        {'fullName': 'Giảng viên A', 'rank': 1, 'isInterested': true},
      ],
    })));
    await tester.pumpAndSettle();
    expect(find.text('Chưa ghép giảng viên'), findsOneWidget);
    expect(find.text('Giảng viên A'), findsOneWidget);
    expect(find.text('Quan tâm nhóm bạn'), findsOneWidget);
    expect(find.text('Chỉnh sửa nguyện vọng'), findsNothing);
  });

  testWidgets('nút chỉnh sửa mở S23 khi route MB-27 đã được đăng ký', (tester) async {
    final router = GoRouter(initialLocation: teamLecturerPath, routes: [
      GoRoute(path: teamLecturerPath, builder: (_, _) => const TeamLecturerScreen()),
      GoRoute(path: '/sv/nhom/giang-vien', builder: (_, _) => const Scaffold(body: Text('S23'))),
    ]);
    addTearDown(router.dispose);
    await tester.pumpWidget(ProviderScope(
      overrides: [
        myTeamProvider.overrideWith((ref) async => team),
        teamLecturerProvider('team-1').overrideWith((ref) async =>
            const TeamLecturerData(preferences: {'locked': false, 'preferences': []})),
      ],
      child: MaterialApp.router(theme: AppTheme.light(), routerConfig: router),
    ));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Chỉnh sửa nguyện vọng'));
    await tester.pumpAndSettle();
    expect(find.text('S23'), findsOneWidget);
  });

  testWidgets('từ S15 mở S25 qua route ứng dụng', (tester) async {
    final router = GoRouter(initialLocation: '/sv/nhom', routes: [
      GoRoute(path: '/sv/nhom', builder: (_, _) => const Scaffold(body: MyTeamScreen())),
      ...featureRoutes(Shell.student),
    ]);
    addTearDown(router.dispose);
    await tester.pumpWidget(ProviderScope(
      overrides: [
        myTeamDetailProvider.overrideWith((ref) async => const MyTeam(
              id: 'team-1', name: 'Nhóm A', status: 'ACTIVE', courseCode: 'EXE101',
              members: [], domains: [], isLeader: true, memberCount: 4, specializationCount: 2,
            )),
        teamProjectProvider('team-1').overrideWith((ref) async => null),
        myTeamProvider.overrideWith((ref) async => team),
        teamLecturerProvider('team-1').overrideWith((ref) async =>
            const TeamLecturerData(preferences: {'locked': false, 'preferences': []})),
      ],
      child: MaterialApp.router(theme: AppTheme.light(), routerConfig: router),
    ));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Giảng viên'));
    await tester.pumpAndSettle();
    expect(find.text('Giảng viên của nhóm'), findsOneWidget);
    expect(find.text('Chưa ghép giảng viên'), findsOneWidget);
  });

  testWidgets('sau ghép: hiện lý do, không cho sửa nguyện vọng', (tester) async {
    await tester.pumpWidget(app(const TeamLecturerData(assignment: {
      'lecturerName': 'Giảng viên B', 'reason': 'PREFERENCE', 'preferenceRank': 2,
    })));
    await tester.pumpAndSettle();
    expect(find.text('Giảng viên B'), findsOneWidget);
    expect(find.text('Nguyện vọng 2'), findsOneWidget);
    expect(find.text('Chỉnh sửa nguyện vọng'), findsNothing);
  });

  testWidgets('đã khóa: không hiển thị nút chỉnh sửa', (tester) async {
    await tester.pumpWidget(app(const TeamLecturerData(preferences: {'locked': true, 'preferences': []})));
    await tester.pumpAndSettle();
    expect(find.text('Đợt chọn nguyện vọng đã đóng.'), findsOneWidget);
    expect(find.text('Chỉnh sửa nguyện vọng'), findsNothing);
  });

  testWidgets('thành viên không phải trưởng nhóm không được chỉnh sửa', (tester) async {
    const memberTeam = TeamDetail(
      card: TeamCard(id: 'team-1', name: 'Nhóm A', status: 'ACTIVE', memberCount: 4),
      members: [], isLeader: false,
    );
    await tester.pumpWidget(app(
      const TeamLecturerData(preferences: {'locked': false, 'preferences': []}),
      selectedTeam: memberTeam,
    ));
    await tester.pumpAndSettle();
    expect(find.text('Chỉnh sửa nguyện vọng'), findsNothing);
  });

  testWidgets('lỗi tải có nút thử lại', (tester) async {
    await tester.pumpWidget(ProviderScope(
      overrides: [
        myTeamProvider.overrideWith((ref) async => team),
        teamLecturerProvider('team-1').overrideWith((ref) async => throw Exception('Lỗi tải')),
      ],
      child: MaterialApp(theme: AppTheme.light(), home: const TeamLecturerScreen()),
    ));
    await tester.pumpAndSettle();
    expect(find.text('Không tải được dữ liệu'), findsOneWidget);
    expect(find.text('Thử lại'), findsOneWidget);
  });

  testWidgets('không có nhóm: hiển thị trạng thái trống', (tester) async {
    await tester.pumpWidget(app(const TeamLecturerData(), selectedTeam: null));
    await tester.pumpAndSettle();
    expect(find.text('Bạn chưa có nhóm'), findsOneWidget);
  });
}
