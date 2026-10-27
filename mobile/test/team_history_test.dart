import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/my_team/my_team_api.dart';
import 'package:exe_team_matching/features/my_team/team_history_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

const _team = MyTeam(
  id: 't1',
  name: 'Nhóm Bình Minh',
  status: 'DRAFT',
  courseCode: 'EXE101',
  members: [],
  domains: [],
  isLeader: false,
  memberCount: 3,
  specializationCount: 1,
);

class _FakeHistory extends TeamHistoryApi {
  _FakeHistory(this.pages) : super(Dio());

  final List<List<AuditLogEntry>> pages;
  final requested = <int>[];

  @override
  Future<(List<AuditLogEntry>, bool)> page(String teamId, int page) async {
    requested.add(page);
    return (pages[page - 1], page < pages.length);
  }
}

AuditLogEntry _entry(int id, String action, String summary) =>
    AuditLogEntry(id: id, action: action, actorName: 'Sinh viên', summary: summary, occurredAt: DateTime.utc(2026, 10, 27, 3, 5));

Future<void> _pump(WidgetTester tester, _FakeHistory api, {MyTeam? team = _team}) async {
  await tester.pumpWidget(ProviderScope(
    overrides: [teamHistoryApiProvider.overrideWithValue(api), myTeamDetailProvider.overrideWith((ref) async => team)],
    child: MaterialApp(theme: AppTheme.light(), home: const TeamHistoryScreen()),
  ));
  await tester.pumpAndSettle();
}

void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  testWidgets('S22: hiện mô tả, người làm, thời điểm; Xem thêm tải trang sau', (tester) async {
    final api = _FakeHistory([
      [_entry(2, 'MEMBER_JOINED', 'SE180002 vào nhóm')],
      [_entry(1, 'TEAM_CREATED', 'Tạo nhóm Nhóm Bình Minh')],
    ]);
    await _pump(tester, api);
    expect(find.text('SE180002 vào nhóm'), findsOneWidget);
    expect(find.textContaining('Sinh viên · '), findsOneWidget);

    await tester.tap(find.text('Xem thêm'));
    await tester.pumpAndSettle();
    expect(find.text('Tạo nhóm Nhóm Bình Minh'), findsOneWidget);
    expect(find.text('Đã hết lịch sử'), findsOneWidget);
    expect(api.requested, [1, 2]);
  });

  testWidgets('S22: chưa có hoạt động → trạng thái trống', (tester) async {
    await _pump(tester, _FakeHistory([[]]));
    expect(find.text('Chưa có hoạt động'), findsOneWidget);
  });

  testWidgets('S22: chưa có nhóm → Bạn chưa có nhóm', (tester) async {
    await _pump(tester, _FakeHistory([[]]), team: null);
    expect(find.text('Bạn chưa có nhóm'), findsOneWidget);
  });
}
