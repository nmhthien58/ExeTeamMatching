import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_exception.dart';
import '../my_team/my_team_api.dart' show Idea;
import 'discover_api.dart';

/// Trạng thái form S09: "Chưa có ý tưởng", lỗi thiếu ý tưởng, đang gửi, lỗi gửi (banner + Thử lại).
class JoinRequestState {
  const JoinRequestState({this.noIdea = false, this.ideasMissing = false, this.sending = false, this.error});

  final bool noIdea;
  final bool ideasMissing;
  final bool sending;
  final String? error;

  JoinRequestState copyWith({bool? noIdea, bool? ideasMissing, bool? sending, String? Function()? error}) => JoinRequestState(
    noIdea: noIdea ?? this.noIdea,
    ideasMissing: ideasMissing ?? this.ideasMissing,
    sending: sending ?? this.sending,
    error: error == null ? this.error : error(),
  );
}

/// Kết quả một lần của lệnh gửi: View đóng sheet khi [JoinRequestSent].
sealed class JoinRequestOutcome {
  const JoinRequestOutcome();
}

final class JoinRequestSent extends JoinRequestOutcome {
  const JoinRequestSent();
}

/// Thiếu ý tưởng hoặc gửi lỗi: sheet giữ nguyên nội dung, lỗi nằm trong state.
final class JoinRequestNotSent extends JoinRequestOutcome {
  const JoinRequestNotSent();
}

/// ViewModel của S09 – Gửi yêu cầu tham gia (MB-16, MVVM MB-49). Ô nhập (TextEditingController) nằm ở View.
class JoinRequestViewModel extends Notifier<JoinRequestState> {
  JoinRequestViewModel(this.teamId);

  final String teamId;

  @override
  JoinRequestState build() => const JoinRequestState();

  void setNoIdea(bool value) => state = state.copyWith(noIdea: value, ideasMissing: false);

  /// Ý tưởng có tiêu đề (đã trim); mô tả rỗng thành null.
  static List<Idea> filledIdeas(List<(String, String)> raw) => [
    for (final (title, description) in raw)
      if (title.trim().isNotEmpty) Idea(title.trim(), description.trim().isEmpty ? null : description.trim()),
  ];

  /// [ideas] là (tiêu đề, mô tả) đúng như người dùng gõ. Quy tắc 17: 1–3 ý tưởng hoặc "Chưa có ý tưởng".
  Future<JoinRequestOutcome> send({required String message, required List<(String, String)> ideas}) async {
    final noIdea = state.noIdea;
    final filled = noIdea ? <Idea>[] : filledIdeas(ideas);
    if (!noIdea && filled.isEmpty) {
      state = state.copyWith(ideasMissing: true);
      return const JoinRequestNotSent();
    }
    state = state.copyWith(sending: true, ideasMissing: false, error: () => null);
    try {
      await ref.read(discoverApiProvider).requestToJoin(teamId, message: message.trim(), ideas: filled, hasNoIdea: noIdea);
      return const JoinRequestSent();
    } on ApiException catch (e) {
      if (ref.mounted) state = state.copyWith(sending: false, error: () => e.detail ?? e.title);
      return const JoinRequestNotSent();
    }
  }
}

final joinRequestViewModelProvider = NotifierProvider.autoDispose.family<JoinRequestViewModel, JoinRequestState, String>(JoinRequestViewModel.new);
