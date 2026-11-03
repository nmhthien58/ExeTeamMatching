import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/meeting_schedule/meeting_schedule_api.dart';
import 'package:exe_team_matching/features/meeting_schedule/meeting_schedule_screen.dart';
import 'package:exe_team_matching/features/my_team/my_team_api.dart';
import 'package:exe_team_matching/features/realtime/collab_hub.dart';
import 'package:exe_team_matching/features/workspace/workspace_api.dart';
import 'package:exe_team_matching/features/workspace/workspace_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';

ScheduleSlot _slot(String id, String time, String state, {String? workspaceId, String? teamName}) =>
    ScheduleSlot(id: id, dayOfWeek: 2, startTime: time, endTime: '', state: state, workspaceId: workspaceId, teamName: teamName);

TeamMeetingSchedule _team({bool canBook = true, ScheduleSlot? mySlot, List<ScheduleSlot>? slots}) => TeamMeetingSchedule(
      lecturerName: 'Cô Hạnh',
      location: 'https://meet.example/abc',
      bookingDeadline: DateTime.utc(2099, 11, 9, 16, 59),
      canBook: canBook,
      mySlot: mySlot,
      slots: slots ?? [_slot('a', '09:00', 'TAKEN'), _slot('b', '09:15', 'FREE')],
    );

class _FakeApi extends MeetingScheduleApi {
  _FakeApi({this.teamSchedule}) : super(Dio());

  TeamMeetingSchedule? teamSchedule;
  MeetingSchedule? mineSchedule;
  Object? bookError;
  final calls = <String>[];

  @override
  Future<TeamMeetingSchedule?> team(String workspaceId) async {
    calls.add('team $workspaceId');
    return teamSchedule;
  }

  @override
  Future<TeamMeetingSchedule> book(String workspaceId, String slotId) async {
    calls.add('book $workspaceId $slotId');
    if (bookError != null) throw bookError!;
    return teamSchedule = _team(mySlot: _slot(slotId, '09:15', 'MINE'));
  }

  @override
  Future<MeetingSchedule?> mine() async => mineSchedule;

  @override
  Future<MeetingSchedule> save(List<ScheduleWindow> windows, int slotMinutes, String location) async {
    calls.add('save ${windows.map((w) => '${w.dayOfWeek} ${w.startTime}-${w.endTime}').join(',')} $slotMinutes $location');
    return mineSchedule = _lecturer('DRAFT');
  }

  @override
  Future<MeetingSchedule> publish() async {
    calls.add('publish');
    return mineSchedule = _lecturer('PUBLISHED');
  }

  @override
  Future<MeetingSchedule> move(String workspaceId, String slotId) async {
    calls.add('move $workspaceId $slotId');
    return mineSchedule!;
  }
}

MeetingSchedule _lecturer(String status) => MeetingSchedule(
      id: 'sc1',
      status: status,
      slotMinutes: 15,
      location: 'https://meet.example/abc',
      bookingDeadline: DateTime.utc(2099, 11, 9),
      windows: const [ScheduleWindow(dayOfWeek: 2, startTime: '09:00', endTime: '09:30')],
      slots: [_slot('a', '09:00', 'TAKEN', workspaceId: 'w2', teamName: 'Nhóm Bình Minh'), _slot('b', '09:15', 'FREE')],
      teamCount: 3,
      teamsWithoutSlot: const ['Nhóm Hoàng Hôn'],
    );

class _FakeConnection implements HubConnectionLike {
  final handlers = <String, List<void Function(Object?)>>{};
  final joined = <String>[];

  void emit(String event, Object? payload) {
    for (final h in [...?handlers[event]]) {
      h(payload);
    }
  }

  @override
  Future<void> start() async {}
  @override
  Future<void> stop() async {}
  @override
  Future<void> invoke(String method, String id) async => joined.add('$method $id');
  @override
  void on(String event, void Function(Object? payload) handler) => (handlers[event] ??= []).add(handler);
  @override
  void off(String event, void Function(Object? payload) handler) => handlers[event]?.remove(handler);
  @override
  void onStatus({required VoidCallback reconnecting, required VoidCallback reconnected, required VoidCallback closed}) {}
}

MyTeam _myTeam({required bool leader}) => MyTeam(
      id: 't2', name: 'Nhóm Bình Minh', status: 'FINALIZED', courseCode: 'EXE201',
      members: const [], domains: const [], isLeader: leader, memberCount: 4, specializationCount: 2,
    );

Future<_FakeConnection> _pump(WidgetTester tester, Widget screen, _FakeApi api, {List<Override> overrides = const []}) async {
  final connection = _FakeConnection();
  await tester.pumpWidget(ProviderScope(
    overrides: [meetingScheduleApiProvider.overrideWithValue(api), collabHubProvider.overrideWithValue(CollabHub(() => connection)), ...overrides],
    child: MaterialApp(theme: AppTheme.light(), home: screen),
  ));
  await tester.pumpAndSettle();
  return connection;
}

List<Override> _student({required bool leader}) => [
      myTeamDetailProvider.overrideWith((ref) async => _myTeam(leader: leader)),
      teamWorkspaceIdProvider.overrideWith((ref, teamId) async => teamId == 't2' ? 'w1' : null),
    ];

