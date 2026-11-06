import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/features/meetings/meetings_api.dart';
import 'package:exe_team_matching/features/meetings/meetings_view_model.dart';
import 'package:exe_team_matching/features/workspace/workspace_models.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

const _vy = PersonRef(personType: 'STUDENT', personId: 's1', displayName: 'Vy', role: WorkspaceRole.leader);
const _detail = MeetingDetail(
  card: MeetingCard(id: 'm1', title: 'Họp', status: MeetingStatus.proposed, organizer: _vy),
  slots: [],
);
const _error = ApiException(status: 409, code: 'x', title: 'Lỗi', detail: 'Khung đã đủ');

final _now = DateTime.utc(2026, 11, 4);
final _future = DateTime.utc(2026, 11, 10, 2);

class _FakeApi extends MeetingsApi {
  _FakeApi() : super(Dio());

  Object? error;
  final calls = <String>[];

  Future<T> _do<T>(String call, T value) async {
    calls.add(call);
    if (error != null) throw error!;
    return value;
  }

  @override
  Future<MeetingDetail> detail(String id) async => _detail;

  @override
  Future<MeetingList> list(String workspaceId) async => throw UnimplementedError();

  @override
  Future<MeetingDetail> answer(String id, String slotId, String answer) => _do('answer $slotId $answer', _detail);

  @override
  Future<MeetingDetail> confirm(String id, String slotId) => _do('confirm $slotId', _detail);

  @override
  Future<MeetingDetail> addSlot(String id, SlotInput slot) => _do('add', _detail);

  @override
  Future<MeetingDetail> cancel(String id, String reason) => _do('cancel $reason', _detail);

  @override
  Future<MeetingDetail> saveMinutes(String id, String content) => _do('minutes', _detail);

  @override
  Future<int> minutesToTasks(String id, List<String> titles) => _do('tasks ${titles.length}', titles.length);

  @override
  Future<MeetingDetail> propose(String workspaceId, {required String title, String? agenda, String? location, required List<SlotInput> slots}) =>
      _do('propose $title $location $agenda ${slots.length}', _detail);
}

/// MB-49 – ViewModel của C08 (chi tiết, đề xuất họp) test riêng, không dựng widget.
void main() {
  late _FakeApi api;
  late ProviderContainer c;

  setUp(() {
    api = _FakeApi();
    c = ProviderContainer(overrides: [meetingsApiProvider.overrideWithValue(api)]);
    addTearDown(c.dispose);
  });

  group('MeetingDetailViewModel', () {
    setUp(() => c.listen(meetingDetailViewModelProvider('m1'), (_, _) {}));
    MeetingDetailViewModel vm() => c.read(meetingDetailViewModelProvider('m1').notifier);

    test('các lệnh trả câu SnackBar; trả lời khung không có câu', () async {
      expect(await vm().answer('x1', 'YES'), isA<MeetingActionDone>().having((o) => o.message, 'message', isNull));
      expect((await vm().confirm('x1') as MeetingActionDone).message, 'Đã chốt khung giờ');
      expect((await vm().cancel('Bận') as MeetingActionDone).message, 'Đã hủy cuộc họp');
      expect((await vm().saveMinutes('- A') as MeetingActionDone).message, 'Đã lưu biên bản');
      expect((await vm().createTasks(['A', 'B']) as MeetingActionDone).message, 'Đã tạo 2 việc');
      expect((await vm().addSlot(_future, now: _now) as MeetingActionDone).message, 'Đã thêm khung giờ');
      expect(api.calls, ['answer x1 YES', 'confirm x1', 'cancel Bận', 'minutes', 'tasks 2', 'add']);
      expect(c.read(meetingDetailViewModelProvider('m1')).busy, isFalse);
    });

    test('khung giờ đã qua: báo lỗi, không gọi API', () async {
      expect(await vm().addSlot(_now.subtract(const Duration(hours: 1)), now: _now), isA<MeetingActionFailed>());
      expect(c.read(meetingDetailViewModelProvider('m1')).error, 'Khung giờ phải ở tương lai.');
      expect(api.calls, isEmpty);
    });

    test('API lỗi: banner có câu lỗi, bỏ trạng thái đang gửi', () async {
      api.error = _error;
      expect((await vm().confirm('x1')).ok, isFalse);
      final state = c.read(meetingDetailViewModelProvider('m1'));
      expect((state.busy, state.error), (false, 'Khung đã đủ'));
    });
  });

  group('ProposeMeetingViewModel', () {
    setUp(() => c.listen(proposeMeetingViewModelProvider('w1'), (_, _) {}));
    ProposeMeetingViewModel vm() => c.read(proposeMeetingViewModelProvider('w1').notifier);
    ProposeMeetingState state() => c.read(proposeMeetingViewModelProvider('w1'));

    test('thiếu tiêu đề và khung giờ: báo lỗi từng ô', () async {
      expect(await vm().submit(title: ' ', location: '', agenda: ''), isNull);
      expect((state().titleError, state().slotError), ('Nhập tiêu đề.', 'Thêm ít nhất một khung giờ.'));
      expect(api.calls, isEmpty);
    });

    test('thêm / bỏ khung, khung đã qua bị từ chối, nhãn nút theo số khung', () {
      vm().addSlot(_now, now: _now);
      expect(state().slots, isEmpty);
      expect(state().slotError, 'Khung giờ phải ở tương lai.');
      vm()
        ..addSlot(_future, now: _now)
        ..addSlot(_future.add(const Duration(days: 1)), now: _now);
      expect((state().slots.length, state().slotError, state().submitLabel), (2, null, 'Gửi đề xuất'));
      vm().removeSlot(0);
      expect(state().submitLabel, 'Tạo cuộc họp');
    });

    test('gửi được: trả id; nơi họp / nội dung trống thành null', () async {
      vm().addSlot(_future, now: _now);
      expect(await vm().submit(title: ' Chốt ý tưởng ', location: ' ', agenda: ''), 'm1');
      expect(api.calls, ['propose Chốt ý tưởng null null 1']);
    });

    test('lỗi gửi: giữ khung giờ, có câu lỗi', () async {
      api.error = _error;
      vm().addSlot(_future, now: _now);
      expect(await vm().submit(title: 'Họp', location: '', agenda: ''), isNull);
      expect((state().error, state().saving, state().slots.length), ('Khung đã đủ', false, 1));
    });
  });
}
