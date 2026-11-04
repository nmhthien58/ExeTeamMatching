import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'notifications_view_model.dart';

/// Biểu tượng A06 cho NavigationDestination: tự cập nhật badge khi ứng dụng đang mở.
class NotificationTabIcon extends ConsumerStatefulWidget {
  const NotificationTabIcon({super.key, required this.icon});

  final IconData icon;

  @override
  ConsumerState<NotificationTabIcon> createState() =>
      _NotificationTabIconState();
}

class _NotificationTabIconState extends ConsumerState<NotificationTabIcon> {
  Timer? _poll;

  @override
  void initState() {
    super.initState();
    Future<void>.microtask(_load);
    _poll = Timer.periodic(const Duration(seconds: 30), (_) => _load());
  }

  Future<void> _load() async {
    if (mounted) await ref.read(unreadCountViewModelProvider.notifier).load();
  }

  @override
  void dispose() {
    _poll?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final count = ref.watch(unreadCountViewModelProvider);
    return Badge.count(
      count: count > 99 ? 99 : count,
      isLabelVisible: count > 0,
      child: Icon(widget.icon),
    );
  }
}
