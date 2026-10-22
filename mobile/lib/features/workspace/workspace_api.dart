import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/providers.dart';
import 'workspace_models.dart';

/// Gọi Collaboration Service qua Gateway (collab.yaml – Workspaces, Tasks, Files). Lỗi ném [ApiException].
class CollabApi {
  CollabApi(this._dio);

  final Dio _dio;

  Future<T> _call<T>(Future<T> Function() request) async {
    try {
      return await request();
    } on DioException catch (e) {
      throw e.apiError;
    }
  }

  Map<String, dynamic> _map(Object? data) => Map<String, dynamic>.from(data! as Map);
  List<Map<String, dynamic>> _list(Object? data) => [for (final e in data! as List) Map<String, dynamic>.from(e as Map)];

  // ---------- Workspace ----------

  Future<List<WorkspaceOverview>> myWorkspaces() =>
      _call(() async => _list((await _dio.get<Object>('/api/workspaces/me')).data).map(WorkspaceOverview.fromJson).toList());

  Future<WorkspaceOverview> workspace(String id) =>
      _call(() async => WorkspaceOverview.fromJson(_map((await _dio.get<Object>('/api/workspaces/$id')).data)));

  // ---------- Tasks (quy tắc 23) ----------

  Future<List<TaskCard>> tasks(TaskBoardQuery q) => _call(() async {
        final response = await _dio.get<Object>('/api/workspaces/${q.workspaceId}/tasks', queryParameters: {
          if (q.mine) 'mine': true,
          if (q.deliverablesOnly) 'kind': 'DELIVERABLE',
          if (q.overdue) 'overdue': true,
          if (q.includeArchive) 'includeArchive': true,
        });
        return _list(response.data).map(TaskCard.fromJson).toList();
      });

  Future<TaskDetail> task(String id) => _call(() async => TaskDetail.fromJson(_map((await _dio.get<Object>('/api/tasks/$id')).data)));

  Future<void> createTask(String workspaceId, TaskInput input) =>
      _call(() => _dio.post<Object>('/api/workspaces/$workspaceId/tasks', data: input.toJson()));

  Future<void> updateTask(String taskId, TaskInput input) => _call(() => _dio.patch<Object>('/api/tasks/$taskId', data: input.toJson()));

  Future<void> deleteTask(String taskId) => _call(() => _dio.delete<Object>('/api/tasks/$taskId'));

  Future<void> moveTask(String taskId, TaskStatus status) =>
      _call(() => _dio.post<Object>('/api/tasks/$taskId/move', data: {'status': status.code}));

  Future<void> commentTask(String taskId, String body) => _call(() => _dio.post<Object>('/api/tasks/$taskId/comments', data: {'body': body}));

  Future<void> submit(String taskId, String fileVersionId) =>
      _call(() => _dio.post<Object>('/api/tasks/$taskId/submissions', data: {'fileVersionId': fileVersionId}));

  Future<void> review(String submissionId, {required bool accepted, String? comment}) => _call(() => _dio.post<Object>(
        '/api/submissions/$submissionId/review',
        data: {'status': accepted ? 'ACCEPTED' : 'NEEDS_REVISION', if (comment != null && comment.isNotEmpty) 'comment': comment},
      ));

  // ---------- Files (quy tắc 24) ----------

  Future<FolderContent> folder(FolderQuery q) => _call(() async => FolderContent.fromJson(_map((await _dio.get<Object>(
        '/api/workspaces/${q.workspaceId}/files',
        queryParameters: {if (q.folderId != null) 'folderId': q.folderId},
      ))
          .data)));

  Future<FileDetail> file(String id) => _call(() async => FileDetail.fromJson(_map((await _dio.get<Object>('/api/files/$id')).data)));

