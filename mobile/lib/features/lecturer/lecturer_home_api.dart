import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/providers.dart';
import '../meeting_schedule/meeting_schedule_api.dart';
import '../meetings/meetings_api.dart';
import '../suggestions/suggestions_api.dart' show TeamDetail;
import '../workspace/workspace_models.dart';
import 'lecturer_home_models.dart';

class LecturerHomeApi {
  LecturerHomeApi(this._dio);

  final Dio _dio;

  Future<T> _call<T>(Future<T> Function() fn) async {
    try {
      return await fn();
    } on DioException catch (e) {
      throw e.apiError;
    }
  }

  Future<List<LecturerPreferenceMe>> getMyPreferences() => _call(() async {
    final response = await _dio.get<List<dynamic>>(
      '/api/lecturer-preferences/me',
    );
    return [
      for (final item in response.data ?? const [])
        LecturerPreferenceMe.fromJson(Map<String, dynamic>.from(item as Map)),
    ];
  });

  Future<List<TopicInterestItem>> getMyTopicInterests({
    String advisorKind = 'LECTURER',
  }) => _call(() async {
    final response = await _dio.get<List<dynamic>>(
      '/api/topic-interests/me',
      queryParameters: {'advisorKind': advisorKind},
    );
    return [
      for (final item in response.data ?? const [])
        TopicInterestItem.fromJson(Map<String, dynamic>.from(item as Map)),
    ];
  });

  Future<TopicRecommendationsResponse> getRecommendations({
    String advisorKind = 'LECTURER',
    String? courseCode,
    int page = 1,
    int pageSize = 20,
  }) => _call(() async {
    final response = await _dio.get<Map<String, dynamic>>(
      '/api/topic-recommendations/me',
      queryParameters: {
        'advisorKind': advisorKind,
        if (courseCode != null && courseCode.isNotEmpty)
          'courseCode': courseCode,
        'page': page,
        'pageSize': pageSize,
      },
    );
    return TopicRecommendationsResponse.fromJson(response.data!);
  });

  Future<void> openInterest(String teamId, {String advisorKind = 'LECTURER'}) =>
      _call(() async {
        await _dio.post<dynamic>(
          '/api/teams/$teamId/topic-interests',
          data: {'advisorKind': advisorKind},
        );
      });

  Future<void> withdrawInterest(String interestId) => _call(() async {
    await _dio.delete<dynamic>('/api/topic-interests/$interestId');
  });

  Future<Map<String, dynamic>> getTeamDetail(String teamId) => _call(() async {
    final response = await _dio.get<Map<String, dynamic>>('/api/teams/$teamId');
    return response.data ?? <String, dynamic>{};
  });

  Future<LecturerTeamsMeResponse> getLecturerTeamsMe({String? courseCode}) =>
      _call(() async {
        final response = await _dio.get<Map<String, dynamic>>(
          '/api/lecturer-teams/me',
          queryParameters: {
            if (courseCode != null && courseCode.isNotEmpty)
              'courseCode': courseCode,
          },
        );
        return LecturerTeamsMeResponse.fromJson(response.data!);
      });

  Future<List<AdvisedTeamItem>> getMentorTeamsMe() => _call(() async {
    final response = await _dio.get<List<dynamic>>('/api/mentor-teams/me');
    return [
      for (final item in response.data ?? const [])
        AdvisedTeamItem.fromJson(Map<String, dynamic>.from(item as Map)),
    ];
  });

  Future<List<AdvisorWorkspaceRef>> getAdvisorWorkspaces() => _call(() async {
    final response = await _dio.get<List<dynamic>>('/api/workspaces/me');
    return [
      for (final item in response.data ?? const [])
        AdvisorWorkspaceRef.fromJson(Map<String, dynamic>.from(item as Map)),
    ];
  });

  Future<AdvisorTeamDetail> getAdvisorTeamDetail(String teamId) =>
      _call(() async {
        final response = await _dio.get<Map<String, dynamic>>(
          '/api/teams/$teamId',
        );
        final data = response.data ?? <String, dynamic>{};
        return AdvisorTeamDetail(
          team: TeamDetail.fromJson(data),
          ideaDescription: data['ideaDescription'] as String?,
        );
      });

  Future<List<CheckpointProgress>> getCheckpoints({int? missing}) =>
      _call(() async {
        final response = await _dio.get<List<dynamic>>(
          '/api/checkpoints/me',
          queryParameters: {'missing': ?missing},
        );
        return [
          for (final item in response.data ?? const [])
            CheckpointProgress.fromJson(Map<String, dynamic>.from(item as Map)),
        ];
      });

  Future<CheckpointProgress> getCheckpoint(String workspaceId) =>
      _call(() async {
        final response = await _dio.get<Map<String, dynamic>>(
          '/api/workspaces/$workspaceId/checkpoints',
        );
        return CheckpointProgress.fromJson(response.data!);
      });

