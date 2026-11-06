import 'dart:ui' show VoidCallback;

import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/features/meeting_schedule/meeting_schedule_api.dart';
import 'package:exe_team_matching/features/meeting_schedule/meeting_schedule_view_model.dart';
import 'package:exe_team_matching/features/realtime/collab_hub.dart';
import 'package:exe_team_matching/features/workspace/workspace_api.dart';
import 'package:exe_team_matching/features/workspace/workspace_models.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

const _slot = ScheduleSlot(id: 'x1', dayOfWeek: 2, startTime: '09:00', endTime: '09:15', state: 'FREE');
const _taken = ScheduleSlot(id: 'x2', dayOfWeek: 2, startTime: '09:15', endTime: '09:30', state: 'TAKEN', workspaceId: 'w1', teamName: 'Nhóm A');
final _schedule = MeetingSchedule(
  id: 'sc1',
  status: 'DRAFT',
  slotMinutes: 15,
  location: 'https://meet',
  bookingDeadline: DateTime.utc(2026, 12),
  windows: const [ScheduleWindow(dayOfWeek: 3, startTime: '13:00', endTime: '14:00')],
  slots: const [],
  teamCount: 0,
  teamsWithoutSlot: const [],
);

class _FakeApi extends MeetingScheduleApi {
  _FakeApi() : super(Dio());

  Object? error;
  MeetingSchedule? mine_;
  final calls = <String>[];

  Future<MeetingSchedule> _do(String call) async {
    calls.add(call);
    if (error != null) throw error!;
    return _schedule;
  }

  @override
  Future<MeetingSchedule?> mine() async => mine_;

  @override
  Future<MeetingSchedule> save(List<ScheduleWindow> windows, int slotMinutes, String location) => _do('save ${windows.length} $slotMinutes $location');

  @override
  Future<MeetingSchedule> publish() => _do('publish');

  @override
  Future<MeetingSchedule> move(String workspaceId, String slotId) => _do('move $workspaceId $slotId');

  @override
  Future<TeamMeetingSchedule?> team(String workspaceId) async => null;

  @override
  Future<TeamMeetingSchedule> book(String workspaceId, String slotId) async {
    calls.add('book $slotId');
    if (error != null) throw error!;
    return TeamMeetingSchedule(lecturerName: 'Cô Hà', location: '', bookingDeadline: DateTime.utc(2026, 12), canBook: true, slots: const []);
  }
}

class _Connection implements HubConnectionLike {
  final handlers = <String, List<void Function(Object?)>>{};
  final invoked = <String>[];

  void emit(String event) {
    for (final h in [...?handlers[event]]) {
      h(null);
    }
  }

  @override
  Future<void> start() async {}
  @override
  Future<void> stop() async {}
  @override
  Future<void> invoke(String method, String id) async => invoked.add('$method $id');
  @override
  void on(String event, void Function(Object? payload) handler) => (handlers[event] ??= []).add(handler);
  @override
  void off(String event, void Function(Object? payload) handler) => handlers[event]?.remove(handler);
  @override
  void onStatus({required VoidCallback reconnecting, required VoidCallback reconnected, required VoidCallback closed}) {}
}

Future<void> _settle() => Future<void>.delayed(Duration.zero);

