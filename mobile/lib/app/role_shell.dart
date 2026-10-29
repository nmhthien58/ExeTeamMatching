import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/phase.dart';
import '../core/theme/app_tokens.dart';
import '../shared/widgets/states.dart';
import '../features/notifications/notification_tab_icon.dart';
import 'navigation.dart';

/// Shell theo vai trò (MB-05, MB-37): tab bar dưới, mục đổi theo giai đoạn. Màn cấp 1 không có nút Back.
class RoleShell extends ConsumerWidget {
  const RoleShell({super.key, required this.shell, required this.location, required this.child});

  final Shell shell;
  final String location;
  final Widget child;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final phase = ref.watch(shellPhaseProvider);
    final items = roleNav(shell, isTeamFormation: phase.isTeamFormation, isMentoringWindow: phase.isMentoringWindow);
    final home = shellHome[shell]!;
    // Tab đang chọn: khớp dài nhất; trang chủ chỉ khớp chính xác.
    var index = items.indexWhere((i) => i.path != home && location.startsWith(i.path));
    if (index < 0) index = items.indexWhere((i) => i.path == home);

    return Scaffold(
      body: child,
      bottomNavigationBar: NavigationBar(
        selectedIndex: index < 0 ? 0 : index,
        onDestinationSelected: (i) => context.go(items[i].path),
        destinations: [
          for (final item in items)
            NavigationDestination(
              icon: item.screen == 'A06' ? NotificationTabIcon(icon: item.icon) : Icon(item.icon),
              selectedIcon: item.screen == 'A06' ? NotificationTabIcon(icon: item.selectedIcon) : Icon(item.selectedIcon),
              label: item.label,
            ),
        ],
      ),
    );
  }
}

/// Chỗ giữ cho màn hình chưa làm: hiện mã màn hình và người phụ trách. Người làm thay bằng màn hình thật
/// trong `lib/features/<feature>/` rồi gắn vào `lib/app/router.dart`.
class ScreenPlaceholder extends StatelessWidget {
  const ScreenPlaceholder({super.key, required this.item});

  final NavItem item;

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: Text(item.label), automaticallyImplyLeading: false),
        body: EmptyState(
          icon: Icons.construction,
          title: '${item.screen} · ${item.label}',
          description: 'Màn hình đang được làm – người phụ trách: ${item.owner}.',
        ),
      );
}

/// Bọc màn Khám phá (S05, S08, S10): đợt lập nhóm đã đóng thì mở hộp nhắc thay vì màn hình (quy tắc 18).
class TeamFormationOnly extends ConsumerStatefulWidget {
  const TeamFormationOnly({super.key, required this.child});

  final Widget child;

  @override
  ConsumerState<TeamFormationOnly> createState() => _TeamFormationOnlyState();
}

class _TeamFormationOnlyState extends ConsumerState<TeamFormationOnly> {
  bool _shown = false;

  @override
  Widget build(BuildContext context) {
    final phase = ref.watch(shellPhaseProvider);
    if (phase.isTeamFormation) return widget.child;
    if (!_shown) {
      _shown = true;
      WidgetsBinding.instance.addPostFrameCallback((_) async {
        final openProject = await showPhaseClosedDialog(context, closedAt: phase.studentPhase?.teamFormationDeadlineAt);
        if (context.mounted) context.go(openProject ? '/sv/du-an' : '/sv');
      });
    }
    return const Scaffold(body: SizedBox(height: AppSpacing.s8));
  }
}
