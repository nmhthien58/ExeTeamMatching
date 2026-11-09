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
  LecturerHomeApi(this._dio, {DateTime Function()? now})
    : _now = now ?? DateTime.now;

  final Dio _dio;

  /// Giờ máy, để test đếm "Họp hôm nay" ổn định.
  final DateTime Function() _now;

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
  }) => _topics(advisorKind, courseCode, page, pageSize, 'suggested');

  /// Mọi nhóm của môn chưa có cố vấn cùng loại (L07 "Tất cả nhóm"); nhóm không trùng chuyên môn có matchedDomains rỗng.
  Future<TopicRecommendationsResponse> getAllTopics({
    String advisorKind = 'LECTURER',
    String? courseCode,
    int page = 1,
    int pageSize = 100,
  }) => _topics(advisorKind, courseCode, page, pageSize, 'all');

  Future<TopicRecommendationsResponse> _topics(
    String advisorKind,
    String? courseCode,
    int page,
    int pageSize,
    String scope,
  ) => _call(() async {
    final response = await _dio.get<Map<String, dynamic>>(
      '/api/topic-recommendations/me',
      queryParameters: {
        'advisorKind': advisorKind,
        if (courseCode != null && courseCode.isNotEmpty)
          'courseCode': courseCode,
        'page': page,
        'pageSize': pageSize,
        'scope': scope,
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

  Future<AdvisorOverview> getAdvisorOverview() => _call(() async {
    final response = await _dio.get<Map<String, dynamic>>(
      '/api/advisor/overview',
    );
    return AdvisorOverview.fromJson(response.data ?? <String, dynamic>{});
  });

  Future<LecturerSemesterStats> getSemesterStats() => _call(() async {
    final overview = await getAdvisorOverview();
    final checkpoints = await _dio.get<List<dynamic>>('/api/checkpoints/me');
    final schedule = await _dio.get<dynamic>('/api/meeting-schedules/me');
    final missingCheckpoints = (checkpoints.data ?? const []).where((progress) {
      final items = (progress as Map)['items'] as List? ?? const [];
      return items.any(
        (item) =>
            item is Map &&
            item['presented'] == false &&
            item['overdue'] == true,
      );
    }).length;
    return LecturerSemesterStats(
      needsReplyCount: overview.needsReplyCount,
      overdueSubmissionsCount: overview.overdueDeliverableCount,
      meetingsTodayCount: overview.meetingsOn(_now()),
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

/// Quan tâm OPEN của giảng viên với nhóm thuộc môn [courseCode]. `/topic-interests/me` trả mọi môn
/// và không kèm mã môn, nên đọc môn của từng nhóm để lọc (L01 đếm "Đang quan tâm" theo môn đang xem).
final lecturerOpenInterestsByCourseProvider = FutureProvider.autoDispose
    .family<List<TopicInterestItem>, String>((ref, courseCode) async {
      final api = ref.watch(lecturerHomeApiProvider);
      final interests = await ref.watch(lecturerMyTopicInterestsProvider.future);
      final open = interests.where((i) => i.status == 'OPEN').toList();
      final teams = await Future.wait(open.map((i) => api.getTeamDetail(i.teamId)));
      return [
        for (var i = 0; i < open.length; i++)
          if ((teams[i]['courseCode']?.toString() ?? 'EXE101').toUpperCase() ==
              courseCode.toUpperCase())
            open[i],
      ];
    });

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

/// L07: giảng viên xem đề tài của cả EXE101 và EXE201 (lọc môn ở client). API trả một môn mỗi lần và
/// thiếu `courseCode` thì mặc định EXE101, nên giảng viên gọi từng môn rồi gộp; mentor giữ như cũ.
final advisorTopicRecommendationsProvider = FutureProvider.autoDispose
    .family<TopicRecommendationsResponse, String>(
      (ref, advisorKind) => _loadTopics(
        advisorKind,
        ref.watch(lecturerHomeApiProvider).getRecommendations,
      ),
    );

/// L07 "Tất cả nhóm": mọi nhóm chưa có cố vấn, kể cả nhóm Matching không gợi ý.
final advisorAllTopicsProvider = FutureProvider.autoDispose
    .family<TopicRecommendationsResponse, String>(
      (ref, advisorKind) => _loadTopics(
        advisorKind,
        ref.watch(lecturerHomeApiProvider).getAllTopics,
      ),
    );

/// Giảng viên: gọi riêng từng môn rồi gộp; mentor: một lần (môn mặc định của API).
Future<TopicRecommendationsResponse> _loadTopics(
  String advisorKind,
  Future<TopicRecommendationsResponse> Function({
    String advisorKind,
    String? courseCode,
    int page,
    int pageSize,
  }) load,
) async {
  if (advisorKind != 'LECTURER') {
    return load(advisorKind: advisorKind);
  }
  final parts = await Future.wait([
    for (final course in const ['EXE101', 'EXE201'])
      load(advisorKind: advisorKind, courseCode: course, pageSize: 100),
  ]);
  return TopicRecommendationsResponse.merge({
    'EXE101': parts[0].onlyCourse('EXE101'),
    'EXE201': parts[1].onlyCourse('EXE201'),
  });
}

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
