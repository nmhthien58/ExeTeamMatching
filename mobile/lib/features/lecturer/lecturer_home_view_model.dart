import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/phase.dart';
import 'lecturer_home_api.dart';
import 'lecturer_home_models.dart';

/// Trạng thái L01: môn đang xem (EXE201 | EXE101). Dữ liệu từng khối vẫn đọc qua các `FutureProvider`
/// chỉ-đọc trong `lecturer_home_api.dart`; ViewModel giữ lựa chọn và mọi lệnh ghi.
class LecturerHomeState {
  const LecturerHomeState({this.course = 'EXE201'});

  final String course;

  LecturerHomeState copyWith({String? course}) => LecturerHomeState(course: course ?? this.course);
}

/// Kết quả một lần của lệnh mở / rút quan tâm và hoàn tác; View hiện SnackBar tương ứng.
sealed class InterestOutcome {
  const InterestOutcome();
}

final class InterestDone extends InterestOutcome {
  const InterestDone();
}

final class InterestFailed extends InterestOutcome {
  const InterestFailed(this.message);

  final String message;
}

/// Quan tâm đang mở (status OPEN) – dùng cho số đếm và tab "Đang quan tâm".
List<TopicInterestItem> openInterestsOf(List<TopicInterestItem> items) =>
    items.where((i) => i.status == 'OPEN').toList();

/// ViewModel của L01 – Trang chủ giảng viên (MB-49): chọn môn, mở / rút quan tâm có hoàn tác, làm mới.
class LecturerHomeViewModel extends Notifier<LecturerHomeState> {
  static const undoFailedMessage = 'Không thể hoàn tác. Vui lòng thử lại.';

  @override
  LecturerHomeState build() => const LecturerHomeState();

  LecturerHomeApi get _api => ref.read(lecturerHomeApiProvider);

  void selectCourse(String course) => state = state.copyWith(course: course);

  Future<InterestOutcome> openInterest(String teamId) => _run(
        () => _api.openInterest(teamId, advisorKind: 'LECTURER'),
        fallback: 'Không thể mở quan tâm',
      );

  Future<InterestOutcome> withdrawInterest(String interestId) => _run(
        () => _api.withdrawInterest(interestId),
        fallback: 'Không thể rút quan tâm',
      );

  /// Hoàn tác "mở quan tâm": tìm quan tâm OPEN của nhóm rồi rút.
  Future<InterestOutcome> undoOpen(String teamId) => _run(() async {
        final current = openInterestsOf(await _api.getMyTopicInterests()).where((i) => i.teamId == teamId).firstOrNull;
        if (current == null) throw StateError('Không tìm thấy quan tâm');
        await _api.withdrawInterest(current.id);
      }, undo: true);

  /// Hoàn tác "rút quan tâm": mở lại.
  Future<InterestOutcome> undoWithdraw(String teamId) =>
      _run(() => _api.openInterest(teamId, advisorKind: 'LECTURER'), undo: true);

  /// Kéo để làm mới: tải lại giai đoạn và các khối của giai đoạn đang hiện.
  void refresh({required bool teamFormation}) {
    ref.invalidate(phasesProvider);
    if (teamFormation) {
      _invalidateInterests();
    } else {
      ref.invalidate(lecturerTeamsMeProvider(state.course));
      ref.invalidate(lecturerSemesterStatsProvider);
    }
  }

  void _invalidateInterests() {
    ref.invalidate(lecturerMyTopicInterestsProvider);
    ref.invalidate(lecturerPreferencesMeProvider);
    ref.invalidate(lecturerTopicRecommendationsProvider(state.course));
  }

  Future<InterestOutcome> _run(Future<void> Function() action, {String? fallback, bool undo = false}) async {
    try {
      await action();
      if (!ref.mounted) return const InterestDone();
      _invalidateInterests();
      return const InterestDone();
    } catch (e) {
      if (undo) return const InterestFailed(undoFailedMessage);
      return InterestFailed(e is Exception ? e.toString().replaceAll('Exception: ', '') : fallback!);
    }
  }
}

final lecturerHomeViewModelProvider =
    NotifierProvider.autoDispose<LecturerHomeViewModel, LecturerHomeState>(LecturerHomeViewModel.new);

/// Giai đoạn của môn đang xem: theo `phasesProvider`, thiếu dữ liệu thì theo khung hiện tại.
final lecturerHomeIsTeamFormationProvider = Provider.autoDispose<bool>((ref) {
  final course = ref.watch(lecturerHomeViewModelProvider.select((s) => s.course));
  final phases = ref.watch(phasesProvider).value;
  final current = phases?.where((p) => p.courseCode.toUpperCase() == course.toUpperCase()).firstOrNull;
  return current?.isTeamFormation ?? ref.watch(shellPhaseProvider).isTeamFormation;
});
