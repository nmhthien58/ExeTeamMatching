import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/providers.dart';
import '../suggestions/suggestions_api.dart' show StudentRef;

// Model theo docs/contracts/openapi/team.yaml (tag Posts) – S10, S11, S21.

/// Schema RecruitmentPost (S21; S08 hiện bài đang mở).
class RecruitmentPost {
  const RecruitmentPost({required this.id, required this.title, this.description, this.specializationIds = const []});

  final String id;
  final String title;
  final String? description;
  final List<int> specializationIds;

  factory RecruitmentPost.fromJson(Map<String, dynamic> j) => RecruitmentPost(
        id: j['id'] as String,
        title: j['title'] as String? ?? '',
        description: j['description'] as String?,
        specializationIds: [for (final s in (j['specializationIds'] as List? ?? const [])) s as int],
      );
}

/// Schema SeekingPost (S10, S11). `skills` lấy từ hồ sơ của Academic – có thể rỗng.
class SeekingPost {
  const SeekingPost({required this.id, required this.title, required this.student, this.description, this.domainIds = const [], this.skills = const []});

  final String id;

  /// Vai trò mong muốn.
  final String title;

  /// Có thể đóng góp gì.
  final String? description;
  final StudentRef student;
  final List<int> domainIds;
  final List<({String skill, int level})> skills;

  factory SeekingPost.fromJson(Map<String, dynamic> j) => SeekingPost(
        id: j['id'] as String,
        title: j['title'] as String? ?? '',
        description: j['description'] as String?,
        student: StudentRef.fromJson(Map<String, dynamic>.from(j['student'] as Map)),
        domainIds: [for (final d in (j['domainIds'] as List? ?? const [])) d as int],
        skills: [
          for (final s in (j['skills'] as List? ?? const []))
            (skill: (s as Map)['skill'] as String? ?? '', level: s['level'] as int? ?? 0),
        ],
      );
}

class SeekingPage {
  const SeekingPage({required this.items, required this.page, required this.totalPages});

  final List<SeekingPost> items;
  final int page;
  final int totalPages;

  bool get hasMore => page < totalPages;
}

/// Bài tuyển thành viên và bài tìm nhóm (BE-31). Chỉ trong đợt lập nhóm (quy tắc 18). Lỗi ném [ApiException].
class PostsApi {
  PostsApi(this._dio);

  final Dio _dio;

  Future<T> _call<T>(Future<T> Function() request) async {
    try {
      return await request();
    } on DioException catch (e) {
      throw e.apiError;
    }
  }

  /// S10 – sinh viên chưa có nhóm đang tìm nhóm trong học kỳ, mới cập nhật trước.
  Future<SeekingPage> seeking({int? specializationId, required int page}) => _call(() async {
        final response = await _dio.get<Map<String, dynamic>>('/api/seeking-posts', queryParameters: {
          'specializationId': ?specializationId,
          'page': page,
          'pageSize': 20,
        });
        final data = response.data!;
        return SeekingPage(
          items: [for (final p in data['items'] as List) SeekingPost.fromJson(Map<String, dynamic>.from(p as Map))],
          page: data['page'] as int? ?? page,
          totalPages: data['totalPages'] as int? ?? 1,
        );
      });

  /// S11 – bài đang mở của mình; null nếu chưa đăng (404).
  Future<SeekingPost?> mySeeking() async {
    try {
      return SeekingPost.fromJson((await _dio.get<Map<String, dynamic>>('/api/seeking-posts/me')).data!);
    } on DioException catch (e) {
      if (e.response?.statusCode == 404) return null;
      throw e.apiError;
    }
  }

  Future<SeekingPost> saveMySeeking({required String title, String? description, required List<int> domainIds}) => _call(() async =>
      SeekingPost.fromJson((await _dio.put<Map<String, dynamic>>('/api/seeking-posts/me', data: {
        'title': title,
        if (description != null && description.isNotEmpty) 'description': description,
        'domainIds': domainIds,
      }))
          .data!));

  Future<void> removeMySeeking() => _call(() => _dio.delete<Object>('/api/seeking-posts/me'));

  /// S21 – trưởng nhóm tạo hoặc sửa bài tuyển đang mở (mỗi nhóm một bài).
  Future<RecruitmentPost> saveRecruitment(String teamId, {required String title, String? description, required List<int> specializationIds}) =>
      _call(() async => RecruitmentPost.fromJson((await _dio.put<Map<String, dynamic>>('/api/teams/$teamId/recruitment-post', data: {
            'title': title,
            if (description != null && description.isNotEmpty) 'description': description,
            'specializationIds': specializationIds,
          }))
              .data!));

  Future<void> closeRecruitment(String teamId) => _call(() => _dio.delete<Object>('/api/teams/$teamId/recruitment-post'));
}

final postsApiProvider = Provider<PostsApi>((ref) => PostsApi(ref.watch(dioProvider)));

final mySeekingPostProvider = FutureProvider.autoDispose<SeekingPost?>((ref) => ref.watch(postsApiProvider).mySeeking());

/// Câu báo lỗi nghiệp vụ của bài đăng.
const postErrors = {
  'round-closed': 'Đợt lập nhóm đã đóng, không đăng hoặc sửa bài được.',
  'already-in-team': 'Bạn đã có nhóm nên không đăng bài tìm nhóm được.',
  'not-leader': 'Chỉ trưởng nhóm được đăng bài tuyển.',
};
