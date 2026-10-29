import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/providers.dart';
import 'lecturers_models.dart';

class LecturersApi {
  LecturersApi(this._dio);

  final Dio _dio;

  Future<T> _call<T>(Future<T> Function() fn) async {
    try {
      return await fn();
    } on DioException catch (e) {
      throw e.apiError;
    }
  }

  Future<List<LecturerPreferenceItem>> getPreferences(String teamId) => _call(() async {
        final response = await _dio.get<List<dynamic>>(
          '/api/teams/$teamId/lecturer-preferences',
        );
        return [
          for (final item in response.data ?? const [])
            LecturerPreferenceItem.fromJson(Map<String, dynamic>.from(item as Map)),
        ];
      });

  Future<List<LecturerPreferenceItem>> updatePreferences(
    String teamId,
    List<String> lecturerIds,
  ) =>
      _call(() async {
        final response = await _dio.put<List<dynamic>>(
          '/api/teams/$teamId/lecturer-preferences',
          data: {'lecturerIds': lecturerIds},
        );
        return [
          for (final item in response.data ?? const [])
            LecturerPreferenceItem.fromJson(Map<String, dynamic>.from(item as Map)),
        ];
      });

  Future<List<TopicInterestItem>> getTopicInterests(String teamId) => _call(() async {
        final response = await _dio.get<List<dynamic>>(
          '/api/teams/$teamId/topic-interests',
          queryParameters: {'advisorKind': 'LECTURER'},
        );
        return [
          for (final item in response.data ?? const [])
            TopicInterestItem.fromJson(Map<String, dynamic>.from(item as Map)),
        ];
      });

  Future<List<LecturerSummary>> listLecturers({
    String? search,
    int page = 1,
    int pageSize = 50,
  }) =>
      _call(() async {
        final response = await _dio.get<Map<String, dynamic>>(
          '/api/lecturers',
          queryParameters: {
            if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
            'page': page,
            'pageSize': pageSize,
          },
        );
        final items = response.data?['items'] as List<dynamic>? ?? const [];
        return [
          for (final item in items)
            LecturerSummary.fromJson(Map<String, dynamic>.from(item as Map)),
        ];
      });

  Future<LecturerSummary> getLecturer(String id) => _call(() async {
        final response = await _dio.get<Map<String, dynamic>>(
          '/api/lecturers/$id',
        );
        return LecturerSummary.fromJson(response.data!);
      });
}

final lecturersApiProvider = Provider<LecturersApi>(
  (ref) => LecturersApi(ref.watch(dioProvider)),
);

final lecturerPreferencesProvider = FutureProvider.autoDispose
    .family<List<LecturerPreferenceItem>, String>(
  (ref, teamId) => ref.watch(lecturersApiProvider).getPreferences(teamId),
);

final teamTopicInterestsProvider = FutureProvider.autoDispose
    .family<List<TopicInterestItem>, String>(
  (ref, teamId) => ref.watch(lecturersApiProvider).getTopicInterests(teamId),
);

final lecturersListProvider = FutureProvider.autoDispose
    .family<List<LecturerSummary>, String?>(
  (ref, search) => ref.watch(lecturersApiProvider).listLecturers(search: search),
);

final lecturerDetailProvider = FutureProvider.autoDispose
    .family<LecturerSummary, String>(
  (ref, id) => ref.watch(lecturersApiProvider).getLecturer(id),
);
