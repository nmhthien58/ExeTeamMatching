import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/core/view_state.dart';
import 'package:exe_team_matching/features/discover/paged_list.dart';
import 'package:exe_team_matching/features/discover/post_form_view_model.dart';
import 'package:exe_team_matching/features/discover/posts_api.dart';
import 'package:exe_team_matching/features/my_team/members_view_model.dart';
import 'package:exe_team_matching/features/my_team/my_team_api.dart';
import 'package:exe_team_matching/features/my_team/requests_view_model.dart';
import 'package:exe_team_matching/features/my_team/team_form_view_model.dart';
import 'package:exe_team_matching/features/my_team/team_history_screen.dart';
import 'package:exe_team_matching/features/my_team/team_history_view_model.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

const _error = ApiException(status: 409, code: 'x', title: 'Lỗi', detail: 'Không được');
const _member = Member(id: 's2', studentCode: 'SE2', fullName: 'Bình', isLeader: false);
const _d05 = CatalogRef(id: 5, code: 'D05', name: 'Giáo dục');

MyTeam _team() => const MyTeam(
  id: 't1',
  name: 'Nhóm A',
  status: 'DRAFT',
  courseCode: 'EXE101',
  members: [],
  domains: [_d05],
  isLeader: true,
  memberCount: 3,
  specializationCount: 1,
  relation: 'LEADER',
);

class _FakeTeamApi extends MyTeamApi {
  _FakeTeamApi() : super(Dio());

  Object? error;
  bool applied = true;
  final calls = <String>[];
  TeamInput? input;

  Future<T> _do<T>(String call, T value) async {
    calls.add(call);
    if (error != null) throw error!;
    return value;
  }

  @override
  Future<MyTeam?> myTeam() async => null;

  @override
  Future<MemberChange> transferLeader(String teamId, String studentId) => _do('leader $studentId', MemberChange(applied: applied));

  @override
  Future<MemberChange> removeMember(String teamId, String studentId) => _do('remove $studentId', MemberChange(applied: applied));

  @override
  Future<MemberChange> leave(String teamId) => _do('leave', MemberChange(applied: applied));

  @override
  Future<MemberChange> acceptJoinRequest(String id) => _do('accept $id', MemberChange(applied: applied));

  @override
  Future<void> declineJoinRequest(String id) => _do('decline $id', null);

  @override
  Future<void> cancelInvitation(String id) => _do('cancel $id', null);

  @override
  Future<List<JoinRequestItem>> joinRequests(String teamId) async => const [];

  @override
  Future<List<SentInvitation>> sentInvitations(String teamId) async => const [];

  @override
  Future<MyTeam> create(TeamInput input) {
    this.input = input;
    return _do('create', _team());
  }

  @override
  Future<MyTeam> update(String teamId, TeamInput input) {
    this.input = input;
    return _do('update $teamId', _team());
  }
}

class _FakeHistory extends TeamHistoryApi {
  _FakeHistory() : super(Dio());

  List<List<AuditLogEntry>> pages = const [];
  Object? error;

  @override
  Future<(List<AuditLogEntry>, bool)> page(String teamId, int page) async {
    if (error != null) throw error!;
    return (page <= pages.length ? pages[page - 1] : const <AuditLogEntry>[], page < pages.length);
  }
}

class _FakePosts extends PostsApi {
  _FakePosts() : super(Dio());

  Object? error;
  final calls = <String>[];

  @override
  Future<RecruitmentPost> saveRecruitment(String teamId, {required String title, String? description, required List<int> specializationIds}) async {
    calls.add('save $teamId $title $specializationIds');
    if (error != null) throw error!;
    return RecruitmentPost(id: 'r', title: title, specializationIds: specializationIds);
  }

  @override
  Future<void> closeRecruitment(String teamId) async {
    calls.add('close $teamId');
    if (error != null) throw error!;
  }
}

AuditLogEntry _entry(int id) => AuditLogEntry(id: id, action: 'MEMBER_JOINED', actorName: 'An', summary: 'Vào nhóm', occurredAt: DateTime.utc(2026, 11));

Future<void> _settle() => Future<void>.delayed(Duration.zero);