/// MB-49 – ViewModel của C09 (nhóm, giảng viên) test riêng, không dựng widget.
void main() {
  late _FakeApi api;
  late _Connection connection;
  late ProviderContainer c;

  setUp(() {
    api = _FakeApi();
    connection = _Connection();
    c = ProviderContainer(
      overrides: [
        meetingScheduleApiProvider.overrideWithValue(api),
        collabHubProvider.overrideWithValue(CollabHub(() => connection)),
        myWorkspacesProvider.overrideWith(
          (ref) async => const [
            WorkspaceOverview(id: 'w1', teamName: 'A', courseCode: 'EXE201', myRole: WorkspaceRole.instructor),
            WorkspaceOverview(id: 'w0', teamName: 'B', courseCode: 'EXE101', myRole: WorkspaceRole.instructor),
          ],
        ),
      ],
    );
    addTearDown(c.dispose);
  });

  group('TeamScheduleViewModel', () {
    test('chọn slot được: không lỗi', () async {
      c.listen(teamScheduleViewModelProvider('w1'), (_, _) {});
      expect(await c.read(teamScheduleViewModelProvider('w1').notifier).book(_slot), isTrue);
      expect(api.calls, ['book x1']);
      expect(c.read(teamScheduleViewModelProvider('w1')).error, isNull);
    });

    test('slot vừa bị nhóm khác chọn: báo tại ô', () async {
      api.error = const ApiException(status: 409, code: 'slot-taken', title: 'Đã có nhóm');
      c.listen(teamScheduleViewModelProvider('w1'), (_, _) {});
      expect(await c.read(teamScheduleViewModelProvider('w1').notifier).book(_slot), isFalse);
      final state = c.read(teamScheduleViewModelProvider('w1'));
      expect(state.failedSlot, 'x1');
      expect(state.error, scheduleErrors['slot-taken'] ?? 'Đã có nhóm');
    });

    test('slotsChanged từ hub: tải lại lịch nhóm', () async {
      var loads = 0;
      c = ProviderContainer(
        overrides: [
          meetingScheduleApiProvider.overrideWithValue(api),
          collabHubProvider.overrideWithValue(CollabHub(() => connection)),
          teamScheduleProvider.overrideWith((ref, id) async {
            loads++;
            return null;
          }),
        ],
      );
      addTearDown(c.dispose);
      c
        ..listen(teamScheduleViewModelProvider('w1'), (_, _) {})
        ..listen(teamScheduleProvider('w1'), (_, _) {});
      await _settle();
      connection.emit('slotsChanged');
      await c.read(teamScheduleProvider('w1').future);
      expect(loads, 2);
    });
  });

  group('LecturerScheduleViewModel', () {
    LecturerScheduleState state(String? id) => c.read(lecturerScheduleViewModelProvider(id));

    test('chưa có lịch: một khung mặc định, 15 phút', () {
      c.listen(lecturerScheduleViewModelProvider(null), (_, _) {});
      expect(state(null).windows, [LecturerScheduleViewModel.defaultWindow]);
      expect((state(null).slotMinutes, state(null).previewCount), (15, 8));
    });

    test('kiểm tra form: giờ kết thúc, link họp, độ dài slot', () async {
      c.listen(lecturerScheduleViewModelProvider(null), (_, _) {});
      final vm = c.read(lecturerScheduleViewModelProvider(null).notifier);
      vm.setWindow(0, const ScheduleWindow(dayOfWeek: 2, startTime: '11:00', endTime: '10:00'));
      expect(state(null).shownBadWindow, -1);
      expect(await vm.save(), isNull);
      expect((state(null).shownBadWindow, state(null).locationError), (0, 'Nhập link họp.'));
      vm
        ..setWindow(0, LecturerScheduleViewModel.defaultWindow)
        ..locationChanged(' https://meet ')
        ..minutesChanged('90');
      expect(await vm.save(), isNull);
      expect(api.calls, isEmpty);
      vm
        ..minutesChanged('20')
        ..addWindow();
      expect(await vm.save(), 'Đã lưu khung giờ');
      expect(api.calls, ['save 2 20 https://meet']);
      vm.removeWindow(1);
      expect(state(null).windows, hasLength(1));
    });

    test('form lấy giá trị từ lịch đang có; đăng lịch, đổi slot; lỗi hiện banner', () async {
      api.mine_ = _schedule;
      await c.read(myScheduleProvider.future);
      c.listen(lecturerScheduleViewModelProvider('sc1'), (_, _) {});
      expect((state('sc1').windows.single.dayOfWeek, state('sc1').minutesText, state('sc1').location), (3, '15', 'https://meet'));
      final vm = c.read(lecturerScheduleViewModelProvider('sc1').notifier);
      expect(await vm.publish(), 'Đã đăng lịch cho các nhóm');
      expect(await vm.move(_taken, _slot), 'Đã đổi slot cho Nhóm A');
      api.error = const ApiException(status: 400, code: 'x', title: 'Lỗi', detail: 'Không lưu được');
      expect(await vm.publish(), isNull);
      expect((state('sc1').error, state('sc1').busy), ('Không lưu được', false));
      expect(api.calls, ['publish', 'move w1 x1', 'publish']);
    });

    test('đồng bộ: chỉ tham gia nhóm hub của dự án EXE201', () async {
      c.listen(lecturerScheduleSyncProvider, (_, _) {});
      await c.read(myWorkspacesProvider.future);
      await _settle();
      expect(connection.handlers['slotsChanged'], hasLength(1));
    });
  });
}
