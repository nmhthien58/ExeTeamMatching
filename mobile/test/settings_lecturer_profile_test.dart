import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/auth/session.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/core/providers.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/lecturer_profile/lecturer_profile_screen.dart';
import 'package:exe_team_matching/features/settings/settings_screen.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

class _FakeAuth extends AuthController {
  var signedOut = false;

  @override
  Future<UserInfo?> build() async => const UserInfo(id: 'u', email: 'ha@example.com', fullName: 'Trần Thu Hà', roles: {Role.lecturer}, lecturerId: 'l1');

  @override
  Future<void> signOut() async {
    signedOut = true;
    state = const AsyncData(null);
  }
}

class _FakeSettingsApi extends SettingsApi {
  _FakeSettingsApi({this.saveError}) : super(Dio());

  bool enabled = true;
  final Object? saveError;
  final saved = <bool>[];

  @override
  Future<bool> emailEnabled() async => enabled;

  @override
  Future<void> setEmailEnabled(bool value) async {
    if (saveError != null) throw saveError!;
    saved.add(value);
    enabled = value;
  }
}

const _d1 = CatalogRef(id: 1, code: 'D01', name: 'AI & Data');
const _d2 = CatalogRef(id: 2, code: 'D02', name: 'Robotics');
const _old = CatalogRef(id: 9, code: 'D09', name: 'Đã ngừng');

class _FakeLecturerApi extends LecturerProfileApi {
  _FakeLecturerApi({this.saveError}) : super(Dio());

  final Object? saveError;
  var profile = const LecturerProfile(fullName: 'Trần Thu Hà', email: 'ha@example.com', domains: [_d1, _old], maxTeamsExe101: 3);
  List<int>? saved;
  int? savedDesired101;
  int? savedDesired201;

  @override
  Future<LecturerProfile> me() async => profile;

  @override
  Future<List<CatalogRef>> domains() async => const [_d1, _d2];

  @override
  Future<LecturerProfile> saveDomains(List<int> domainIds) async {
    if (saveError != null) throw saveError!;
    saved = domainIds;
    profile = LecturerProfile(fullName: profile.fullName, email: profile.email, domains: [for (final d in [_d1, _d2, _old]) if (domainIds.contains(d.id)) d], maxTeamsExe101: 3);
    return profile;
  }

  @override
  Future<LecturerProfile> saveProfile({
    required List<int> domainIds,
    int? desiredTeamsExe101,
    int? desiredTeamsExe201,
  }) async {
    if (saveError != null) throw saveError!;
    saved = domainIds;
    savedDesired101 = desiredTeamsExe101;
    savedDesired201 = desiredTeamsExe201;
    profile = LecturerProfile(
      fullName: profile.fullName,
      email: profile.email,
      domains: [for (final d in [_d1, _d2, _old]) if (domainIds.contains(d.id)) d],
      maxTeamsExe101: 3,
      desiredTeamsExe101: desiredTeamsExe101,
      desiredTeamsExe201: desiredTeamsExe201,
      isProfileCompleted: true,
    );
    return profile;
  }
}

Future<_FakeAuth> _pump(WidgetTester tester, Widget screen, {SettingsApi? settings, LecturerProfileApi? lecturer}) async {
  final auth = _FakeAuth();
  await tester.pumpWidget(ProviderScope(
    overrides: [
      authControllerProvider.overrideWith(() => auth),
      settingsApiProvider.overrideWithValue(settings ?? _FakeSettingsApi()),
      lecturerProfileApiProvider.overrideWithValue(lecturer ?? _FakeLecturerApi()),
    ],
    child: MaterialApp(theme: AppTheme.light(), home: screen),
  ));
  await tester.pumpAndSettle();
  return auth;
}

