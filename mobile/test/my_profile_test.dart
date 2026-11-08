import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/core/phase.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/profile/my_profile_screens.dart';
import 'package:exe_team_matching/features/profile/profile_api.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'support/suggestions_fakes.dart';

const _full = {'TECHNOLOGY': 4, 'UI_UX_DESIGN': 2, 'MANAGEMENT': 3, 'MARKETING': 1, 'COMMUNICATION_PLANNING': 5};

const _an = StudentProfile(
  id: 's1',
  studentCode: 'SE184118',
  fullName: 'Nguyễn Văn An',
  specialization: 'Thiết kế đồ họa',
  specializationId: 5,
  majorCode: 'SE',
  majorName: 'Kỹ thuật phần mềm',
  classCode: 'SE1801',
  skills: _full,
  description: 'Thích làm backend',
);

const _new = StudentProfile(
  id: 's1',
  studentCode: 'SE184118',
  fullName: 'Nguyễn Văn An',
  specialization: 'Thiết kế đồ họa',
  specializationId: 5,
  majorCode: 'SE',
  majorName: 'Kỹ thuật phần mềm',
  profileCompleted: false,
);

class _FakeProfileApi extends ProfileApi {
  _FakeProfileApi(this.profile) : super(Dio());

  StudentProfile profile;
  Object? saveError;
  Map<String, Object?>? saved;

  @override
  Future<StudentProfile> me() async => profile;

  @override
  Future<StudentProfile> saveMe({
    required Map<String, int> skills,
    required String description,
    int? specializationId,
    bool? isSeekingTeam,
  }) async {
    if (saveError != null) throw saveError!;
    saved = {'skills': skills, 'description': description, 'specializationId': specializationId, 'isSeekingTeam': isSeekingTeam};
    profile = StudentProfile(
      id: profile.id,
      studentCode: profile.studentCode,
      fullName: profile.fullName,
      specialization: profile.specialization,
      specializationId: specializationId ?? profile.specializationId,
      majorCode: profile.majorCode,
      majorName: profile.majorName,
      skills: skills,
      description: description,
      isSeekingTeam: isSeekingTeam ?? profile.isSeekingTeam,
    );
    return profile;
  }
}

Future<void> _pump(WidgetTester tester, _FakeProfileApi api, {required String location, TeamDetail? myTeam, bool isTeamFormation = true}) async {
  // Cao 1100 dp để cả form A05 / S03 (5 thanh kỹ năng có nhãn hai đầu) dựng hết, test không phải cuộn.
  tester.view.physicalSize = const Size(1080, 3300);
  addTearDown(tester.view.resetPhysicalSize);
  final router = GoRouter(initialLocation: location, routes: [
    GoRoute(path: '/sv', builder: (_, _) => const ProfileGate(child: Scaffold(body: Text('S01 Trang chủ')))),
    GoRoute(path: '/sv/ho-so', builder: (_, _) => const ProfileGate(child: MyProfileScreen())),
    GoRoute(path: editProfilePath, builder: (_, _) => const EditProfileScreen()),
    GoRoute(path: '/sv/nhom', builder: (_, _) => const Scaffold(body: Text('S15 Nhóm'))),
  ]);
  await tester.pumpWidget(ProviderScope(
    overrides: [
      profileApiProvider.overrideWithValue(api),
      suggestionsApiProvider.overrideWithValue(FakeSuggestionsApi(team: myTeam)),
      shellPhaseProvider.overrideWithValue(ShellPhase(isTeamFormation: isTeamFormation, isMentoringWindow: false)),
    ],
    child: MaterialApp.router(theme: AppTheme.light(), routerConfig: router),
  ));
  await tester.pumpAndSettle();
}

Future<void> _rateAll(WidgetTester tester) async {
  for (final slider in find.byType(Slider).evaluate().toList()) {
    await tester.tap(find.byWidget(slider.widget));
    await tester.pumpAndSettle();
  }
}

