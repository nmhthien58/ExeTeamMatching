import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import 'suggestions_api.dart';
import '../../core/firebase/analytics_service.dart';
import 'dart:async';
import '../../core/firebase/remote_config_service.dart';

/// Câu báo lỗi nghiệp vụ khi gửi lời mời (S18, S19).
const inviteErrors = {
  'team-full': 'Nhóm đã đủ 6 người.',
  'already-in-team': 'Sinh viên này vừa vào một nhóm khác.',
  'pending-exists': 'Bạn đã mời sinh viên này, lời mời còn hiệu lực.',
  'round-closed': 'Đợt lập nhóm đã đóng, không gửi được lời mời.',
};

/// Trạng thái sheet "Gửi lời mời": đang gửi và câu lỗi (lời nhắn giữ trong ô nhập của View).
class InviteState {
  const InviteState({this.sending = false, this.error});

  final bool sending;
  final String? error;
}

typedef InviteKey = ({String teamId, String studentId});

/// ViewModel của sheet "Gửi lời mời" (S18, S19 – MB-49): gửi lời mời kèm lời nhắn, đổi mã lỗi thành câu.
class InviteViewModel extends Notifier<InviteState> {
  InviteViewModel(this.key);

  final InviteKey key;

  @override
  InviteState build() => const InviteState();

  /// Trả `true` khi đã gửi.
  Future<bool> send(String message) async {
    state = const InviteState(sending: true);
    try {
      await ref.read(suggestionsApiProvider).invite(key.teamId, key.studentId, message: message.trim());
      // Sheet mời dùng chung cho S18 (gợi ý) và S19 (tìm) – chưa phân biệt nguồn, ghi là gợi ý.
      unawaited(ref.read(analyticsServiceProvider).logSendInvitation(key.teamId, source: InviteSource.suggestions));
      if (ref.mounted) state = const InviteState();
      return true;
    } catch (e) {
      final error = e.apiError;
      if (ref.mounted) state = InviteState(error: inviteErrors[error.code] ?? error.detail ?? error.title);
      return false;
    }
  }
}

final inviteViewModelProvider =
    NotifierProvider.autoDispose.family<InviteViewModel, InviteState, InviteKey>(InviteViewModel.new);

/// ViewModel của S18 – Gợi ý thành viên (MB-49): ghi nhận sinh viên đã mời trong phiên (hiện "Đã mời" ngay), làm mới.
/// Gợi ý đọc qua `memberSuggestionsProvider(teamId)`.
class SuggestedMembersViewModel extends Notifier<Set<String>> {
  @override
  Set<String> build() => const {};

  void markInvited(String studentId) => state = {...state, studentId};

  void refresh(String teamId) => ref.invalidate(memberSuggestionsProvider(teamId));

  void retryTeam() => ref.invalidate(myTeamProvider);
}

final suggestedMembersViewModelProvider =
    NotifierProvider.autoDispose<SuggestedMembersViewModel, Set<String>>(SuggestedMembersViewModel.new);

/// Trạng thái S19: từ khóa (đã chờ gõ xong), lọc chuyên ngành, sinh viên đã mời trong phiên.
class InviteStudentsState {
  const InviteStudentsState({this.query = '', this.specializationId, this.invited = const {}});

  final String query;
  final int? specializationId;
  final Set<String> invited;

  ({String search, int? specializationId}) get searchQuery => (search: query, specializationId: specializationId);
}

/// ViewModel của S19 – Mời sinh viên (MB-49): tìm theo tên / MSSV, lọc chuyên ngành, ghi nhận đã mời.
/// Kết quả đọc qua `studentSearchProvider(searchQuery)`.
class InviteStudentsViewModel extends Notifier<InviteStudentsState> {
  @override
  InviteStudentsState build() => const InviteStudentsState();

  void search(String text) =>
      state = InviteStudentsState(query: text.trim(), specializationId: state.specializationId, invited: state.invited);

  /// Bấm chip đang chọn thì bỏ lọc.
  void toggleSpecialization(int id, bool selected) =>
      state = InviteStudentsState(query: state.query, specializationId: selected ? id : null, invited: state.invited);

  void markInvited(String studentId) => state =
      InviteStudentsState(query: state.query, specializationId: state.specializationId, invited: {...state.invited, studentId});

  void retry() => ref.invalidate(studentSearchProvider);
}

final inviteStudentsViewModelProvider =
    NotifierProvider.autoDispose<InviteStudentsViewModel, InviteStudentsState>(InviteStudentsViewModel.new);

/// S07 – nhóm gợi ý, cắt theo Remote Config `suggestions_max_items` (MB-51); ghi `view_suggestions` mỗi lần tải.
/// Làm mới bằng `ref.invalidate(teamsForMeProvider)` như trước.
final suggestedTeamsProvider = FutureProvider.autoDispose<List<TeamSuggestion>>((ref) async {
  final maxItems = ref.watch(remoteConfigProvider.select((c) => c.suggestionsMaxItems));
  final all = await ref.watch(teamsForMeProvider.future);
  final shown = all.take(maxItems).toList();
  unawaited(ref.read(analyticsServiceProvider).logViewSuggestions(shown.length, maxItems: maxItems));
  return shown;
});
