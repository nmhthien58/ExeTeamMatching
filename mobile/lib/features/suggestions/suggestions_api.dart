import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/providers.dart';

// Model theo docs/contracts/openapi/team.yaml và academic.yaml – chỉ các trường màn hình S07, S18, S19, S30, S31 dùng.

class CatalogRef {
  const CatalogRef({required this.id, required this.code, required this.name});

  final int id;
  final String code;
  final String name;

  factory CatalogRef.fromJson(Map<String, dynamic> j) => CatalogRef(id: j['id'] as int, code: j['code'] as String? ?? '', name: j['name'] as String? ?? '');
}

class StudentRef {
  const StudentRef({required this.id, required this.studentCode, required this.fullName, this.specialization, this.email, this.phone, this.isLeader = false});

  final String id;
  final String studentCode;
  final String fullName;
  final CatalogRef? specialization;

  /// Liên hệ chỉ trả cho đồng đội (quy tắc 11).
  final String? email;
  final String? phone;
  final bool isLeader;

  factory StudentRef.fromJson(Map<String, dynamic> j) => StudentRef(
        id: j['id'] as String,
        studentCode: j['studentCode'] as String? ?? '',
        fullName: j['fullName'] as String? ?? '',
        specialization: j['specialization'] == null ? null : CatalogRef.fromJson(Map<String, dynamic>.from(j['specialization'] as Map)),
        email: j['email'] as String?,
        phone: j['phone'] as String?,
        isLeader: j['role'] == 'LEADER',
      );
}

class TeamCard {
  const TeamCard({
    required this.id,
    required this.name,
    required this.status,
    required this.memberCount,
    this.projectName,
    this.domains = const [],
    this.missingSpecializations = const [],
    this.minSize = 4,
    this.maxSize = 6,
    this.minSpecializations = 2,
  });

  final String id;
  final String name;
  final String status;
  final int memberCount;
  final String? projectName;
  final List<CatalogRef> domains;
  final List<CatalogRef> missingSpecializations;

  /// Quy tắc Bộ môn đặt ở W05 (BE-90), API trả trong `requirement`.
  final int minSize;
  final int maxSize;
  final int minSpecializations;

  factory TeamCard.fromJson(Map<String, dynamic> j) {
    final requirement = j['requirement'] == null ? const <String, dynamic>{} : Map<String, dynamic>.from(j['requirement'] as Map);
    return TeamCard(
      id: j['id'] as String,
      name: j['name'] as String? ?? '',
      status: j['status'] as String? ?? 'ACTIVE',
      memberCount: j['memberCount'] as int? ?? 0,
      projectName: j['projectName'] as String?,
      domains: [for (final d in (j['domains'] as List? ?? const [])) CatalogRef.fromJson(Map<String, dynamic>.from(d as Map))],
      missingSpecializations: [
        for (final s in (requirement['missingSpecializations'] as List? ?? const [])) CatalogRef.fromJson(Map<String, dynamic>.from(s as Map)),
      ],
      minSize: requirement['minSize'] as int? ?? 4,
      maxSize: requirement['maxSize'] as int? ?? 6,
      minSpecializations: requirement['minSpecializations'] as int? ?? 2,
    );
  }
}

class TeamDetail {
  const TeamDetail({required this.card, required this.members, required this.isLeader, this.isAutoAssigned = false});

  final TeamCard card;
  final List<StudentRef> members;
  final bool isLeader;
  final bool isAutoAssigned;

  bool get isFull => card.memberCount >= card.maxSize;

  factory TeamDetail.fromJson(Map<String, dynamic> j) => TeamDetail(
        card: TeamCard.fromJson(j),
        members: [for (final m in (j['members'] as List? ?? const [])) StudentRef.fromJson(Map<String, dynamic>.from(m as Map))],
        isLeader: j['myRelation'] == 'LEADER',
        isAutoAssigned: j['isAutoAssigned'] as bool? ?? false,
      );
}

class TeamSuggestion {
  const TeamSuggestion({required this.team, required this.score, required this.reasons});

  final TeamCard team;
  final double score;
  final List<String> reasons;

  factory TeamSuggestion.fromJson(Map<String, dynamic> j) => TeamSuggestion(
        team: TeamCard.fromJson(Map<String, dynamic>.from(j['team'] as Map)),
        score: (j['score'] as num?)?.toDouble() ?? 0,
        reasons: [for (final r in (j['reasons'] as List? ?? const [])) '$r'],
      );
}

class MemberSuggestion {
  const MemberSuggestion({required this.student, required this.skills, required this.score, required this.reasons, required this.alreadyInvited});

  final StudentRef student;
  final List<String> skills;
  final double score;
  final List<String> reasons;
  final bool alreadyInvited;

