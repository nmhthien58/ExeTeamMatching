import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../workspace/format.dart';
import '../workspace/workspace_widgets.dart';
import 'invitations_api.dart';
import 'invitations_view_model.dart';

/// Nhãn trạng thái lời mời / yêu cầu – luôn có chữ, không chỉ màu.
(String, StatusTone) requestStatusChip(String status) => switch (status) {
      'PENDING' => ('Đang chờ', StatusTone.warning),
      'ACCEPTED' => ('Đã chấp nhận', StatusTone.success),
      'DECLINED' => ('Đã từ chối', StatusTone.error),
      'EXPIRED' => ('Hết hạn', StatusTone.neutral),
      _ => ('Đã hủy', StatusTone.neutral),
    };

/// Nhãn trạng thái yêu cầu xin vào nhóm của chính sinh viên (người quyết định là trưởng nhóm).
(String, StatusTone) joinRequestStatusChip(String status) => switch (status) {
      'ACCEPTED' => ('Được chấp nhận', StatusTone.success),
      'DECLINED' => ('Bị từ chối', StatusTone.error),
      _ => requestStatusChip(status),
    };

/// Hỏi xác nhận trước khi hủy yêu cầu xin vào nhóm (S08, S12).
Future<bool> confirmCancelJoinRequest(BuildContext context, String teamName) async =>
    await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Hủy yêu cầu?'),
        content: Text('Trưởng nhóm $teamName sẽ không còn thấy yêu cầu của bạn. Bạn có thể gửi lại sau nếu nhóm còn nhận người.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Không')),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Hủy yêu cầu')),
        ],
      ),
    ) ??
    false;

/// S12 – Lời mời và yêu cầu của tôi (MB-18): tab Lời mời nhận được (thời gian còn lại, Chấp nhận có hộp xác nhận / Từ chối)
/// và tab Yêu cầu đã gửi (trạng thái, Hủy khi còn chờ).
/// [initialTab] = 1 mở thẳng tab Yêu cầu đã gửi (lối vào từ Cài đặt A07).
class InvitationsScreen extends StatelessWidget {
  const InvitationsScreen({super.key, this.initialTab = 0});

  final int initialTab;

  @override
  Widget build(BuildContext context) => DefaultTabController(
        length: 2,
        initialIndex: initialTab,
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
  ReceivedInvitationsViewModel get _vm => ref.read(receivedInvitationsViewModelProvider.notifier);

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
    final outcome = await _vm.accept(invitation);
    if (outcome is! AcceptJoined || !mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(outcome.message)));
    context.go('/sv/nhom');
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final state = ref.watch(receivedInvitationsViewModelProvider);
    final busyId = state.busyId;
    final invitations = ref.watch(myInvitationsProvider);
    return RefreshIndicator(
      onRefresh: () async => _vm.refresh(),
      child: invitations.when(
        loading: () => const SkeletonList(),
        error: (e, _) => ErrorState(error: e, onRetry: _vm.refresh),
        data: (items) => ListView(
          padding: const EdgeInsets.all(AppSpacing.s4),
          children: [
            if (state.error != null) ...[ErrorBanner(message: state.error!), const SizedBox(height: AppSpacing.s3)],
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
                              onPressed: busyId == null ? () => _vm.decline(i) : null,
                              child: const Text('Từ chối'),
                            ),
                            LoadingButton(label: 'Chấp nhận', expand: false, loading: busyId == i.id, onPressed: busyId == null ? () => _accept(i) : null),
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

class _Sent extends ConsumerWidget {
  const _Sent();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final vm = ref.read(sentJoinRequestsViewModelProvider.notifier);
    final error = ref.watch(sentJoinRequestsViewModelProvider);
    final requests = ref.watch(myJoinRequestsProvider);
    return RefreshIndicator(
      onRefresh: () async => vm.refresh(),
      child: requests.when(
        loading: () => const SkeletonList(),
        error: (e, _) => ErrorState(error: e, onRetry: vm.refresh),
        data: (items) => ListView(
          padding: const EdgeInsets.all(AppSpacing.s4),
          children: [
            if (error != null) ...[ErrorBanner(message: error), const SizedBox(height: AppSpacing.s3)],
            if (items.isEmpty) const EmptyState(icon: Icons.outgoing_mail, title: 'Chưa gửi yêu cầu nào'),
            for (final r in items)
              Card.outlined(
                margin: const EdgeInsets.only(bottom: AppSpacing.s3),
                child: ListTile(
                  title: Text(r.team?.name ?? 'Nhóm'),
                  subtitle: Wrap(
                    spacing: AppSpacing.s2,
                    runSpacing: AppSpacing.s1,
                    crossAxisAlignment: WrapCrossAlignment.center,
                    children: [
                      StatusChip(label: joinRequestStatusChip(r.status).$1, tone: joinRequestStatusChip(r.status).$2),
                      if (r.createdAt != null) Text('Gửi lúc ${formatDateTime(r.createdAt!)}'),
                      if (r.status == 'PENDING') Text(timeLeft(r.expiresAt)),
                    ],
                  ),
                  onTap: r.team == null ? null : () => context.push('/sv/kham-pha/nhom/${r.team!.id}'),
                  trailing: r.status == 'PENDING'
                      ? TextButton(
                          onPressed: () async {
                            if (await confirmCancelJoinRequest(context, r.team?.name ?? '')) await vm.cancel(r);
                          },
                          child: const Text('Hủy'),
                        )
                      : null,
                ),
              ),
          ],
        ),
      ),
    );
  }
}