void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  testWidgets('A07: tài khoản, tắt email thông báo, thông tin ứng dụng', (tester) async {
    final api = _FakeSettingsApi();
    await _pump(tester, const SettingsScreen(), settings: api);

    expect(find.text('ha@example.com'), findsOneWidget);
    expect(find.text('Phiên bản $appVersion'), findsOneWidget);
    expect(tester.widget<SwitchListTile>(find.byType(SwitchListTile)).value, isTrue);
    await tester.tap(find.text('Nhận email thông báo'));
    await tester.pumpAndSettle();
    expect(api.saved, [false]);
    expect(tester.widget<SwitchListTile>(find.byType(SwitchListTile)).value, isFalse);
  });

  testWidgets('A07: lưu email lỗi thì trả công tắc về như cũ và báo', (tester) async {
    await _pump(tester, const SettingsScreen(), settings: _FakeSettingsApi(saveError: const ApiException(status: 0, code: 'network', title: 'Mất mạng')));
    await tester.tap(find.text('Nhận email thông báo'));
    await tester.pumpAndSettle();
    expect(find.text('Chưa lưu được cài đặt email. Thử lại sau.'), findsOneWidget);
    expect(tester.widget<SwitchListTile>(find.byType(SwitchListTile)).value, isTrue);
  });

  testWidgets('A07: đăng xuất phải xác nhận; Hủy thì vẫn đăng nhập', (tester) async {
    final auth = await _pump(tester, const SettingsScreen());
    await tester.tap(find.text('Đăng xuất'));
    await tester.pumpAndSettle();
    expect(find.text('Đăng xuất?'), findsOneWidget);
    await tester.tap(find.text('Hủy'));
    await tester.pumpAndSettle();
    expect(auth.signedOut, isFalse);

    await tester.tap(find.text('Đăng xuất'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Đăng xuất'));
    await tester.pumpAndSettle();
    expect(auth.signedOut, isTrue);
  });

  testWidgets('L06: xem chuyên môn và số nhóm tối đa do Bộ môn đặt; sửa domain rồi lưu', (tester) async {
    final api = _FakeLecturerApi();
    await _pump(tester, const LecturerProfileScreen(), lecturer: api);

    expect(find.text('EXE101: 3 nhóm · EXE201: Không giới hạn'), findsOneWidget);
    expect(find.text('D01 · AI & Data'), findsOneWidget);

    await tester.tap(find.text('Sửa'));
    await tester.pumpAndSettle();
    expect(find.text('Đã chọn 2/18'), findsOneWidget);
    expect(find.text('D09 · Đã ngừng'), findsOneWidget, reason: 'domain đã ngừng đang có vẫn bỏ chọn được');
    await tester.tap(find.text('D02 · Robotics'));
    await tester.tap(find.text('D09 · Đã ngừng'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Lưu'));
    await tester.pumpAndSettle();

    expect(api.saved, [1, 2]);
    expect(find.text('Đã lưu chuyên môn'), findsOneWidget);
    expect(find.text('D02 · Robotics'), findsOneWidget);
    expect(find.text('Sửa'), findsOneWidget);
  });

  testWidgets('L06: lỗi lưu hiện tại chỗ, giữ lựa chọn', (tester) async {
    final api = _FakeLecturerApi(
      saveError: const ApiException(status: 400, code: 'validation', title: 'Dữ liệu không hợp lệ', fieldErrors: {'domainIds': ['Domain không tồn tại.']}),
    );
    await _pump(tester, const LecturerProfileScreen(), lecturer: api);
    await tester.tap(find.text('Sửa'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('D02 · Robotics'));
    await tester.tap(find.text('Lưu'));
    await tester.pumpAndSettle();
    expect(find.text('Domain không tồn tại.'), findsOneWidget);
    expect(find.text('Đã chọn 3/18'), findsOneWidget);
  });

  testWidgets('L06: hiện banner cảnh báo khi chưa hoàn thiện và nhập số nhóm mong muốn (MB-66)', (tester) async {
    final api = _FakeLecturerApi();
    api.profile = const LecturerProfile(
      fullName: 'Trần Thu Hà',
      email: 'ha@example.com',
      domains: [_d1],
      maxTeamsExe101: 3,
      maxTeamsExe201: 2,
      isProfileCompleted: false,
    );

    await _pump(tester, const LecturerProfileScreen(), lecturer: api);

    expect(find.text('Hồ sơ chưa hoàn thiện'), findsOneWidget);
    expect(find.text('EXE101: Chưa khai báo · EXE201: Chưa khai báo'), findsOneWidget);

    await tester.tap(find.text('Sửa'));
    await tester.pumpAndSettle();

    final textFields = find.byType(TextFormField);
    await tester.enterText(textFields.at(0), '2');
    await tester.enterText(textFields.at(1), '1');

    await tester.ensureVisible(find.text('Lưu'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Lưu'));
    await tester.pumpAndSettle();

    expect(api.saved, [1]);
    expect(api.savedDesired101, 2);
    expect(api.savedDesired201, 1);
    expect(find.text('Đã lưu chuyên môn'), findsOneWidget);
  });
}
