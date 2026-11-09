import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/providers.dart';

// Model theo docs/contracts/openapi/academic.yaml – chỉ các trường màn hình A05, S02–S04 dùng.

/// 5 kỹ năng cố định, đúng thứ tự hiển thị (quy tắc 16).
const skillLabels = {
  'TECHNOLOGY': 'Công nghệ',
  'UI_UX_DESIGN': 'Thiết kế UI/UX',
  'MANAGEMENT': 'Quản trị',
  'MARKETING': 'Marketing',
  'COMMUNICATION_PLANNING': 'Plan truyền thông',
};

class StudentProfile {
  const StudentProfile({
    required this.id,
    required this.studentCode,
    required this.fullName,
    this.specialization,
    this.specializationId,
    this.majorId,
    this.majorCode,
    this.majorName,
    this.classCode,
    this.email,
    this.hasTeam = false,
    this.isSeekingTeam = false,
    this.profileCompleted = true,
    this.courseCode,
    this.skills = const {},
    this.description,
    this.phone,
  });

  final String id;
  final String studentCode;
  final String fullName;
  final String? specialization;
  final int? specializationId;
  final int? majorId;
  final String? majorCode;
  final String? majorName;
  final String? classCode;

  /// Null với người ngoài nhóm (quy tắc 11).
  final String? email;
  final bool hasTeam;
  final bool isSeekingTeam;

  /// Đã đánh giá đủ 5 kỹ năng (quy tắc 16); false → A05.
  final bool profileCompleted;
  final String? courseCode;

  /// Mức 1–5 theo khóa của [skillLabels]; rỗng khi chưa đánh giá.
  final Map<String, int> skills;
  final String? description;

  /// Chỉ trả cho chính sinh viên, người cùng nhóm và Bộ môn (quy tắc 11).
  final String? phone;

  factory StudentProfile.fromJson(Map<String, dynamic> j) {
    final email = j['email'] as String?;
    final specialization = j['specialization'] == null ? null : Map<String, dynamic>.from(j['specialization'] as Map);
    final major = j['major'] == null ? null : Map<String, dynamic>.from(j['major'] as Map);
    return StudentProfile(
      id: j['id'] as String,
      studentCode: j['studentCode'] as String? ?? '',
      fullName: j['fullName'] as String? ?? '',
      specialization: specialization?['name'] as String?,
      specializationId: specialization?['id'] as int?,
      majorId: major?['id'] as int?,
      majorCode: major?['code'] as String?,
      majorName: major?['name'] as String?,
      classCode: j['classCode'] as String?,
      email: email == null || email.isEmpty ? null : email,
      hasTeam: j['hasTeam'] as bool? ?? false,
      isSeekingTeam: j['isSeekingTeam'] as bool? ?? false,
      profileCompleted: j['profileCompleted'] as bool? ?? true,
      courseCode: j['courseCode'] as String?,
      skills: {for (final s in (j['skills'] as List? ?? const [])) (s as Map)['skill'] as String: s['level'] as int},
      description: j['description'] as String?,
      phone: j['phone'] as String?,
    );
  }
}

/// Gọi Academic (hồ sơ) và Team (lời mời của nhóm). Lỗi ném [ApiException].
class ProfileApi {
  ProfileApi(this._dio);

  final Dio _dio;

  Future<StudentProfile> student(String id) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>('/api/students/$id');
      return StudentProfile.fromJson(response.data!);
    } on DioException catch (e) {
      throw e.apiError;
    }
  }

  /// Hồ sơ của tôi (A05, S02, S03).
  Future<StudentProfile> me() async {
    try {
      final response = await _dio.get<Map<String, dynamic>>('/api/students/me');
      return StudentProfile.fromJson(response.data!);
    } on DioException catch (e) {
      throw e.apiError;
    }
  }

  /// 5 kỹ năng, giới thiệu, chuyên ngành, công tắc tìm nhóm (quy tắc 16) và SĐT. [isSeekingTeam], [phone] null thì giữ nguyên; [phone] rỗng thì xóa.
  Future<StudentProfile> saveMe({
    required Map<String, int> skills,
    required String description,
    int? specializationId,
    bool? isSeekingTeam,
    String? phone,
  }) async {
    try {
      final response = await _dio.put<Map<String, dynamic>>('/api/students/me', data: {
        'skills': [for (final skill in skillLabels.keys) {'skill': skill, 'level': skills[skill]}],
        'description': description,
        'specializationId': ?specializationId,
        'isSeekingTeam': ?isSeekingTeam,
        'phone': ?phone,
      });
      return StudentProfile.fromJson(response.data!);
    } on DioException catch (e) {
      throw e.apiError;
    }
  }

  /// Id sinh viên đang có lời mời còn hiệu lực của nhóm. API chỉ cho trưởng nhóm gọi.
  Future<Set<String>> invitedStudentIds(String teamId) async {
    try {
      final response = await _dio.get<List<dynamic>>('/api/teams/$teamId/invitations', queryParameters: {'status': 'PENDING'});
      return {for (final i in response.data!) ((i as Map)['invitee'] as Map)['id'] as String};
    } on DioException catch (e) {
      throw e.apiError;
    }
  }
}

final profileApiProvider = Provider<ProfileApi>((ref) => ProfileApi(ref.watch(dioProvider)));

/// Không tự thử lại: Riverpod sẽ giữ màn hình đang tải trong lúc thử, 404 và lỗi phải hiện ngay kèm nút Thử lại.
final studentProfileProvider = FutureProvider.autoDispose.family<StudentProfile, String>(
  (ref, id) => ref.watch(profileApiProvider).student(id),
  retry: (_, _) => null,
);

final invitedStudentIdsProvider =
    FutureProvider.autoDispose.family<Set<String>, String>((ref, teamId) => ref.watch(profileApiProvider).invitedStudentIds(teamId));

final myProfileProvider = FutureProvider.autoDispose<StudentProfile>((ref) => ref.watch(profileApiProvider).me(), retry: (_, _) => null);
