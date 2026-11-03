import 'package:dio/dio.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_api.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('L01 counts only scheduled meetings occurring today', () async {
    final now = DateTime.now();
    final tomorrow = now.add(const Duration(days: 1));
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
              '/api/workspaces/me' => <dynamic>[
                {
                  'id': 'workspace-1',
                  'teamId': 'team-1',
                  'teamName': 'Nhóm 1',
                  'courseCode': 'EXE201',
                  'myRole': 'INSTRUCTOR',
                },
              ],
              '/api/workspaces/workspace-1/meetings' => {
                'items': [
                  {
                    'id': 'meeting-today',
                    'status': 'SCHEDULED',
                    'startsAt': now.toIso8601String(),
                  },
                  {
                    'id': 'meeting-tomorrow',
                    'status': 'SCHEDULED',
                    'startsAt': tomorrow.toIso8601String(),
                  },
                  {
                    'id': 'meeting-proposed',
                    'status': 'PROPOSED',
                    'startsAt': now.toIso8601String(),
                  },
                ],
              },
              _ => throw StateError('Unexpected request: ${request.path}'),
            };
            handler.resolve(Response(requestOptions: request, data: data));
          },
        ),
      );

    final stats = await LecturerHomeApi(dio).getSemesterStats();

    expect(stats.needsReplyCount, 2);
    expect(stats.overdueSubmissionsCount, 1);
    expect(stats.meetingsTodayCount, 1);
    expect(stats.hasPostedWeeklySchedule, isTrue);
  });
}
