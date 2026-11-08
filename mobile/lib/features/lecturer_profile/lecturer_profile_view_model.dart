import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_exception.dart';
import 'lecturer_profile_screen.dart' show LecturerProfile, lecturerProfileApiProvider, lecturerProfileProvider;

/// Trạng thái L06: domain đang chọn khi sửa (null = đang xem), số nhóm mong muốn, đang lưu, câu lỗi lưu.
class LecturerProfileEditState {
  const LecturerProfileEditState({
    this.editing,
    this.desiredTeamsExe101,
    this.desiredTeamsExe201,
    this.saving = false,
    this.error,
  });

  final Set<int>? editing;
  final int? desiredTeamsExe101;
  final int? desiredTeamsExe201;
  final bool saving;
  final String? error;
}

/// ViewModel của L06 – Hồ sơ giảng viên (MB-35, MB-66, MVVM MB-49): sửa domain chuyên môn và số nhóm mong muốn. Hồ sơ đọc từ `lecturerProfileProvider`.
class LecturerProfileViewModel extends Notifier<LecturerProfileEditState> {
  @override
  LecturerProfileEditState build() => const LecturerProfileEditState();

  void refresh() => ref.invalidate(lecturerProfileProvider);

  void startEditing(LecturerProfile profile) => state = LecturerProfileEditState(
        editing: {for (final d in profile.domains) d.id},
        desiredTeamsExe101: profile.desiredTeamsExe101,
        desiredTeamsExe201: profile.desiredTeamsExe201,
      );

  void setDesiredTeamsExe101(int? count) {
    state = LecturerProfileEditState(
      editing: state.editing,
      desiredTeamsExe101: count,
      desiredTeamsExe201: state.desiredTeamsExe201,
      saving: state.saving,
      error: state.error,
    );
  }

  void setDesiredTeamsExe201(int? count) {
    state = LecturerProfileEditState(
      editing: state.editing,
      desiredTeamsExe101: state.desiredTeamsExe101,
      desiredTeamsExe201: count,
      saving: state.saving,
      error: state.error,
    );
  }

  void toggle(int domainId) {
    final editing = {...?state.editing};
    editing.contains(domainId) ? editing.remove(domainId) : editing.add(domainId);
    state = LecturerProfileEditState(
      editing: editing,
      desiredTeamsExe101: state.desiredTeamsExe101,
      desiredTeamsExe201: state.desiredTeamsExe201,
      saving: state.saving,
      error: state.error,
    );
  }

  void cancel() => state = const LecturerProfileEditState();

  /// Trả true khi đã lưu (View hiện SnackBar "Đã lưu chuyên môn").
  Future<bool> save([LecturerProfile? profile]) async {
    final editing = state.editing;
    if (editing == null) return false;

    // Hồ sơ chỉ hoàn thiện khi khai báo đủ số nhóm mong muốn của cả hai môn (BE-93): khảo sát lần đầu bắt buộc nhập.
    if (profile != null &&
        !profile.isProfileCompleted &&
        (state.desiredTeamsExe101 == null || state.desiredTeamsExe201 == null)) {
      state = LecturerProfileEditState(
        editing: editing,
        desiredTeamsExe101: state.desiredTeamsExe101,
        desiredTeamsExe201: state.desiredTeamsExe201,
        error: 'Nhập số nhóm mong muốn cho cả EXE101 và EXE201',
      );
      return false;
    }

    final max101 = profile?.maxTeamsExe101 ?? 4;
    final max201 = profile?.maxTeamsExe201 ?? 4;

    if (state.desiredTeamsExe101 != null && (state.desiredTeamsExe101! < 0 || state.desiredTeamsExe101! > max101)) {
      state = LecturerProfileEditState(
        editing: editing,
        desiredTeamsExe101: state.desiredTeamsExe101,
        desiredTeamsExe201: state.desiredTeamsExe201,
        error: 'Số nhóm EXE101 phải từ 0 đến $max101',
      );
      return false;
    }

    if (state.desiredTeamsExe201 != null && (state.desiredTeamsExe201! < 0 || state.desiredTeamsExe201! > max201)) {
      state = LecturerProfileEditState(
        editing: editing,
        desiredTeamsExe101: state.desiredTeamsExe101,
        desiredTeamsExe201: state.desiredTeamsExe201,
        error: 'Số nhóm EXE201 phải từ 0 đến $max201',
      );
      return false;
    }

    state = LecturerProfileEditState(
      editing: editing,
      desiredTeamsExe101: state.desiredTeamsExe101,
      desiredTeamsExe201: state.desiredTeamsExe201,
      saving: true,
    );
    try {
      if (state.desiredTeamsExe101 != null || state.desiredTeamsExe201 != null) {
        await ref.read(lecturerProfileApiProvider).saveProfile(
              domainIds: editing.toList()..sort(),
              desiredTeamsExe101: state.desiredTeamsExe101,
              desiredTeamsExe201: state.desiredTeamsExe201,
            );
      } else {
        await ref.read(lecturerProfileApiProvider).saveDomains(editing.toList()..sort());
      }
      if (!ref.mounted) return false;
      ref.invalidate(lecturerProfileProvider);
      state = const LecturerProfileEditState();
      return true;
    } on ApiException catch (e) {
      if (ref.mounted) {
        state = LecturerProfileEditState(
          editing: editing,
          desiredTeamsExe101: state.desiredTeamsExe101,
          desiredTeamsExe201: state.desiredTeamsExe201,
          error: e.fieldErrors['domainIds']?.first ??
              e.fieldErrors['desiredTeamsExe101']?.first ??
              e.fieldErrors['desiredTeamsExe201']?.first ??
              e.detail ??
              e.title,
        );
      }
      return false;
    }
  }
}

final lecturerProfileViewModelProvider = NotifierProvider.autoDispose<LecturerProfileViewModel, LecturerProfileEditState>(LecturerProfileViewModel.new);
