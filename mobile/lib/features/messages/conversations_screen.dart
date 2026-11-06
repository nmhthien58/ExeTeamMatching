import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/phase.dart';
import '../../core/theme/app_tokens.dart';
import '../../core/view_state.dart';
import '../../shared/widgets/brand_app_bar.dart';
import '../../shared/widgets/states.dart';
import '../realtime/collab_hub.dart';
import '../workspace/format.dart';
import '../workspace/workspace_api.dart' show currentWorkspaceProvider;
import 'conversations_view_model.dart';
import 'messages_api.dart';

/// C02 của sinh viên trong dự án (MB-39): kênh Nhóm và Cố vấn của nhóm mình → C03. Cố vấn mở từ dự án của nhóm cũng dùng màn này
/// (chỉ thấy kênh Cố vấn – quy tắc 22).
/// Từ 06/11/2026 (MB-62) màn này còn là tab Tin nhắn của sinh viên ([inTab]): app bar chung có avatar, chưa có nhóm thì
/// có nút dẫn tới nơi tìm nhóm.
class ConversationsScreen extends ConsumerWidget {
  const ConversationsScreen({super.key, required this.basePath, this.workspaceId, this.inTab = false});

  final String basePath;
  final String? workspaceId;
  final bool inTab;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final workspace = ref.watch(currentWorkspaceProvider(workspaceId));
    return Scaffold(
      appBar: inTab ? const BrandAppBar(title: 'Tin nhắn') : AppBar(title: const Text('Tin nhắn')),
      body: workspace.when(
        loading: () => const SkeletonList(count: 2),
        error: (e, _) => ErrorState(error: e, onRetry: () => ref.invalidate(currentWorkspaceProvider(workspaceId))),
        data: (ws) => ws == null
            ? _NoTeamState(inTab: inTab)
            : _ChannelList(
                channels: ref.watch(channelsProvider(ws.id)),
                onRefresh: () async => ref.invalidate(channelsProvider(ws.id)),
                titleOf: (c) => c.label,
                onOpen: (c) => context.push('$basePath/tin-nhan/${c.id}?ws=${ws.id}'),
              ),
      ),
    );
  }
}

/// C02 của giảng viên / mentor – tab Tin nhắn (MB-39): một dòng kênh Cố vấn cho mỗi nhóm, sắp theo tin mới nhất,
/// lọc "Tất cả / Cần trả lời", số chưa đọc.
class AdvisorConversationsScreen extends ConsumerWidget {
  const AdvisorConversationsScreen({super.key, required this.basePath});

  /// `/gv/tin-nhan` hoặc `/mentor/tin-nhan`.
  final String basePath;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final vm = ref.read(advisorConversationsViewModelProvider.notifier);
    final state = ref.watch(advisorConversationsViewModelProvider);
    final waiting = state.waiting;
    return switch (state.inbox) {
      ViewLoading() || ViewEmpty() => const SkeletonList(),
      ViewError(:final error) => ErrorState(error: error, onRetry: vm.retry),
      ViewData(value: final inbox) => Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s3, AppSpacing.s4, 0),
              child: SegmentedButton<bool>(
                segments: [
                  const ButtonSegment(value: false, label: Text('Tất cả')),
                  ButtonSegment(value: true, label: Text(waiting.isEmpty ? 'Cần trả lời' : 'Cần trả lời (${waiting.length})')),
                ],
                selected: {state.needsReplyOnly},
                onSelectionChanged: (s) => vm.setNeedsReplyOnly(s.first),
              ),
            ),
            Expanded(
              child: switch (inbox.channels) {
                ViewError(:final error) => ErrorState(error: error, onRetry: vm.retry),
                ViewLoading() => const SkeletonList(),
                ViewEmpty() || ViewData() => _ChannelList(
                    channels: AsyncData(inbox.channels.valueOrNull ?? const []),
                    emptyTitle: state.emptyTitle,
                    waiting: waiting,
                    onRefresh: vm.refresh,
                    titleOf: inbox.titleOf,
                    onOpen: (c) => context.push('$basePath/${c.id}?ws=${c.workspaceId}'),
                  ),
              },
            ),
          ],
        ),
    };
  }
}