  /// Tải lên kèm tiến độ 0..1. Trả (file mới, có phải phiên bản mới).
  Future<(ProjectFile, bool)> upload(String workspaceId, MultipartFile content, {String? folderId, void Function(double)? onProgress}) =>
      _call(() async {
        final form = FormData.fromMap({'file': content, 'folderId': ?folderId});
        final response = await _dio.post<Object>(
          '/api/workspaces/$workspaceId/files',
          data: form,
          onSendProgress: (sent, total) => total > 0 ? onProgress?.call(sent / total) : null,
        );
        final data = _map(response.data);
        return (ProjectFile.fromJson(Map<String, dynamic>.from(data['file'] as Map)), data['createdNewVersion'] as bool? ?? false);
      });

  Future<void> createFolder(String workspaceId, String name, {String? parentId}) =>
      _call(() => _dio.post<Object>('/api/workspaces/$workspaceId/folders', data: {'name': name, 'parentId': ?parentId}));

  Future<void> deleteFile(String fileId) => _call(() => _dio.delete<Object>('/api/files/$fileId'));

  Future<void> commentFile(String fileId, String body, {int? version}) =>
      _call(() => _dio.post<Object>('/api/files/$fileId/comments', data: {'body': body, 'version': ?version}));

  /// URL ký sẵn (hết hạn sau 10 phút) – mở bằng trình duyệt / ứng dụng xem file.
  Future<String> downloadUrl(String fileId, {int? version}) => _call(() async =>
      _map((await _dio.get<Object>('/api/files/$fileId/download-url', queryParameters: {'version': ?version})).data)['url'] as String);
}

/// Tham số bảng việc (bản ghi Dart có so sánh theo giá trị nên dùng được làm khóa của provider family).
typedef TaskBoardQuery = ({String workspaceId, bool mine, bool deliverablesOnly, bool overdue, bool includeArchive});

typedef FolderQuery = ({String workspaceId, String? folderId});

class TaskInput {
  const TaskInput({required this.title, this.description, this.deliverable = false, this.assigneeStudentIds = const [], this.dueAt});

  final String title;
  final String? description;
  final bool deliverable;
  final List<String> assigneeStudentIds;
  final DateTime? dueAt;

  Map<String, dynamic> toJson() => {
        'title': title,
        'description': description,
        'kind': deliverable ? 'DELIVERABLE' : 'TASK',
        'assigneeStudentIds': assigneeStudentIds,
        'dueAt': dueAt?.toUtc().toIso8601String(),
      };
}

final collabApiProvider = Provider<CollabApi>((ref) => CollabApi(ref.watch(dioProvider)));

final myWorkspacesProvider = FutureProvider.autoDispose<List<WorkspaceOverview>>((ref) => ref.watch(collabApiProvider).myWorkspaces());

final workspaceProvider = FutureProvider.autoDispose.family<WorkspaceOverview, String>((ref, id) => ref.watch(collabApiProvider).workspace(id));

/// Workspace đang xem: cố vấn mở theo id (từ L04, M01); sinh viên chỉ có một nên lấy workspace đầu tiên của mình.
/// `null` = sinh viên chưa có nhóm.
final currentWorkspaceProvider = FutureProvider.autoDispose.family<WorkspaceOverview?, String?>((ref, id) async {
  if (id != null) return ref.watch(workspaceProvider(id).future);
  final mine = await ref.watch(myWorkspacesProvider.future);
  return mine.isEmpty ? null : ref.watch(workspaceProvider(mine.first.id).future);
});

final taskBoardProvider = FutureProvider.autoDispose.family<List<TaskCard>, TaskBoardQuery>((ref, q) => ref.watch(collabApiProvider).tasks(q));

final taskDetailProvider = FutureProvider.autoDispose.family<TaskDetail, String>((ref, id) => ref.watch(collabApiProvider).task(id));

final folderProvider = FutureProvider.autoDispose.family<FolderContent, FolderQuery>((ref, q) => ref.watch(collabApiProvider).folder(q));

final fileDetailProvider = FutureProvider.autoDispose.family<FileDetail, String>((ref, id) => ref.watch(collabApiProvider).file(id));
