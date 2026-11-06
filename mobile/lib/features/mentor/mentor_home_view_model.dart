import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../lecturer/lecturer_home_api.dart';
import '../lecturer/lecturer_home_models.dart';

/// Trạng thái M01: sửa số nhóm mong muốn (đang sửa, giá trị chọn, đang lưu, lỗi).
class MentorHomeState {
  const MentorHomeState({this.editing = false, this.preferredTeams = 1, this.saving = false, this.saveError});

  final bool editing;
  final int preferredTeams;
  final bool saving;
  final String? saveError;

  MentorHomeState copyWith({bool? editing, int? preferredTeams, bool? saving, String? Function()? saveError}) =>
      MentorHomeState(
        editing: editing ?? this.editing,
        preferredTeams: preferredTeams ?? this.preferredTeams,
        saving: saving ?? this.saving,
        saveError: saveError != null ? saveError() : this.saveError,
      );
}

/// ViewModel của M01 – Trang chủ mentor (MB-49): sửa / lưu số nhóm mong muốn, làm mới các khối.
/// Hồ sơ, nhóm, không gian làm việc, câu hỏi, đề tài đọc qua `FutureProvider` chỉ-đọc của `lecturer_home_api.dart`.
class MentorHomeViewModel extends Notifier<MentorHomeState> {
  static const saveFailed = 'Không thể lưu số nhóm mong muốn.';

  @override
  MentorHomeState build() => const MentorHomeState();

  /// Mở chỗ sửa với giá trị hiện tại (chưa đặt thì lấy số tối đa).
  void startEdit(MentorProfile profile) =>
      state = state.copyWith(editing: true, preferredTeams: profile.preferredTeams ?? profile.maxTeams);

  void choose(int? count) => state = state.copyWith(preferredTeams: count ?? state.preferredTeams);

  void cancelEdit() => state = state.copyWith(editing: false);

  Future<void> save() async {
    state = state.copyWith(saving: true, saveError: () => null);
    try {
      await ref.read(lecturerHomeApiProvider).savePreferredTeams(state.preferredTeams);
      if (!ref.mounted) return;
      ref.invalidate(mentorProfileProvider);
      state = state.copyWith(editing: false);
    } catch (_) {
      if (ref.mounted) state = state.copyWith(saveError: () => saveFailed);
    } finally {
      if (ref.mounted) state = state.copyWith(saving: false);
    }
  }

  Future<void> refresh() async {
    ref.invalidate(mentorProfileProvider);
    ref.invalidate(mentorTeamsMeProvider);
    ref.invalidate(advisorWorkspacesProvider);
    ref.invalidate(advisorOpenQuestionCountProvider);
    ref.invalidate(advisorTopicRecommendationsProvider('MENTOR'));
    await Future.wait([ref.read(mentorProfileProvider.future), ref.read(mentorTeamsMeProvider.future)]);
  }

  /// Thử lại số liệu lịch họp của mọi nhóm.
  void retryActivities() {
    for (final workspace in ref.read(advisorWorkspacesProvider).value ?? const <AdvisorWorkspaceRef>[]) {
      ref.invalidate(advisorTeamActivityProvider(_activityQuery(workspace)));
    }
  }
}

final mentorHomeViewModelProvider =
    NotifierProvider.autoDispose<MentorHomeViewModel, MentorHomeState>(MentorHomeViewModel.new);

AdvisorActivityQuery _activityQuery(AdvisorWorkspaceRef workspace) =>
    (workspace: workspace, includeTasks: false, includeSchedule: false);

/// Số liệu lịch họp gộp từ hoạt động của các nhóm đang mentor (thẻ "Họp sắp tới", "Chờ chốt lịch").
class MentorActivitySummary {
  const MentorActivitySummary({
    required this.activities,
    required this.loading,
    this.error,
    required this.upcomingMeetings,
    required this.pendingSchedules,
  });

  final List<AdvisorTeamActivity> activities;
  final bool loading;
  final Object? error;
  final int upcomingMeetings;
  final int pendingSchedules;

  AdvisorTeamActivity? activityOf(String teamId) => activities.where((item) => item.workspace.teamId == teamId).firstOrNull;
}

/// Hoạt động của mọi không gian làm việc mentor đang theo, kèm số họp sắp tới / chờ chốt lịch.
final mentorActivitySummaryProvider = Provider.autoDispose<MentorActivitySummary>((ref) {
  final activities = <AdvisorTeamActivity>[];
  var loading = false;
  Object? error;
  for (final workspace in ref.watch(advisorWorkspacesProvider).value ?? const <AdvisorWorkspaceRef>[]) {
    final value = ref.watch(advisorTeamActivityProvider(_activityQuery(workspace)));
    loading |= value.isLoading;
    error ??= value.error;
    if (value.value != null) activities.add(value.value!);
  }
  final now = DateTime.now();
  final meetings = activities.expand((value) => value.meetings.items);
  return MentorActivitySummary(
    activities: activities,
    loading: loading,
    error: error,
    upcomingMeetings:
        meetings.where((meeting) => meeting.startsAt?.isAfter(now) == true && meeting.status.name == 'scheduled').length,
    pendingSchedules: meetings.where((meeting) => meeting.status.name == 'proposed').length,
  );
});

/// Không gian làm việc của nhóm [teamId] (null khi chưa có).
AdvisorWorkspaceRef? workspaceOfTeam(List<AdvisorWorkspaceRef> items, String teamId) =>
    items.where((item) => item.teamId == teamId).firstOrNull;