/// MB-49 – ViewModel của S13/S16, S17, S20, S21, S22 test riêng, không dựng widget.
void main() {
  test('S13: đủ số nhóm của môn thì không cho thử lại (BE-89)', () {
    final state = TeamFormState(error: ApiException(status: 409, code: 'team-limit-reached', title: 'Đã đủ số nhóm của môn'));
    expect(state.isTeamLimitReached, isTrue);
    expect(const TeamFormState().isTeamLimitReached, isFalse);
  });

  late _FakeTeamApi api;
  late _FakeHistory history;
  late _FakePosts posts;
  late ProviderContainer c;

  setUp(() {
    api = _FakeTeamApi();
    history = _FakeHistory();
    posts = _FakePosts();
    c = ProviderContainer(
      overrides: [
        myTeamApiProvider.overrideWithValue(api),
        teamHistoryApiProvider.overrideWithValue(history),
        postsApiProvider.overrideWithValue(posts),
        myTeamProvider.overrideWith((ref) async => null),
      ],
    );
    addTearDown(c.dispose);
  });

  group('MembersViewModel (S17)', () {
    MembersViewModel vm() => c.read(membersViewModelProvider.notifier);
    setUp(() => c.listen(membersViewModelProvider, (_, _) {}));

    test('chuyển trưởng nhóm / xóa: áp dụng ngay hoặc chờ Bộ môn duyệt', () async {
      final applied = await vm().transferLeader('t1', 's2') as MemberChanged;
      expect((applied.message, applied.leaving), ('Đã cập nhật', false));
      api.applied = false;
      expect((await vm().remove('t1', 's2') as MemberChanged).message, 'Đã thêm vào yêu cầu thay đổi (chưa gửi). Vào Yêu cầu thay đổi để gửi Bộ môn duyệt.');
      expect(api.calls, ['leader s2', 'remove s2']);
    });

    test('rời nhóm: leaving = true', () async {
      expect((await vm().leave('t1') as MemberChanged).leaving, isTrue);
    });

    test('lỗi: state là câu lỗi; thành công sau đó xóa lỗi', () async {
      api.error = _error;
      expect(await vm().leave('t1'), isA<MemberFailed>());
      expect(c.read(membersViewModelProvider), 'Không được');
      api.error = null;
      await vm().leave('t1');
      expect(c.read(membersViewModelProvider), isNull);
    });
  });

  group('JoinRequestsViewModel / SentInvitationsViewModel (S20)', () {
    const request = JoinRequestItem(id: 'j1', student: _member, ideas: [], hasNoIdea: true, status: 'PENDING');

    test('nhận / từ chối: câu SnackBar theo kết quả', () async {
      c.listen(joinRequestsViewModelProvider('t1'), (_, _) {});
      final vm = c.read(joinRequestsViewModelProvider('t1').notifier);
      expect((await vm.decide(request, accept: true) as DecisionDone).message, 'Đã nhận Bình');
      api.applied = false;
      expect((await vm.decide(request, accept: true) as DecisionDone).message, 'Đã thêm vào yêu cầu thay đổi (chưa gửi). Vào Yêu cầu thay đổi để gửi Bộ môn duyệt.');
      expect((await vm.decide(request, accept: false) as DecisionDone).message, 'Đã từ chối');
      expect(c.read(joinRequestsViewModelProvider('t1')).busyId, isNull);
    });

    test('lỗi: hiện câu lỗi, bỏ trạng thái đang xử lý', () async {
      c.listen(joinRequestsViewModelProvider('t1'), (_, _) {});
      api.error = _error;
      expect(await c.read(joinRequestsViewModelProvider('t1').notifier).decide(request, accept: true), isA<DecisionFailed>());
      final state = c.read(joinRequestsViewModelProvider('t1'));
      expect((state.busyId, state.error), (null, 'Không được'));
    });

    test('thu hồi lời mời: lỗi thì state là câu lỗi', () async {
      c.listen(sentInvitationsViewModelProvider('t1'), (_, _) {});
      final vm = c.read(sentInvitationsViewModelProvider('t1').notifier);
      final invitation = SentInvitation(id: 'i1', invitee: _member, status: 'PENDING', expiresAt: DateTime.utc(2026, 12));
      await vm.cancel(invitation);
      expect(c.read(sentInvitationsViewModelProvider('t1')), isNull);
      api.error = _error;
      await vm.cancel(invitation);
      expect(c.read(sentInvitationsViewModelProvider('t1')), 'Không được');
      expect(api.calls, ['cancel i1', 'cancel i1']);
    });
  });

  group('TeamFormViewModel (S13, S16)', () {
    test('tạo mới: kiểm tra tên ≥ 2 ký tự và có domain', () async {
      c.listen(teamFormViewModelProvider(null), (_, _) {});
      final vm = c.read(teamFormViewModelProvider(null).notifier);
      expect(await vm.save(name: 'A', projectName: '', ideaDescription: ''), isA<TeamFormNotSaved>());
      var state = c.read(teamFormViewModelProvider(null));
      expect((state.nameError, state.domainError), ('Tên nhóm cần ít nhất 2 ký tự.', 'Chọn ít nhất một domain.'));
      expect(api.calls, isEmpty);

      vm.setDomains(const [_d05]);
      final outcome = await vm.save(name: ' Nhóm B ', projectName: ' ', ideaDescription: 'Ý tưởng');
      expect((outcome as TeamFormSaved).message, 'Đã tạo nhóm');
      expect((api.input!.name, api.input!.primaryDomainId), ('Nhóm B', 5));
      expect(api.input!.domainIds, [5]);
    });

    test('sửa: lưu theo id, lỗi theo ô và lỗi chung', () async {
      final team = _team();
      c.listen(teamFormViewModelProvider(team), (_, _) {});
      final vm = c.read(teamFormViewModelProvider(team).notifier);
      expect(c.read(teamFormViewModelProvider(team)).domains, [_d05]);
      expect((await vm.save(name: 'Nhóm A', projectName: '', ideaDescription: '') as TeamFormSaved).message, 'Đã lưu');
      expect(api.calls, ['update t1']);

      api.error = const ApiException(
        status: 400,
        code: 'validation',
        title: 'Sai',
        fieldErrors: {
          'name': ['Tên đã có'],
        },
      );
      await vm.save(name: 'Nhóm A', projectName: '', ideaDescription: '');
      expect(c.read(teamFormViewModelProvider(team)).nameError, 'Tên đã có');
      vm.nameChanged('Nhóm C');
      expect(c.read(teamFormViewModelProvider(team)).nameError, isNull);

      api.error = _error;
      await vm.save(name: 'Nhóm A', projectName: '', ideaDescription: '');
      expect(c.read(teamFormViewModelProvider(team)).generalError, 'Không được');
      vm.removeDomainAt(0);
      expect(c.read(teamFormViewModelProvider(team)).domains, isEmpty);
    });
  });

  group('RecruitmentPostViewModel (S21)', () {
    test('tiêu đề trống, đăng, đóng bài, lỗi', () async {
      c.listen(recruitmentPostViewModelProvider('t1'), (_, _) {});
      final vm = c.read(recruitmentPostViewModelProvider('t1').notifier);
      await vm.save(title: '', description: '', specializationIds: const [], isEdit: false);
      expect(c.read(recruitmentPostViewModelProvider('t1')).titleError, 'Nhập tiêu đề bài tuyển.');
      expect((await vm.save(title: 'Cần BA', description: '', specializationIds: const [2], isEdit: false) as PostFormDone).message, 'Đã đăng bài tuyển');
      expect((await vm.close() as PostFormDone).message, 'Đã đóng bài tuyển');
      expect(posts.calls, ['save t1 Cần BA [2]', 'close t1']);
      posts.error = const ApiException(
        status: 400,
        code: 'v',
        title: 'Sai',
        fieldErrors: {
          'specializationIds': ['Chọn chuyên ngành'],
        },
      );
      await vm.save(title: 'Cần BA', description: '', specializationIds: const [], isEdit: true);
      expect(c.read(recruitmentPostViewModelProvider('t1')).fieldError('specializationIds'), 'Chọn chuyên ngành');
    });
  });

  group('TeamHistoryViewModel (S22)', () {
    ViewState<PagedList<AuditLogEntry>> state() => c.read(teamHistoryViewModelProvider('t1'));

    test('tải được và Xem thêm', () async {
      history.pages = [
        [_entry(1)],
        [_entry(2)],
      ];
      c.listen(teamHistoryViewModelProvider('t1'), (_, _) {});
      expect(state(), isA<ViewLoading<PagedList<AuditLogEntry>>>());
      await _settle();
      await c.read(teamHistoryViewModelProvider('t1').notifier).loadMore();
      expect(state().valueOrNull!.items.map((e) => e.id), [1, 2]);
      expect(state().valueOrNull!.hasMore, isFalse);
    });

    test('trống và lỗi', () async {
      c.listen(teamHistoryViewModelProvider('t1'), (_, _) {});
      await _settle();
      expect(state(), isA<ViewEmpty<PagedList<AuditLogEntry>>>());
      history.error = _error;
      await c.read(teamHistoryViewModelProvider('t1').notifier).refresh();
      expect((state() as ViewError).error, _error);
    });
  });
}
