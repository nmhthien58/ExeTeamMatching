import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../suggestions/suggestions_api.dart';
import 'change_requests_api.dart';
import '../../core/firebase/analytics_service.dart';
import 'dart:async';

/// Bộ lọc S26: trạng thái và đợt (null = tất cả).
class ChangeRequestFilter {
  const ChangeRequestFilter({this.status, this.round});

  final String? status;
  final String? round;
}

/// ViewModel của S26 – Yêu cầu đổi nhóm (MB-49): lọc theo trạng thái / đợt, làm mới.
/// Danh sách đọc qua `changeRequestsProvider(teamId)`.
class ChangeRequestListViewModel extends Notifier<ChangeRequestFilter> {
  ChangeRequestListViewModel(this.teamId);

  final String teamId;

  @override
  ChangeRequestFilter build() => const ChangeRequestFilter();

  void filterStatus(String? status) => state = ChangeRequestFilter(status: status, round: state.round);

  void filterRound(String? round) => state = ChangeRequestFilter(status: state.status, round: round);

  void refresh() => ref.invalidate(changeRequestsProvider(teamId));

  /// Tên các đợt có trong danh sách, sắp xếp.
  static List<String> rounds(List<ChangeRequest> items) => items.map((item) => item.roundName).toSet().toList()..sort();

  List<ChangeRequest> visible(List<ChangeRequest> items) => items
      .where((item) =>
          (state.status == null || item.status == state.status) && (state.round == null || item.roundName == state.round))
      .toList();
}

final changeRequestListViewModelProvider = NotifierProvider.autoDispose
    .family<ChangeRequestListViewModel, ChangeRequestFilter, String>(ChangeRequestListViewModel.new);

/// Trạng thái S27/S28: thay đổi đã chọn, bước, đang gửi, lỗi lý do / gửi.
class CreateChangeRequestState {
  const CreateChangeRequestState({this.selected = const {}, this.step = 0, this.busy = false, this.error});

  final Set<String> selected;
  final int step;
  final bool busy;
  final String? error;

  CreateChangeRequestState copyWith({Set<String>? selected, int? step, bool? busy, String? Function()? error}) =>
      CreateChangeRequestState(
        selected: selected ?? this.selected,
        step: step ?? this.step,
        busy: busy ?? this.busy,
        error: error != null ? error() : this.error,
      );
}

/// Tóm tắt bước 1: số thành viên và chuyên ngành sau thay đổi, có được tiếp tục không.
class ChangeSelectionSummary {
  const ChangeSelectionSummary({required this.count, required this.specializationCount, required this.canContinue});

  final int count;
  final int specializationCount;
  final bool canContinue;
}

sealed class CreateChangeRequestOutcome {
  const CreateChangeRequestOutcome();
}

/// Đã gửi; View chuyển sang S29.
final class ChangeRequestCreated extends CreateChangeRequestOutcome {
  const ChangeRequestCreated(this.requestId);

  final String requestId;
}

final class ChangeRequestNotCreated extends CreateChangeRequestOutcome {
  const ChangeRequestNotCreated();
}

/// ViewModel của S27/S28 – Tạo yêu cầu đổi nhóm (MB-49): chọn thay đổi, tính nhóm sau thay đổi, kiểm tra lý do, gửi.
/// Chữ đang nhập (tên / mô tả đề tài, lý do) nằm ở `TextEditingController` của View và truyền vào lệnh.
class CreateChangeRequestViewModel extends Notifier<CreateChangeRequestState> {
  CreateChangeRequestViewModel(this.teamId);

  final String teamId;

  static const reasonTooShort = 'Lý do cần ít nhất 10 ký tự.';
  static const submitFailed = 'Không gửi được yêu cầu. Nội dung đã nhập được giữ lại.';

  /// Lúc mở S27 – tham số `elapsed` của `submit_change_request`.
  final DateTime _openedAt = DateTime.now();

  @override
  CreateChangeRequestState build() => const CreateChangeRequestState();