/// Sinh viên chưa có nhóm: không có kênh nào để nhắn. Ở tab thì chỉ đường đi tìm nhóm (đợt lập nhóm đóng → S31).
class _NoTeamState extends ConsumerWidget {
  const _NoTeamState({required this.inTab});

  final bool inTab;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (!inTab) return const EmptyState(icon: Icons.group_off_outlined, title: 'Bạn chưa có nhóm');
    final formation = ref.watch(shellPhaseProvider).isTeamFormation;
    return EmptyState(
      icon: Icons.forum_outlined,
      title: 'Bạn chưa có nhóm',
      description: formation
          ? 'Tham gia hoặc tạo nhóm để trò chuyện với các thành viên và cố vấn.'
          : 'Đợt lập nhóm đã đóng. Hệ thống sẽ xếp bạn vào một nhóm, khi đó tin nhắn của nhóm hiện ở đây.',
      action: FilledButton(
        onPressed: () => formation ? context.go('/sv/kham-pha') : context.push('/sv/nhom/ghep-tu-dong'),
        child: Text(formation ? 'Tìm nhóm' : 'Xem thông tin ghép tự động'),
      ),
    );
  }
}

class _ChannelList extends StatelessWidget {
  const _ChannelList({
    required this.channels,
    required this.onRefresh,
    required this.titleOf,
    required this.onOpen,
    this.waiting = const {},
    this.emptyTitle = 'Chưa có kênh nào',
  });

  final AsyncValue<List<Channel>> channels;
  final Future<void> Function() onRefresh;
  final String Function(Channel) titleOf;
  final void Function(Channel) onOpen;
  final Set<String> waiting;
  final String emptyTitle;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      children: [
        const RealtimeStatusBanner(),
        Expanded(
          child: RefreshIndicator(
            onRefresh: onRefresh,
            child: channels.when(
              loading: () => const SkeletonList(count: 2),
              error: (e, _) => ErrorState(error: e, onRetry: onRefresh),
              data: (list) => list.isEmpty
                  ? ListView(children: [EmptyState(icon: Icons.forum_outlined, title: emptyTitle)])
                  : ListView(
                      children: [
                        for (final c in list)
                          ListTile(
                            leading: CircleAvatar(child: Icon(c.isTeam ? Icons.groups_outlined : Icons.support_agent_outlined)),
                            // Wrap: tên dài hoặc chữ 130% thì nhãn "Cần trả lời" xuống dòng thay vì tràn (flutter-handoff §5).
                            title: Wrap(
                              spacing: AppSpacing.s2,
                              crossAxisAlignment: WrapCrossAlignment.center,
                              children: [
                                Text(titleOf(c), maxLines: 1, overflow: TextOverflow.ellipsis),
                                if (waiting.contains(c.id))
                                  Chip(visualDensity: VisualDensity.compact, label: const Text('Cần trả lời'), backgroundColor: context.appColors.warningContainer),
                              ],
                            ),
                            subtitle: Text(
                              c.lastMessage == null
                                  ? 'Chưa có tin nhắn'
                                  : '${c.lastMessage!.author.displayName}: ${c.lastMessage!.isDeleted ? 'Tin nhắn đã bị xóa' : c.lastMessage!.body}',
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                            ),
                            trailing: Column(
                              mainAxisAlignment: MainAxisAlignment.center,
                              crossAxisAlignment: CrossAxisAlignment.end,
                              children: [
                                if (c.lastMessage != null) Text(formatRelative(c.lastMessage!.createdAt), style: theme.textTheme.bodySmall),
                                if (c.unreadCount > 0)
                                  Semantics(label: '${c.unreadCount} tin chưa đọc', child: Badge.count(count: c.unreadCount)),
                              ],
                            ),
                            onTap: () => onOpen(c),
                          ),
                      ],
                    ),
            ),
          ),
        ),
      ],
    );
  }
}
