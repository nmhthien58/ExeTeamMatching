import 'package:dio/dio.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_api.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  Map<String, dynamic> meeting(String id, DateTime startsAt) => {
    'id': id,
    'workspaceId': 'ws-1',
    'teamName': 'Nhóm A',
    'title': 'Họp $id',
    'status': 'SCHEDULED',
    'organizer': {
      'personType': 'LECTURER',
      'personId': 'lec-1',
      'displayName': 'Thầy Khoa',
      'role': 'INSTRUCTOR',
    },
    'isMentorSession': false,
    'startsAt': startsAt.toUtc().toIso8601String(),
    'slotCount': 1,
    'myAnswerPending': false,
    'isWeekly': false,
  };

  test('L01 reads totals from /api/advisor/overview', () async {
    final now = DateTime(2026, 11, 4, 9);
    final requestedPaths = <String>[];
    final dio = Dio()
      ..interceptors.add(
        InterceptorsWrapper(
          onRequest: (request, handler) {
            requestedPaths.add(request.path);
            final data = switch (request.path) {
              '/api/advisor/overview' => {
                'needsReplyCount': 2,
                'overdueDeliverableCount': 3,
                'upcomingMeetingCount': 7,
                'pendingScheduleCount': 1,
                'needsReply': <dynamic>[],
                'overdueDeliverables': <dynamic>[],
                'upcomingMeetings': [
                  meeting('sang', DateTime(2026, 11, 4, 10)),
                  meeting('toi', DateTime(2026, 11, 4, 23, 30)),
                  meeting('mai', DateTime(2026, 11, 5, 8)),
                ],
              },
              '/api/checkpoints/me' => <dynamic>[
                {
                  'items': [
                    {'checkpointNo': 2, 'presented': false, 'overdue': true},
                  ],
                },
              ],
              '/api/meeting-schedules/me' => {'status': 'PUBLISHED'},
              _ => throw StateError('Unexpected request: ${request.path}'),
            };
            handler.resolve(Response(requestOptions: request, data: data));
          },
        ),
      );

    final stats = await LecturerHomeApi(dio, now: () => now).getSemesterStats();

    expect(stats.needsReplyCount, 2);
    expect(stats.overdueSubmissionsCount, 3);
    expect(stats.meetingsTodayCount, 2);
    expect(stats.missingCheckpointsCount, 1);
    expect(stats.hasPostedWeeklySchedule, isTrue);
    expect(requestedPaths, [
      '/api/advisor/overview',
      '/api/checkpoints/me',
      '/api/meeting-schedules/me',
    ]);
  });

  test('mentor overview without meetings gives zero, not empty', () async {
    final dio = Dio()
      ..interceptors.add(
        InterceptorsWrapper(
          onRequest: (request, handler) {
            final data = switch (request.path) {
              '/api/advisor/overview' => {
                'needsReplyCount': 0,
                'overdueDeliverableCount': 0,
                'upcomingMeetingCount': 0,
                'pendingScheduleCount': 0,
                'needsReply': <dynamic>[],
                'overdueDeliverables': <dynamic>[],
                'upcomingMeetings': <dynamic>[],
              },
              '/api/checkpoints/me' => <dynamic>[],
              '/api/meeting-schedules/me' => null,
              _ => throw StateError('Unexpected request: ${request.path}'),
            };
            handler.resolve(Response(requestOptions: request, data: data));
          },
        ),
      );

    final stats = await LecturerHomeApi(dio).getSemesterStats();

    expect(stats.overdueSubmissionsCount, 0);
    expect(stats.meetingsTodayCount, 0);
  });
}
