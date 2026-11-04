import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_exception.dart';
import '../../core/view_state.dart';
import 'lecturer_home_api.dart';
import 'lecturer_home_models.dart';

/// Một nhóm cố vấn đang theo, kèm vai trò của người xem với nhóm đó (hướng dẫn hay mentor).
class AdvisorEntry {
  const AdvisorEntry(this.item, this.isMentorRole);

  final AdvisedTeamItem item;
  final bool isMentorRole;
}

/// Một dòng của L04: nhóm và tiến độ Checkpoint (nếu đã có không gian làm việc).
class AdvisedTeamRow {
  const AdvisedTeamRow(this.entry, this.checkpoint);

  final AdvisorEntry entry;
  final CheckpointProgress? checkpoint;
}

/// Đổi `AsyncValue` sang [ViewState] giữ đúng cách `AsyncValue.when` chọn nhánh (làm mới không nháy skeleton).
ViewState<T> _view<T>(AsyncValue<T> value) =>
    value.when(loading: () => ViewLoading<T>(), error: (e, _) => ViewError<T>(e), data: ViewData<T>.new);

/// Danh sách nhóm của cố vấn theo vai trò: mentor đọc `mentorTeamsMeProvider`, giảng viên đọc theo môn.
AsyncValue<List<AdvisorEntry>> _entries(Ref ref, String advisorKind, String? course) => advisorKind == 'MENTOR'
    ? ref.watch(mentorTeamsMeProvider).whenData((items) => [for (final item in items) AdvisorEntry(item, true)])
    : ref.watch(lecturerTeamsMeProvider(course)).whenData((response) => [
          for (final item in response.instructing) AdvisorEntry(item, false),
          for (final item in response.mentoring) AdvisorEntry(item, true),
        ]);

class AdvisedTeamsState {
  const AdvisedTeamsState({this.course = 'EXE101', this.missingCheckpoint = 0, this.search = ''});

  final String course;

  /// 0 = tất cả; 1–4 = chỉ nhóm chưa trình bày CPn (EXE101).
  final int missingCheckpoint;
  final String search;

  bool get isFiltering => search.isNotEmpty || missingCheckpoint > 0;

  AdvisedTeamsState copyWith({String? course, int? missingCheckpoint, String? search}) => AdvisedTeamsState(
        course: course ?? this.course,
        missingCheckpoint: missingCheckpoint ?? this.missingCheckpoint,
        search: search ?? this.search,
      );
}

/// ViewModel của L04 – Nhóm tôi hướng dẫn / Nhóm đang mentor (MB-49): chọn môn, tìm, lọc Checkpoint, làm mới.
/// Danh sách đã lọc nằm ở [advisedTeamsListProvider].
class AdvisedTeamsViewModel extends Notifier<AdvisedTeamsState> {
  AdvisedTeamsViewModel(this.advisorKind);

  final String advisorKind;

  bool get _isMentor => advisorKind == 'MENTOR';

  @override
  AdvisedTeamsState build() => const AdvisedTeamsState();

  /// Đổi môn bỏ bộ lọc Checkpoint (chỉ EXE101 có).
  void selectCourse(String course) => state = state.copyWith(course: course, missingCheckpoint: 0);

  void search(String value) => state = state.copyWith(search: value.trim());

  void filterMissingCheckpoint(int? no) => state = state.copyWith(missingCheckpoint: no ?? 0);

  Future<void> refresh() async {
    ref.invalidate(advisorWorkspacesProvider);
    ref.invalidate(advisorCheckpointsProvider);
    retryTeams();
    await Future.wait([
      ref.read(advisorWorkspacesProvider.future),
      if (_isMentor) ref.read(mentorTeamsMeProvider.future) else ref.read(lecturerTeamsMeProvider(state.course).future),
    ]);
  }

  /// Thử lại phần đang lỗi theo thứ tự hiển thị: nhóm → không gian làm việc → Checkpoint.
  void retry() {
    final teams = _isMentor ? ref.read(mentorTeamsMeProvider) : ref.read(lecturerTeamsMeProvider(state.course));
    if (teams.hasError) {
      retryTeams();
    } else if (ref.read(advisorWorkspacesProvider).hasError) {
      retryWorkspaces();
    } else {
      retryCheckpoints();
    }
  }

  void retryTeams() =>
      _isMentor ? ref.invalidate(mentorTeamsMeProvider) : ref.invalidate(lecturerTeamsMeProvider(state.course));

  void retryWorkspaces() => ref.invalidate(advisorWorkspacesProvider);

  void retryCheckpoints() => ref.invalidate(advisorCheckpointsProvider);
}