void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  testWidgets('C09: trưởng nhóm chọn slot trống qua xác nhận; ô đã có nhóm không bấm được; slotsChanged tải lại', (tester) async {
    final semantics = tester.ensureSemantics();
    final api = _FakeApi(teamSchedule: _team());
    final hub = await _pump(tester, const TeamScheduleScreen(), api, overrides: _student(leader: true));

    expect(hub.joined, contains('JoinWorkspace w1'));
    await tester.tap(find.bySemanticsLabel('Thứ Ba 09:00, đã có nhóm'));
    await tester.pumpAndSettle();
    expect(find.text('Chọn slot'), findsNothing);

    await tester.tap(find.bySemanticsLabel('Thứ Ba 09:15, còn trống'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Chọn slot'));
    await tester.pumpAndSettle();
    expect(api.calls, contains('book w1 b'));
    expect(find.text('Họp hằng tuần · Thứ Ba 09:15'), findsOneWidget);

    final before = api.calls.where((c) => c == 'team w1').length;
    hub.emit('slotsChanged', {'scheduleId': 'sc1'});
    await tester.pumpAndSettle();
    expect(api.calls.where((c) => c == 'team w1').length, greaterThan(before));
    semantics.dispose();
  });

  testWidgets('C09: slot vừa bị nhóm khác chọn → báo lỗi tại màn, lưới tải lại', (tester) async {
    final semantics = tester.ensureSemantics();
    final api = _FakeApi(teamSchedule: _team())..bookError = const ApiException(status: 409, code: 'slot-taken', title: 'Slot vừa có nhóm khác chọn');
    await _pump(tester, const TeamScheduleScreen(), api, overrides: _student(leader: true));

    await tester.tap(find.bySemanticsLabel('Thứ Ba 09:15, còn trống'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Chọn slot'));
    await tester.pumpAndSettle();
    expect(find.text('Slot này vừa có nhóm khác chọn. Hãy chọn slot còn trống.'), findsOneWidget);
    expect(api.calls.where((c) => c == 'team w1').length, 2);
    semantics.dispose();
  });

  testWidgets('C09: thành viên chỉ xem slot của nhóm; chưa có lịch → trạng thái trống', (tester) async {
    final api = _FakeApi(teamSchedule: _team(canBook: false, mySlot: _slot('b', '09:15', 'MINE')));
    await _pump(tester, const TeamScheduleScreen(), api, overrides: _student(leader: false));
    expect(find.text('Họp hằng tuần · Thứ Ba 09:15'), findsOneWidget);
    expect(find.text('Trưởng nhóm chọn slot cho nhóm.'), findsOneWidget);
    expect(find.text('09:00'), findsNothing);
  });

  testWidgets('C09: giảng viên chưa đăng lịch → nhóm thấy "Giảng viên chưa đăng lịch"', (tester) async {
    await _pump(tester, const TeamScheduleScreen(), _FakeApi(), overrides: _student(leader: true));
    expect(find.text('Giảng viên chưa đăng lịch'), findsOneWidget);
  });

  testWidgets('C09 giảng viên: xem trước số slot, bắt thiếu link, lưu, cảnh báo thiếu slot, đăng lịch, đổi slot cho nhóm', (tester) async {
    tester.view.physicalSize = const Size(800, 2400);
    addTearDown(tester.view.resetPhysicalSize);
    final semantics = tester.ensureSemantics();
    final api = _FakeApi();
    final hub = await _pump(tester, const LecturerScheduleScreen(), api, overrides: [
      myWorkspacesProvider.overrideWith((ref) async => const [WorkspaceOverview(id: 'w2', teamName: 'Nhóm Bình Minh', courseCode: 'EXE201', myRole: WorkspaceRole.instructor)]),
    ]);

    expect(hub.joined, contains('JoinWorkspace w2'));
    expect(find.text('Xem trước: 8 slot'), findsOneWidget);
    await tester.tap(find.text('Lưu khung giờ'));
    await tester.pump();
    expect(find.text('Nhập link họp.'), findsOneWidget);
    expect(api.calls, isEmpty);

    await tester.enterText(find.widgetWithText(TextField, 'Link họp'), 'https://meet.example/abc');
    await tester.tap(find.text('Lưu khung giờ'));
    await tester.pumpAndSettle();
    expect(api.calls.single, 'save 2 09:00-11:00 15 https://meet.example/abc');
    expect(find.text('2 slot · 3 nhóm · còn 1 nhóm chưa chọn'), findsOneWidget);
    expect(find.text('Thiếu slot so với số nhóm'), findsOneWidget);

    await tester.tap(find.text('Đăng lịch'));
    await tester.pumpAndSettle();
    expect(api.calls, contains('publish'));

    await tester.scrollUntilVisible(find.text('Nhóm Bình Minh'), 200, scrollable: find.byType(Scrollable).first);
    await tester.tap(find.bySemanticsLabel('Thứ Ba 09:00, Nhóm Bình Minh'));
    await tester.pumpAndSettle();
    expect(find.text('Đổi slot cho Nhóm Bình Minh'), findsOneWidget);
    await tester.tap(find.text('Thứ Ba 09:15–'));
    await tester.pumpAndSettle();
    expect(api.calls.last, 'move w2 b');
    semantics.dispose();
  });
}
