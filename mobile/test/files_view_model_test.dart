import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/features/files/file_services.dart';
import 'package:exe_team_matching/features/files/files_view_model.dart';
import 'package:exe_team_matching/features/workspace/workspace_models.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/workspace_fakes.dart';

PickedFile _picked(String name, int size) =>
    PickedFile(name: name, size: size, toMultipart: () async => MultipartFile.fromString('x', filename: name));

ProjectFile _file(String id, String name, {int version = 1}) => ProjectFile(
      id: id,
      name: name,
      currentVersion: version,
      sizeBytes: 10,
      updatedByName: 'Vy',
      updatedAt: DateTime.utc(2026, 11, 1),
    );

const _folder = Folder(id: 'f1', name: 'Báo cáo', isArchive: false, itemCount: 2);
const _archive = Folder(id: 'old', name: 'EXE101 (kỳ trước)', isArchive: true, itemCount: 5);

class _Api extends FakeCollabApi {
  Object? error;
  ProjectFile uploaded = _file('n', 'a.pdf');
  bool newVersion = false;

  @override
  Future<(ProjectFile, bool)> upload(String workspaceId, MultipartFile content, {String? folderId, void Function(double)? onProgress}) async {
    calls.add('upload $workspaceId $folderId');
    onProgress?.call(0.5);
    if (error != null) throw error!;
    return (uploaded, newVersion);
  }

  @override
  Future<void> deleteFile(String fileId) async {
    calls.add('delete $fileId');
    if (error != null) throw error!;
  }

  @override
  Future<void> createFolder(String workspaceId, String name, {String? parentId}) async {
    calls.add('folder $name $parentId');
    if (error != null) throw error!;
  }

  @override
  Future<String> downloadUrl(String fileId, {int? version}) async {
    if (error != null) throw error!;
    return 'https://files/$fileId?v=$version';
  }

  @override
  Future<FileDetail> file(String id) async {
    if (error != null) throw error!;
    return FileDetail(
      file: _file(id, 'a.pdf', version: 2),
      versions: [
        FileVersion(id: 'v1', version: 1, sizeBytes: 1, uploadedByName: 'Vy', createdAt: DateTime.utc(2026)),
        FileVersion(id: 'v2', version: 2, sizeBytes: 1, uploadedByName: 'Vy', createdAt: DateTime.utc(2026)),
      ],
      comments: const [],
    );
  }
}

const _error = ApiException(status: 409, code: 'conflict', title: 'Không được phép', detail: 'Trùng tên thư mục');

/// MB-49 – ViewModel của C06 (file dự án, chi tiết file, chọn file nộp bài), không dựng widget.
void main() {
  late _Api api;
  late ProviderContainer container;
  PickedFile? nextPick;
  final opened = <String>[];

  setUp(() {
    api = _Api();
    nextPick = null;
    opened.clear();
    container = ProviderContainer(overrides: [
      collabApiOverride(api),
      pickLocalFileProvider.overrideWithValue(() async => nextPick),
      openUrlProvider.overrideWithValue((url) async {
        opened.add(url);
        return true;
      }),
    ]);
    addTearDown(container.dispose);
    container.listen(filesViewModelProvider('w1'), (_, _) {});
  });

  FilesViewModel vm() => container.read(filesViewModelProvider('w1').notifier);
  FilesState state() => container.read(filesViewModelProvider('w1'));

  test('duyệt thư mục: mở, đường dẫn, quay lại', () {
    vm()
      ..openFolder(_folder)
      ..openFolder(_archive);
    expect(vm().query, (workspaceId: 'w1', folderId: 'old'));
    vm().goTo(1);
    expect(state().trail.map((f) => f.id), ['f1']);
    vm().back();
    expect(state().trail, isEmpty);
  });

  test('chọn file: hủy, sai định dạng, trùng tên, sẵn sàng', () async {
    const content = FolderContent(canUpload: true);
    expect(await vm().pick(content), isA<PickCancelled>());

    nextPick = _picked('virus.exe', 10);
    expect(await vm().pick(content), isA<PickInvalid>());
    expect(state().upload?.error, contains('Chỉ nhận'));
    expect(state().upload?.canRetry, isFalse);

    nextPick = _picked('A.pdf', 10);
    final dup = await vm().pick(FolderContent(files: [_file('x', 'a.pdf', version: 3)]));
    expect((dup as PickNeedsNewVersion).existing.currentVersion, 3);
    expect(await vm().pick(content), isA<PickReady>());
  });

  test('tải lên: tiến độ, xong bỏ dòng tải; lỗi giữ file để thử lại', () async {
    vm().openFolder(_folder);
    final progress = <double>[];
    container.listen(filesViewModelProvider('w1'), (_, next) {
      if (next.upload != null) progress.add(next.upload!.progress);
    });
    api
      ..newVersion = true
      ..uploaded = _file('n', 'a.pdf', version: 4);
    final outcome = await vm().upload(_picked('a.pdf', 10));
    expect((outcome as Uploaded).version, 4);
    expect(progress, contains(0.5));
    expect(state().upload, isNull);
    expect(api.calls, ['upload w1 f1']);

    api.error = _error;
    expect(await vm().upload(_picked('a.pdf', 10)), isA<UploadFailed>());
    expect((state().upload?.error, state().upload?.canRetry, state().canStartUpload), ('Trùng tên thư mục', true, true));
    vm().dismissUpload();
    expect(state().upload, isNull);
  });

  test('xóa, tạo thư mục, mở link: null khi xong, câu lỗi khi hỏng', () async {
    expect(await vm().delete(_file('x', 'a.pdf')), isNull);
    expect(await vm().createFolder('Slide'), isNull);
    expect(await vm().open(_file('x', 'a.pdf')), isNull);
    expect(opened.single, startsWith('https://files/x'));
    api.error = _error;
    expect(await vm().delete(_file('x', 'a.pdf')), 'Không được phép');
    expect(await vm().createFolder('Slide'), 'Trùng tên thư mục');
  });

  test('chi tiết file: tải phiên bản, lỗi trả câu', () async {
    final detail = container.read(fileDetailViewModelProvider('x').notifier);
    expect(await detail.download(2), isNull);
    expect(opened.single, 'https://files/x?v=2');
    api.error = _error;
    expect(await detail.download(1), 'Không được phép');
  });

  test('chọn file nộp bài: ẩn kho EXE101, lấy phiên bản mới nhất, lỗi vào state', () async {
    container.listen(projectFilePickerViewModelProvider('w1'), (_, _) {});
    final picker = container.read(projectFilePickerViewModelProvider('w1').notifier);
    expect(ProjectFilePickerViewModel.selectableFolders(const FolderContent(folders: [_folder, _archive])).single.id, 'f1');
    picker.openFolder('f1');
    expect(picker.query.folderId, 'f1');
    expect(await picker.pick('x'), 'v2');
    api.error = _error;
    expect(await picker.pick('x'), isNull);
    final state = container.read(projectFilePickerViewModelProvider('w1'));
    expect((state.error, state.resolving), ('Không được phép', null));
    expect(state.path, ['f1']);
  });
}
