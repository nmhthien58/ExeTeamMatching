import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../workspace/workspace_api.dart';
import '../workspace/workspace_models.dart';
import 'file_services.dart';
import 'dart:async';
import '../../core/firebase/analytics_service.dart';

/// Lượt tải lên đang hiện: tiến độ hoặc lỗi (giữ file để Thử lại).
class UploadState {
  const UploadState(this.file, {this.progress = 0, this.error});

  final PickedFile file;
  final double progress;
  final String? error;

  /// Lỗi do file sai định dạng / quá cỡ thì không cho Thử lại.
  bool get canRetry => validateUpload(file.name, file.size) == null;
}

/// Trạng thái C06: đường dẫn thư mục đang mở và lượt tải lên.
class FilesState {
  const FilesState({this.trail = const [], this.upload});

  /// Thư mục đang mở từ gốc (rỗng = gốc).
  final List<Folder> trail;
  final UploadState? upload;

  String? get folderId => trail.lastOrNull?.id;

  /// Ẩn nút "Tải lên" khi đang tải (còn lỗi thì hiện để chọn file khác).
  bool get canStartUpload => upload == null || upload!.error != null;

  FilesState copyWith({List<Folder>? trail, UploadState? Function()? upload}) =>
      FilesState(trail: trail ?? this.trail, upload: upload != null ? upload() : this.upload);
}

/// Kết quả chọn file: hủy, lỗi tại chỗ, cần hỏi "Tạo phiên bản mới?", hoặc sẵn sàng tải lên.
sealed class PickOutcome {
  const PickOutcome();
}

final class PickCancelled extends PickOutcome {
  const PickCancelled();
}

final class PickInvalid extends PickOutcome {
  const PickInvalid();
}

final class PickNeedsNewVersion extends PickOutcome {
  const PickNeedsNewVersion(this.file, this.existing);

  final PickedFile file;
  final ProjectFile existing;
}

final class PickReady extends PickOutcome {
  const PickReady(this.file);

  final PickedFile file;
}

sealed class UploadOutcome {
  const UploadOutcome();
}

final class Uploaded extends UploadOutcome {
  const Uploaded({required this.newVersion, required this.version});

  final bool newVersion;
  final int version;
}

final class UploadFailed extends UploadOutcome {
  const UploadFailed();
}

/// ViewModel của C06 – File dự án (MB-49): duyệt thư mục, chọn / kiểm tra / tải file có tiến độ,
/// xóa, tạo thư mục, mở link tải. Nội dung thư mục đọc qua `folderProvider(query)`.
class FilesViewModel extends Notifier<FilesState> {
  FilesViewModel(this.workspaceId);

  final String workspaceId;

  @override
  FilesState build() => const FilesState();

  CollabApi get _api => ref.read(collabApiProvider);

  FolderQuery get query => (workspaceId: workspaceId, folderId: state.folderId);

  void openFolder(Folder folder) => state = state.copyWith(trail: [...state.trail, folder]);

  /// Về thư mục cha (nút Back hệ thống).
  void back() => state = state.copyWith(trail: [...state.trail]..removeLast());

  /// Bấm một mục trên đường dẫn: giữ các thư mục trước vị trí [index].
  void goTo(int index) => state = state.copyWith(trail: state.trail.sublist(0, index));

  void refresh() => ref.invalidate(folderProvider(query));

  void dismissUpload() => state = state.copyWith(upload: () => null);

  /// Chọn file trên máy, kiểm tra quy tắc 24 và trùng tên trong thư mục [content].
  Future<PickOutcome> pick(FolderContent content) async {
    final picked = await ref.read(pickLocalFileProvider)();
    if (picked == null || !ref.mounted) return const PickCancelled();
    final invalid = validateUpload(picked.name, picked.size);
    if (invalid != null) {
      state = state.copyWith(upload: () => UploadState(picked, error: invalid));
      return const PickInvalid();
    }
    final existing = content.files.where((f) => f.name.toLowerCase() == picked.name.toLowerCase()).firstOrNull;
    return existing != null ? PickNeedsNewVersion(picked, existing) : PickReady(picked);
  }

  Future<UploadOutcome> upload(PickedFile picked) async {
    state = state.copyWith(upload: () => UploadState(picked));
    try {
      final (file, newVersion) = await _api.upload(
        workspaceId,
        await picked.toMultipart(),
        folderId: state.folderId,
        onProgress: (p) {
          if (ref.mounted) state = state.copyWith(upload: () => UploadState(picked, progress: p));
        },
      );
      if (!ref.mounted) return const UploadFailed();
      state = state.copyWith(upload: () => null);
      ref.invalidate(folderProvider);
      return Uploaded(newVersion: newVersion, version: file.currentVersion);
    } catch (e) {
      final error = e.apiError;
      if (ref.mounted) state = state.copyWith(upload: () => UploadState(picked, error: error.detail ?? error.title));
      return const UploadFailed();
    }
  }

