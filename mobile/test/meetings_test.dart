import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/meetings/meeting_detail_screen.dart';
import 'package:exe_team_matching/features/meetings/meetings_api.dart';
import 'package:exe_team_matching/features/meetings/meetings_screen.dart';
import 'package:exe_team_matching/features/workspace/workspace_api.dart';
import 'package:exe_team_matching/features/workspace/workspace_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'support/workspace_fakes.dart';

const _vy = PersonRef(personType: 'STUDENT', personId: 's1', displayName: 'Lê Thảo Vy', role: WorkspaceRole.leader);
const _ha = PersonRef(personType: 'LECTURER', personId: 'l1', displayName: 'Trần Thu Hà', role: WorkspaceRole.instructor);

final _proposed = MeetingDetail(
  card: const MeetingCard(id: 'm1', title: 'Chốt ý tưởng', status: MeetingStatus.proposed, organizer: _vy, slotCount: 2, myAnswerPending: true),
  suggestedSlotId: 'x2',
  slots: [
    MeetingSlot(id: 'x1', startsAt: DateTime.utc(2030, 11, 10, 2), durationMinutes: 60, yesCount: 1, inviteeCount: 5),
    MeetingSlot(id: 'x2', startsAt: DateTime.utc(2030, 11, 11, 2), durationMinutes: 60, yesCount: 3, inviteeCount: 5, myAnswer: 'YES', yesPeople: const ['Lê Thảo Vy']),
  ],
);
final _done = MeetingDetail(
  card: MeetingCard(id: 'm2', title: 'Họp với giảng viên', status: MeetingStatus.done, organizer: _ha, startsAt: DateTime.utc(2030, 10, 1, 2), isWeekly: true, weekNo: 3),
  slots: const [],
  minutes: '- Làm landing page',
  createdTasks: const ['Làm landing page'],
);
final _held = MeetingDetail(
  card: MeetingCard(id: 'm3', title: 'Buổi mentor 1', status: MeetingStatus.done, organizer: _vy, startsAt: DateTime.utc(2030, 10, 2, 2), isMentorSession: true),
  slots: const [],
  minutes: '- Làm landing page\n- Gọi 5 khách hàng',
);

class _FakeMeetingsApi extends MeetingsApi {
  _FakeMeetingsApi() : super(Dio());

  final calls = <String>[];
  Object? error;
  late final details = {'m1': _proposed, 'm2': _done, 'm3': _held};

  @override
  Future<MeetingList> list(String workspaceId) async =>
      MeetingList(items: [for (final d in details.values) d.card], mentorSessionsDone: 1, expectedMentorSessions: 3);

  @override
  Future<MeetingDetail> detail(String id) async => details[id]!;

  @override
  Future<MeetingDetail> answer(String id, String slotId, String answer) async {
    calls.add('answer $slotId $answer');
    return details[id]!;
  }

  @override
  Future<MeetingDetail> confirm(String id, String slotId) async {
    if (error != null) throw error!;
    calls.add('confirm $slotId');
    return details[id]!;
  }

  @override
  Future<MeetingDetail> cancel(String id, String reason) async {
    calls.add('cancel $reason');
    return details[id]!;
  }

  @override
  Future<MeetingDetail> propose(String workspaceId, {required String title, String? agenda, String? location, required List<SlotInput> slots}) async {
    calls.add('propose $title ${slots.length}');
    return _proposed;
  }

  @override
  Future<int> minutesToTasks(String id, List<String> titles) async {
    calls.add('tasks ${titles.join('|')}');
    return titles.length;
  }
}

Future<void> _pump(WidgetTester tester, _FakeMeetingsApi api, String location, {WorkspaceRole role = WorkspaceRole.leader}) async {
  tester.view.physicalSize = const Size(1080, 2400);
  addTearDown(tester.view.resetPhysicalSize);
  final router = GoRouter(initialLocation: location, routes: [
    GoRoute(path: '/sv/du-an/lich-hop', builder: (_, _) => const MeetingsScreen(basePath: '/sv/du-an')),
    GoRoute(
      path: '/sv/du-an/lich-hop/:id',
      builder: (_, s) => MeetingDetailScreen(meetingId: s.pathParameters['id']!, pickTime: (_) async => DateTime(2030, 12, 1, 9)),
    ),
  ]);
  await tester.pumpWidget(ProviderScope(
    overrides: [
      authOverride(),
      meetingsApiProvider.overrideWithValue(api),
      currentWorkspaceProvider.overrideWith((ref, id) async => WorkspaceOverview(id: 'w1', teamName: 'EduSpark', courseCode: 'EXE201', myRole: role)),
    ],
    child: MaterialApp.router(theme: AppTheme.light(), routerConfig: router),
  ));
  await tester.pumpAndSettle();
}

