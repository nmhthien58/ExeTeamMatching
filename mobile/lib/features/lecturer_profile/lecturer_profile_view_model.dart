import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_exception.dart';
import 'lecturer_profile_screen.dart' show LecturerProfile, lecturerProfileApiProvider, lecturerProfileProvider;

/// Trạng thái L06: domain đang chọn khi sửa (null = đang xem), đang lưu, câu lỗi lưu.
class LecturerProfileEditState {
  const LecturerProfileEditState({this.editing, this.saving = false, this.error});

  final Set<int>? editing;
  final bool saving;
  final String? error;
}

/// ViewModel của L06 – Hồ sơ giảng viên (MB-35, MVVM MB-49): sửa domain chuyên môn. Hồ sơ đọc từ `lecturerProfileProvider`.
class LecturerProfileViewModel extends Notifier<LecturerProfileEditState> {
  @override
  LecturerProfileEditState build() => const LecturerProfileEditState();

  void refresh() => ref.invalidate(lecturerProfileProvider);

  void startEditing(LecturerProfile profile) => state = LecturerProfileEditState(editing: {for (final d in profile.domains) d.id});

  void toggle(int domainId) {
    final editing = {...?state.editing};
    editing.contains(domainId) ? editing.remove(domainId) : editing.add(domainId);
    state = LecturerProfileEditState(editing: editing, saving: state.saving, error: state.error);
  }

  void cancel() => state = const LecturerProfileEditState();

  /// Trả true khi đã lưu (View hiện SnackBar "Đã lưu chuyên môn").
  Future<bool> save() async {
    final editing = state.editing;
    if (editing == null) return false;
    state = LecturerProfileEditState(editing: editing, saving: true);
    try {
      await ref.read(lecturerProfileApiProvider).saveDomains(editing.toList()..sort());
      if (!ref.mounted) return false;
      ref.invalidate(lecturerProfileProvider);
      state = const LecturerProfileEditState();
      return true;
    } on ApiException catch (e) {
      if (ref.mounted) state = LecturerProfileEditState(editing: editing, error: e.fieldErrors['domainIds']?.first ?? e.detail ?? e.title);
      return false;
    }
  }
}

final lecturerProfileViewModelProvider = NotifierProvider.autoDispose<LecturerProfileViewModel, LecturerProfileEditState>(LecturerProfileViewModel.new);