final advisedTeamsViewModelProvider = NotifierProvider.autoDispose
    .family<AdvisedTeamsViewModel, AdvisedTeamsState, String>(AdvisedTeamsViewModel.new);

/// Danh sách L04 đã tìm / lọc. Thứ tự lỗi như trước: nhóm → không gian làm việc → Checkpoint (chỉ khi đang lọc CP).
final advisedTeamsListProvider = Provider.autoDispose.family<ViewState<List<AdvisedTeamRow>>, String>((ref, advisorKind) {
  final filter = ref.watch(advisedTeamsViewModelProvider(advisorKind));
  final teams = _view(_entries(ref, advisorKind, filter.course));
  final workspaces = _view(ref.watch(advisorWorkspacesProvider));
  final checkpoints = ref.watch(advisorCheckpointsProvider);
  if (teams is! ViewData<List<AdvisorEntry>>) return teams is ViewError<List<AdvisorEntry>> ? ViewError(teams.error) : const ViewLoading();
  if (workspaces is! ViewData<List<AdvisorWorkspaceRef>>) {
    return workspaces is ViewError<List<AdvisorWorkspaceRef>> ? ViewError(workspaces.error) : const ViewLoading();
  }
  if (filter.missingCheckpoint > 0 && checkpoints.hasError) return ViewError(checkpoints.error!);
  final workspaceItems = workspaces.value;
  final checkpointItems = checkpoints.value ?? const <CheckpointProgress>[];
  CheckpointProgress? checkpointOf(String teamId) {
    final workspace = workspaceItems.where((value) => value.teamId == teamId).firstOrNull;
    return checkpointItems.where((value) => value.workspaceId == workspace?.id).firstOrNull;
  }

  final query = filter.search.toLowerCase();
  final rows = <AdvisedTeamRow>[];
  for (final entry in teams.value) {
    final item = entry.item;
    final matchesSearch = query.isEmpty ||
        item.teamName.toLowerCase().contains(query) ||
        (item.classCode?.toLowerCase().contains(query) ?? false);
    if (!matchesSearch) continue;
    final progress = checkpointOf(item.teamId);
    if (filter.missingCheckpoint > 0 &&
        !(progress?.items.any((cp) => cp.checkpointNo == filter.missingCheckpoint && !cp.presented) ?? false)) {
      continue;
    }
    rows.add(AdvisedTeamRow(entry, progress));
  }
  return rows.isEmpty ? const ViewEmpty() : ViewData(rows);
});

/// Dữ liệu L05 đã ghép: chi tiết nhóm, phân công của người xem, không gian làm việc, môn.
class AdvisedTeamDetailData {
  const AdvisedTeamDetailData({required this.detail, this.assignment, this.workspace, required this.courseCode});

  final AdvisorTeamDetail detail;
  final AdvisorEntry? assignment;
  final AdvisorWorkspaceRef? workspace;
  final String courseCode;
}

typedef AdvisedTeamKey = ({String teamId, String advisorKind});

/// ViewModel của L05 – Chi tiết nhóm cố vấn (MB-49): ghép dữ liệu, tính truy vấn hoạt động, làm mới / thử lại.
class AdvisedTeamDetailViewModel extends Notifier<ViewState<AdvisedTeamDetailData>> {
  AdvisedTeamDetailViewModel(this.key);

  final AdvisedTeamKey key;

  bool get _isMentor => key.advisorKind == 'MENTOR';

  @override
  ViewState<AdvisedTeamDetailData> build() {
    final detail = _view(ref.watch(advisorTypedTeamDetailProvider(key.teamId)));
    final workspaces = _view(ref.watch(advisorWorkspacesProvider));
    final assignments = _view(_entries(ref, key.advisorKind, null));
    for (final part in <ViewState<Object>>[detail, workspaces, assignments]) {
      if (part is ViewLoading) return const ViewLoading();
      if (part case ViewError(:final error)) return ViewError(error);
    }
    final assignment =
        assignments.valueOrNull!.where((entry) => entry.item.teamId == key.teamId).firstOrNull;
    final workspace = workspaces.valueOrNull!.where((item) => item.teamId == key.teamId).firstOrNull;
    return ViewData(AdvisedTeamDetailData(
      detail: detail.valueOrNull!,
      assignment: assignment,
      workspace: workspace,
      courseCode: assignment?.item.courseCode ?? workspace?.courseCode ?? 'EXE101',
    ));
  }

