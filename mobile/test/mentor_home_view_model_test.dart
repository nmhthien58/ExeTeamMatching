import 'package:dio/dio.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_api.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_models.dart';
import 'package:exe_team_matching/features/mentor/mentor_home_view_model.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

class _FakeApi extends LecturerHomeApi {
  _FakeApi() : super(Dio());

  Object? error;
  final saved = <int>[];

  @override
  Future<MentorProfile> savePreferredTeams(int preferredTeams) async {
    if (error != null) throw error!;
    saved.add(preferredTeams);
    return MentorProfile(id: 'm', fullName: 'Mentor', maxTeams: 4, currentTeamCount: 1, preferredTeams: preferredTeams);
  }

  @override
  Future<List<AdvisorWorkspaceRef>> getAdvisorWorkspaces() async => const [];
}

/// MB-49 – ViewModel của M01, không dựng widget.
void main() {
  late _FakeApi api;
  late ProviderContainer container;

  setUp(() {
    api = _FakeApi();
    container = ProviderContainer(overrides: [lecturerHomeApiProvider.overrideWithValue(api)]);
    addTearDown(container.dispose);
    container.listen(mentorHomeViewModelProvider, (_, _) {});
  });

  MentorHomeViewModel vm() => container.read(mentorHomeViewModelProvider.notifier);
  MentorHomeState state() => container.read(mentorHomeViewModelProvider);

  test('mở sửa: lấy số mong muốn, chưa đặt thì lấy tối đa; hủy', () {
    vm().startEdit(const MentorProfile(id: 'm', fullName: 'M', maxTeams: 4, currentTeamCount: 0));
    expect((state().editing, state().preferredTeams), (true, 4));
    vm().startEdit(const MentorProfile(id: 'm', fullName: 'M', maxTeams: 4, currentTeamCount: 0, preferredTeams: 2));
    expect(state().preferredTeams, 2);
    vm().choose(null);
    expect(state().preferredTeams, 2);
    vm().cancelEdit();
    expect(state().editing, isFalse);
  });

  test('lưu được: gửi số đã chọn, đóng chỗ sửa', () async {
    vm()
      ..startEdit(const MentorProfile(id: 'm', fullName: 'M', maxTeams: 4, currentTeamCount: 0))
      ..choose(3);
    final future = vm().save();
    expect(state().saving, isTrue);
    await future;
    expect(api.saved, [3]);
    expect((state().editing, state().saving, state().saveError), (false, false, null));
  });

  test('lưu lỗi: giữ chỗ sửa, hiện câu lỗi', () async {
    api.error = Exception('500');
    vm().startEdit(const MentorProfile(id: 'm', fullName: 'M', maxTeams: 4, currentTeamCount: 0));
    await vm().save();
    expect((state().editing, state().saving, state().saveError), (true, false, MentorHomeViewModel.saveFailed));
  });

  test('số liệu lịch họp: chưa có không gian làm việc → 0', () async {
    container.listen(mentorActivitySummaryProvider, (_, _) {});
    await container.read(advisorWorkspacesProvider.future);
    final summary = container.read(mentorActivitySummaryProvider);
    expect((summary.loading, summary.upcomingMeetings, summary.pendingSchedules, summary.activityOf('t')), (false, 0, 0, null));
  });
}
