import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/network/api_client.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import 'notifications_api.dart';

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
  List<AppNotification> _items = [];
  int _page = 0;
  int _totalPages = 1;
  bool _loading = true;
  bool _loadingMore = false;
  bool _markingAll = false;
  Object? _error;

  @override
  void initState() {
    super.initState();
    _scroll.addListener(_onScroll);
    Future<void>.microtask(_refresh);
    _poll = Timer.periodic(
      const Duration(seconds: 30),
      (_) => _refresh(silent: true),
    );
  }

  void _onScroll() {
    if (_scroll.hasClients &&
        _scroll.position.extentAfter < AppSpacing.s8 * 2) {
      _nextPage();
    }
  }

  Future<void> _refresh({bool silent = false}) async {
    if (silent && (_page > 1 || _loadingMore || _loading)) return;
    if (!silent && mounted) {
      setState(() {
        _loading = true;
        _error = null;
      });
    }
    try {
      final result = await ref.read(notificationsApiProvider).list();
      if (!mounted) return;
      setState(() {
        _items = result.items;
        _page = 1;
        _totalPages = result.totalPages;
        _loading = false;
        _error = null;
      });
    } catch (error) {
      if (mounted && !silent) {
        setState(() {
          _error = error.apiError;
          _loading = false;
        });
      }
    }
  }

  Future<void> _nextPage() async {
    if (_loading || _loadingMore || _page >= _totalPages) return;
    setState(() => _loadingMore = true);
    try {
      final result = await ref
          .read(notificationsApiProvider)
          .list(page: _page + 1);
      if (!mounted) return;
      setState(() {
        _items = [..._items, ...result.items];
        _page++;
        _totalPages = result.totalPages;
      });
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(error.apiError.title)));
      }
    } finally {
      if (mounted) setState(() => _loadingMore = false);
    }
  }

  Future<void> _read(AppNotification item) async {
    try {
      if (!item.isRead) {
        await ref.read(notificationsApiProvider).read(item.id);
        if (mounted) {
          setState(
            () => _items = [
              for (final row in _items) row.id == item.id ? row.read() : row,
            ],
          );
        }
      }
      if (mounted) _open(item.linkUrl);
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(error.apiError.title)));
      }
    }
  }

  Future<void> _readAll() async {
    if (_markingAll) return;
    setState(() => _markingAll = true);
    try {
      await ref.read(notificationsApiProvider).readAll();
      if (mounted) {
        setState(() => _items = [for (final item in _items) item.read()]);
      }
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(error.apiError.title)));
      }
    } finally {
      if (mounted) setState(() => _markingAll = false);
    }
  }

  void _open(String? link) {
    if (link == null) return;
    final uri = Uri.tryParse(link);
    if (uri == null || uri.scheme != 'app') return;
    final route = switch (uri.host) {
      'teams' => '${widget.roleRoot}/nhom',
      'invitations' when widget.roleRoot == '/sv' => '/sv/loi-moi',
      'invitations' => widget.roleRoot,
      'change-requests' when widget.roleRoot == '/sv' =>
        '/sv/yeu-cau-doi-nhom',
      'change-requests' => '${widget.roleRoot}/nhom',
      'workspaces' when widget.roleRoot == '/sv' => '/sv/du-an',
      'workspaces' when uri.pathSegments.isNotEmpty =>
        '${widget.roleRoot}/du-an/${uri.pathSegments.first}',
      'meeting-schedule' =>
        widget.roleRoot == '/sv'
            ? '/sv/du-an/lich-hop'
            : widget.roleRoot == '/gv'
            ? '/gv/lich'
            : widget.roleRoot,
      'lecturer' => '${widget.roleRoot}/de-tai',
      _ => widget.roleRoot,
    };
    context.go(route);
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
    return Scaffold(
      appBar: AppBar(
        title: const Text('Thông báo'),
        actions: [
          TextButton(
            onPressed: _markingAll || !_items.any((x) => !x.isRead)
                ? null
                : _readAll,
            child: const Text('Đọc tất cả'),
          ),
        ],
      ),
      body: _loading
          ? const SkeletonList()
          : _error != null
          ? ErrorState(error: _error!, onRetry: _refresh)
          : _items.isEmpty
          ? EmptyState(
              icon: Icons.notifications_none,
              title: 'Chưa có thông báo',
              description: 'Thông báo mới sẽ xuất hiện ở đây.',
              action: TextButton(
                onPressed: _refresh,
                child: const Text('Làm mới'),
              ),
            )
          : RefreshIndicator(
              onRefresh: _refresh,
              child: ListView.builder(
                controller: _scroll,
                physics: const AlwaysScrollableScrollPhysics(),
                itemCount: _items.length + (_loadingMore ? 1 : 0),
                itemBuilder: (context, index) {
                  if (index == _items.length) {
                    return const Center(child: CircularProgressIndicator());
                  }
                  final item = _items[index];
                  final date = item.createdAt.toLocal();
                  final previous = index == 0
                      ? null
                      : _items[index - 1].createdAt.toLocal();
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
