import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/core/phase.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/profile/profile_api.dart';
import 'package:exe_team_matching/features/profile/student_profile_screen.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/suggestions_fakes.dart';

const _anh = StudentProfile(
  id: 's2',
  studentCode: 'SE184119',
  fullName: 'Ngô Bảo Anh',
  specialization: 'Thiết kế đồ họa',
  classCode: 'EXE101GD01',
  skills: {'TECHNOLOGY': 2, 'UI_UX_DESIGN': 5, 'MANAGEMENT': 3, 'MARKETING': 1, 'COMMUNICATION_PLANNING': 4},
  description: 'Thích thiết kế',
);

class _FakeProfileApi extends ProfileApi {
  _FakeProfileApi({this.profile = _anh, this.invited = const {}, this.error}) : super(Dio());

  final StudentProfile profile;
  final Set<String> invited;
  final Object? error;
  var invitationCalls = 0;

  @override
  Future<StudentProfile> student(String id) async => error == null ? profile : throw error!;

  @override
  Future<Set<String>> invitedStudentIds(String teamId) async {
    invitationCalls++;
    return invited;
  }
}

Future<void> _pump(WidgetTester tester, _FakeProfileApi api, {TeamDetail? myTeam, bool isTeamFormation = true, FakeSuggestionsApi? suggestions}) async {
  await tester.pumpWidget(ProviderScope(
    overrides: [
      profileApiProvider.overrideWithValue(api),
      suggestionsApiProvider.overrideWithValue(suggestions ?? FakeSuggestionsApi(team: myTeam)),
      shellPhaseProvider.overrideWithValue(ShellPhase(isTeamFormation: isTeamFormation, isMentoringWindow: false)),
    ],
    child: MaterialApp(theme: AppTheme.light(), home: const StudentProfileScreen(studentId: 's2')),
  ));
  await tester.pumpAndSettle();
}