void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  test('đọc hồ sơ của tôi: cờ hoàn thiện, đang tìm nhóm, môn', () {
    final profile = StudentProfile.fromJson({
      'id': 's1',
      'studentCode': 'SE184118',
      'fullName': 'An',
      'specialization': {'id': 1, 'code': 'SE', 'name': 'Kỹ thuật phần mềm'},
      'isSeekingTeam': true,
      'profileCompleted': false,
      'courseCode': 'EXE101',
      'skills': [],
    });
    expect(profile.profileCompleted, isFalse);
    expect(profile.isSeekingTeam, isTrue);
    expect(profile.courseCode, 'EXE101');
  });

  testWidgets('A05: chưa hoàn thiện thì thay S01; khóa nút tới khi đánh giá đủ 5 kỹ năng; lưu xong về S01', (tester) async {
    final api = _FakeProfileApi(_new);
    await _pump(tester, api, location: '/sv');

    expect(find.text('Hoàn thiện hồ sơ'), findsOneWidget);
    expect(find.text('S01 Trang chủ'), findsNothing);
    expect(find.text('Chưa đánh giá'), findsNWidgets(5));
    expect(find.text('Còn 5 kỹ năng chưa đánh giá'), findsOneWidget);
    expect(find.byType(TextField), findsOneWidget, reason: 'không có ô SĐT, Zalo (quy tắc 16)');
    expect(tester.widget<FilledButton>(find.widgetWithText(FilledButton, 'Hoàn tất')).onPressed, isNull);

    await tester.tap(find.text('Thông tin từ bộ môn'));
    await tester.pumpAndSettle();
    expect(find.text('SE184118'), findsOneWidget);
    await tester.tap(find.text('Thông tin từ bộ môn'));
    await tester.pumpAndSettle();

    await _rateAll(tester);
    expect(find.text('Còn 0 kỹ năng chưa đánh giá'), findsNothing);
    await tester.enterText(find.byType(TextField), '  Thích làm backend  ');
    await tester.ensureVisible(find.text('Hoàn tất'));
    await tester.tap(find.text('Hoàn tất'));
    await tester.pumpAndSettle();

    expect(api.saved!['description'], 'Thích làm backend');
    expect((api.saved!['skills'] as Map).length, 5);
    expect(api.saved!['isSeekingTeam'], isNull);
    expect(find.text('S01 Trang chủ'), findsOneWidget);
  });

  testWidgets('A05: lỗi lưu hiện banner và giữ dữ liệu đã nhập', (tester) async {
    final api = _FakeProfileApi(_new)..saveError = const ApiException(status: 500, code: 'x', title: 'Lỗi máy chủ');
    await _pump(tester, api, location: '/sv');
    await _rateAll(tester);
    await tester.enterText(find.byType(TextField), 'Giữ lại');
    await tester.ensureVisible(find.text('Hoàn tất'));
    await tester.tap(find.text('Hoàn tất'));
    await tester.pumpAndSettle();

    expect(find.textContaining('Chưa lưu được hồ sơ'), findsOneWidget);
    expect(find.text('Giữ lại'), findsOneWidget);
    expect(find.text('Hoàn thiện hồ sơ'), findsOneWidget);
  });

  testWidgets('S02: kỹ năng, giới thiệu, nhóm hiện tại; không có mục liên lạc', (tester) async {
    final semantics = tester.ensureSemantics();
    await _pump(tester, _FakeProfileApi(_an), location: '/sv/ho-so', myTeam: team(leader: false));

    expect(find.text('Nguyễn Văn An'), findsOneWidget);
    expect(find.bySemanticsLabel('Công nghệ, 4 trên 5'), findsOneWidget);
    expect(find.text('Thích làm backend'), findsOneWidget);
    expect(find.text(team(leader: false).card.name), findsOneWidget);
    expect(find.textContaining('SĐT'), findsNothing);
    semantics.dispose();
  });

  testWidgets('S02: chưa có nhóm', (tester) async {
    await _pump(tester, _FakeProfileApi(_an), location: '/sv/ho-so');
    expect(find.text('Bạn chưa có nhóm.'), findsOneWidget);
  });

  testWidgets('S03: sửa kỹ năng, bật tìm nhóm trong đợt lập nhóm, lưu xong quay lại S02', (tester) async {
    final api = _FakeProfileApi(_an);
    await _pump(tester, api, location: '/sv/ho-so');
    await tester.tap(find.byTooltip('Sửa hồ sơ'));
    await tester.pumpAndSettle();

    expect(find.text('Chỉnh sửa hồ sơ'), findsOneWidget);
    expect(find.text('4/5'), findsOneWidget);
    await tester.tap(find.text('Tôi đang tìm nhóm'));
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('Lưu'));
    await tester.tap(find.text('Lưu'));
    await tester.pumpAndSettle();

    expect(api.saved!['isSeekingTeam'], isTrue);
    expect(api.saved!['skills'], _full);
    expect(find.text('Đã lưu hồ sơ'), findsOneWidget);
    expect(find.text('Hồ sơ của tôi'), findsOneWidget);
    expect(find.text('Đang tìm nhóm'), findsOneWidget);
  });

  testWidgets('S03: ngoài đợt lập nhóm không có công tắc tìm nhóm', (tester) async {
    await _pump(tester, _FakeProfileApi(_an), location: editProfilePath, isTeamFormation: false);
    expect(find.text('Tôi đang tìm nhóm'), findsNothing);
  });
}
