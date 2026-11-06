import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../lecturer_profile/lecturer_profile_screen.dart' show lecturerProfileProvider;
import 'lecturer_home_api.dart';
import 'lecturer_home_models.dart';

/// Kết quả một lần của lệnh quan tâm / rút quan tâm ở L07; View hiện SnackBar (có "Hoàn tác").
sealed class TopicToggleOutcome {
  const TopicToggleOutcome();
}

/// [wasInterested]: trước khi bấm đã quan tâm (vừa rút) hay chưa (vừa mở).
final class TopicToggled extends TopicToggleOutcome {
  const TopicToggled({required this.wasInterested});

  final bool wasInterested;
}

final class TopicToggleFailed extends TopicToggleOutcome {
  const TopicToggleFailed();
}

/// Đang có lệnh khác cho cùng đề tài: bỏ qua lần bấm.
final class TopicToggleIgnored extends TopicToggleOutcome {
  const TopicToggleIgnored();
}

/// Lệnh ghi dùng chung của L07 và chi tiết đề tài: mở / rút quan tâm, hoàn tác, tải lại dữ liệu liên quan.
mixin _InterestCommands {
  Ref get ref;
  String get advisorKind;

  bool get isLecturer => advisorKind == 'LECTURER';

  LecturerHomeApi get api => ref.read(lecturerHomeApiProvider);

  /// Quan tâm OPEN của người xem với nhóm [teamId] (null khi không có).
  Future<String?> openInterestId(String teamId) async => (await api.getMyTopicInterests(advisorKind: advisorKind))
      .where((i) => i.teamId == teamId && i.status == 'OPEN')
      .firstOrNull
      ?.id;

  Future<void> withdrawOpened(String teamId) async {
    final id = await openInterestId(teamId);
    if (id != null) await api.withdrawInterest(id);
  }

  void invalidateInterests() {
    ref.invalidate(advisorTopicRecommendationsProvider(advisorKind));
    ref.invalidate(advisorMyInterestsProvider(advisorKind));
    if (isLecturer) ref.invalidate(lecturerPreferencesMeProvider);
  }
}

/// Trạng thái L07: lọc theo môn (null = tất cả), tab đang xem và các đề tài đang gửi lệnh.
class TopicInterestsState {
  const TopicInterestsState({this.course, this.pending = const {}});

  final String? course;
  final Set<String> pending;

  TopicInterestsState copyWith({String? Function()? course, Set<String>? pending}) =>
      TopicInterestsState(course: course != null ? course() : this.course, pending: pending ?? this.pending);
}

/// ViewModel của L07 – Đề tài (MB-49): lọc môn, quan tâm / rút có hoàn tác, làm mới.
/// Danh sách đề tài và nguyện vọng vẫn đọc qua `FutureProvider` chỉ-đọc trong `lecturer_home_api.dart`.
class TopicInterestsViewModel extends Notifier<TopicInterestsState> with _InterestCommands {
  TopicInterestsViewModel(this.advisorKind);

  @override
  final String advisorKind;

  @override
  TopicInterestsState build() => const TopicInterestsState();

  void selectCourse(String? course) => state = state.copyWith(course: () => course);

  /// Lọc theo môn đang chọn (đề tài và nguyện vọng).
  List<T> byCourse<T>(List<T> items, String Function(T) courseOf) =>
      state.course == null ? items : items.where((i) => courseOf(i) == state.course).toList();

  Future<void> refresh() async {
    ref.invalidate(advisorTopicRecommendationsProvider(advisorKind));
    ref.invalidate(advisorMyInterestsProvider(advisorKind));
    if (isLecturer) {
      ref.invalidate(lecturerPreferencesMeProvider);
      ref.invalidate(lecturerProfileProvider);
    }
    try {
      await ref.read(advisorTopicRecommendationsProvider(advisorKind).future);
    } catch (_) {
      // ErrorState hiện lỗi và nút Thử lại.
    }
  }