void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  test('đọc hồ sơ: email rỗng là không có, kỹ năng theo khóa', () {
    final profile = StudentProfile.fromJson({
      'id': 's2',
      'studentCode': 'SE184119',
      'fullName': 'Ngô Bảo Anh',
      'email': '',
      'specialization': {'id': 5, 'code': 'GD', 'name': 'Thiết kế đồ họa'},
      'hasTeam': true,
      'skills': [
        {'skill': 'MARKETING', 'level': 2},
      ],
    });
    expect(profile.email, isNull);
    expect(profile.specialization, 'Thiết kế đồ họa');
    expect(profile.hasTeam, isTrue);
    expect(profile.skills, {'MARKETING': 2});
  });

  testWidgets('người khác xem: có kỹ năng, giới thiệu; không có nút mời, email, không gọi API lời mời', (tester) async {
    final semantics = tester.ensureSemantics();
    final api = _FakeProfileApi();
    await _pump(tester, api, myTeam: team(leader: false));

    expect(find.text('Ngô Bảo Anh'), findsOneWidget);
    expect(find.bySemanticsLabel('Thiết kế UI/UX, 5 trên 5'), findsOneWidget);
    expect(find.text('Thích thiết kế'), findsOneWidget);
    expect(find.text('Gửi lời mời'), findsNothing);
    expect(api.invitationCalls, 0);
    semantics.dispose();
  });

  testWidgets('chưa có nhóm: không có nút mời', (tester) async {
    await _pump(tester, _FakeProfileApi());
    expect(find.text('Ngô Bảo Anh'), findsOneWidget);
    expect(find.text('Gửi lời mời'), findsNothing);
  });

  testWidgets('trưởng nhóm gửi lời mời, nút chuyển "Đã mời"', (tester) async {
    final suggestions = FakeSuggestionsApi(team: team());
    await _pump(tester, _FakeProfileApi(), suggestions: suggestions);

    await tester.tap(find.widgetWithText(FilledButton, 'Gửi lời mời'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField), 'Nhóm cần bạn làm UI');
    await tester.tap(find.widgetWithText(FilledButton, 'Gửi lời mời').last);
    await tester.pumpAndSettle();

    expect(suggestions.invites, ['t1 s2 Nhóm cần bạn làm UI']);
    expect(find.text('Đã mời'), findsOneWidget);
    expect(find.text('Đã gửi lời mời'), findsOneWidget);
  });

  testWidgets('đã mời từ trước: hiện "Đã mời", không có nút', (tester) async {
    await _pump(tester, _FakeProfileApi(invited: {'s2'}), myTeam: team());
    expect(find.text('Đã mời'), findsOneWidget);
    expect(find.text('Gửi lời mời'), findsNothing);
  });

  testWidgets('sinh viên đã có nhóm: hiện nhãn, không có nút', (tester) async {
    const taken = StudentProfile(id: 's2', studentCode: 'SE184119', fullName: 'Ngô Bảo Anh', specialization: 'SE', hasTeam: true);
    await _pump(tester, _FakeProfileApi(profile: taken), myTeam: team());
    expect(find.text('Đã có nhóm'), findsOneWidget);
    expect(find.text('Gửi lời mời'), findsNothing);
  });

  testWidgets('nhóm đủ 6: không mời được', (tester) async {
    await _pump(tester, _FakeProfileApi(), myTeam: team(members: 6));
    expect(find.text('Nhóm đã đủ 6 người'), findsOneWidget);
    expect(find.text('Gửi lời mời'), findsNothing);
  });

  testWidgets('hết giai đoạn lập nhóm: không mời được', (tester) async {
    await _pump(tester, _FakeProfileApi(), myTeam: team(), isTeamFormation: false);
    expect(find.text('Ngô Bảo Anh'), findsOneWidget);
    expect(find.text('Gửi lời mời'), findsNothing);
  });

  testWidgets('đồng đội thấy email, không bao giờ có SĐT', (tester) async {
    final mine = TeamDetail(
      card: const TeamCard(id: 't1', name: 'EduSpark', status: 'ACTIVE', memberCount: 3),
      members: const [StudentRef(id: 's2', studentCode: 'SE184119', fullName: 'Ngô Bảo Anh', email: 'anh@example.test', phone: '0901234567')],
      isLeader: true,
    );
    await _pump(tester, _FakeProfileApi(), myTeam: mine);

    expect(find.text('anh@example.test'), findsOneWidget);
    expect(find.text('Cùng nhóm với bạn'), findsOneWidget);
    expect(find.text('0901234567'), findsNothing);
    expect(find.text('Gửi lời mời'), findsNothing);
  });

  testWidgets('không tìm thấy sinh viên', (tester) async {
    await _pump(tester, _FakeProfileApi(error: const ApiException(status: 404, code: 'not-found', title: 'Không tìm thấy')));
    expect(find.text('Không tìm thấy sinh viên'), findsOneWidget);
  });

  testWidgets('lỗi tải có nút Thử lại', (tester) async {
    await _pump(tester, _FakeProfileApi(error: const ApiException(status: 500, code: 'server', title: 'Lỗi hệ thống')));
    expect(find.text('Thử lại'), findsOneWidget);
  });

  testWidgets('chữ phóng to 200% không tràn', (tester) async {
    await tester.pumpWidget(ProviderScope(
      overrides: [
        profileApiProvider.overrideWithValue(_FakeProfileApi()),
        suggestionsApiProvider.overrideWithValue(FakeSuggestionsApi(team: team())),
        shellPhaseProvider.overrideWithValue(const ShellPhase(isTeamFormation: true, isMentoringWindow: false)),
      ],
      child: MaterialApp(
        theme: AppTheme.light(),
        builder: (context, child) => MediaQuery(data: MediaQuery.of(context).copyWith(textScaler: const TextScaler.linear(2)), child: child!),
        home: const StudentProfileScreen(studentId: 's2'),
      ),
    ));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
  });
}