  void toggle(String itemId, bool selected) => state = state.copyWith(
        selected: selected ? {...state.selected, itemId} : ({...state.selected}..remove(itemId)),
      );

  void goToStep(int step) => state = state.copyWith(step: step);

  /// Thành viên sau khi áp các thay đổi đã chọn lên nhóm hiện tại.
  static Map<String, StudentRef> projectedMembers(TeamDetail team, PendingChanges data, Set<String> selected) {
    final members = {for (final member in team.members) member.id: member};
    for (final item in data.pendingItems) {
      if (!selected.contains(item.id)) continue;
      final student = item.student;
      if (student == null) continue;
      if (item.action == 'ADD') members[student.id] = student;
      if (item.action == 'REMOVE') members.remove(student.id);
    }
    return members;
  }

  ChangeSelectionSummary summary(TeamDetail team, PendingChanges data, String projectName) {
    final projected = projectedMembers(team, data, state.selected);
    final count = projected.length;
    final name = projectName.trim();
    return ChangeSelectionSummary(
      count: count,
      specializationCount: projected.values.map((member) => member.specialization?.id).whereType<int>().toSet().length,
      canContinue: !(state.selected.isEmpty && name.isEmpty || count < 4 || count > 6 || (name.isNotEmpty && name.length < 2)),
    );
  }

  /// Số thành viên thêm / bớt đã chọn và tỷ lệ so với nhóm mốc.
  ({int memberChangeCount, num percent}) changeRatio(PendingChanges data) {
    final memberChangeCount = data.pendingItems
        .where((item) => (item.action == 'ADD' || item.action == 'REMOVE') && state.selected.contains(item.id))
        .length;
    final total = data.baselineMembers.length;
    return (memberChangeCount: memberChangeCount, percent: total == 0 ? 0 : (100 * memberChangeCount / total));
  }

  List<ChangeItem> selectedItems(PendingChanges data) =>
      data.pendingItems.where((i) => state.selected.contains(i.id)).toList();

  /// Kiểm tra lý do trước khi hỏi xác nhận; sai thì hiện lỗi.
  bool validateReason(String reason) {
    if (reason.trim().length >= 10) return true;
    state = state.copyWith(error: () => reasonTooShort);
    return false;
  }

  Future<CreateChangeRequestOutcome> submit(
    PendingChanges data, {
    required String projectName,
    required String projectDescription,
    required String reason,
  }) async {
    if (!validateReason(reason)) return const ChangeRequestNotCreated();
    state = state.copyWith(busy: true, error: () => null);
    try {
      final api = ref.read(changeRequestsApiProvider);
      final itemIds = [...state.selected];
      final name = projectName.trim();
      if (name.isNotEmpty) {
        final oldProjectIds =
            data.pendingItems.where((item) => item.action == 'PROJECT_CHANGE').map((item) => item.id).toSet();
        itemIds.removeWhere(oldProjectIds.contains);
        final projectChange = await api.setProjectChange(teamId, name: name, description: projectDescription);
        if (!itemIds.contains(projectChange.id)) itemIds.add(projectChange.id);
      }
      final request = await api.create(teamId, itemIds: itemIds, reason: reason.trim());
      unawaited(ref.read(analyticsServiceProvider).logSubmitChangeRequest(changeCount: itemIds.length, elapsed: DateTime.now().difference(_openedAt)));
      if (ref.mounted) ref.invalidate(changeRequestsProvider(teamId));
      return ChangeRequestCreated(request.id);
    } catch (_) {
      if (ref.mounted) state = state.copyWith(error: () => submitFailed);
      return const ChangeRequestNotCreated();
    } finally {
      if (ref.mounted) state = state.copyWith(busy: false);
    }
  }
}

final createChangeRequestViewModelProvider = NotifierProvider.autoDispose
    .family<CreateChangeRequestViewModel, CreateChangeRequestState, String>(CreateChangeRequestViewModel.new);
