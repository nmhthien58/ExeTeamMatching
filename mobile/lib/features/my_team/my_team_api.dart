import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/providers.dart';
import '../discover/posts_api.dart' show RecruitmentPost;
import '../suggestions/suggestions_api.dart' show CatalogRef, myTeamProvider;

// Model theo docs/contracts/openapi/team.yaml (tag Teams, Members, Invitations, JoinRequests) – các trường S13–S17, S20 dùng.

class Member {
  const Member({required this.id, required this.studentCode, required this.fullName, required this.isLeader, this.specialization});

  final String id;
  final String studentCode;
  final String fullName;
  final bool isLeader;
  final CatalogRef? specialization;

  factory Member.fromJson(Map<String, dynamic> j) => Member(
        id: j['id'] as String,
        studentCode: j['studentCode'] as String? ?? '',
        fullName: j['fullName'] as String? ?? '',
        isLeader: j['role'] == 'LEADER',
        specialization: j['specialization'] == null ? null : CatalogRef.fromJson(_map(j['specialization'])),
      );
}

/// Schema TeamDetail (S15, S16, S17).
class MyTeam {
  const MyTeam({
    required this.id,
    required this.name,
    required this.status,
    required this.courseCode,
    required this.members,
    required this.domains,
    required this.isLeader,
    required this.memberCount,
    required this.specializationCount,
    this.minSize = 4,
    this.maxSize = 6,
    this.minSpecializations = 2,
    this.projectName,
    this.ideaDescription,
    this.lecturerName,
    this.mentorName,
    this.isInherited = false,
    this.hasPendingChanges = false,
    this.relation = 'NONE',
    this.recruitmentPost,
  });

  final String id;
  final String name;
  final String status;
  final String courseCode;
  final List<Member> members;
  final List<CatalogRef> domains;
  final bool isLeader;
  final int memberCount;
  final int specializationCount;
  final int minSize;
  final int maxSize;
  final int minSpecializations;
  final String? projectName;
  final String? ideaDescription;
  final String? lecturerName;
  final String? mentorName;
  final bool isInherited;
  final bool hasPendingChanges;

  /// Quan hệ của người xem: LEADER, MEMBER, INVITED, REQUESTED, NONE (chọn nút ở S08).
  final String relation;

  /// Bài tuyển đang mở (S21), nếu có.
  final RecruitmentPost? recruitmentPost;

  /// Bài tuyển dạng "tiêu đề – mô tả" cho S08.
  String? get recruitment => recruitmentPost == null ? null : [recruitmentPost!.title, ?recruitmentPost!.description].join(' – ');

  bool get isFull => memberCount >= maxSize;

  /// Còn sửa / mời được (Nháp, Hoạt động); sau hạn chót thì khóa.
  bool get isOpen => status == 'DRAFT' || status == 'ACTIVE';

  factory MyTeam.fromJson(Map<String, dynamic> j) {
    final r = j['requirement'] == null ? const <String, dynamic>{} : _map(j['requirement']);
    return MyTeam(
      id: j['id'] as String,
      name: j['name'] as String? ?? '',
      status: j['status'] as String? ?? 'DRAFT',
      courseCode: j['courseCode'] as String? ?? '',
      members: [for (final m in (j['members'] as List? ?? const [])) Member.fromJson(_map(m))],
      domains: [for (final d in (j['domains'] as List? ?? const [])) CatalogRef.fromJson(_map(d))],
      isLeader: j['myRelation'] == 'LEADER',
      memberCount: j['memberCount'] as int? ?? 0,
      specializationCount: r['specializationCount'] as int? ?? 0,
      minSize: r['minSize'] as int? ?? 4,
      maxSize: r['maxSize'] as int? ?? 6,
      minSpecializations: r['minSpecializations'] as int? ?? 2,
      projectName: j['projectName'] as String?,
      ideaDescription: j['ideaDescription'] as String?,
      lecturerName: j['lecturerName'] as String?,
      mentorName: j['mentorName'] as String?,
      isInherited: j['isInherited'] as bool? ?? false,
      hasPendingChanges: j['hasPendingChanges'] as bool? ?? false,
      relation: j['myRelation'] as String? ?? 'NONE',
      recruitmentPost: j['recruitmentPost'] == null ? null : RecruitmentPost.fromJson(_map(j['recruitmentPost'])),
    );
  }
}

/// Body POST / PUT /api/teams (TeamInput).
class TeamInput {
  const TeamInput({required this.name, required this.domainIds, this.projectName, this.ideaDescription, this.primaryDomainId});

  final String name;
  final String? projectName;
  final String? ideaDescription;
  final List<int> domainIds;
  final int? primaryDomainId;

  Map<String, dynamic> toJson() => {
        'name': name,
        if (projectName != null && projectName!.isNotEmpty) 'projectName': projectName,
        if (ideaDescription != null && ideaDescription!.isNotEmpty) 'ideaDescription': ideaDescription,
        'domainIds': domainIds,
        'primaryDomainId': ?primaryDomainId,
      };
}

class Idea {
  const Idea(this.title, this.description);

  final String title;
  final String? description;
}

/// Yêu cầu tham gia gửi tới nhóm (S20 tab 1).
class JoinRequestItem {
  const JoinRequestItem({required this.id, required this.student, required this.ideas, required this.hasNoIdea, required this.status, this.message});

  final String id;
  final Member student;
  final List<Idea> ideas;
  final bool hasNoIdea;
  final String status;
  final String? message;