  /// Trả câu lỗi (dialog "Không xóa được") hoặc null khi xong.
  Future<String?> delete(ProjectFile file) => _run(() => _api.deleteFile(file.id), message: (e) => e.apiError.title);

  Future<String?> createFolder(String name) => _run(
        () => _api.createFolder(workspaceId, name, parentId: state.folderId),
        message: (e) => e.apiError.detail ?? e.apiError.title,
      );

  /// Lấy link tải rồi mở bằng trình duyệt; trả câu lỗi hoặc null.
  Future<String?> open(ProjectFile file) async {
    try {
      await ref.read(openUrlProvider)(await _api.downloadUrl(file.id));
      unawaited(ref.read(analyticsServiceProvider).logExportReport(reportType: 'workspace_file', format: _ext(file.name)));
      return null;
    } catch (e) {
      return e.apiError.title;
    }
  }

  Future<String?> _run(Future<void> Function() action, {required String Function(Object) message}) async {
    try {
      await action();
      if (ref.mounted) ref.invalidate(folderProvider);
      return null;
    } catch (e) {
      return message(e);
    }
  }
}

final filesViewModelProvider =
    NotifierProvider.autoDispose.family<FilesViewModel, FilesState, String>(FilesViewModel.new);

/// ViewModel của sheet chi tiết file (C06): tải một phiên bản, bình luận. Chi tiết đọc qua `fileDetailProvider`.
class FileDetailViewModel extends Notifier<void> {
  FileDetailViewModel(this.fileId);

  final String fileId;

  @override
  void build() {}

  CollabApi get _api => ref.read(collabApiProvider);

  void refresh() => ref.invalidate(fileDetailProvider(fileId));

  /// Mở link tải phiên bản [version]; trả câu lỗi (SnackBar) hoặc null.
  Future<String?> download(int version) async {
    try {
      await ref.read(openUrlProvider)(await _api.downloadUrl(fileId, version: version));
      return null;
    } catch (e) {
      return e.apiError.title;
    }
  }

  /// Bình luận vào phiên bản hiện tại; lỗi ném lại để ô soạn giữ nội dung.
  Future<void> comment(String body, {required int version}) async {
    await _api.commentFile(fileId, body, version: version);
    if (ref.mounted) refresh();
  }
}

final fileDetailViewModelProvider =
    NotifierProvider.autoDispose.family<FileDetailViewModel, void, String>(FileDetailViewModel.new);

/// Trạng thái sheet chọn file để nộp: thư mục đang mở, file đang lấy phiên bản, lỗi.
class FilePickerState {
  const FilePickerState({this.path = const [], this.resolving, this.error});

  /// Id thư mục từ gốc.
  final List<String> path;
  final String? resolving;
  final String? error;
}

/// ViewModel của sheet chọn file nộp bài (C05 → C06): duyệt thư mục (ẩn kho EXE101), lấy id phiên bản mới nhất.
class ProjectFilePickerViewModel extends Notifier<FilePickerState> {
  ProjectFilePickerViewModel(this.workspaceId);

  final String workspaceId;

  @override
  FilePickerState build() => const FilePickerState();

  FolderQuery get query => (workspaceId: workspaceId, folderId: state.path.lastOrNull);

  void openFolder(String folderId) => state = FilePickerState(path: [...state.path, folderId], resolving: state.resolving, error: state.error);

  void back() => state = FilePickerState(path: [...state.path]..removeLast(), resolving: state.resolving, error: state.error);

  void retry() => ref.invalidate(folderProvider);

  /// Thư mục chọn được (không có kho EXE101).
  static List<Folder> selectableFolders(FolderContent content) => content.folders.where((f) => !f.isArchive).toList();

  /// Trả id phiên bản mới nhất của file, hoặc null khi lỗi (lỗi nằm trong state).
  Future<String?> pick(String fileId) async {
    state = FilePickerState(path: state.path, resolving: fileId);
    try {
      final detail = await ref.read(collabApiProvider).file(fileId);
      final latest =
          detail.versions.firstWhere((v) => v.version == detail.file.currentVersion, orElse: () => detail.versions.first);
      return latest.id;
    } catch (e) {
      if (ref.mounted) state = FilePickerState(path: state.path, resolving: state.resolving, error: e.apiError.title);
      return null;
    } finally {
      if (ref.mounted) state = FilePickerState(path: state.path, error: state.error);
    }
  }
}

final projectFilePickerViewModelProvider = NotifierProvider.autoDispose
    .family<ProjectFilePickerViewModel, FilePickerState, String>(ProjectFilePickerViewModel.new);

/// Đuôi file cho tham số `format` của `export_report` ("pdf", "docx"…); không có đuôi thì "unknown".
String _ext(String name) {
  final dot = name.lastIndexOf('.');
  return dot < 0 || dot == name.length - 1 ? 'unknown' : name.substring(dot + 1).toLowerCase();
}
