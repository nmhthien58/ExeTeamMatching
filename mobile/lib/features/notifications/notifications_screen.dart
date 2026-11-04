import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/view_state.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import 'notifications_api.dart';
import 'notifications_view_model.dart';

/// A06 – màn dùng chung cho sinh viên, giảng viên và mentor.
class NotificationsScreen extends ConsumerStatefulWidget {
  const NotificationsScreen({super.key, required this.roleRoot});

  /// `/sv`, `/gv` hoặc `/mentor` – dùng khi mở màn liên quan.
  final String roleRoot;

  @override
  ConsumerState<NotificationsScreen> createState() =>
      _NotificationsScreenState();
}

class _NotificationsScreenState extends ConsumerState<NotificationsScreen> {
  final _scroll = ScrollController();
  Timer? _poll;

  NotificationsViewModel get _vm =>
      ref.read(notificationsViewModelProvider(widget.roleRoot).notifier);

  @override
  void initState() {
    super.initState();
    _scroll.addListener(_onScroll);
    _poll = Timer.periodic(
      const Duration(seconds: 30),
      (_) => _vm.refresh(silent: true),
    );
  }

  void _onScroll() {
    if (_scroll.hasClients &&
        _scroll.position.extentAfter < AppSpacing.s8 * 2) {
      _nextPage();
    }
  }

  Future<void> _refresh() => _vm.refresh();

  Future<void> _nextPage() async => _handle(await _vm.loadMore());

  Future<void> _read(AppNotification item) async =>
      _handle(await _vm.open(item));

  Future<void> _readAll() async => _handle(await _vm.readAll());

  void _handle(NotificationOutcome outcome) {
    if (!mounted) return;
    switch (outcome) {
      case NotificationDone():
        return;
      case NotificationOpen(:final route):
        context.go(route);
      case NotificationFailed(:final message):
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(message)));
    }
  }

  @override
  void dispose() {
    _poll?.cancel();
    _scroll.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final state = ref.watch(notificationsViewModelProvider(widget.roleRoot));
    final loaded = state.list.valueOrNull;
    final items = loaded?.items ?? const <AppNotification>[];
    final loadingMore = loaded?.loadingMore ?? false;
    return Scaffold(
      appBar: AppBar(
        title: const Text('Thông báo'),
        actions: [
          TextButton(
            onPressed: state.markingAll || !(loaded?.hasUnread ?? false)
                ? null
                : _readAll,
            child: const Text('Đọc tất cả'),
          ),
        ],
      ),
      body: switch (state.list) {
        ViewLoading() => const SkeletonList(),
        ViewError(:final error) => ErrorState(error: error, onRetry: _refresh),
        ViewEmpty() => EmptyState(
              icon: Icons.notifications_none,
              title: 'Chưa có thông báo',
              description: 'Thông báo mới sẽ xuất hiện ở đây.',
              action: TextButton(
                onPressed: _refresh,
                child: const Text('Làm mới'),
              ),
            ),
        ViewData() => RefreshIndicator(
              onRefresh: _refresh,
              child: ListView.builder(
                controller: _scroll,
                physics: const AlwaysScrollableScrollPhysics(),
                itemCount: items.length + (loadingMore ? 1 : 0),
                itemBuilder: (context, index) {
                  if (index == items.length) {
                    return const Center(child: CircularProgressIndicator());
                  }
                  final item = items[index];
                  final date = item.createdAt.toLocal();
                  final previous = index == 0
                      ? null
                      : items[index - 1].createdAt.toLocal();
                  final header = previous == null || !_sameDay(previous, date);
                  return Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      if (header)
                        Padding(
                          padding: const EdgeInsets.fromLTRB(
                            AppSpacing.s4,
                            AppSpacing.s4,
                            AppSpacing.s4,
                            AppSpacing.s2,
                          ),
                          child: Text(
                            _dayLabel(date),
                            style: theme.textTheme.titleSmall,
                          ),
                        ),
                      ListTile(
                        onTap: () => _read(item),
                        leading: Icon(
                          item.isRead
                              ? Icons.notifications_outlined
                              : Icons.circle,
                          size: item.isRead ? AppSpacing.s6 : AppSpacing.s3,
                          color: item.isRead
                              ? theme.colorScheme.onSurfaceVariant
                              : theme.colorScheme.primary,
                        ),
                        title: Text(
                          item.title,
                          style: item.isRead
                              ? theme.textTheme.titleSmall
                              : theme.textTheme.titleSmall?.copyWith(
                                  fontWeight: FontWeight.bold,
                                ),
                        ),
                        subtitle: Text(item.content),
                        trailing: Text(
                          '${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}',
                        ),
                      ),
                    ],
                  );
                },
              ),
            ),
      },
    );
  }

  static bool _sameDay(DateTime a, DateTime b) =>
      a.year == b.year && a.month == b.month && a.day == b.day;

  static String _dayLabel(DateTime date) {
    final today = DateTime.now();
    if (_sameDay(today, date)) return 'Hôm nay';
    if (_sameDay(today.subtract(const Duration(days: 1)), date)) {
      return 'Hôm qua';
    }
    return '${date.day.toString().padLeft(2, '0')}/${date.month.toString().padLeft(2, '0')}/${date.year}';
  }
}
