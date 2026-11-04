import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/core/view_state.dart';
import 'package:exe_team_matching/features/lecturer/advised_teams_view_model.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_api.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_models.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

AdvisedTeamItem _team(String id, {String? classCode, String course = 'EXE101'}) => AdvisedTeamItem(
      teamId: id,
      teamName: 'Nhóm $id',
      courseCode: course,
      classCode: classCode,
      memberCount: 5,
      reason: 'PREFERENCE',
    );

class _FakeApi extends LecturerHomeApi {
  _FakeApi() : super(Dio());

  List<AdvisedTeamItem> instructing = const [];
  List<AdvisedTeamItem> mentoring = const [];
  Object? teamsError;
  Object? markError;
  final courses = <String?>[];
  final calls = <String>[];

  @override
  Future<LecturerTeamsMeResponse> getLecturerTeamsMe({String? courseCode}) async {
    courses.add(courseCode);
    if (teamsError != null) throw teamsError!;
    return LecturerTeamsMeResponse(instructing: instructing, mentoring: mentoring);
  }

  @override
  Future<List<AdvisedTeamItem>> getMentorTeamsMe() async => mentoring;

  @override
  Future<List<AdvisorWorkspaceRef>> getAdvisorWorkspaces() async => [
        for (final t in [...instructing, ...mentoring])
          AdvisorWorkspaceRef(id: 'ws-${t.teamId}', teamId: t.teamId, teamName: t.teamName, courseCode: t.courseCode, myRole: 'LECTURER'),
      ];

  @override
  Future<List<CheckpointProgress>> getCheckpoints({int? missing}) async => const [
        CheckpointProgress(workspaceId: 'ws-a', teamName: 'Nhóm a', doneCount: 1, items: [
          CheckpointItem(checkpointNo: 1, presented: true),
          CheckpointItem(checkpointNo: 2, presented: false),
        ]),
        CheckpointProgress(workspaceId: 'ws-b', teamName: 'Nhóm b', doneCount: 2, items: [
          CheckpointItem(checkpointNo: 1, presented: true),
          CheckpointItem(checkpointNo: 2, presented: true),
        ]),
      ];

  @override
  Future<CheckpointProgress> markCheckpoint(String workspaceId, {required int checkpointNo, required DateTime presentedOn, String? comment}) async {
    calls.add('mark:$workspaceId:$checkpointNo:$comment');
    if (markError != null) throw markError!;
    return const CheckpointProgress(workspaceId: 'ws-a', teamName: 'Nhóm a', doneCount: 2);
  }

  @override
  Future<void> undoCheckpoint(String workspaceId, int checkpointNo) async => calls.add('undo:$workspaceId:$checkpointNo');
}

