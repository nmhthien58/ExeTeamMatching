import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../workspace/workspace_api.dart' show currentWorkspaceProvider;
import 'messages_api.dart';

/// Icon tab Tin nhắn của sinh viên (MB-62): badge = tổng tin chưa đọc của các kênh trong workspace của nhóm mình.
/// Làm mới 30 giây một lần như badge Thông báo; chưa có nhóm hoặc lỗi mạng thì không hiện số.
class MessagesTabIcon extends ConsumerStatefulWidget {
  const MessagesTabIcon({super.key, required this.icon});

  final IconData icon;

  @override
  ConsumerState<MessagesTabIcon> createState() => _MessagesTabIconState();
}

class _MessagesTabIconState extends ConsumerState<MessagesTabIcon> {
  Timer? _poll;

  @override
  void initState() {
    super.initState();
    _poll = Timer.periodic(const Duration(seconds: 30), (_) {
      final ws = ref.read(currentWorkspaceProvider(null)).value;
      if (mounted && ws != null) ref.invalidate(channelsProvider(ws.id));
    });
  }

  @override
  void dispose() {
    _poll?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final ws = ref.watch(currentWorkspaceProvider(null)).value;
    final channels = ws == null ? const <Channel>[] : ref.watch(channelsProvider(ws.id)).value ?? const <Channel>[];
    final count = channels.fold<int>(0, (sum, c) => sum + c.unreadCount);
    return Badge.count(count: count > 99 ? 99 : count, isLabelVisible: count > 0, child: Icon(widget.icon));
  }
}