  factory MemberSuggestion.fromJson(Map<String, dynamic> j) => MemberSuggestion(
        student: StudentRef.fromJson(Map<String, dynamic>.from(j['student'] as Map)),
        skills: [for (final s in (j['skills'] as List? ?? const [])) '$s'],
        score: (j['score'] as num?)?.toDouble() ?? 0,
        reasons: [for (final r in (j['reasons'] as List? ?? const [])) '$r'],
        alreadyInvited: j['alreadyInvited'] as bool? ?? false,
      );
}

class MemberSuggestions {
  const MemberSuggestions({required this.missingSpecializations, required this.items});

  final List<CatalogRef> missingSpecializations;
  final List<MemberSuggestion> items;

  factory MemberSuggestions.fromJson(Map<String, dynamic> j) => MemberSuggestions(
        missingSpecializations: [
          for (final s in (j['missingSpecializations'] as List? ?? const [])) CatalogRef.fromJson(Map<String, dynamic>.from(s as Map)),
        ],
        items: [for (final i in (j['items'] as List? ?? const [])) MemberSuggestion.fromJson(Map<String, dynamic>.from(i as Map))],
      );
}

/// Nhãn mức phù hợp – điểm chỉ để sắp xếp, không hiện số % (matching.proto).
String fitLabel(double score) => score >= 0.6 ? 'Rất phù hợp' : score >= 0.3 ? 'Phù hợp' : 'Có thể phù hợp';

/// Gọi Team Service (gợi ý, lời mời) và Academic (tìm sinh viên, danh mục). Lỗi ném [ApiException].
class SuggestionsApi {
  SuggestionsApi(this._dio);

  final Dio _dio;

  Future<T> _call<T>(Future<T> Function() request) async {
    try {
      return await request();
    } on DioException catch (e) {
      throw e.apiError;
    }
  }

  /// Nhóm của tôi; null nếu chưa có nhóm (404).
  Future<TeamDetail?> myTeam() async {
    try {
      final response = await _dio.get<Map<String, dynamic>>('/api/teams/me');
      return TeamDetail.fromJson(response.data!);
    } on DioException catch (e) {
      if (e.response?.statusCode == 404) return null;
      throw e.apiError;
    }
  }

  Future<List<TeamSuggestion>> teamsForMe() => _call(() async {
        final response = await _dio.get<List<dynamic>>('/api/teams/suggested-for-me', queryParameters: {'limit': 10});
        return [for (final s in response.data!) TeamSuggestion.fromJson(Map<String, dynamic>.from(s as Map))];
      });

  Future<MemberSuggestions> membersFor(String teamId) => _call(() async {
        final response = await _dio.get<Map<String, dynamic>>('/api/teams/$teamId/suggested-members', queryParameters: {'limit': 10});
        return MemberSuggestions.fromJson(response.data!);
      });

  /// S19 – chỉ sinh viên chưa có nhóm.
  Future<List<StudentRef>> searchStudents(String search, int? specializationId) => _call(() async {
        final response = await _dio.get<Map<String, dynamic>>('/api/students', queryParameters: {
          if (search.isNotEmpty) 'search': search,
          'specializationId': ?specializationId,
          'hasTeam': false,
          'pageSize': 20,
        });
        return [for (final s in (response.data!['items'] as List)) StudentRef.fromJson(Map<String, dynamic>.from(s as Map))];
      });

  Future<List<CatalogRef>> specializations() => _call(() async {
        final response = await _dio.get<List<dynamic>>('/api/specializations');
        return [for (final s in response.data!) CatalogRef.fromJson(Map<String, dynamic>.from(s as Map))];
      });

  Future<void> invite(String teamId, String studentId, {String? message}) => _call(() => _dio.post<Object>(
        '/api/teams/$teamId/invitations',
        data: {'studentId': studentId, if (message != null && message.isNotEmpty) 'message': message},
      ));
}

final suggestionsApiProvider = Provider<SuggestionsApi>((ref) => SuggestionsApi(ref.watch(dioProvider)));

final myTeamProvider = FutureProvider.autoDispose<TeamDetail?>((ref) => ref.watch(suggestionsApiProvider).myTeam());

final teamsForMeProvider = FutureProvider.autoDispose<List<TeamSuggestion>>((ref) => ref.watch(suggestionsApiProvider).teamsForMe());

final memberSuggestionsProvider =
    FutureProvider.autoDispose.family<MemberSuggestions, String>((ref, teamId) => ref.watch(suggestionsApiProvider).membersFor(teamId));

final studentSearchProvider = FutureProvider.autoDispose.family<List<StudentRef>, ({String search, int? specializationId})>(
  (ref, q) => ref.watch(suggestionsApiProvider).searchStudents(q.search, q.specializationId),
);

final specializationsProvider = FutureProvider<List<CatalogRef>>((ref) => ref.watch(suggestionsApiProvider).specializations());
