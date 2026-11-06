import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_exception.dart';
import '../home/home_api.dart';
import '../my_team/my_team_api.dart' show refreshMyTeamFrom;
import 'invitations_api.dart';

/// Trạng thái tab "Lời mời" của S12: lời mời đang xử lý (nút Chấp nhận quay) và câu lỗi gần nhất.
class ReceivedInvitationsState {
  const ReceivedInvitationsState({this.busyId, this.error});

  final String? busyId;
  final String? error;
}

/// Kết quả một lần của Chấp nhận: View hiện SnackBar rồi về S15.
sealed class AcceptOutcome {
  const AcceptOutcome();
}

final class AcceptJoined extends AcceptOutcome {
  const AcceptJoined({required this.applied});

  /// false: nhóm EXE201 kế thừa, chờ Bộ môn duyệt.
  final bool applied;

  String get message => applied ? 'Đã vào nhóm' : 'Đã ghi nhận, chờ Bộ môn duyệt';
}

final class AcceptFailed extends AcceptOutcome {
  const AcceptFailed();
}

/// ViewModel tab "Lời mời nhận được" của S12 (MB-18, MVVM MB-49). Danh sách lấy từ `myInvitationsProvider`;
/// hộp xác nhận nằm ở View.
class ReceivedInvitationsViewModel extends Notifier<ReceivedInvitationsState> {
  @override
  ReceivedInvitationsState build() => const ReceivedInvitationsState();

  void refresh() => ref.invalidate(myInvitationsProvider);

  Future<AcceptOutcome> accept(MyRequest invitation) async {
    final applied = await _run(invitation, () async => (await ref.read(invitationsApiProvider).accept(invitation.id)).applied);
    if (applied == null || !ref.mounted) return const AcceptFailed();
    refreshMyTeamFrom(ref);
    ref.invalidate(homeProvider);
    return AcceptJoined(applied: applied);
  }

  Future<void> decline(MyRequest invitation) => _run(invitation, () => ref.read(invitationsApiProvider).decline(invitation.id));

  /// Trả null khi lỗi (câu lỗi nằm trong state). Luôn tải lại danh sách lời mời.
  Future<T?> _run<T>(MyRequest r, Future<T> Function() call) async {
    state = ReceivedInvitationsState(busyId: r.id);
    try {
      final result = await call();
      if (ref.mounted) state = const ReceivedInvitationsState();
      return result;
    } on ApiException catch (e) {
      if (ref.mounted) state = ReceivedInvitationsState(error: e.detail ?? e.title);
      return null;
    } finally {
      if (ref.mounted) refresh();
    }
  }
}

final receivedInvitationsViewModelProvider = NotifierProvider.autoDispose<ReceivedInvitationsViewModel, ReceivedInvitationsState>(
  ReceivedInvitationsViewModel.new,
);

/// ViewModel tab "Yêu cầu đã gửi" của S12 (MB-18). State là câu lỗi hủy gần nhất.
class SentJoinRequestsViewModel extends Notifier<String?> {
  @override
  String? build() => null;

  void refresh() => ref.invalidate(myJoinRequestsProvider);

  /// Hủy yêu cầu đang chờ; trả câu lỗi hoặc null khi hủy được.
  Future<String?> cancel(MyRequest request) => cancelById(request.id);

  Future<String?> cancelById(String joinRequestId) async {
    String? error;
    try {
      await ref.read(invitationsApiProvider).cancelJoinRequest(joinRequestId);
    } on ApiException catch (e) {
      error = e.detail ?? e.title;
    }
    if (ref.mounted) {
      state = error;
      refresh();
    }
    return error;
  }
}

final sentJoinRequestsViewModelProvider = NotifierProvider.autoDispose<SentJoinRequestsViewModel, String?>(SentJoinRequestsViewModel.new);
