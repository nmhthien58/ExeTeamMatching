import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/providers.dart';
import '../suggestions/suggestions_api.dart' show TeamCard;

/// `GET /api/teams/me/home` (S01, team.yaml).
class HomeData {
  const HomeData({
    required this.isLeader,
    required this.pendingInvitationCount,
    required this.pendingJoinRequestCount,
    required this.nextAction,
    required this.isTeamFormation,
    this.team,
    this.isAutoAssigned = false,
  });

  final TeamCard? team;
  final bool isLeader;
  final int pendingInvitationCount;
  final int pendingJoinRequestCount;

  /// CREATE_OR_JOIN, INVITE_MEMBERS, ADD_SPECIALIZATION, CHOOSE_LECTURER, SUBMIT_CHANGE_REQUEST, WAIT_REVIEW, OPEN_WORKSPACE, NONE.
  final String nextAction;
  final bool isTeamFormation;
  final bool isAutoAssigned;

  factory HomeData.fromJson(Map<String, dynamic> j) => HomeData(
        team: j['team'] == null ? null : TeamCard.fromJson(Map<String, dynamic>.from(j['team'] as Map)),
        isLeader: j['isLeader'] as bool? ?? false,
        pendingInvitationCount: j['pendingInvitationCount'] as int? ?? 0,
        pendingJoinRequestCount: j['pendingJoinRequestCount'] as int? ?? 0,
        nextAction: j['nextAction'] as String? ?? 'NONE',
        isTeamFormation: j['phase'] == 'TEAM_FORMATION',
        isAutoAssigned: j['isAutoAssigned'] as bool? ?? false,
      );
}

class HomeApi {
  HomeApi(this._dio);

  final Dio _dio;

  Future<HomeData> home() async {
    try {
      return HomeData.fromJson((await _dio.get<Map<String, dynamic>>('/api/teams/me/home')).data!);
    } on DioException catch (e) {
      throw e.apiError;
    }
  }
}

final homeApiProvider = Provider<HomeApi>((ref) => HomeApi(ref.watch(dioProvider)));

final homeProvider = FutureProvider.autoDispose<HomeData>((ref) => ref.watch(homeApiProvider).home());
