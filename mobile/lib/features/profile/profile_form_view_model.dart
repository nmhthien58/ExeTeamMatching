import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_exception.dart';
import 'profile_api.dart';

/// Trạng thái form hồ sơ (A05, S03): mức 5 kỹ năng, chuyên ngành, công tắc tìm nhóm, đang lưu, câu lỗi.
class ProfileFormState {
  const ProfileFormState({required this.levels, this.specializationId, required this.seeking, this.saving = false, this.error});

  final Map<String, int> levels;
  final int? specializationId;
  final bool seeking;
  final bool saving;
  final String? error;

  /// Số kỹ năng chưa đánh giá; còn thiếu thì chưa lưu được (quy tắc 16).
  int get missing => skillLabels.keys.where((k) => !levels.containsKey(k)).length;

  bool get missingSpec => specializationId == null;

  bool get canSave => missing == 0 && !missingSpec;

  ProfileFormState copyWith({
    Map<String, int>? levels,
    int? Function()? specializationId,
    bool? seeking,
    bool? saving,
    String? Function()? error,
  }) => ProfileFormState(
    levels: levels ?? this.levels,
    specializationId: specializationId == null ? this.specializationId : specializationId(),
    seeking: seeking ?? this.seeking,
    saving: saving ?? this.saving,
    error: error == null ? this.error : error(),
  );
}

/// ViewModel của form hồ sơ dùng chung A05 Hoàn thiện hồ sơ và S03 Chỉnh sửa hồ sơ (MB-08, MB-12; MVVM MB-49),
/// theo hồ sơ đang sửa. Lưu xong làm mới `myProfileProvider`; View tự điều hướng.
class ProfileFormViewModel extends Notifier<ProfileFormState> {
  ProfileFormViewModel(this.profile);

  final StudentProfile profile;

  @override
  ProfileFormState build() => ProfileFormState(
    levels: {...profile.skills},
    specializationId: profile.specializationId,
    seeking: profile.isSeekingTeam,
  );

  void setLevel(String skill, int level) => state = state.copyWith(levels: {...state.levels, skill: level});

  void setSpecialization(int? id) => state = state.copyWith(specializationId: () => id);

  void setSeeking(bool value) => state = state.copyWith(seeking: value);

  /// [withSeeking]: màn có công tắc "Tôi đang tìm nhóm" (S03 trong giai đoạn lập nhóm). Trả true khi đã lưu.
  Future<bool> save({required String description, required bool withSeeking}) async {
    state = state.copyWith(saving: true, error: () => null);
    try {
      await ref
          .read(profileApiProvider)
          .saveMe(
            skills: Map.of(state.levels),
            description: description.trim(),
            specializationId: state.specializationId,
            isSeekingTeam: withSeeking ? state.seeking : null,
          );
      if (!ref.mounted) return false;
      state = state.copyWith(saving: false);
      ref.invalidate(myProfileProvider);
      return true;
    } on ApiException catch (e) {
      if (ref.mounted) state = state.copyWith(saving: false, error: () => e.detail ?? e.title);
      return false;
    }
  }
}

final profileFormViewModelProvider = NotifierProvider.autoDispose.family<ProfileFormViewModel, ProfileFormState, StudentProfile>(ProfileFormViewModel.new);
