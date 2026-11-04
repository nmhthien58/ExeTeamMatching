import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/phase.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_api.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_models.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_view_model.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

TopicInterestItem _interest(String id, String teamId, String status) =>
    TopicInterestItem(id: id, teamId: teamId, advisorId: 'gv', advisorName: 'GV', status: status);

class _FakeApi extends LecturerHomeApi {
  _FakeApi() : super(Dio());

  final calls = <String>[];
  Object? error;
  List<TopicInterestItem> interests = const [];

  @override
  Future<void> openInterest(String teamId, {String advisorKind = 'LECTURER'}) async {
    calls.add('open:$teamId:$advisorKind');
    if (error != null) throw error!;
  }

  @override
  Future<void> withdrawInterest(String interestId) async {
    calls.add('withdraw:$interestId');
    if (error != null) throw error!;
  }

  @override
  Future<List<TopicInterestItem>> getMyTopicInterests({String advisorKind = 'LECTURER'}) async {
    calls.add('mine');
    return interests;
  }
}

/// MB-49 – ViewModel của L01 test riêng, không dựng widget.
void main() {
  late _FakeApi api;
  late ProviderContainer container;

  setUp(() {
    api = _FakeApi();
    container = ProviderContainer(overrides: [
      lecturerHomeApiProvider.overrideWithValue(api),
      phasesProvider.overrideWith((ref) async => const []),
    ]);
    addTearDown(container.dispose);
    container.listen(lecturerHomeViewModelProvider, (_, _) {});
  });

  LecturerHomeViewModel vm() => container.read(lecturerHomeViewModelProvider.notifier);

  test('mặc định EXE201, đổi môn', () {
    expect(container.read(lecturerHomeViewModelProvider).course, 'EXE201');
    vm().selectCourse('EXE101');
    expect(container.read(lecturerHomeViewModelProvider).course, 'EXE101');
  });

  test('mở quan tâm thành công → InterestDone', () async {
    final outcome = await vm().openInterest('t1');
    expect(outcome, isA<InterestDone>());
    expect(api.calls, ['open:t1:LECTURER']);
  });

  test('mở quan tâm lỗi → câu thông báo từ lỗi', () async {
    api.error = Exception('Nhóm đã có cố vấn');
    final outcome = await vm().openInterest('t1');
    expect((outcome as InterestFailed).message, 'Nhóm đã có cố vấn');
  });

  test('rút quan tâm lỗi không phải Exception → câu mặc định', () async {
    api.error = StateError('x');
    final outcome = await vm().withdrawInterest('i1');
    expect((outcome as InterestFailed).message, 'Không thể rút quan tâm');
  });

  test('hoàn tác mở quan tâm: tìm quan tâm OPEN của nhóm rồi rút', () async {
    api.interests = [_interest('i0', 't1', 'WITHDRAWN'), _interest('i1', 't1', 'OPEN'), _interest('i2', 't2', 'OPEN')];
    final outcome = await vm().undoOpen('t1');
    expect(outcome, isA<InterestDone>());
    expect(api.calls, ['mine', 'withdraw:i1']);
  });

  test('hoàn tác không tìm thấy quan tâm → câu hoàn tác lỗi', () async {
    final outcome = await vm().undoOpen('t1');
    expect((outcome as InterestFailed).message, LecturerHomeViewModel.undoFailedMessage);
  });

  test('hoàn tác rút quan tâm: mở lại', () async {
    expect(await vm().undoWithdraw('t2'), isA<InterestDone>());
    expect(api.calls, ['open:t2:LECTURER']);
  });

  test('openInterestsOf chỉ giữ OPEN', () {
    final items = [_interest('a', 't', 'OPEN'), _interest('b', 't', 'WITHDRAWN')];
    expect(openInterestsOf(items).map((i) => i.id), ['a']);
  });

  test('giai đoạn: theo phasesProvider của môn đang xem, thiếu thì theo khung', () async {
    final c = ProviderContainer(overrides: [
      lecturerHomeApiProvider.overrideWithValue(api),
      shellPhaseProvider.overrideWithValue(const ShellPhase(isTeamFormation: false, isMentoringWindow: true)),
      phasesProvider.overrideWith((ref) async => const []),
    ]);
    addTearDown(c.dispose);
    c.listen(lecturerHomeIsTeamFormationProvider, (_, _) {});
    await c.read(phasesProvider.future);
    expect(c.read(lecturerHomeIsTeamFormationProvider), isFalse);
  });
}
