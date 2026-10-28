import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'notifications_api.dart';

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
  int _count = 0;

  @override
  void initState() {
    super.initState();
    Future<void>.microtask(_load);
    _poll = Timer.periodic(const Duration(seconds: 30), (_) => _load());
  }

  Future<void> _load() async {
    try {
      final count = await ref.read(notificationsApiProvider).unreadCount();
      if (mounted) setState(() => _count = count);
    } catch (_) {
      // Giữ badge gần nhất; màn A06 hiển thị lỗi và nút thử lại.
    }
  }

  @override
  void dispose() {
    _poll?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Badge.count(
    count: _count > 99 ? 99 : _count,
    isLabelVisible: _count > 0,
    child: Icon(widget.icon),
  );
}