  Future<TopicToggleOutcome> toggle(TopicRecommendationItem item) async {
    if (state.pending.contains(item.teamId)) return const TopicToggleIgnored();
    state = state.copyWith(pending: {...state.pending, item.teamId});
    final wasInterested = item.isInterested;
    try {
      if (wasInterested) {
        var interestId = item.myInterestId;
        if (interestId == null || interestId.isEmpty) interestId = await openInterestId(item.teamId);
        if (interestId != null && interestId.isNotEmpty) await api.withdrawInterest(interestId);
      } else {
        await api.openInterest(item.teamId, advisorKind: advisorKind);
      }
      if (ref.mounted) unawaited(refresh());
      return TopicToggled(wasInterested: wasInterested);
    } catch (_) {
      return const TopicToggleFailed();
    } finally {
      if (ref.mounted) state = state.copyWith(pending: {...state.pending}..remove(item.teamId));
    }
  }

  /// Hoàn tác lệnh vừa xong; trả false khi lỗi.
  Future<bool> undo(String teamId, {required bool wasInterested}) async {
    try {
      if (wasInterested) {
        await api.openInterest(teamId, advisorKind: advisorKind);
      } else {
        await withdrawOpened(teamId);
      }
      if (ref.mounted) unawaited(refresh());
      return true;
    } catch (_) {
      return false;
    }
  }
}

final topicInterestsViewModelProvider = NotifierProvider.autoDispose
    .family<TopicInterestsViewModel, TopicInterestsState, String>(TopicInterestsViewModel.new);

typedef TopicDetailKey = ({String teamId, String advisorKind});

/// ViewModel của chi tiết đề tài (L07): trạng thái đang gửi và lệnh quan tâm / rút có hoàn tác.
class TopicDetailViewModel extends Notifier<bool> with _InterestCommands {
  TopicDetailViewModel(this.key);

  final TopicDetailKey key;

  @override
  String get advisorKind => key.advisorKind;

  /// true khi đang gửi lệnh.
  @override
  bool build() => false;

  /// Đã quan tâm khi có quan tâm OPEN hoặc gợi ý báo đã quan tâm.
  static bool isInterested(TopicInterestItem? myInterest, TopicRecommendationItem? rec) =>
      myInterest != null || (rec?.isInterested == true);

  /// Được bấm khi đợt quan tâm đang mở, nhóm chưa có cố vấn và chưa chạm giới hạn (hoặc đang quan tâm để rút).
  static bool canToggle({
    required TopicRecommendationsResponse? response,
    required bool hasAdvisor,
    required bool isInterested,
  }) =>
      response != null &&
      response.interestOpen &&
      !hasAdvisor &&
      (isInterested || response.openInterestCount < response.interestLimit);

  Future<TopicToggleOutcome> toggle(TopicInterestItem? currentInterest) async {
    if (state) return const TopicToggleIgnored();
    state = true;
    final wasInterested = currentInterest != null;
    try {
      if (wasInterested) {
        await api.withdrawInterest(currentInterest.id);
      } else {
        await api.openInterest(key.teamId, advisorKind: advisorKind);
      }
      if (ref.mounted) invalidateInterests();
      return TopicToggled(wasInterested: wasInterested);
    } catch (_) {
      return const TopicToggleFailed();
    } finally {
      if (ref.mounted) state = false;
    }
  }

  /// Hoàn tác; lỗi bỏ qua như trước.
  Future<void> undo({required bool wasInterested}) async {
    try {
      if (wasInterested) {
        await api.openInterest(key.teamId, advisorKind: advisorKind);
      } else {
        await withdrawOpened(key.teamId);
      }
      if (ref.mounted) invalidateInterests();
    } catch (_) {}
  }
}

final topicDetailViewModelProvider =
    NotifierProvider.autoDispose.family<TopicDetailViewModel, bool, TopicDetailKey>(TopicDetailViewModel.new);
