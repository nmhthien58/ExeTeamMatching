import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/providers.dart';
import '../suggestions/suggestions_api.dart';

class ChangeRequest {
  const ChangeRequest({
    required this.id,
    required this.roundName,
    required this.reason,
    required this.changedMemberCount,
    required this.changedPercent,
    required this.status,
    required this.createdAt,
    this.reviewedByName,
    this.reviewedAt,
    this.reviewNote,
  });

  final String id;
  final String roundName;
  final String reason;
  final int changedMemberCount;
  final double changedPercent;
  final String status;
  final DateTime createdAt;
  final String? reviewedByName;
  final DateTime? reviewedAt;
  final String? reviewNote;

  factory ChangeRequest.fromJson(Map<String, dynamic> json) => ChangeRequest(
    id: json['id'] as String,
    roundName: json['roundName'] as String? ?? 'Đợt đổi nhóm',
    reason: json['reason'] as String? ?? '',
    changedMemberCount: json['changedMemberCount'] as int? ?? 0,
    changedPercent: (json['changedPercent'] as num?)?.toDouble() ?? 0,
    status: json['status'] as String? ?? 'PENDING',
    createdAt: DateTime.tryParse('${json['createdAt']}') ?? DateTime.now(),
    reviewedByName: json['reviewedByName'] as String?,
    reviewedAt: DateTime.tryParse('${json['reviewedAt']}'),
    reviewNote: json['reviewNote'] as String?,
  );
}

class ChangeItem {
  const ChangeItem({
    required this.id,
    required this.action,
    this.student,
    this.newProjectName,
    this.newProjectDescription,
  });

  final String id;
  final String action;
  final StudentRef? student;
  final String? newProjectName;
  final String? newProjectDescription;

  factory ChangeItem.fromJson(Map<String, dynamic> json) => ChangeItem(
    id: json['id'] as String,
    action: json['action'] as String? ?? '',
    student: json['student'] == null
        ? null
        : StudentRef.fromJson(
            Map<String, dynamic>.from(json['student'] as Map),
          ),
    newProjectName: json['newProjectName'] as String?,
    newProjectDescription: json['newProjectDescription'] as String?,
  );

  String get title => switch (action) {
    'ADD' => 'Thêm ${student?.fullName ?? 'thành viên'}',
    'REMOVE' => 'Xóa ${student?.fullName ?? 'thành viên'}',
    'SET_LEADER' =>
      'Chuyển trưởng nhóm cho ${student?.fullName ?? 'thành viên'}',
    'PROJECT_CHANGE' => 'Đổi đề tài: ${newProjectName ?? 'Đề tài mới'}',
    _ => 'Thay đổi nhóm',
  };
}

class PendingChanges {
  const PendingChanges({
    required this.baselineMembers,
    required this.pendingItems,
    required this.wouldHaveMemberCount,
    required this.wouldHaveSpecializationCount,
    this.currentProjectName,
  });

  final List<StudentRef> baselineMembers;
  final List<ChangeItem> pendingItems;
  final int wouldHaveMemberCount;
  final int wouldHaveSpecializationCount;
  final String? currentProjectName;

  factory PendingChanges.fromJson(Map<String, dynamic> json) {
    final project = json['currentProject'] as Map?;
    return PendingChanges(
      baselineMembers: [
        for (final m in (json['baselineMembers'] as List? ?? const []))
          StudentRef.fromJson(Map<String, dynamic>.from(m as Map)),
      ],
      pendingItems: [
        for (final i in (json['pendingItems'] as List? ?? const []))
          ChangeItem.fromJson(Map<String, dynamic>.from(i as Map)),
      ],
      wouldHaveMemberCount: json['wouldHaveMemberCount'] as int? ?? 0,
      wouldHaveSpecializationCount:
          json['wouldHaveSpecializationCount'] as int? ?? 0,
      currentProjectName: project?['name'] as String?,
    );
  }
}

class ChangeRequestDetail {
  const ChangeRequestDetail({required this.request, required this.items});

  final ChangeRequest request;
  final List<ChangeItem> items;

  factory ChangeRequestDetail.fromJson(Map<String, dynamic> json) =>
      ChangeRequestDetail(
        request: ChangeRequest.fromJson(json),
        items: [
          for (final i in (json['items'] as List? ?? const []))
            ChangeItem.fromJson(Map<String, dynamic>.from(i as Map)),
        ],
      );
}

class ChangeRequestsApi {
  ChangeRequestsApi(this._dio);
  final Dio _dio;

  Future<T> _call<T>(Future<T> Function() request) async {
    try {
      return await request();
    } on DioException catch (e) {
      throw e.apiError;
    }
  }

  Future<List<ChangeRequest>> list(String teamId) => _call(() async {
    final response = await _dio.get<List<dynamic>>(
      '/api/teams/$teamId/change-requests',
    );
    return [
      for (final item in response.data ?? const [])
        ChangeRequest.fromJson(Map<String, dynamic>.from(item as Map)),
    ];
  });

  Future<PendingChanges> pending(String teamId) => _call(() async {
    final response = await _dio.get<Map<String, dynamic>>(
      '/api/teams/$teamId/pending-changes',
    );
    return PendingChanges.fromJson(response.data!);
  });

  Future<ChangeRequestDetail> detail(String id) => _call(() async {
    final response = await _dio.get<Map<String, dynamic>>(
      '/api/change-requests/$id',
    );
    return ChangeRequestDetail.fromJson(response.data!);
  });

  Future<ChangeItem> setProjectChange(
    String teamId, {
    required String name,
    String? description,
  }) => _call(() async {
    final response = await _dio.put<Map<String, dynamic>>(
      '/api/teams/$teamId/pending-changes/project',
      data: {
        'name': name,
        if (description != null && description.trim().isNotEmpty)
          'description': description.trim(),
      },
    );
    return ChangeItem.fromJson(response.data!);
  });

  Future<ChangeRequest> create(
    String teamId, {
    required List<String> itemIds,
    required String reason,
  }) => _call(() async {
    final response = await _dio.post<Map<String, dynamic>>(
      '/api/teams/$teamId/change-requests',
      data: {'itemIds': itemIds, 'reason': reason.trim()},
    );
    return ChangeRequest.fromJson(response.data!);
  });

  Future<bool> hasOpenChangeRound() => _call(() async {
    final response = await _dio.get<List<dynamic>>(
      '/api/registration-rounds/active',
      queryParameters: {'courseCode': 'EXE201'},
    );
    return (response.data ?? const []).any((item) {
      final round = Map<String, dynamic>.from(item as Map);
      return round['roundType'] == 'CHANGE_REQUEST';
    });
  });
}

final changeRequestsApiProvider = Provider<ChangeRequestsApi>(
  (ref) => ChangeRequestsApi(ref.watch(dioProvider)),
);
final changeRequestsProvider = FutureProvider.autoDispose
    .family<List<ChangeRequest>, String>(
      (ref, teamId) => ref.watch(changeRequestsApiProvider).list(teamId),
    );
final pendingChangesProvider = FutureProvider.autoDispose
    .family<PendingChanges, String>(
      (ref, teamId) => ref.watch(changeRequestsApiProvider).pending(teamId),
    );
final changeRequestDetailProvider = FutureProvider.autoDispose
    .family<ChangeRequestDetail, String>(
      (ref, id) => ref.watch(changeRequestsApiProvider).detail(id),
    );
final openChangeRoundProvider = FutureProvider.autoDispose<bool>(
  (ref) => ref.watch(changeRequestsApiProvider).hasOpenChangeRound(),
);
