import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/network/api_exception.dart';
import '../../core/providers.dart';
import '../workspace/workspace_models.dart';

// Model theo docs/contracts/openapi/collab.yaml, tag Meetings (C07, C08, quy tắc 25).

enum MeetingStatus { proposed, scheduled, cancelled, done }

MeetingStatus _status(Object? v) => switch (v) {
      'SCHEDULED' => MeetingStatus.scheduled,
      'CANCELLED' => MeetingStatus.cancelled,
      'DONE' => MeetingStatus.done,
      _ => MeetingStatus.proposed,
    };

DateTime? _date(Object? v) => v == null ? null : DateTime.tryParse('$v');

const maxSlots = 3;

class MeetingCard {
  const MeetingCard({
    required this.id,
    required this.title,
    required this.status,
    required this.organizer,
    this.startsAt,
    this.slotCount = 1,
    this.location,
    this.isMentorSession = false,
    this.myAnswerPending = false,
    this.isWeekly = false,
    this.weekNo,
  });

  final String id;
  final String title;
  final MeetingStatus status;
  final PersonRef organizer;

  /// Khung đã chốt.
  final DateTime? startsAt;
  final int slotCount;
  final String? location;
  final bool isMentorSession;
  final bool myAnswerPending;
  final bool isWeekly;
  final int? weekNo;

  factory MeetingCard.fromJson(Map<String, dynamic> j) => MeetingCard(
        id: j['id'] as String,
        title: j['title'] as String? ?? '',
        status: _status(j['status']),
        organizer: PersonRef.fromJson(Map<String, dynamic>.from(j['organizer'] as Map)),
        startsAt: _date(j['startsAt']),
        slotCount: j['slotCount'] as int? ?? 1,
        location: j['location'] as String?,
        isMentorSession: j['isMentorSession'] as bool? ?? false,
        myAnswerPending: j['myAnswerPending'] as bool? ?? false,
        isWeekly: j['isWeekly'] as bool? ?? false,
        weekNo: j['weekNo'] as int?,
      );
}

class MeetingSlot {
  const MeetingSlot({required this.id, required this.startsAt, required this.durationMinutes, this.yesCount = 0, this.inviteeCount = 0, this.myAnswer, this.yesPeople = const []});

  final String id;
  final DateTime startsAt;
  final int durationMinutes;
  final int yesCount;
  final int inviteeCount;

  /// `YES`, `NO` hoặc null khi chưa trả lời.
  final String? myAnswer;
  final List<String> yesPeople;

  factory MeetingSlot.fromJson(Map<String, dynamic> j) => MeetingSlot(
        id: j['id'] as String,
        startsAt: DateTime.parse(j['startsAt'] as String),
        durationMinutes: j['durationMinutes'] as int? ?? 60,
        yesCount: j['yesCount'] as int? ?? 0,
        inviteeCount: j['inviteeCount'] as int? ?? 0,
        myAnswer: j['myAnswer'] as String?,
        yesPeople: [for (final p in (j['yesPeople'] as List? ?? const [])) (p as Map)['displayName'] as String? ?? ''],
      );
}

class MeetingDetail {
  const MeetingDetail({
    required this.card,
    required this.slots,
    this.agenda,
    this.suggestedSlotId,
    this.cancelReason,
    this.minutes,
    this.minutesBy,
    this.createdTasks = const [],
  });

  final MeetingCard card;
  final List<MeetingSlot> slots;
  final String? agenda;
  final String? suggestedSlotId;
  final String? cancelReason;
  final String? minutes;
  final String? minutesBy;
  final List<String> createdTasks;

  factory MeetingDetail.fromJson(Map<String, dynamic> j) {
    final minutes = j['minutes'] == null ? null : Map<String, dynamic>.from(j['minutes'] as Map);
    return MeetingDetail(
      card: MeetingCard.fromJson(j),
      slots: [for (final s in (j['slots'] as List? ?? const [])) MeetingSlot.fromJson(Map<String, dynamic>.from(s as Map))],
      agenda: j['agenda'] as String?,
      suggestedSlotId: j['suggestedSlotId'] as String?,
      cancelReason: j['cancelReason'] as String?,
      minutes: minutes?['content'] as String?,
      minutesBy: minutes?['recordedByName'] as String?,
      createdTasks: [for (final t in (j['createdTasks'] as List? ?? const [])) (t as Map)['title'] as String? ?? ''],
    );
  }
}

class MeetingList {
  const MeetingList({required this.items, this.mentorSessionsDone = 0, this.expectedMentorSessions = 0});

  final List<MeetingCard> items;
  final int mentorSessionsDone;
  final int expectedMentorSessions;
}