  /// Thử lại phần đầu tiên bị lỗi theo đúng thứ tự hiển thị.
  void retry() {
    if (ref.read(advisorTypedTeamDetailProvider(key.teamId)).hasError) {
      ref.invalidate(advisorTypedTeamDetailProvider(key.teamId));
    } else if (ref.read(advisorWorkspacesProvider).hasError) {
      ref.invalidate(advisorWorkspacesProvider);
    } else {
      _isMentor ? ref.invalidate(mentorTeamsMeProvider) : ref.invalidate(lecturerTeamsMeProvider(null));
    }
  }

  /// Truy vấn hoạt động dự án: giảng viên xem việc; EXE201 của giảng viên xem thêm lịch họp.
  static AdvisorActivityQuery activityQuery(String advisorKind, AdvisorWorkspaceRef workspace, String courseCode) => (
        workspace: workspace,
        includeTasks: advisorKind == 'LECTURER',
        includeSchedule: advisorKind == 'LECTURER' && courseCode == 'EXE201',
      );

  /// Checkpoint chỉ hiện cho giảng viên hướng dẫn nhóm EXE101 có không gian làm việc.
  static bool showsCheckpoint(String advisorKind, AdvisedTeamDetailData data) =>
      data.workspace != null &&
      data.courseCode == 'EXE101' &&
      advisorKind == 'LECTURER' &&
      !isMentorRole(advisorKind, data);

  static bool isMentorRole(String advisorKind, AdvisedTeamDetailData data) =>
      advisorKind == 'MENTOR' || (data.assignment?.isMentorRole ?? false);

  void refresh(AdvisedTeamDetailData data) {
    ref.invalidate(advisorTypedTeamDetailProvider(key.teamId));
    ref.invalidate(advisorWorkspacesProvider);
    final workspace = data.workspace;
    if (workspace != null) {
      ref.invalidate(advisorTeamActivityProvider(activityQuery(key.advisorKind, workspace, data.courseCode)));
      ref.invalidate(advisorCheckpointProvider(workspace.id));
    }
  }
}

final advisedTeamDetailViewModelProvider = NotifierProvider.autoDispose
    .family<AdvisedTeamDetailViewModel, ViewState<AdvisedTeamDetailData>, AdvisedTeamKey>(AdvisedTeamDetailViewModel.new);

/// Trạng thái form "Đánh dấu Checkpoint": đang lưu và lỗi (banner).
class CheckpointMarkState {
  const CheckpointMarkState({this.saving = false, this.error});

  final bool saving;
  final String? error;
}

sealed class CheckpointOutcome {
  const CheckpointOutcome();
}

final class CheckpointDone extends CheckpointOutcome {
  const CheckpointDone();
}

final class CheckpointFailed extends CheckpointOutcome {
  const CheckpointFailed(this.message);

  final String message;
}

/// ViewModel đánh dấu / hoàn tác Checkpoint của một không gian làm việc (L05, MB-49).
class CheckpointViewModel extends Notifier<CheckpointMarkState> {
  CheckpointViewModel(this.workspaceId);

  final String workspaceId;

  static const markFailedMessage = 'Không thể ghi nhận Checkpoint. Hãy thử lại.';
  static const undoFailedMessage = 'Không thể hoàn tác. Hãy thử lại.';

  @override
  CheckpointMarkState build() => const CheckpointMarkState();

  LecturerHomeApi get _api => ref.read(lecturerHomeApiProvider);

  /// Mở form mới: bỏ lỗi cũ.
  void reset() => state = const CheckpointMarkState();

  Future<CheckpointOutcome> mark({required int checkpointNo, required DateTime presentedOn, required String comment}) async {
    state = const CheckpointMarkState(saving: true);
    try {
      await _api.markCheckpoint(workspaceId, checkpointNo: checkpointNo, presentedOn: presentedOn, comment: comment);
      if (!ref.mounted) return const CheckpointDone();
      _invalidate();
      state = const CheckpointMarkState();
      return const CheckpointDone();
    } catch (cause) {
      final message = cause is ApiException ? cause.detail ?? cause.title : markFailedMessage;
      if (ref.mounted) state = CheckpointMarkState(error: message);
      return CheckpointFailed(message);
    }
  }

  Future<CheckpointOutcome> undo(int checkpointNo) async {
    try {
      await _api.undoCheckpoint(workspaceId, checkpointNo);
      if (ref.mounted) _invalidate();
      return const CheckpointDone();
    } catch (_) {
      return const CheckpointFailed(undoFailedMessage);
    }
  }

  void _invalidate() {
    ref.invalidate(advisorCheckpointProvider(workspaceId));
    ref.invalidate(advisorCheckpointsProvider);
  }
}

final checkpointViewModelProvider =
    NotifierProvider.autoDispose.family<CheckpointViewModel, CheckpointMarkState, String>(CheckpointViewModel.new);
