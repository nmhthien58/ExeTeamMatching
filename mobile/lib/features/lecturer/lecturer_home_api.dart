import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/providers.dart';
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

  Future<LecturerSemesterStats> getSemesterStats() => _call(() async {
    final overview = await _dio.get<Map<String, dynamic>>(
      '/api/advisor/overview',
    );
    final checkpoints = await _dio.get<List<dynamic>>('/api/checkpoints/me');
    final schedule = await _dio.get<dynamic>('/api/meeting-schedules/me');
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
    return LecturerSemesterStats(
      needsReplyCount: data['needsReplyCount'] as int? ?? 0,
      overdueSubmissionsCount: data['overdueDeliverableCount'] as int? ?? 0,
      // `upcomingMeetingCount` includes other days; never show it as today's count.
      meetingsTodayCount: data['meetingsTodayCount'] as int?,
      missingCheckpointsCount: missingCheckpoints,
      hasPostedWeeklySchedule:
          schedule.data is Map &&
          (schedule.data as Map)['status'] == 'PUBLISHED',
    );
  });
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
