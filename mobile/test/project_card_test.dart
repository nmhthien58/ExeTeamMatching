import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/my_team/my_team_api.dart';
import 'package:exe_team_matching/features/my_team/project_card.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

MyTeam _team({String course = 'EXE201', String? projectName = 'EduSpark AI'}) => MyTeam(
      id: 't1',
      name: 'Nhóm Bình Minh',
      status: 'ACTIVE',
      courseCode: course,
      members: const [],
      domains: const [],
      isLeader: true,
      memberCount: 5,
      specializationCount: 2,
      projectName: projectName,
    );

ProjectVersion _v(int n, String name, String type, String course, {String? reason}) => ProjectVersion(
      version: n,
      name: name,
      changeType: type,
      semesterCode: course == 'EXE101' ? 'SP26' : 'SU26',
      courseCode: course,
      reason: reason,
      createdAt: DateTime.utc(2026, n, 10),
    );

final _project = ProjectDetail(
  id: 'p1',
  name: 'EduSpark AI',
  originCourseCode: 'EXE101',
  originSemesterCode: 'SP26',
  versions: [
    _v(3, 'EduSpark AI', 'PIVOT', 'EXE201', reason: 'Thị trường nhỏ'),
    _v(2, 'EduSpark', 'EDITED', 'EXE101'),
    _v(1, 'EduSpark', 'CREATED', 'EXE101'),
  ],
);

class _FakeApi extends TeamProjectApi {
  _FakeApi(this.result) : super(Dio());

  final ProjectDetail? result;

  @override
  Future<ProjectDetail?> project(String teamId) async => result;
}

Future<void> _pump(WidgetTester tester, MyTeam team, ProjectDetail? project) async {
  await tester.pumpWidget(ProviderScope(
    overrides: [teamProjectApiProvider.overrideWithValue(_FakeApi(project))],
    child: MaterialApp(theme: AppTheme.light(), home: Scaffold(body: ProjectCard(team: team))),
  ));
  await tester.pumpAndSettle();
}

void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  testWidgets('EXE201 kế thừa: "Tiếp tục từ EXE101 · SP26", Lịch sử mở dòng thời gian mới nhất ở trên', (tester) async {
    await _pump(tester, _team(), _project);
    expect(find.text('Tiếp tục từ EXE101 · SP26'), findsOneWidget);

    await tester.tap(find.text('Lịch sử'));
    await tester.pumpAndSettle();
    expect(find.text('Lịch sử đề tài'), findsOneWidget);
    expect(find.text('Đổi đề tài: EduSpark → EduSpark AI · Thị trường nhỏ'), findsOneWidget);
    expect(find.text('Tạo đề tài: EduSpark'), findsOneWidget);
    final pivot = tester.getTopLeft(find.textContaining('Đổi đề tài')).dy;
    final created = tester.getTopLeft(find.text('Tạo đề tài: EduSpark')).dy;
    expect(pivot, lessThan(created));
  });

  testWidgets('EXE101 không có dòng kế thừa, vẫn xem được Lịch sử', (tester) async {
    await _pump(tester, _team(course: 'EXE101'), _project);
    expect(find.textContaining('Tiếp tục từ'), findsNothing);
    expect(find.text('Lịch sử'), findsOneWidget);
  });

  testWidgets('Chưa có đề tài (404): giữ tên đề tài của nhóm, không có Lịch sử', (tester) async {
    await _pump(tester, _team(projectName: 'EduSpark'), null);
    expect(find.text('EduSpark'), findsOneWidget);
    expect(find.text('Lịch sử'), findsNothing);
  });
}