/// MB-49 – ViewModel của L04 / L05 / Checkpoint, không dựng widget.
void main() {
  late _FakeApi api;
  late ProviderContainer container;

  setUp(() {
    api = _FakeApi();
    container = ProviderContainer(retry: (_, _) => null, overrides: [lecturerHomeApiProvider.overrideWithValue(api)]);
    addTearDown(container.dispose);
  });

  Future<void> settle() async {
    for (var i = 0; i < 5; i++) {
      await Future<void>.delayed(Duration.zero);
    }
  }

  ViewState<List<AdvisedTeamRow>> listen(String kind) {
    container.listen(advisedTeamsListProvider(kind), (_, _) {});
    return container.read(advisedTeamsListProvider(kind));
  }

  AdvisedTeamsViewModel vm(String kind) => container.read(advisedTeamsViewModelProvider(kind).notifier);

  test('L04: đang tải rồi có danh sách, gồm cả nhóm mentor, kèm Checkpoint', () async {
    api.instructing = [_team('a'), _team('b')];
    api.mentoring = [_team('c')];
    expect(listen('LECTURER'), isA<ViewLoading<List<AdvisedTeamRow>>>());
    await settle();
    final rows = container.read(advisedTeamsListProvider('LECTURER')).valueOrNull!;
    expect(rows.map((r) => (r.entry.item.teamId, r.entry.isMentorRole)), [('a', false), ('b', false), ('c', true)]);
    expect(rows.first.checkpoint?.workspaceId, 'ws-a');
    expect(api.courses, ['EXE101']);
  });

  test('L04: không có nhóm → trống; lỗi → ViewError', () async {
    listen('LECTURER');
    await settle();
    expect(container.read(advisedTeamsListProvider('LECTURER')), isA<ViewEmpty<List<AdvisedTeamRow>>>());

    api.teamsError = const ApiException(status: 500, code: 'error', title: 'Lỗi');
    vm('LECTURER').retryTeams();
    await settle();
    expect(container.read(advisedTeamsListProvider('LECTURER')), isA<ViewError<List<AdvisedTeamRow>>>());
  });

  test('L04: tìm theo tên / mã lớp và lọc chưa trình bày CP', () async {
    api.instructing = [_team('a', classCode: 'SE1801'), _team('b', classCode: 'SE1802')];
    listen('LECTURER');
    await settle();
    vm('LECTURER').search('  se1802 ');
    expect(container.read(advisedTeamsListProvider('LECTURER')).valueOrNull!.single.entry.item.teamId, 'b');

    vm('LECTURER').search('');
    vm('LECTURER').filterMissingCheckpoint(2);
    await settle();
    expect(container.read(advisedTeamsListProvider('LECTURER')).valueOrNull!.single.entry.item.teamId, 'a');
    expect(container.read(advisedTeamsViewModelProvider('LECTURER')).isFiltering, isTrue);

    vm('LECTURER').filterMissingCheckpoint(1);
    expect(container.read(advisedTeamsListProvider('LECTURER')), isA<ViewEmpty<List<AdvisedTeamRow>>>());
  });

  test('L04: đổi môn bỏ bộ lọc CP và tải theo môn mới', () async {
    listen('LECTURER');
    await settle();
    vm('LECTURER').filterMissingCheckpoint(3);
    vm('LECTURER').selectCourse('EXE201');
    await settle();
    final state = container.read(advisedTeamsViewModelProvider('LECTURER'));
    expect((state.course, state.missingCheckpoint), ('EXE201', 0));
    expect(api.courses, ['EXE101', 'EXE201']);
  });

  test('L05: ghép phân công, không gian làm việc và môn', () async {
    api.instructing = [_team('a', course: 'EXE201')];
    final key = (teamId: 'a', advisorKind: 'LECTURER');
    container.listen(advisedTeamDetailViewModelProvider(key), (_, _) {});
    // Chi tiết nhóm cần JSON thật; ở đây chỉ kiểm tra trạng thái tải đầu tiên.
    expect(container.read(advisedTeamDetailViewModelProvider(key)), isA<ViewLoading<AdvisedTeamDetailData>>());
    final ws = AdvisorWorkspaceRef(id: 'w', teamId: 'a', teamName: 'A', courseCode: 'EXE201', myRole: 'LECTURER');
    final query = AdvisedTeamDetailViewModel.activityQuery('LECTURER', ws, 'EXE201');
    expect((query.includeTasks, query.includeSchedule), (true, true));
    expect(AdvisedTeamDetailViewModel.activityQuery('MENTOR', ws, 'EXE201').includeTasks, isFalse);
  });

  test('Checkpoint: lưu được → Done, lỗi API → câu từ ProblemDetails, hoàn tác', () async {
    container.listen(checkpointViewModelProvider('ws-a'), (_, _) {});
    final cp = container.read(checkpointViewModelProvider('ws-a').notifier);
    expect(await cp.mark(checkpointNo: 2, presentedOn: DateTime(2026, 11, 1), comment: 'Tốt'), isA<CheckpointDone>());
    expect(container.read(checkpointViewModelProvider('ws-a')).saving, isFalse);

    api.markError = const ApiException(status: 409, code: 'conflict', title: 'Xung đột', detail: 'CP2 đã ghi nhận');
    final failed = await cp.mark(checkpointNo: 2, presentedOn: DateTime(2026, 11, 1), comment: '');
    expect((failed as CheckpointFailed).message, 'CP2 đã ghi nhận');
    expect(container.read(checkpointViewModelProvider('ws-a')).error, 'CP2 đã ghi nhận');

    api.markError = StateError('x');
    final generic = await cp.mark(checkpointNo: 2, presentedOn: DateTime(2026, 11, 1), comment: '');
    expect((generic as CheckpointFailed).message, CheckpointViewModel.markFailedMessage);

    cp.reset();
    expect(container.read(checkpointViewModelProvider('ws-a')).error, isNull);
    expect(await cp.undo(2), isA<CheckpointDone>());
    expect(api.calls.last, 'undo:ws-a:2');
  });
}
