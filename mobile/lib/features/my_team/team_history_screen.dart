import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/network/api_exception.dart';
import '../../core/providers.dart';
import '../../core/theme/app_tokens.dart';
import '../../core/view_state.dart';
import '../../shared/widgets/states.dart';
import '../workspace/format.dart';
import 'my_team_api.dart';
import 'team_history_view_model.dart';

/// Schema AuditLog (team.yaml tag AuditLog) – một dòng lịch sử nhóm.
class AuditLogEntry {
  const AuditLogEntry({required this.id, required this.action, required this.actorName, required this.summary, required this.occurredAt, this.reason});

  final int id;
  final String action;
  final String actorName;
  final String summary;
  final String? reason;
  final DateTime occurredAt;

  factory AuditLogEntry.fromJson(Map<String, dynamic> j) => AuditLogEntry(
        id: j['id'] as int,
        action: j['action'] as String? ?? '',
        actorName: j['actorName'] as String? ?? '',
        summary: j['summary'] as String? ?? '',
        reason: j['reason'] as String?,
        occurredAt: DateTime.parse(j['occurredAt'] as String),
      );
}

/// Lịch sử nhóm (BE-32). Lỗi ném [ApiException].
class TeamHistoryApi {
  TeamHistoryApi(this._dio);

  final Dio _dio;

  /// Mới nhất trước; trả kèm có còn trang sau không.
  Future<(List<AuditLogEntry>, bool)> page(String teamId, int page) async {
    try {
      final data = (await _dio.get<Map<String, dynamic>>('/api/teams/$teamId/audit-logs', queryParameters: {'page': page, 'pageSize': 20})).data!;
      return (
        [for (final e in data['items'] as List) AuditLogEntry.fromJson(Map<String, dynamic>.from(e as Map))],
        (data['page'] as int? ?? page) < (data['totalPages'] as int? ?? 1),
      );
    } on DioException catch (e) {
      throw e.apiError;
    }
  }
}

final teamHistoryApiProvider = Provider<TeamHistoryApi>((ref) => TeamHistoryApi(ref.watch(dioProvider)));

/// S22 – Lịch sử hoạt động nhóm (MB-26): ai làm gì, lúc nào – vào / rời nhóm, đổi trưởng nhóm, sửa thông tin, bài tuyển.
class TeamHistoryScreen extends ConsumerWidget {
  const TeamHistoryScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) => Scaffold(
        appBar: AppBar(title: const Text('Lịch sử hoạt động')),
        body: ref.watch(myTeamDetailProvider).when(
              loading: () => const SkeletonList(),
              error: (e, _) => ErrorState(error: e, onRetry: () => refreshMyTeam(ref)),
              data: (team) => team == null
                  ? const EmptyState(icon: Icons.group_off_outlined, title: 'Bạn chưa có nhóm')
                  : _HistoryList(teamId: team.id),
            ),
      );
}

class _HistoryList extends ConsumerWidget {
  const _HistoryList({required this.teamId});

  final String teamId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final vm = ref.read(teamHistoryViewModelProvider(teamId).notifier);
    return switch (ref.watch(teamHistoryViewModelProvider(teamId))) {
      ViewError(:final error) => ErrorState(error: error, onRetry: vm.refresh),
      ViewLoading() => const SkeletonList(),
      ViewEmpty() => RefreshIndicator(
          onRefresh: vm.refresh,
          child: ListView(children: const [EmptyState(icon: Icons.history, title: 'Chưa có hoạt động', description: 'Các thay đổi của nhóm sẽ hiện ở đây.')]),
        ),
      ViewData(value: final history) => RefreshIndicator(
          onRefresh: vm.refresh,
          child: ListView(
            padding: const EdgeInsets.symmetric(vertical: AppSpacing.s2),
            children: [
              for (final e in history.items)
                ListTile(
                  leading: Icon(_iconOf(e.action)),
                  title: Text(e.summary),
                  subtitle: Text([e.actorName, formatDateTime(e.occurredAt), ?e.reason].join(' · ')),
                ),
              if (history.hasMore)
                Padding(
                  padding: const EdgeInsets.all(AppSpacing.s4),
                  child: history.loading
                      ? const Center(child: CircularProgressIndicator())
                      : OutlinedButton(onPressed: vm.loadMore, child: const Text('Xem thêm')),
                ),
              if (history.loadMoreError != null) ErrorBanner(message: 'Không tải được trang tiếp theo.', onRetry: vm.loadMore),
              if (!history.hasMore)
                Center(child: Padding(padding: const EdgeInsets.all(AppSpacing.s4), child: Text('Đã hết lịch sử', style: theme.textTheme.bodyMedium))),
            ],
          ),
        ),
    };
  }

  static IconData _iconOf(String action) => switch (action) {
        'MEMBER_JOINED' || 'MEMBER_ADD_PENDING' => Icons.person_add_alt_outlined,
        'MEMBER_LEFT' || 'MEMBER_REMOVED' || 'MEMBER_REMOVE_PENDING' => Icons.person_remove_outlined,
        'LEADER_CHANGED' => Icons.star_outline,
        'INVITATION_SENT' || 'INVITATION_CANCELLED' => Icons.mail_outline,
        'JOIN_REQUEST_SUBMITTED' || 'JOIN_REQUEST_CANCELLED' => Icons.how_to_reg_outlined,
        'RECRUITMENT_POST_OPENED' || 'RECRUITMENT_POST_UPDATED' || 'RECRUITMENT_POST_CLOSED' => Icons.campaign_outlined,
        _ => Icons.edit_note,
      };
}