  Future<CheckpointProgress> markCheckpoint(
    String workspaceId, {
    required int checkpointNo,
    required DateTime presentedOn,
    String? comment,
  }) => _call(() async {
    final response = await _dio.put<Map<String, dynamic>>(
      '/api/workspaces/$workspaceId/checkpoints/$checkpointNo',
      data: {
        'presentedOn': _dateOnly(presentedOn),
        'comment': comment?.trim().isEmpty ?? true ? null : comment!.trim(),
      },
    );
    return CheckpointProgress.fromJson(response.data!);
  });

  Future<void> undoCheckpoint(String workspaceId, int checkpointNo) =>
      _call(() async {
        await _dio.delete<dynamic>(
          '/api/workspaces/$workspaceId/checkpoints/$checkpointNo',
        );
      });

  Future<MentorProfile> getMentorProfile() => _call(() async {
    final response = await _dio.get<Map<String, dynamic>>('/api/mentors/me');
    return MentorProfile.fromJson(response.data!);
  });

  Future<MentorProfile> savePreferredTeams(int preferredTeams) =>
      _call(() async {
        final response = await _dio.put<Map<String, dynamic>>(
          '/api/mentors/me',
          data: {'preferredTeams': preferredTeams},
        );
        return MentorProfile.fromJson(response.data!);
      });

  Future<int> getOpenQuestionCount() => _call(() async {
    final response = await _dio.get<Map<String, dynamic>>(
      '/api/advisor/questions',
      queryParameters: {'status': 'OPEN', 'page': 1, 'pageSize': 1},
    );
    return response.data?['totalCount'] as int? ?? 0;
  });

  Future<AdvisorTeamActivity> getTeamActivity(
    AdvisorWorkspaceRef workspace, {
    required bool includeTasks,
    required bool includeSchedule,
  }) => _call(() async {
    final meetingsResponse = await _dio.get<Map<String, dynamic>>(
      '/api/workspaces/${workspace.id}/meetings',
    );
    final meetingData = meetingsResponse.data ?? <String, dynamic>{};
    final meetings = MeetingList(
      items: [
        for (final item in meetingData['items'] as List? ?? const [])
          MeetingCard.fromJson(Map<String, dynamic>.from(item as Map)),
      ],
      mentorSessionsDone: meetingData['mentorSessionsDone'] as int? ?? 0,
      expectedMentorSessions:
          meetingData['expectedMentorSessions'] as int? ?? 0,
    );
    var tasks = const <TaskCard>[];
    if (includeTasks) {
      final taskResponse = await _dio.get<List<dynamic>>(
        '/api/workspaces/${workspace.id}/tasks',
      );
      tasks = [
        for (final item in taskResponse.data ?? const [])
          TaskCard.fromJson(Map<String, dynamic>.from(item as Map)),
      ];
    }
    TeamMeetingSchedule? schedule;
    if (includeSchedule) {
      final scheduleResponse = await _dio.get<dynamic>(
        '/api/workspaces/${workspace.id}/meeting-schedule',
      );
      if (scheduleResponse.data is Map) {
        schedule = TeamMeetingSchedule.fromJson(
          Map<String, dynamic>.from(scheduleResponse.data as Map),
        );
      }
    }
    return AdvisorTeamActivity(
      workspace: workspace,
      tasks: tasks,
      meetings: meetings,
      schedule: schedule,
    );
  });

  Future<LecturerSemesterStats> getSemesterStats() => _call(() async {
    final overview = await _dio.get<Map<String, dynamic>>(
      '/api/advisor/overview',
    );
    final checkpoints = await _dio.get<List<dynamic>>('/api/checkpoints/me');
    final schedule = await _dio.get<dynamic>('/api/meeting-schedules/me');
    final workspaces = await getAdvisorWorkspaces();
    final meetingLists = await Future.wait([
      for (final workspace in workspaces)
        _dio.get<Map<String, dynamic>>(
          '/api/workspaces/${workspace.id}/meetings',
        ),
    ]);
    final data = overview.data ?? {};
    final missingCheckpoints = (checkpoints.data ?? const []).where((progress) {
      final items = (progress as Map)['items'] as List? ?? const [];
      return items.any(
        (item) =>
            item is Map &&
            item['presented'] == false &&
            item['overdue'] == true,
      );
    }).length;
    final today = DateTime.now();
    final meetingsToday = meetingLists
        .expand(
          (response) => response.data?['items'] as List? ?? const <dynamic>[],
        )
        .where((item) {
          final meeting = Map<String, dynamic>.from(item as Map);
          final startsAt = DateTime.tryParse(
            '${meeting['startsAt']}',
          )?.toLocal();
          return meeting['status'] == 'SCHEDULED' &&
              startsAt != null &&
              startsAt.year == today.year &&
              startsAt.month == today.month &&
              startsAt.day == today.day;
        })
        .length;
    return LecturerSemesterStats(
      needsReplyCount: data['needsReplyCount'] as int? ?? 0,
      overdueSubmissionsCount: data['overdueDeliverableCount'] as int? ?? 0,
      meetingsTodayCount: meetingsToday,
      missingCheckpointsCount: missingCheckpoints,
      hasPostedWeeklySchedule:
          schedule.data is Map &&
          (schedule.data as Map)['status'] == 'PUBLISHED',
    );
  });
}

