import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/providers.dart';
import '../my_team/my_team_api.dart';
import '../suggestions/suggestions_api.dart' show TeamCard;

/// Bộ lọc S06 (query của `GET /api/teams`). Mã lớp chưa có: Team Service chưa lọc được theo lớp.
class TeamFilter {
  const TeamFilter({this.domain, this.missingSpecializationId, this.minOpenSlots, this.sort = '-createdAt'});

  final String? domain;
  final int? missingSpecializationId;
  final int? minOpenSlots;

  /// `-createdAt` (mới nhất), `name`, `-openSlots` (nhiều chỗ trống).
  final String sort;

  /// Số bộ lọc đang áp dụng (nút "Áp dụng (n)", chip lọc).
  int get count => [domain, missingSpecializationId, minOpenSlots].nonNulls.length + (sort == '-createdAt' ? 0 : 1);

  Map<String, Object> toQuery() => {
        'domain': ?domain,
        'missingSpecializationId': ?missingSpecializationId,
        'minOpenSlots': ?minOpenSlots,
        'sort': sort,
      };
}

class TeamPage {
  const TeamPage({required this.items, required this.page, required this.totalPages});

  final List<TeamCard> items;
  final int page;
  final int totalPages;

  bool get hasMore => page < totalPages;
}

/// Gọi Team Service cho Khám phá (S05, S06, S08, S09). Lỗi ném [ApiException].
class DiscoverApi {
  DiscoverApi(this._dio);

  final Dio _dio;

  Future<T> _call<T>(Future<T> Function() request) async {
    try {
      return await request();
    } on DioException catch (e) {
      throw e.apiError;
    }
  }

  /// S05 – nhóm đang tuyển của môn và học kỳ của mình; nhóm đủ người không xuất hiện; ngoài đợt lập nhóm → 409 round-closed.
  Future<TeamPage> search(String search, TeamFilter filter, int page) => _call(() async {
        final response = await _dio.get<Map<String, dynamic>>('/api/teams', queryParameters: {
          if (search.isNotEmpty) 'search': search,
          ...filter.toQuery(),
          'page': page,
          'pageSize': 20,
        });
        final data = response.data!;
        return TeamPage(
          items: [for (final t in data['items'] as List) TeamCard.fromJson(Map<String, dynamic>.from(t as Map))],
          page: data['page'] as int? ?? page,
          totalPages: data['totalPages'] as int? ?? 1,
        );
      });

  /// S08 – chi tiết nhóm kèm quan hệ của người xem.
  Future<MyTeam> team(String teamId) => _call(() async => MyTeam.fromJson((await _dio.get<Map<String, dynamic>>('/api/teams/$teamId')).data!));

  /// S09 – 1–3 ý tưởng hoặc "Chưa có ý tưởng" (quy tắc 17).
  Future<void> requestToJoin(String teamId, {String? message, required List<Idea> ideas, required bool hasNoIdea}) => _call(() => _dio.post<Object>(
        '/api/teams/$teamId/join-requests',
        data: {
          if (message != null && message.isNotEmpty) 'message': message,
          'ideas': [for (final i in ideas) {'title': i.title, if (i.description != null && i.description!.isNotEmpty) 'description': i.description}],
          'hasNoIdea': hasNoIdea,
        },
      ));
}

final discoverApiProvider = Provider<DiscoverApi>((ref) => DiscoverApi(ref.watch(dioProvider)));

final teamDetailProvider = FutureProvider.autoDispose.family<MyTeam, String>((ref, teamId) => ref.watch(discoverApiProvider).team(teamId));
