import 'package:dio/dio.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_api.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test(
    'L01 uses implemented APIs and leaves unavailable totals empty',
    () async {
      final requestedPaths = <String>[];
      final dio = Dio()
        ..interceptors.add(
          InterceptorsWrapper(
            onRequest: (request, handler) {
              requestedPaths.add(request.path);
              final data = switch (request.path) {
                '/api/advisor/questions' => {
                  'items': <dynamic>[],
                  'totalCount': 2,
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

      final stats = await LecturerHomeApi(dio).getSemesterStats();

      expect(stats.needsReplyCount, 2);
      expect(stats.overdueSubmissionsCount, isNull);
      expect(stats.meetingsTodayCount, isNull);
      expect(stats.missingCheckpointsCount, 1);
      expect(stats.hasPostedWeeklySchedule, isTrue);
      expect(requestedPaths, [
        '/api/advisor/questions',
        '/api/checkpoints/me',
        '/api/meeting-schedules/me',
      ]);
    },
  );
}