  factory JoinRequestItem.fromJson(Map<String, dynamic> j) => JoinRequestItem(
        id: j['id'] as String,
        student: Member.fromJson({..._map(j['student']), 'role': 'MEMBER'}),
        ideas: [for (final i in (j['ideas'] as List? ?? const [])) Idea(_map(i)['title'] as String? ?? '', _map(i)['description'] as String?)],
        hasNoIdea: j['hasNoIdea'] as bool? ?? false,
        status: j['status'] as String? ?? 'PENDING',
        message: j['message'] as String?,
      );
}

/// Lời mời nhóm đã gửi (S20 tab 2).
class SentInvitation {
  const SentInvitation({required this.id, required this.invitee, required this.status, required this.expiresAt});

  final String id;
  final Member invitee;
  final String status;
  final DateTime expiresAt;

  factory SentInvitation.fromJson(Map<String, dynamic> j) => SentInvitation(
        id: j['id'] as String,
        invitee: Member.fromJson({..._map(j['invitee']), 'role': 'MEMBER'}),
        status: j['status'] as String? ?? 'PENDING',
        expiresAt: DateTime.parse(j['expiresAt'] as String),
      );
}

/// Kết quả xóa / rời / chuyển quyền / chấp nhận: `applied = false` khi nhóm EXE201 kế thừa ghi thay đổi chờ duyệt (quy tắc 7).
class MemberChange {
  const MemberChange({required this.applied});

  final bool applied;
}

Map<String, dynamic> _map(Object? o) => Map<String, dynamic>.from(o! as Map);

/// Gọi Team Service cho nhóm của tôi. Lỗi ném [ApiException].
class MyTeamApi {
  MyTeamApi(this._dio);

  final Dio _dio;

  Future<T> _call<T>(Future<T> Function() request) async {
    try {
      return await request();
    } on DioException catch (e) {
      throw e.apiError;
    }
  }

  /// Null nếu chưa có nhóm (404).
  Future<MyTeam?> myTeam() async {
    try {
      final response = await _dio.get<Map<String, dynamic>>('/api/teams/me');
      return MyTeam.fromJson(response.data!);
    } on DioException catch (e) {
      if (e.response?.statusCode == 404) return null;
      throw e.apiError;
    }
  }

  Future<List<CatalogRef>> domains() => _call(() async {
        final response = await _dio.get<List<dynamic>>('/api/domains');
        return [for (final d in response.data!) CatalogRef.fromJson(_map(d))];
      });

  Future<MyTeam> create(TeamInput input) => _call(() async => MyTeam.fromJson((await _dio.post<Map<String, dynamic>>('/api/teams', data: input.toJson())).data!));

  Future<MyTeam> update(String teamId, TeamInput input) =>
      _call(() async => MyTeam.fromJson((await _dio.put<Map<String, dynamic>>('/api/teams/$teamId', data: input.toJson())).data!));

  Future<MemberChange> removeMember(String teamId, String studentId) => _change(() => _dio.delete<Map<String, dynamic>>('/api/teams/$teamId/members/$studentId'));

  Future<MemberChange> leave(String teamId) => _change(() => _dio.post<Map<String, dynamic>>('/api/teams/$teamId/leave'));

  Future<MemberChange> transferLeader(String teamId, String studentId) =>
      _change(() => _dio.post<Map<String, dynamic>>('/api/teams/$teamId/transfer-leader', data: {'studentId': studentId}));

  Future<List<JoinRequestItem>> joinRequests(String teamId) => _call(() async {
        final response = await _dio.get<List<dynamic>>('/api/teams/$teamId/join-requests', queryParameters: {'status': 'PENDING'});
        return [for (final r in response.data!) JoinRequestItem.fromJson(_map(r))];
      });

  Future<MemberChange> acceptJoinRequest(String id) => _change(() => _dio.post<Map<String, dynamic>>('/api/join-requests/$id/accept'));

  Future<void> declineJoinRequest(String id) => _call(() => _dio.post<Object>('/api/join-requests/$id/decline'));

  Future<List<SentInvitation>> sentInvitations(String teamId) => _call(() async {
        final response = await _dio.get<List<dynamic>>('/api/teams/$teamId/invitations');
        return [for (final i in response.data!) SentInvitation.fromJson(_map(i))];
      });

  Future<void> cancelInvitation(String id) => _call(() => _dio.post<Object>('/api/invitations/$id/cancel'));

  Future<MemberChange> _change(Future<Response<Map<String, dynamic>>> Function() request) =>
      _call(() async => MemberChange(applied: (await request()).data?['appliedImmediately'] as bool? ?? true));
}

final myTeamApiProvider = Provider<MyTeamApi>((ref) => MyTeamApi(ref.watch(dioProvider)));

final myTeamDetailProvider = FutureProvider.autoDispose<MyTeam?>((ref) => ref.watch(myTeamApiProvider).myTeam());

final domainsProvider = FutureProvider<List<CatalogRef>>((ref) => ref.watch(myTeamApiProvider).domains());

final joinRequestsProvider = FutureProvider.autoDispose.family<List<JoinRequestItem>, String>((ref, teamId) => ref.watch(myTeamApiProvider).joinRequests(teamId));

final sentInvitationsProvider =
    FutureProvider.autoDispose.family<List<SentInvitation>, String>((ref, teamId) => ref.watch(myTeamApiProvider).sentInvitations(teamId));

/// Sau khi nhóm thay đổi: tải lại cả bản của S15 và bản gợi ý (S18) của Nguyên.
void refreshMyTeam(WidgetRef ref) {
  ref.invalidate(myTeamDetailProvider);
  ref.invalidate(myTeamProvider);
}
