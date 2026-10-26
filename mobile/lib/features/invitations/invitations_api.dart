import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/providers.dart';
import '../my_team/my_team_api.dart' show MemberChange;
import '../suggestions/suggestions_api.dart' show TeamCard;

/// Lời mời tôi nhận được, hoặc yêu cầu tôi đã gửi (S12) – cùng dạng: nhóm, trạng thái, hạn.
class MyRequest {
  const MyRequest({required this.id, required this.team, required this.status, required this.expiresAt, this.message});

  final String id;
  final TeamCard? team;

  /// PENDING, ACCEPTED, DECLINED, EXPIRED, CANCELLED.
  final String status;
  final DateTime expiresAt;
  final String? message;

  factory MyRequest.fromJson(Map<String, dynamic> j) => MyRequest(
        id: j['id'] as String,
        team: j['team'] == null ? null : TeamCard.fromJson(Map<String, dynamic>.from(j['team'] as Map)),
        status: j['status'] as String? ?? 'PENDING',
        expiresAt: DateTime.parse(j['expiresAt'] as String),
        message: j['message'] as String?,
      );
}

/// Gọi Team Service cho S12. Lỗi ném [ApiException].
class InvitationsApi {
  InvitationsApi(this._dio);

  final Dio _dio;

  Future<T> _call<T>(Future<T> Function() request) async {
    try {
      return await request();
    } on DioException catch (e) {
      throw e.apiError;
    }
  }

  Future<List<MyRequest>> _list(String path) => _call(() async {
        final response = await _dio.get<List<dynamic>>(path);
        return [for (final r in response.data!) MyRequest.fromJson(Map<String, dynamic>.from(r as Map))];
      });

  Future<List<MyRequest>> invitations() => _list('/api/invitations/me');

  Future<List<MyRequest>> joinRequests() => _list('/api/join-requests/me');

  Future<MemberChange> accept(String invitationId) => _call(() async {
        final response = await _dio.post<Map<String, dynamic>>('/api/invitations/$invitationId/accept');
        return MemberChange(applied: response.data?['appliedImmediately'] as bool? ?? true);
      });

  Future<void> decline(String invitationId) => _call(() => _dio.post<Object>('/api/invitations/$invitationId/decline'));

  Future<void> cancelJoinRequest(String joinRequestId) => _call(() => _dio.post<Object>('/api/join-requests/$joinRequestId/cancel'));
}

final invitationsApiProvider = Provider<InvitationsApi>((ref) => InvitationsApi(ref.watch(dioProvider)));

final myInvitationsProvider = FutureProvider.autoDispose<List<MyRequest>>((ref) => ref.watch(invitationsApiProvider).invitations());

final myJoinRequestsProvider = FutureProvider.autoDispose<List<MyRequest>>((ref) => ref.watch(invitationsApiProvider).joinRequests());

/// "Còn 2 ngày 3 giờ" / "Còn 45 phút" – thời gian còn lại của lời mời / yêu cầu (quy tắc 8).
String timeLeft(DateTime expiresAt, [DateTime? now]) {
  final left = expiresAt.difference(now ?? DateTime.now());
  if (left.isNegative) return 'Đã hết hạn';
  if (left.inDays > 0) return 'Còn ${left.inDays} ngày ${left.inHours % 24} giờ';
  if (left.inHours > 0) return 'Còn ${left.inHours} giờ';
  return 'Còn ${left.inMinutes} phút';
}