class SlotInput {
  const SlotInput(this.startsAt, {this.durationMinutes = 60});

  final DateTime startsAt;
  final int durationMinutes;

  Map<String, dynamic> toJson() => {'startsAt': startsAt.toUtc().toIso8601String(), 'durationMinutes': durationMinutes};
}

/// Người được đề xuất họp: trưởng nhóm, giảng viên hướng dẫn, mentor (quy tắc 25).
bool canPropose(WorkspaceRole? role) => role == WorkspaceRole.leader || role == WorkspaceRole.instructor || role == WorkspaceRole.mentor;

/// Dòng biên bản → tên việc: bỏ gạch đầu dòng, bỏ dòng trống, cắt 200 ký tự.
List<String> minutesLines(String content) => [
      for (final line in content.split('\n'))
        if (line.replaceFirst(RegExp(r'^\s*([-*•]|\d+[.)])\s*'), '').trim() case final l when l.isNotEmpty) l.length > 200 ? l.substring(0, 200) : l,
    ];

class MeetingsApi {
  MeetingsApi(this._dio);

  final Dio _dio;

  Future<T> _call<T>(Future<T> Function() run) async {
    try {
      return await run();
    } on DioException catch (e) {
      throw e.apiError;
    }
  }

  Future<MeetingDetail> _detail(Future<Response<Map<String, dynamic>>> request) => _call(() async => MeetingDetail.fromJson((await request).data!));

  Future<MeetingList> list(String workspaceId) => _call(() async {
        final j = (await _dio.get<Map<String, dynamic>>('/api/workspaces/$workspaceId/meetings')).data!;
        return MeetingList(
          items: [for (final m in (j['items'] as List? ?? const [])) MeetingCard.fromJson(Map<String, dynamic>.from(m as Map))],
          mentorSessionsDone: j['mentorSessionsDone'] as int? ?? 0,
          expectedMentorSessions: j['expectedMentorSessions'] as int? ?? 0,
        );
      });

  Future<MeetingDetail> detail(String id) => _detail(_dio.get('/api/meetings/$id'));

  Future<MeetingDetail> propose(String workspaceId, {required String title, String? agenda, String? location, required List<SlotInput> slots}) =>
      _detail(_dio.post('/api/workspaces/$workspaceId/meetings', data: {
        'title': title,
        'agenda': agenda,
        'location': location,
        'slots': [for (final s in slots) s.toJson()],
      }));

  Future<MeetingDetail> answer(String id, String slotId, String answer) => _detail(_dio.put('/api/meetings/$id/responses', data: {
        'answers': [
          {'slotId': slotId, 'answer': answer},
        ],
      }));

  Future<MeetingDetail> addSlot(String id, SlotInput slot) => _detail(_dio.post('/api/meetings/$id/slots', data: slot.toJson()));

  Future<MeetingDetail> confirm(String id, String slotId) => _detail(_dio.post('/api/meetings/$id/confirm', data: {'slotId': slotId}));

  Future<MeetingDetail> cancel(String id, String reason) => _detail(_dio.post('/api/meetings/$id/cancel', data: {'reason': reason}));

  Future<MeetingDetail> saveMinutes(String id, String content) => _detail(_dio.put('/api/meetings/$id/minutes', data: {'content': content}));

  Future<int> minutesToTasks(String id, List<String> titles) => _call(() async {
        final response = await _dio.post<List<dynamic>>('/api/meetings/$id/minutes/tasks', data: {
          'items': [for (final t in titles) {'title': t}],
        });
        return response.data!.length;
      });
}

final meetingsApiProvider = Provider<MeetingsApi>((ref) => MeetingsApi(ref.watch(dioProvider)));

final meetingListProvider = FutureProvider.autoDispose.family<MeetingList, String>((ref, workspaceId) => ref.watch(meetingsApiProvider).list(workspaceId), retry: (_, _) => null);

final meetingDetailProvider = FutureProvider.autoDispose.family<MeetingDetail, String>((ref, id) => ref.watch(meetingsApiProvider).detail(id), retry: (_, _) => null);

/// Tôi là người tổ chức: so personId của người tổ chức với id hồ sơ của tôi theo loại người.
bool isOrganizer(WidgetRef ref, PersonRef organizer) {
  final user = ref.watch(authControllerProvider).value;
  final mine = switch (organizer.personType) {
    'LECTURER' => user?.lecturerId,
    'MENTOR' => user?.mentorId,
    _ => user?.studentId,
  };
  return mine != null && mine == organizer.personId;
}

/// Lỗi thao tác hiện cho người dùng.
String errorText(Object e) => e is ApiException ? (e.detail ?? e.title) : 'Có lỗi xảy ra. Thử lại sau.';
