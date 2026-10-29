import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/network/api_exception.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../home/home_api.dart';
import '../my_team/my_team_api.dart' show refreshMyTeam;
import '../workspace/workspace_widgets.dart';
import 'invitations_api.dart';

/// Nhãn trạng thái lời mời / yêu cầu – luôn có chữ, không chỉ màu.
(String, StatusTone) requestStatusChip(String status) => switch (status) {
      'PENDING' => ('Đang chờ', StatusTone.warning),
      'ACCEPTED' => ('Đã chấp nhận', StatusTone.success),
      'DECLINED' => ('Đã từ chối', StatusTone.error),
      'EXPIRED' => ('Hết hạn', StatusTone.neutral),
      _ => ('Đã hủy', StatusTone.neutral),
    };

/// S12 – Lời mời và yêu cầu của tôi (MB-18): tab Lời mời nhận được (thời gian còn lại, Chấp nhận có hộp xác nhận / Từ chối)
/// và tab Yêu cầu đã gửi (trạng thái, Hủy khi còn chờ).
class InvitationsScreen extends StatelessWidget {
  const InvitationsScreen({super.key});

  @override
  Widget build(BuildContext context) => DefaultTabController(
        length: 2,
        child: Scaffold(
          appBar: AppBar(
            title: const Text('Lời mời và yêu cầu'),
            bottom: const TabBar(tabs: [Tab(text: 'Lời mời'), Tab(text: 'Yêu cầu đã gửi')]),
          ),
          body: const TabBarView(children: [_Received(), _Sent()]),
        ),
      );
}

class _Received extends ConsumerStatefulWidget {
  const _Received();

  @override
  ConsumerState<_Received> createState() => _ReceivedState();
}

class _ReceivedState extends ConsumerState<_Received> {
  String? _busyId;
  String? _error;

  Future<void> _accept(MyRequest invitation) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Vào nhóm?'),
        content: Text('Bạn sẽ vào ${invitation.team?.name ?? 'nhóm'}; các lời mời và yêu cầu khác sẽ bị hủy.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy')),
          TextButton(onPressed: () => Navigator.pop(context, true), child: const Text('Vào nhóm')),
        ],
      ),
    );
    if (ok != true) return;
    final applied = await _run(invitation, () async => (await ref.read(invitationsApiProvider).accept(invitation.id)).applied);
    if (applied == null || !mounted) return;
    refreshMyTeam(ref);
    ref.invalidate(homeProvider);
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(applied ? 'Đã vào nhóm' : 'Đã ghi nhận, chờ Bộ môn duyệt')));
    context.go('/sv/nhom');
  }

  Future<T?> _run<T>(MyRequest r, Future<T> Function() call) async {
    setState(() {
      _busyId = r.id;
      _error = null;
    });
    try {
      return await call();
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.detail ?? e.title);
      return null;
    } finally {
      if (mounted) setState(() => _busyId = null);
      ref.invalidate(myInvitationsProvider);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final invitations = ref.watch(myInvitationsProvider);
    return RefreshIndicator(
      onRefresh: () async => ref.invalidate(myInvitationsProvider),
      child: invitations.when(
        loading: () => const SkeletonList(),
        error: (e, _) => ErrorState(error: e, onRetry: () => ref.invalidate(myInvitationsProvider)),
        data: (items) => ListView(
          padding: const EdgeInsets.all(AppSpacing.s4),
          children: [
            if (_error != null) ...[ErrorBanner(message: _error!), const SizedBox(height: AppSpacing.s3)],
            if (items.isEmpty) const EmptyState(icon: Icons.mail_outline, title: 'Chưa có lời mời'),
            for (final i in items)
              Card.outlined(
                margin: const EdgeInsets.only(bottom: AppSpacing.s3),
                child: Padding(
                  padding: const EdgeInsets.all(AppSpacing.s4),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Wrap(
                        alignment: WrapAlignment.spaceBetween,
                        spacing: AppSpacing.s2,
                        children: [
                          Text(i.team?.name ?? 'Nhóm', style: theme.textTheme.titleMedium),
                          StatusChip(label: requestStatusChip(i.status).$1, tone: requestStatusChip(i.status).$2),
                        ],
                      ),
                      if (i.team?.projectName != null) Text(i.team!.projectName!),
                      if (i.message != null) ...[const SizedBox(height: AppSpacing.s2), Text('“${i.message}”')],
                      if (i.status == 'PENDING') ...[
                        Text(timeLeft(i.expiresAt), style: theme.textTheme.bodyMedium),
                        const SizedBox(height: AppSpacing.s2),
                        // OverflowBar: đủ chỗ thì một hàng; 360 dp + chữ 130% thì xếp dọc thay vì tràn (flutter-handoff §5).
                        OverflowBar(
                          alignment: MainAxisAlignment.end,
                          spacing: AppSpacing.s2,
                          overflowAlignment: OverflowBarAlignment.end,
                          overflowSpacing: AppSpacing.s1,
                          children: [
                            if (i.team != null)
                              TextButton(onPressed: () => context.push('/sv/kham-pha/nhom/${i.team!.id}'), child: const Text('Xem nhóm')),
                            TextButton(
                              onPressed: _busyId == null ? () => _run(i, () => ref.read(invitationsApiProvider).decline(i.id)) : null,
                              child: const Text('Từ chối'),
                            ),
                            LoadingButton(label: 'Chấp nhận', expand: false, loading: _busyId == i.id, onPressed: _busyId == null ? () => _accept(i) : null),
                          ],
                        ),
                      ],
                    ],
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _Sent extends ConsumerStatefulWidget {
  const _Sent();

  @override
  ConsumerState<_Sent> createState() => _SentState();
}

class _SentState extends ConsumerState<_Sent> {
  String? _error;

  Future<void> _cancel(MyRequest request) async {
    try {
      await ref.read(invitationsApiProvider).cancelJoinRequest(request.id);
      setState(() => _error = null);
    } on ApiException catch (e) {
      setState(() => _error = e.detail ?? e.title);
    }
    ref.invalidate(myJoinRequestsProvider);
  }

  @override
  Widget build(BuildContext context) {
    final requests = ref.watch(myJoinRequestsProvider);
    return RefreshIndicator(
      onRefresh: () async => ref.invalidate(myJoinRequestsProvider),
      child: requests.when(
        loading: () => const SkeletonList(),
        error: (e, _) => ErrorState(error: e, onRetry: () => ref.invalidate(myJoinRequestsProvider)),
        data: (items) => ListView(
          padding: const EdgeInsets.all(AppSpacing.s4),
          children: [
            if (_error != null) ...[ErrorBanner(message: _error!), const SizedBox(height: AppSpacing.s3)],
            if (items.isEmpty) const EmptyState(icon: Icons.outgoing_mail, title: 'Chưa gửi yêu cầu nào'),
            for (final r in items)
              Card.outlined(
                margin: const EdgeInsets.only(bottom: AppSpacing.s3),
                child: ListTile(
                  title: Text(r.team?.name ?? 'Nhóm'),
                  subtitle: Text(r.status == 'PENDING' ? timeLeft(r.expiresAt) : requestStatusChip(r.status).$1),
                  onTap: r.team == null ? null : () => context.push('/sv/kham-pha/nhom/${r.team!.id}'),
                  trailing: r.status == 'PENDING'
                      ? TextButton(onPressed: () => _cancel(r), child: const Text('Hủy'))
                      : StatusChip(label: requestStatusChip(r.status).$1, tone: requestStatusChip(r.status).$2),
                ),
              ),
          ],
        ),
      ),
    );
  }
}
