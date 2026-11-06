import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'lecturers_api.dart';
import 'team_lecturer_screen.dart' show teamLecturerDetailsProvider;

/// Kết quả một lần của lệnh cập nhật nguyện vọng: thành công thì View hiện SnackBar có "Hoàn tác".
sealed class PreferenceUpdateOutcome {
  const PreferenceUpdateOutcome();
}

final class PreferenceUpdated extends PreferenceUpdateOutcome {
  const PreferenceUpdated();
}

final class PreferenceUpdateFailed extends PreferenceUpdateOutcome {
  const PreferenceUpdateFailed(this.error);

  final Object error;

  /// Câu lỗi: nội dung của Exception (bỏ tiền tố "Exception: "), lỗi khác thì [fallback].
  String messageOr(String fallback) => error is Exception ? error.toString().replaceAll('Exception: ', '') : fallback;
}

/// ViewModel lệnh nguyện vọng giảng viên của nhóm (S23, S24, S25 – MVVM MB-49), theo id nhóm: lưu danh sách 1–3 mới
/// (thêm, bỏ, đổi thứ tự) và hoàn tác. Dữ liệu đọc từ `lecturerPreferencesProvider` / `teamLecturerDetailsProvider`,
/// cả hai được tải lại sau mỗi lần lưu.
class LecturerPreferencesViewModel extends Notifier<void> {
  LecturerPreferencesViewModel(this.teamId);

  final String teamId;

  @override
  void build() {}

  /// Thứ tự [ids] là NV1, NV2, NV3.
  Future<PreferenceUpdateOutcome> update(List<String> ids) async {
    try {
      await ref.read(lecturersApiProvider).updatePreferences(teamId, ids);
      _reload();
      return const PreferenceUpdated();
    } catch (e) {
      return PreferenceUpdateFailed(e);
    }
  }

  /// "Hoàn tác" trên SnackBar: lưu lại danh sách cũ, lỗi thì bỏ qua.
  Future<void> undo(List<String> previousIds) async {
    try {
      await ref.read(lecturersApiProvider).updatePreferences(teamId, previousIds);
      _reload();
    } catch (_) {}
  }

  void _reload() {
    if (!ref.mounted) return;
    ref
      ..invalidate(lecturerPreferencesProvider(teamId))
      ..invalidate(teamLecturerDetailsProvider(teamId));
  }
}

/// Không autoDispose: lệnh "Hoàn tác" trên SnackBar có thể chạy sau khi màn đã đóng.
final lecturerPreferencesViewModelProvider = NotifierProvider.family<LecturerPreferencesViewModel, void, String>(LecturerPreferencesViewModel.new);

/// Từ khóa tìm giảng viên ở S24 (Enter mới tìm, nút xóa về rỗng).
class LecturerSearchViewModel extends Notifier<String> {
  @override
  String build() => '';

  void search(String query) => state = query.trim();

  void clear() => state = '';

  /// Kéo để làm mới danh sách nguyện vọng và kết quả tìm.
  void refresh(String teamId) {
    ref
      ..invalidate(lecturerPreferencesProvider(teamId))
      ..invalidate(lecturersListProvider(state));
  }
}

final lecturerSearchViewModelProvider = NotifierProvider.autoDispose<LecturerSearchViewModel, String>(LecturerSearchViewModel.new);
