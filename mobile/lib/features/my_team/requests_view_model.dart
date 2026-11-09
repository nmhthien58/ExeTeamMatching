import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_exception.dart';
import 'members_view_model.dart' show pendingChangeMessage;
import 'my_team_api.dart';

/// Trạng thái tab "Yêu cầu" của S20: yêu cầu đang xử lý (nút Nhận quay) và câu lỗi gần nhất.
class JoinRequestsState {
  const JoinRequestsState({this.busyId, this.error});

  final String? busyId;
  final String? error;
}

/// Kết quả một lần của Nhận / Từ chối: [DecisionDone] mang câu SnackBar.
sealed class DecisionOutcome {
  const DecisionOutcome();
}

final class DecisionDone extends DecisionOutcome {
  const DecisionDone(this.message);

  final String message;
}

final class DecisionFailed extends DecisionOutcome {
  const DecisionFailed();
}

/// ViewModel tab "Yêu cầu" của S20 (MB-24, MVVM MB-49), theo id nhóm. Danh sách lấy từ `joinRequestsProvider`.
class JoinRequestsViewModel extends Notifier<JoinRequestsState> {
  JoinRequestsViewModel(this.teamId);

  final String teamId;

  @override
  JoinRequestsState build() => const JoinRequestsState();

  void refresh() => ref.invalidate(joinRequestsProvider(teamId));

  Future<DecisionOutcome> decide(JoinRequestItem request, {required bool accept}) async {
    if (state.busyId != null) return const DecisionFailed();
    state = JoinRequestsState(busyId: request.id);
    final api = ref.read(myTeamApiProvider);
    try {
      String message;
      if (accept) {
        final applied = (await api.acceptJoinRequest(request.id)).applied;
        message = applied ? 'Đã nhận ${request.student.fullName}' : pendingChangeMessage;
      } else {
        await api.declineJoinRequest(request.id);
        message = 'Đã từ chối';
      }
      if (!ref.mounted) return const DecisionFailed();
      state = const JoinRequestsState();
      refresh();
      refreshMyTeamFrom(ref);
      return DecisionDone(message);
    } on ApiException catch (e) {
      if (ref.mounted) state = JoinRequestsState(error: e.detail ?? e.title);
      return const DecisionFailed();
    }
  }
}

final joinRequestsViewModelProvider = NotifierProvider.autoDispose.family<JoinRequestsViewModel, JoinRequestsState, String>(JoinRequestsViewModel.new);

/// ViewModel tab "Lời mời đã gửi" của S20 (MB-24), theo id nhóm. State là câu lỗi thu hồi gần nhất.
class SentInvitationsViewModel extends Notifier<String?> {
  SentInvitationsViewModel(this.teamId);

  final String teamId;

  @override
  String? build() => null;

  void refresh() => ref.invalidate(sentInvitationsProvider(teamId));

  Future<void> cancel(SentInvitation invitation) async {
    try {
      await ref.read(myTeamApiProvider).cancelInvitation(invitation.id);
      if (ref.mounted) state = null;
    } on ApiException catch (e) {
      if (ref.mounted) state = e.detail ?? e.title;
    }
    if (ref.mounted) refresh();
  }
}

final sentInvitationsViewModelProvider = NotifierProvider.autoDispose.family<SentInvitationsViewModel, String?, String>(SentInvitationsViewModel.new);
