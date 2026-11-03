import 'package:exe_team_matching/app/feature_routes.dart';
import 'package:exe_team_matching/app/navigation.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

void main() {
  test('L04 and M01 tab screens are registered', () {
    expect(tabScreens, contains('/gv/nhom'));
    expect(tabScreens, contains('/mentor'));
    expect(tabScreens, contains('/mentor/nhom'));
  });

  test('L05 detail routes are registered for lecturer and mentor', () {
    final lecturerPaths = featureRoutes(
      Shell.lecturer,
    ).whereType<GoRoute>().map((route) => route.path);
    final mentorPaths = featureRoutes(
      Shell.mentor,
    ).whereType<GoRoute>().map((route) => route.path);

    expect(lecturerPaths, contains('/gv/nhom/:teamId'));
    expect(lecturerPaths, contains('/gv/lich-hop'));
    expect(mentorPaths, contains('/mentor/nhom/:teamId'));
  });
}
