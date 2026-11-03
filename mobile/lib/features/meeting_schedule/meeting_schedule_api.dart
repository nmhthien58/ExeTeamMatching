import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/providers.dart';

// Model theo docs/contracts/openapi/collab.yaml (tag MeetingSchedules) – C09, quy tắc 25.

Map<String, dynamic> _map(Object? v) => Map<String, dynamic>.from(v as Map);

const dayNames = ['', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy', 'Chủ nhật'];

class ScheduleWindow {
  const ScheduleWindow({required this.dayOfWeek, required this.startTime, required this.endTime});

  /// 1 = thứ Hai … 7 = Chủ nhật.
  final int dayOfWeek;
  final String startTime;
  final String endTime;

  factory ScheduleWindow.fromJson(Map<String, dynamic> j) =>
      ScheduleWindow(dayOfWeek: j['dayOfWeek'] as int, startTime: j['startTime'] as String, endTime: j['endTime'] as String);

  Map<String, dynamic> toJson() => {'dayOfWeek': dayOfWeek, 'startTime': startTime, 'endTime': endTime};

  ScheduleWindow copyWith({int? dayOfWeek, String? startTime, String? endTime}) =>
      ScheduleWindow(dayOfWeek: dayOfWeek ?? this.dayOfWeek, startTime: startTime ?? this.startTime, endTime: endTime ?? this.endTime);
}

class ScheduleSlot {
  const ScheduleSlot({required this.id, required this.dayOfWeek, required this.startTime, required this.endTime, required this.state, this.workspaceId, this.teamName});

  final String id;
  final int dayOfWeek;
  final String startTime;
  final String endTime;

  /// FREE, TAKEN, MINE (giảng viên chỉ thấy FREE / TAKEN).
  final String state;
  final String? workspaceId;
  final String? teamName;

  /// "Thứ Ba 09:15".
  String get label => '${dayNames[dayOfWeek]} $startTime';

  factory ScheduleSlot.fromJson(Map<String, dynamic> j) => ScheduleSlot(
        id: j['id'] as String,
        dayOfWeek: j['dayOfWeek'] as int,
        startTime: j['startTime'] as String,
        endTime: j['endTime'] as String? ?? '',
        state: j['state'] as String? ?? 'FREE',
        workspaceId: j['workspaceId'] as String?,
        teamName: j['teamName'] as String?,
      );
}

/// Góc nhìn giảng viên.
class MeetingSchedule {
  const MeetingSchedule({
    required this.id,
    required this.status,
    required this.slotMinutes,
    required this.location,
    required this.bookingDeadline,
    required this.windows,
    required this.slots,
    required this.teamCount,
    required this.teamsWithoutSlot,
  });

  final String id;

  /// DRAFT, PUBLISHED, CLOSED.
  final String status;
  final int slotMinutes;
  final String location;
  final DateTime bookingDeadline;
  final List<ScheduleWindow> windows;
  final List<ScheduleSlot> slots;
  final int teamCount;
  final List<String> teamsWithoutSlot;

  factory MeetingSchedule.fromJson(Map<String, dynamic> j) => MeetingSchedule(
        id: j['id'] as String,
        status: j['status'] as String? ?? 'DRAFT',
        slotMinutes: j['slotMinutes'] as int? ?? 15,
        location: j['location'] as String? ?? '',
        bookingDeadline: DateTime.parse(j['bookingDeadline'] as String),
        windows: [for (final w in j['windows'] as List? ?? const []) ScheduleWindow.fromJson(_map(w))],
        slots: [for (final s in j['slots'] as List? ?? const []) ScheduleSlot.fromJson(_map(s))],
        teamCount: j['teamCount'] as int? ?? 0,
        teamsWithoutSlot: [for (final t in j['teamsWithoutSlot'] as List? ?? const []) _map(t)['teamName'] as String? ?? ''],
      );
}

/// Góc nhìn nhóm – không có tên nhóm khác.
class TeamMeetingSchedule {
  const TeamMeetingSchedule({
    required this.lecturerName,
    required this.location,
    required this.bookingDeadline,
    required this.canBook,
    required this.slots,
    this.mySlot,
  });

  final String lecturerName;
  final String location;
  final DateTime bookingDeadline;

  /// Trưởng nhóm và còn trong hạn.
  final bool canBook;
  final ScheduleSlot? mySlot;
  final List<ScheduleSlot> slots;

  factory TeamMeetingSchedule.fromJson(Map<String, dynamic> j) => TeamMeetingSchedule(
        lecturerName: j['lecturerName'] as String? ?? '',
        location: j['location'] as String? ?? '',
        bookingDeadline: DateTime.parse(j['bookingDeadline'] as String),
        canBook: j['canBook'] as bool? ?? false,
        mySlot: j['mySlot'] == null ? null : ScheduleSlot.fromJson(_map(j['mySlot'])),
        slots: [for (final s in j['slots'] as List? ?? const []) ScheduleSlot.fromJson(_map(s))],
      );
}

int _minutes(String time) {
  final parts = time.split(':');
  return int.parse(parts[0]) * 60 + int.parse(parts[1]);
}

/// Xem trước số slot trước khi lưu (server cắt slot giống vậy); khung sai giờ tính 0.
int countSlots(List<ScheduleWindow> windows, int slotMinutes) => slotMinutes <= 0
    ? 0
    : windows.fold(0, (n, w) => n + ((_minutes(w.endTime) - _minutes(w.startTime)) ~/ slotMinutes).clamp(0, 1 << 20));

/// Lỗi nghiệp vụ C09 → câu tiếng Việt.
const scheduleErrors = {
  'slot-taken': 'Slot này vừa có nhóm khác chọn. Hãy chọn slot còn trống.',
  'booking-closed': 'Đã hết hạn chọn slot. Liên hệ giảng viên nếu cần đổi.',
  'not-leader': 'Chỉ trưởng nhóm chọn được slot.',
  'slot-booked': 'Khung giờ mới bỏ mất slot đã có nhóm. Đổi nhóm sang slot khác trước.',
  'no-exe201-team': 'Bạn chưa hướng dẫn nhóm EXE201 nào.',
  'schedule-has-no-slot': 'Lịch chưa có slot. Thêm khung giờ trước khi đăng.',
};

/// Gọi lịch họp hằng tuần của Collaboration (BE-82). Lỗi ném [ApiException].
class MeetingScheduleApi {
  MeetingScheduleApi(this._dio);

  final Dio _dio;

  Future<T> _call<T>(Future<T> Function() request) async {
    try {
      return await request();
    } on DioException catch (e) {
      throw e.apiError;
    }
  }

  MeetingSchedule _schedule(Response<Object> r) => MeetingSchedule.fromJson(_map(r.data));

  /// Giảng viên – null khi chưa tạo lịch.
  Future<MeetingSchedule?> mine() => _call(() async {
        final data = (await _dio.get<Object>('/api/meeting-schedules/me')).data;
        return data is Map ? MeetingSchedule.fromJson(_map(data)) : null;
      });

  Future<MeetingSchedule> save(List<ScheduleWindow> windows, int slotMinutes, String location) => _call(() async => _schedule(await _dio.put<Object>(
        '/api/meeting-schedules/me',
        data: {'windows': [for (final w in windows) w.toJson()], 'slotMinutes': slotMinutes, 'location': location},
      )));

  Future<MeetingSchedule> publish() => _call(() async => _schedule(await _dio.post<Object>('/api/meeting-schedules/me/publish')));

  /// Giảng viên đổi slot cho một nhóm (kể cả sau hạn).
  Future<MeetingSchedule> move(String workspaceId, String slotId) =>
      _call(() async => _schedule(await _dio.put<Object>('/api/meeting-schedules/me/bookings/$workspaceId', data: {'slotId': slotId})));

  /// Dự án của nhóm hiện tại: khớp theo teamId, vì sinh viên EXE201 còn dự án EXE101 của kỳ trước.
  Future<String?> workspaceOfTeam(String teamId) => _call(() async {
        final list = (await _dio.get<List<dynamic>>('/api/workspaces/me')).data ?? const [];
        return [for (final w in list) _map(w)].where((w) => w['teamId'] == teamId).firstOrNull?['id'] as String?;
      });

  /// Nhóm – null khi giảng viên chưa đăng lịch.
  Future<TeamMeetingSchedule?> team(String workspaceId) => _call(() async {
        final data = (await _dio.get<Object>('/api/workspaces/$workspaceId/meeting-schedule')).data;
        return data is Map ? TeamMeetingSchedule.fromJson(_map(data)) : null;
      });

  /// Trưởng nhóm đặt / đổi slot tới hạn.
  Future<TeamMeetingSchedule> book(String workspaceId, String slotId) => _call(() async =>
      TeamMeetingSchedule.fromJson(_map((await _dio.put<Object>('/api/workspaces/$workspaceId/slot-booking', data: {'slotId': slotId})).data)));
}

final meetingScheduleApiProvider = Provider<MeetingScheduleApi>((ref) => MeetingScheduleApi(ref.watch(dioProvider)));

final myScheduleProvider = FutureProvider.autoDispose<MeetingSchedule?>((ref) => ref.watch(meetingScheduleApiProvider).mine());

final teamWorkspaceIdProvider = FutureProvider.autoDispose.family<String?, String>((ref, teamId) => ref.watch(meetingScheduleApiProvider).workspaceOfTeam(teamId));

final teamScheduleProvider = FutureProvider.autoDispose.family<TeamMeetingSchedule?, String>((ref, workspaceId) => ref.watch(meetingScheduleApiProvider).team(workspaceId));