String _dateOnly(DateTime value) {
  String two(int number) => number.toString().padLeft(2, '0');
  return '${value.year}-${two(value.month)}-${two(value.day)}';
}

final lecturerHomeApiProvider = Provider<LecturerHomeApi>(
  (ref) => LecturerHomeApi(ref.watch(dioProvider)),
);

final lecturerPreferencesMeProvider =
    FutureProvider.autoDispose<List<LecturerPreferenceMe>>(
      (ref) => ref.watch(lecturerHomeApiProvider).getMyPreferences(),
    );

final lecturerMyTopicInterestsProvider =
    FutureProvider.autoDispose<List<TopicInterestItem>>(
      (ref) => ref.watch(lecturerHomeApiProvider).getMyTopicInterests(),
    );

final lecturerTopicRecommendationsProvider = FutureProvider.autoDispose
    .family<TopicRecommendationsResponse, String?>(
      (ref, courseCode) => ref
          .watch(lecturerHomeApiProvider)
          .getRecommendations(courseCode: courseCode),
    );

final lecturerTeamsMeProvider = FutureProvider.autoDispose
    .family<LecturerTeamsMeResponse, String?>(
      (ref, courseCode) => ref
          .watch(lecturerHomeApiProvider)
          .getLecturerTeamsMe(courseCode: courseCode),
    );

final lecturerSemesterStatsProvider =
    FutureProvider.autoDispose<LecturerSemesterStats>(
      (ref) => ref.watch(lecturerHomeApiProvider).getSemesterStats(),
    );

final mentorTeamsMeProvider = FutureProvider.autoDispose<List<AdvisedTeamItem>>(
  (ref) => ref.watch(lecturerHomeApiProvider).getMentorTeamsMe(),
);

final advisorWorkspacesProvider =
    FutureProvider.autoDispose<List<AdvisorWorkspaceRef>>(
      (ref) => ref.watch(lecturerHomeApiProvider).getAdvisorWorkspaces(),
    );

final advisorCheckpointsProvider =
    FutureProvider.autoDispose<List<CheckpointProgress>>(
      (ref) => ref.watch(lecturerHomeApiProvider).getCheckpoints(),
    );

final advisorCheckpointProvider = FutureProvider.autoDispose
    .family<CheckpointProgress, String>(
      (ref, workspaceId) =>
          ref.watch(lecturerHomeApiProvider).getCheckpoint(workspaceId),
    );

final advisorTypedTeamDetailProvider = FutureProvider.autoDispose
    .family<AdvisorTeamDetail, String>(
      (ref, teamId) =>
          ref.watch(lecturerHomeApiProvider).getAdvisorTeamDetail(teamId),
    );

final mentorProfileProvider = FutureProvider.autoDispose<MentorProfile>(
  (ref) => ref.watch(lecturerHomeApiProvider).getMentorProfile(),
);

final advisorOpenQuestionCountProvider = FutureProvider.autoDispose<int>(
  (ref) => ref.watch(lecturerHomeApiProvider).getOpenQuestionCount(),
);

typedef AdvisorActivityQuery = ({
  AdvisorWorkspaceRef workspace,
  bool includeTasks,
  bool includeSchedule,
});

final advisorTeamActivityProvider = FutureProvider.autoDispose
    .family<AdvisorTeamActivity, AdvisorActivityQuery>(
      (ref, query) => ref
          .watch(lecturerHomeApiProvider)
          .getTeamActivity(
            query.workspace,
            includeTasks: query.includeTasks,
            includeSchedule: query.includeSchedule,
          ),
    );

final advisorTopicRecommendationsProvider = FutureProvider.autoDispose
    .family<TopicRecommendationsResponse, String>(
      (ref, advisorKind) => ref
          .watch(lecturerHomeApiProvider)
          .getRecommendations(advisorKind: advisorKind),
    );

final advisorMyInterestsProvider = FutureProvider.autoDispose
    .family<List<TopicInterestItem>, String>(
      (ref, advisorKind) => ref
          .watch(lecturerHomeApiProvider)
          .getMyTopicInterests(advisorKind: advisorKind),
    );

final advisorTeamDetailProvider = FutureProvider.autoDispose
    .family<Map<String, dynamic>, String>(
      (ref, teamId) => ref.watch(lecturerHomeApiProvider).getTeamDetail(teamId),
    );
