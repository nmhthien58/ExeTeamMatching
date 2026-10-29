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

  Future<List<TopicInterestItem>> getMyTopicInterests({String advisorKind = 'LECTURER'}) => _call(() async {
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
  }) =>
      _call(() async {
        final response = await _dio.get<Map<String, dynamic>>(
          '/api/topic-recommendations/me',
          queryParameters: {
            'advisorKind': advisorKind,
            if (courseCode != null && courseCode.isNotEmpty) 'courseCode': courseCode,
            'page': page,
            'pageSize': pageSize,
          },
        );
        return TopicRecommendationsResponse.fromJson(response.data!);
      });

  Future<void> openInterest(String teamId, {String advisorKind = 'LECTURER'}) => _call(() async {
        await _dio.post<dynamic>(
          '/api/teams/$teamId/topic-interests',
          data: {'advisorKind': advisorKind},
        );
      });

  Future<void> withdrawInterest(String interestId) => _call(() async {
        await _dio.delete<dynamic>(
          '/api/topic-interests/$interestId',
        );
      });

  Future<LecturerTeamsMeResponse> getLecturerTeamsMe({String? courseCode}) => _call(() async {
        final response = await _dio.get<Map<String, dynamic>>(
          '/api/lecturer-teams/me',
          queryParameters: {
            if (courseCode != null && courseCode.isNotEmpty) 'courseCode': courseCode,
          },
        );
        return LecturerTeamsMeResponse.fromJson(response.data!);
      });

  Future<LecturerSemesterStats> getSemesterStats() => _call(() async {
        final response = await _dio.get<Map<String, dynamic>>(
          '/api/advisors/me/semester-stats',
        );
        final data = response.data ?? {};
        return LecturerSemesterStats(
          needsReplyCount: data['needsReplyCount'] as int? ?? 0,
          overdueSubmissionsCount: data['overdueSubmissionsCount'] as int? ?? 0,
          meetingsTodayCount: data['meetingsTodayCount'] as int? ?? 0,
          missingCheckpointsCount: data['missingCheckpointsCount'] as int? ?? 0,
          hasPostedWeeklySchedule: data['hasPostedWeeklySchedule'] as bool? ?? true,
        );
      });
}

final lecturerHomeApiProvider = Provider<LecturerHomeApi>(
  (ref) => LecturerHomeApi(ref.watch(dioProvider)),
);

final lecturerPreferencesMeProvider = FutureProvider.autoDispose<List<LecturerPreferenceMe>>(
  (ref) => ref.watch(lecturerHomeApiProvider).getMyPreferences(),
);

final lecturerMyTopicInterestsProvider = FutureProvider.autoDispose<List<TopicInterestItem>>(
  (ref) => ref.watch(lecturerHomeApiProvider).getMyTopicInterests(),
);

final lecturerTopicRecommendationsProvider = FutureProvider.autoDispose
    .family<TopicRecommendationsResponse, String?>(
  (ref, courseCode) => ref.watch(lecturerHomeApiProvider).getRecommendations(courseCode: courseCode),
);

final lecturerTeamsMeProvider = FutureProvider.autoDispose
    .family<LecturerTeamsMeResponse, String?>(
  (ref, courseCode) => ref.watch(lecturerHomeApiProvider).getLecturerTeamsMe(courseCode: courseCode),
);

final lecturerSemesterStatsProvider = FutureProvider.autoDispose<LecturerSemesterStats>(
  (ref) => ref.watch(lecturerHomeApiProvider).getSemesterStats(),
);
