import 'package:dio/dio.dart';
import 'package:exe_team_matching/features/change_requests/change_requests_api.dart';
import 'package:exe_team_matching/features/change_requests/change_requests_view_model.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

StudentRef _s(String id, {int? spec}) => StudentRef(
      id: id,
      studentCode: 'SE$id',
      fullName: 'SV $id',
      specialization: spec == null ? null : CatalogRef(id: spec, code: 'C$spec', name: 'CN $spec'),
    );

ChangeRequest _req(String id, String status, String round) => ChangeRequest(
      id: id,
      roundName: round,
      reason: 'Lý do',
      changedMemberCount: 1,
      changedPercent: 20,
      status: status,
      createdAt: DateTime.utc(2026, 11, 1),
    );

final _team = TeamDetail(
  card: const TeamCard(id: 't1', name: 'Nhóm', status: 'ACTIVE', memberCount: 4),
  members: [_s('a', spec: 1), _s('b', spec: 1), _s('c', spec: 1), _s('d', spec: 1)],
  isLeader: true,
);

final _pending = PendingChanges(
  baselineMembers: [_s('a'), _s('b'), _s('c'), _s('d'), _s('e')],
  pendingItems: [
    ChangeItem(id: 'add-x', action: 'ADD', student: _s('x', spec: 2)),
    ChangeItem(id: 'rm-a', action: 'REMOVE', student: _s('a', spec: 1)),
    const ChangeItem(id: 'old-project', action: 'PROJECT_CHANGE'),
  ],
  wouldHaveMemberCount: 4,
  wouldHaveSpecializationCount: 1,
);

class _FakeApi extends ChangeRequestsApi {
  _FakeApi() : super(Dio());

  final calls = <String>[];
  Object? error;

  @override
  Future<ChangeItem> setProjectChange(String teamId, {required String name, String? description}) async {
    calls.add('project $teamId $name');
    return const ChangeItem(id: 'new-project', action: 'PROJECT_CHANGE');
  }

  @override
  Future<ChangeRequest> create(String teamId, {required List<String> itemIds, required String reason}) async {
    calls.add('create $teamId ${itemIds.join(',')} $reason');
    if (error != null) throw error!;
    return _req('r1', 'PENDING', 'Đợt 1');
  }
}

/// MB-49 – ViewModel của S26 và S27/S28, không dựng widget.
void main() {
  late _FakeApi api;
  late ProviderContainer container;

  setUp(() {
    api = _FakeApi();
    container = ProviderContainer(overrides: [changeRequestsApiProvider.overrideWithValue(api)]);
    addTearDown(container.dispose);
    container.listen(changeRequestListViewModelProvider('t1'), (_, _) {});
    container.listen(createChangeRequestViewModelProvider('t1'), (_, _) {});
  });

  test('S26: đợt sắp xếp, không trùng; lọc theo trạng thái và đợt', () {
    final items = [_req('1', 'PENDING', 'Đợt 2'), _req('2', 'APPROVED', 'Đợt 1'), _req('3', 'PENDING', 'Đợt 1')];
    expect(ChangeRequestListViewModel.rounds(items), ['Đợt 1', 'Đợt 2']);
    final vm = container.read(changeRequestListViewModelProvider('t1').notifier);
    expect(vm.visible(items).length, 3);
    vm.filterStatus('PENDING');
    expect(vm.visible(items).map((r) => r.id), ['1', '3']);
    vm.filterRound('Đợt 1');
    expect(vm.visible(items).map((r) => r.id), ['3']);
  });

  group('S27/S28', () {
    CreateChangeRequestViewModel vm() => container.read(createChangeRequestViewModelProvider('t1').notifier);
    CreateChangeRequestState state() => container.read(createChangeRequestViewModelProvider('t1'));

    test('chưa chọn gì và chưa đổi đề tài → chưa được tiếp tục', () {
      expect(vm().summary(_team, _pending, '').canContinue, isFalse);
      expect(vm().summary(_team, _pending, 'Đề tài mới').canContinue, isTrue);
      expect(vm().summary(_team, _pending, 'X').canContinue, isFalse);
    });

    test('chọn thêm / bớt: tính số người, chuyên ngành, tỷ lệ thay đổi', () {
      vm().toggle('add-x', true);
      var summary = vm().summary(_team, _pending, '');
      expect((summary.count, summary.specializationCount, summary.canContinue), (5, 2, true));
      vm().toggle('rm-a', true);
      vm().toggle('add-x', false);
      summary = vm().summary(_team, _pending, '');
      expect((summary.count, summary.canContinue), (3, false));
      expect(vm().changeRatio(_pending), (memberChangeCount: 1, percent: 20));
      expect(vm().selectedItems(_pending).single.id, 'rm-a');
    });

    test('lý do dưới 10 ký tự → lỗi, không gọi API', () async {
      expect(vm().validateReason('ngắn'), isFalse);
      expect(state().error, CreateChangeRequestViewModel.reasonTooShort);
      expect(await vm().submit(_pending, projectName: '', projectDescription: '', reason: 'ngắn'), isA<ChangeRequestNotCreated>());
      expect(api.calls, isEmpty);
    });

    test('gửi kèm đổi đề tài: bỏ mục đề tài cũ, thêm mục mới', () async {
      vm()
        ..toggle('add-x', true)
        ..toggle('old-project', true)
        ..goToStep(1);
      final outcome = await vm().submit(_pending, projectName: ' Đề tài mới ', projectDescription: '', reason: ' Cần thêm người làm mobile ');
      expect((outcome as ChangeRequestCreated).requestId, 'r1');
      expect(api.calls, ['project t1 Đề tài mới', 'create t1 add-x,new-project Cần thêm người làm mobile']);
      expect((state().busy, state().step), (false, 1));
    });

    test('gửi lỗi → giữ nội dung, câu lỗi', () async {
      api.error = Exception('500');
      vm().toggle('add-x', true);
      expect(await vm().submit(_pending, projectName: '', projectDescription: '', reason: 'Lý do đủ dài rồi'), isA<ChangeRequestNotCreated>());
      expect((state().busy, state().error), (false, CreateChangeRequestViewModel.submitFailed));
    });
  });
}
