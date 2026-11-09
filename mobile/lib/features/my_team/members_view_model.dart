import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_exception.dart';
import 'my_team_api.dart';

/// Kết quả một lần của thao tác thành viên (S17): View hiện SnackBar, rời nhóm thành công thì về S15.
sealed class MemberOutcome {
  const MemberOutcome();
}

final class MemberChanged extends MemberOutcome {
  const MemberChanged({required this.applied, required this.leaving});

  /// false: nhóm EXE201 kế thừa, thay đổi chỉ được thêm vào yêu cầu thay đổi (chưa gửi); trưởng nhóm phải gửi thì Bộ môn mới duyệt (quy tắc 7).
  final bool applied;
  final bool leaving;

  String get message => applied
      ? 'Đã cập nhật'
      : leaving
          ? 'Đã thêm vào yêu cầu thay đổi (chưa gửi). Trưởng nhóm cần gửi Bộ môn duyệt.'
          : pendingChangeMessage;
}

/// Thông báo khi thay đổi của nhóm EXE201 kế thừa mới vào danh sách chờ, chưa gửi Bộ môn.
const pendingChangeMessage = 'Đã thêm vào yêu cầu thay đổi (chưa gửi). Vào Yêu cầu thay đổi để gửi Bộ môn duyệt.';

final class MemberFailed extends MemberOutcome {
  const MemberFailed();
}

/// ViewModel của S17 – Thành viên nhóm (MB-22, MVVM MB-49). State là câu lỗi của thao tác gần nhất (banner), null khi không lỗi.
/// Danh sách thành viên lấy từ `myTeamDetailProvider`; hộp thoại xác nhận nằm ở View.
class MembersViewModel extends Notifier<String?> {
  @override
  String? build() => null;

  MyTeamApi get _api => ref.read(myTeamApiProvider);

  Future<MemberOutcome> transferLeader(String teamId, String studentId) => _run(() => _api.transferLeader(teamId, studentId));

  Future<MemberOutcome> remove(String teamId, String studentId) => _run(() => _api.removeMember(teamId, studentId));

  Future<MemberOutcome> leave(String teamId) => _run(() => _api.leave(teamId), leaving: true);

  Future<MemberOutcome> _run(Future<MemberChange> Function() call, {bool leaving = false}) async {
    try {
      final result = await call();
      if (!ref.mounted) return const MemberFailed();
      state = null;
      refreshMyTeamFrom(ref);
      return MemberChanged(applied: result.applied, leaving: leaving);
    } on ApiException catch (e) {
      if (ref.mounted) state = e.detail ?? e.title;
      return const MemberFailed();
    }
  }
}

final membersViewModelProvider = NotifierProvider.autoDispose<MembersViewModel, String?>(MembersViewModel.new);
