import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/providers.dart';
import '../workspace/workspace_models.dart' show PersonRef, ProjectFile;

// Model theo docs/contracts/openapi/collab.yaml (tag Messages) – C02, C03, quy tắc 22.

Map<String, dynamic> _map(Object? v) => Map<String, dynamic>.from(v as Map);

class Message {
  const Message({
    required this.id,
    required this.channelId,
    required this.author,
    required this.body,
    required this.createdAt,
    this.parentMessageId,
    this.isDeleted = false,
    this.isAdvisorQuestion = false,
    this.questionStatus,
    this.replyCount = 0,
    this.attachments = const [],
    this.editedAt,
  });

  final String id;
  final String channelId;
  final String? parentMessageId;
  final PersonRef author;
  final String body;
  final bool isDeleted;
  final bool isAdvisorQuestion;

  /// OPEN (Chờ trả lời) hoặc ANSWERED (Đã trả lời).
  final String? questionStatus;
  final int replyCount;
  final List<ProjectFile> attachments;
  final DateTime createdAt;
  final DateTime? editedAt;

  factory Message.fromJson(Map<String, dynamic> j) => Message(
        id: j['id'] as String,
        channelId: j['channelId'] as String,
        parentMessageId: j['parentMessageId'] as String?,
        author: PersonRef.fromJson(_map(j['author'])),
        body: j['body'] as String? ?? '',
        isDeleted: j['isDeleted'] as bool? ?? false,
        isAdvisorQuestion: j['isAdvisorQuestion'] as bool? ?? false,
        questionStatus: j['questionStatus'] as String?,
        replyCount: j['replyCount'] as int? ?? 0,
        attachments: [for (final f in (j['attachments'] as List? ?? const [])) ProjectFile.fromJson(_map(f))],
        createdAt: DateTime.parse(j['createdAt'] as String),
        editedAt: j['editedAt'] == null ? null : DateTime.parse(j['editedAt'] as String),
      );

  Message copyWith({int? replyCount, String? questionStatus, bool? isDeleted}) => Message(
        id: id,
        channelId: channelId,
        parentMessageId: parentMessageId,
        author: author,
        body: isDeleted == true ? '' : body,
        createdAt: createdAt,
        isDeleted: isDeleted ?? this.isDeleted,
        isAdvisorQuestion: isAdvisorQuestion,
        questionStatus: questionStatus ?? this.questionStatus,
        replyCount: replyCount ?? this.replyCount,
        attachments: isDeleted == true ? const [] : attachments,
        editedAt: editedAt,
      );
}

class Channel {
  const Channel({required this.id, required this.workspaceId, required this.kind, this.unreadCount = 0, this.lastMessage});

  final String id;
  final String workspaceId;

  /// TEAM (Nhóm) hoặc ADVISORS (Cố vấn).
  final String kind;
  final int unreadCount;
  final Message? lastMessage;

  bool get isTeam => kind == 'TEAM';

  String get label => isTeam ? 'Nhóm' : 'Cố vấn';

  factory Channel.fromJson(Map<String, dynamic> j) => Channel(
        id: j['id'] as String,
        workspaceId: j['workspaceId'] as String,
        kind: j['kind'] as String? ?? 'ADVISORS',
        unreadCount: j['unreadCount'] as int? ?? 0,
        lastMessage: j['lastMessage'] == null ? null : Message.fromJson(_map(j['lastMessage'])),
      );
}

/// Gọi module Messages của Collaboration (BE-69). Lỗi ném [ApiException].
class MessagesApi {
  MessagesApi(this._dio);

  final Dio _dio;

  Future<T> _call<T>(Future<T> Function() request) async {
    try {
      return await request();
    } on DioException catch (e) {
      throw e.apiError;
    }
  }

  Future<List<Channel>> channels(String workspaceId) => _call(() async =>
      [for (final c in (await _dio.get<List<dynamic>>('/api/workspaces/$workspaceId/channels')).data!) Channel.fromJson(_map(c))]);

  /// Tin gốc mới nhất trước; `before` = cursor của trang trước. Trả kèm cursor trang sau (null = hết).
  Future<(List<Message>, String?)> history(String channelId, {String? before}) => _call(() async {
        final data = (await _dio.get<Map<String, dynamic>>('/api/channels/$channelId/messages', queryParameters: {'before': ?before, 'limit': 30})).data!;
        return ([for (final m in data['items'] as List) Message.fromJson(_map(m))], data['nextCursor'] as String?);
      });

  Future<(Message, List<Message>)> thread(String messageId) => _call(() async {
        final data = (await _dio.get<Map<String, dynamic>>('/api/messages/$messageId/thread')).data!;
        return (Message.fromJson(_map(data['root'])), [for (final r in data['replies'] as List) Message.fromJson(_map(r))]);
      });

  /// Gửi lại cùng [clientMessageId] không tạo tin trùng (server trả tin đã có).
  Future<Message> send(
    String channelId, {
    required String clientMessageId,
    required String body,
    String? parentMessageId,
    bool isAdvisorQuestion = false,
    List<String> attachmentFileIds = const [],
    List<String> mentionPersonIds = const [],
  }) =>
      _call(() async => Message.fromJson(_map((await _dio.post<Map<String, dynamic>>('/api/channels/$channelId/messages', data: {
            'clientMessageId': clientMessageId,
            'body': body,
            'parentMessageId': ?parentMessageId,
            if (isAdvisorQuestion) 'isAdvisorQuestion': true,
            if (attachmentFileIds.isNotEmpty) 'attachmentFileIds': attachmentFileIds,
            if (mentionPersonIds.isNotEmpty) 'mentionPersonIds': mentionPersonIds,
          }))
              .data)));

  Future<void> markRead(String channelId, String lastReadMessageId) =>
      _call(() => _dio.post<Object>('/api/channels/$channelId/read', data: {'lastReadMessageId': lastReadMessageId}));

  /// Kênh có câu hỏi đang chờ trả lời (C02 "Cần trả lời" của cố vấn).
  Future<Set<String>> channelsWithOpenQuestions() => _call(() async {
        final data = (await _dio.get<Map<String, dynamic>>('/api/advisor/questions', queryParameters: {'status': 'OPEN', 'pageSize': 100})).data!;
        return {for (final q in data['items'] as List) _map(_map(q)['message'])['channelId'] as String};
      });
}

final messagesApiProvider = Provider<MessagesApi>((ref) => MessagesApi(ref.watch(dioProvider)));

final channelsProvider = FutureProvider.autoDispose.family<List<Channel>, String>((ref, workspaceId) => ref.watch(messagesApiProvider).channels(workspaceId));

final openQuestionChannelsProvider = FutureProvider.autoDispose<Set<String>>((ref) => ref.watch(messagesApiProvider).channelsWithOpenQuestions());

/// id của người dùng hiện tại (sinh viên / giảng viên / mentor) để nhận ra tin của mình.
final myPersonIdsProvider = Provider<Set<String>>((ref) {
  final user = ref.watch(authControllerProvider).value;
  return {?user?.studentId, ?user?.lecturerId, ?user?.mentorId};
});
