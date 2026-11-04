import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_exception.dart';
import '../my_team/my_team_api.dart' show refreshMyTeamFrom;
import 'posts_api.dart';

/// Trạng thái form bài đăng (S11 bài tìm nhóm, S21 bài tuyển): tiêu đề đang gõ, đã bấm lưu, đang lưu, lỗi API.
class PostFormState {
  const PostFormState({required this.requiredMessage, this.title = '', this.saving = false, this.submitted = false, this.error});

  /// Câu báo khi tiêu đề trống.
  final String requiredMessage;
  final String title;
  final bool saving;
  final bool submitted;
  final ApiException? error;

  PostFormState copyWith({String? title, bool? saving, bool? submitted, ApiException? Function()? error}) => PostFormState(
    requiredMessage: requiredMessage,
    title: title ?? this.title,
    saving: saving ?? this.saving,
    submitted: submitted ?? this.submitted,
    error: error == null ? this.error : error(),
  );

  /// Lỗi chung (không gắn ô nào) hiện bằng banner.
  String? get generalError => error != null && error!.fieldErrors.isEmpty ? postErrors[error!.code] ?? error!.detail ?? error!.title : null;

  String? get titleError {
    if (error?.fieldErrors['title'] case [final first, ...]) return first;
    return submitted && title.trim().isEmpty ? requiredMessage : null;
  }

  String? fieldError(String field) => switch (error?.fieldErrors[field]) {
    [final first, ...] => first,
    _ => null,
  };
}

/// Kết quả một lần: [PostFormDone] mang câu SnackBar, View đóng màn.
sealed class PostFormOutcome {
  const PostFormOutcome();
}

final class PostFormDone extends PostFormOutcome {
  const PostFormDone(this.message);

  final String message;
}

final class PostFormNotSaved extends PostFormOutcome {
  const PostFormNotSaved();
}

/// Phần chung của ViewModel form bài đăng (MVVM MB-49): kiểm tra tiêu đề, gọi [PostsApi], đổi lỗi thành state.
/// Ô nhập (TextEditingController) và chip đang chọn nằm ở View.
abstract class PostFormViewModel extends Notifier<PostFormState> {
  String get requiredMessage;

  /// Làm mới dữ liệu liên quan sau khi lưu thành công.
  void onSaved();

  @override
  PostFormState build() => PostFormState(requiredMessage: requiredMessage);

  /// Gõ lại tiêu đề: bỏ lỗi cũ từ API, kiểm tra lại ô bắt buộc.
  void titleChanged(String title) => state = state.copyWith(title: title, error: () => null);

  Future<PostFormOutcome> submit(String title, Future<void> Function(PostsApi api) action, String done) async {
    state = state.copyWith(title: title, submitted: true);
    if (state.titleError != null) return const PostFormNotSaved();
    return run(action, done);
  }

  Future<PostFormOutcome> run(Future<void> Function(PostsApi api) action, String done) async {
    state = state.copyWith(saving: true, error: () => null);
    try {
      await action(ref.read(postsApiProvider));
      if (!ref.mounted) return const PostFormNotSaved();
      onSaved();
      return PostFormDone(done);
    } on ApiException catch (e) {
      if (ref.mounted) state = state.copyWith(saving: false, error: () => e);
      return const PostFormNotSaved();
    }
  }
}

/// ViewModel của S11 – Đăng / sửa bài "Tôi đang tìm nhóm" (MB-17).
class SeekingPostFormViewModel extends PostFormViewModel {
  @override
  String get requiredMessage => 'Nhập vai trò mong muốn.';

  @override
  void onSaved() => ref.invalidate(mySeekingPostProvider);

  Future<PostFormOutcome> save({required String title, required String description, required List<int> domainIds, required bool isEdit}) => submit(
    title,
    (api) => api.saveMySeeking(title: title.trim(), description: description.trim(), domainIds: domainIds),
    isEdit ? 'Đã lưu bài' : 'Đã đăng bài',
  );

  Future<PostFormOutcome> remove() => run((api) => api.removeMySeeking(), 'Đã gỡ bài');
}

final seekingPostFormViewModelProvider = NotifierProvider.autoDispose<SeekingPostFormViewModel, PostFormState>(SeekingPostFormViewModel.new);

/// ViewModel của S21 – Bài tuyển thành viên (MB-25), theo id nhóm.
class RecruitmentPostViewModel extends PostFormViewModel {
  RecruitmentPostViewModel(this.teamId);

  final String teamId;

  @override
  String get requiredMessage => 'Nhập tiêu đề bài tuyển.';

  @override
  void onSaved() => refreshMyTeamFrom(ref);

  Future<PostFormOutcome> save({required String title, required String description, required List<int> specializationIds, required bool isEdit}) => submit(
    title,
    (api) => api.saveRecruitment(teamId, title: title.trim(), description: description.trim(), specializationIds: specializationIds),
    isEdit ? 'Đã lưu bài tuyển' : 'Đã đăng bài tuyển',
  );

  Future<PostFormOutcome> close() => run((api) => api.closeRecruitment(teamId), 'Đã đóng bài tuyển');
}

final recruitmentPostViewModelProvider = NotifierProvider.autoDispose.family<RecruitmentPostViewModel, PostFormState, String>(RecruitmentPostViewModel.new);
