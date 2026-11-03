import 'package:dio/dio.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_api.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('L01 does not count upcoming meetings as meetings today', () async {
    final dio = Dio()
      ..interceptors.add(
        InterceptorsWrapper(
          onRequest: (request, handler) {
            final data = switch (request.path) {
              '/api/advisor/overview' => {
                'needsReplyCount': 2,
                'overdueDeliverableCount': 1,
                'upcomingMeetingCount': 3,
              },
              '/api/checkpoints/me' => <dynamic>[],
              '/api/meeting-schedules/me' => {'status': 'PUBLISHED'},
              _ => throw StateError('Unexpected request: ${request.path}'),
            };
            handler.resolve(Response(requestOptions: request, data: data));
          },
        ),
      );

    final stats = await LecturerHomeApi(dio).getSemesterStats();

    expect(stats.needsReplyCount, 2);
    expect(stats.overdueSubmissionsCount, 1);
    expect(stats.meetingsTodayCount, isNull);
    expect(stats.hasPostedWeeklySchedule, isTrue);
  });
}
