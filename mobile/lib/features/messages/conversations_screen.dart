import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../realtime/collab_hub.dart';
import '../workspace/format.dart';
import '../workspace/workspace_api.dart' show currentWorkspaceProvider, myWorkspacesProvider;
import 'messages_api.dart';

/// C02 của sinh viên trong dự án (MB-39): kênh Nhóm và Cố vấn của nhóm mình → C03. Cố vấn mở từ dự án của nhóm cũng dùng màn này
/// (chỉ thấy kênh Cố vấn – quy tắc 22).
class ConversationsScreen extends ConsumerWidget {
  const ConversationsScreen({super.key, required this.basePath, this.workspaceId});

  final String basePath;
  final String? workspaceId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final workspace = ref.watch(currentWorkspaceProvider(workspaceId));
    return Scaffold(
      appBar: AppBar(title: const Text('Tin nhắn')),
      body: workspace.when(
        loading: () => const SkeletonList(count: 2),
        error: (e, _) => ErrorState(error: e, onRetry: () => ref.invalidate(currentWorkspaceProvider(workspaceId))),
        data: (ws) => ws == null
            ? const EmptyState(icon: Icons.group_off_outlined, title: 'Bạn chưa có nhóm')
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
class AdvisorConversationsScreen extends ConsumerStatefulWidget {
  const AdvisorConversationsScreen({super.key, required this.basePath});

  /// `/gv/tin-nhan` hoặc `/mentor/tin-nhan`.
  final String basePath;

  @override
  ConsumerState<AdvisorConversationsScreen> createState() => _AdvisorConversationsScreenState();
}

class _AdvisorConversationsScreenState extends ConsumerState<AdvisorConversationsScreen> {
  bool _needsReplyOnly = false;

  @override
  Widget build(BuildContext context) {
    final workspaces = ref.watch(myWorkspacesProvider);
    final waiting = ref.watch(openQuestionChannelsProvider).value ?? const <String>{};
    return workspaces.when(
      loading: () => const SkeletonList(),
      error: (e, _) => ErrorState(error: e, onRetry: () => ref.invalidate(myWorkspacesProvider)),
      data: (list) {
        final teamName = {for (final w in list) w.id: w.teamName};
        final perWorkspace = [for (final w in list) ref.watch(channelsProvider(w.id))];
        final error = perWorkspace.where((c) => c.hasError).firstOrNull?.error;
        final loading = perWorkspace.any((c) => c.isLoading);
        final channels = [
          for (final c in perWorkspace) ...?c.value,
        ].where((c) => !_needsReplyOnly || waiting.contains(c.id)).toList()
          ..sort((a, b) => (b.lastMessage?.createdAt ?? DateTime(0)).compareTo(a.lastMessage?.createdAt ?? DateTime(0)));
        return Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s3, AppSpacing.s4, 0),
              child: SegmentedButton<bool>(
                segments: [
                  const ButtonSegment(value: false, label: Text('Tất cả')),
                  ButtonSegment(value: true, label: Text(waiting.isEmpty ? 'Cần trả lời' : 'Cần trả lời (${waiting.length})')),
                ],
                selected: {_needsReplyOnly},
                onSelectionChanged: (s) => setState(() => _needsReplyOnly = s.first),
              ),
            ),
            Expanded(
              child: error != null
                  ? ErrorState(error: error, onRetry: () => ref.invalidate(myWorkspacesProvider))
                  : loading
                      ? const SkeletonList()
                      : _ChannelList(
                          channels: AsyncData(channels),
                          emptyTitle: _needsReplyOnly ? 'Không còn câu hỏi chờ trả lời' : 'Bạn chưa được ghép với nhóm nào',
                          waiting: waiting,
                          onRefresh: () async {
                            ref.invalidate(openQuestionChannelsProvider);
                            for (final w in list) {
                              ref.invalidate(channelsProvider(w.id));
                            }
                          },
                          titleOf: (c) => '${teamName[c.workspaceId] ?? 'Nhóm'} · ${c.label}',
                          onOpen: (c) => context.push('${widget.basePath}/${c.id}?ws=${c.workspaceId}'),
                        ),
            ),
          ],
        );
      },
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