void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  test('dòng biên bản thành tên việc', () {
    expect(minutesLines('- Làm landing page\n\n2) Gọi khách\n• Viết báo cáo'), ['Làm landing page', 'Gọi khách', 'Viết báo cáo']);
  });

  testWidgets('C07: nhóm theo trạng thái, số buổi mentor, chip; thành viên không có nút đề xuất', (tester) async {
    await _pump(tester, _FakeMeetingsApi(), '/sv/du-an/lich-hop', role: WorkspaceRole.member);
    expect(find.text('Buổi mentor: 1/3'), findsOneWidget);
    expect(find.text('Chờ chốt (1)'), findsOneWidget);
    expect(find.text('Đã họp (2)'), findsOneWidget);
    expect(find.text('Chưa trả lời'), findsOneWidget);
    expect(find.text('Hằng tuần · tuần 3'), findsOneWidget);
    expect(find.text('Đề xuất họp'), findsNothing);
  });

  testWidgets('C07: trưởng nhóm đề xuất họp – thiếu tiêu đề, khung thì báo; đủ thì gửi và mở C08', (tester) async {
    final api = _FakeMeetingsApi();
    await _pump(tester, api, '/sv/du-an/lich-hop');
    await tester.tap(find.text('Đề xuất họp'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Tạo cuộc họp'));
    await tester.pumpAndSettle();
    expect(find.text('Nhập tiêu đề.'), findsOneWidget);
    expect(find.text('Thêm ít nhất một khung giờ.'), findsOneWidget);
  });

  testWidgets('C08: trả lời Có / Không, người tổ chức chốt khung nhiều người chọn', (tester) async {
    final api = _FakeMeetingsApi();
    await _pump(tester, api, '/sv/du-an/lich-hop/m1');
    expect(find.text('Nhiều người chọn nhất'), findsOneWidget);
    await tester.tap(find.widgetWithText(ChoiceChip, 'Không').first);
    await tester.pumpAndSettle();
    expect(api.calls, ['answer x1 NO']);
    await tester.tap(find.text('Chốt khung này').last);
    await tester.pumpAndSettle();
    expect(api.calls.last, 'confirm x2');
    expect(find.text('Đã chốt khung giờ'), findsOneWidget);
  });

  testWidgets('C08: lỗi chốt hiện banner; hủy họp cần lý do', (tester) async {
    final api = _FakeMeetingsApi()..error = const ApiException(status: 403, code: 'not-organizer', title: 'Không có quyền', detail: 'Chỉ người tổ chức được chốt.');
    await _pump(tester, api, '/sv/du-an/lich-hop/m1');
    await tester.tap(find.text('Chốt khung này').first);
    await tester.pumpAndSettle();
    expect(find.text('Chỉ người tổ chức được chốt.'), findsOneWidget);

    await tester.scrollUntilVisible(find.text('Hủy cuộc họp'), 200);
    await tester.tap(find.text('Hủy cuộc họp'));
    await tester.pumpAndSettle();
    expect(tester.widget<FilledButton>(find.widgetWithText(FilledButton, 'Hủy họp')).onPressed, isNull);
    await tester.enterText(find.byType(TextField).last, 'Trùng lịch thi');
    await tester.pumpAndSettle();
    await tester.tap(find.text('Hủy họp'));
    await tester.pumpAndSettle();
    expect(api.calls.last, 'cancel Trùng lịch thi');
  });

  testWidgets('C08: thành viên xem biên bản và việc của cuộc họp người khác tổ chức', (tester) async {
    await _pump(tester, _FakeMeetingsApi(), '/sv/du-an/lich-hop/m2', role: WorkspaceRole.member);
    expect(find.text('- Làm landing page'), findsOneWidget);
    expect(find.text('Làm landing page'), findsOneWidget);
    expect(find.text('Hủy cuộc họp'), findsNothing);
    expect(find.byType(TextField), findsNothing);
  });

  testWidgets('C08: người tổ chức chuyển dòng biên bản thành việc', (tester) async {
    final api = _FakeMeetingsApi();
    await _pump(tester, api, '/sv/du-an/lich-hop/m3');
    await tester.tap(find.widgetWithText(CheckboxListTile, 'Gọi 5 khách hàng'));
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('Tạo 1 việc'));
    await tester.tap(find.text('Tạo 1 việc'));
    await tester.pumpAndSettle();
    expect(api.calls.last, 'tasks Gọi 5 khách hàng');
    expect(find.text('Đã tạo 1 việc'), findsOneWidget);
  });
}
