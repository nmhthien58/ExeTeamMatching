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

  Future<List<TopicInterestItem>> getMyTopicInterests() => _call(() async {
        final response = await _dio.get<List<dynamic>>(
          '/api/topic-interests/me',
          queryParameters: {'advisorKind': 'LECTURER'},
        );
        return [
          for (final item in response.data ?? const [])
            TopicInterestItem.fromJson(Map<String, dynamic>.from(item as Map)),
        ];
      });

  Future<List<TopicRecommendationItem>> getRecommendations() => _call(() async {
        final response = await _dio.get<List<dynamic>>(
          '/api/topic-recommendations/me',
        );
        return [
          for (final item in response.data ?? const [])
            TopicRecommendationItem.fromJson(Map<String, dynamic>.from(item as Map)),
        ];
      });

  Future<void> openInterest(String teamId) => _call(() async {
        await _dio.post<dynamic>(
          '/api/teams/$teamId/topic-interests',
        );
      });

  Future<void> withdrawInterest(String interestId) => _call(() async {
        await _dio.delete<dynamic>(
          '/api/topic-interests/$interestId',
        );
      });

  Future<LecturerSemesterStats> getSemesterStats() => _call(() async {
        try {
          final response = await _dio.get<Map<String, dynamic>>(
            '/api/advisors/me/semester-stats',
          );
          final data = response.data ?? {};
          final teams = [
            for (final t in data['assignedTeams'] ?? const [])
              LecturerAssignedTeam.fromJson(Map<String, dynamic>.from(t as Map)),
          ];
          return LecturerSemesterStats(
            needsReplyCount: data['needsReplyCount'] as int? ?? 0,
            overdueSubmissionsCount: data['overdueSubmissionsCount'] as int? ?? 0,
            meetingsTodayCount: data['meetingsTodayCount'] as int? ?? 0,
            missingCheckpointsCount: data['missingCheckpointsCount'] as int? ?? 0,
            hasPostedWeeklySchedule: data['hasPostedWeeklySchedule'] as bool? ?? true,
            assignedTeams: teams,
          );
        } catch (_) {
          return const LecturerSemesterStats();
        }
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

final lecturerTopicRecommendationsProvider = FutureProvider.autoDispose<List<TopicRecommendationItem>>(
  (ref) => ref.watch(lecturerHomeApiProvider).getRecommendations(),
);

final lecturerSemesterStatsProvider = FutureProvider.autoDispose<LecturerSemesterStats>(
  (ref) => ref.watch(lecturerHomeApiProvider).getSemesterStats(),
);
