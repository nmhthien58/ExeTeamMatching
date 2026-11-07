import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/phase.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/brand_header.dart';
import '../../shared/widgets/deadline_banner.dart';
import '../../shared/widgets/states.dart';
import '../../shared/widgets/team_requirement_bar.dart';
import '../suggestions/suggestion_widgets.dart';
import '../suggestions/suggestions_api.dart';
import 'home_api.dart';

/// S01 – Trang chủ sinh viên (MB-11, flutter-handoff S01): lời chào, banner hạn chót, thẻ việc nên làm,
/// thẻ nhóm có TeamRequirementBar, lời mời / yêu cầu đang chờ, nhóm gợi ý khi chưa có nhóm.
class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final home = ref.watch(homeProvider);
    return RefreshIndicator(
      onRefresh: () async {
        ref.invalidate(homeProvider);
        ref.invalidate(teamsForMeProvider);
      },
      child: home.when(
        loading: () => const SkeletonList(),
        error: (e, _) => ErrorState(error: e, onRetry: () => ref.invalidate(homeProvider)),
        data: (h) => _Home(home: h),
      ),
    );
  }
}

class _Home extends ConsumerWidget {
  const _Home({required this.home});

  final HomeData home;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final deadline = ref.watch(shellPhaseProvider).studentPhase?.teamFormationDeadlineAt;
    final team = home.team;
    return ListView(
      padding: const EdgeInsets.all(AppSpacing.s4),
      children: [
        // Header thương hiệu: logo, môn / học kỳ, chip giai đoạn, lời chào, tuần học, tiến độ học kỳ.
        const BrandHeader(),
        const SizedBox(height: AppSpacing.s4),
        if (home.isTeamFormation && deadline != null) ...[
          DeadlineBanner(deadline: deadline, title: 'Hạn lập nhóm'),
          const SizedBox(height: AppSpacing.s3),
        ],
        if (home.isAutoAssigned)
          _Row(icon: Icons.auto_awesome_outlined, title: 'Bạn được ghép nhóm tự động', path: '/sv/nhom/ghep-tu-dong'),
        ?_nextAction(context, home),
        if (team != null) ...[
          const SizedBox(height: AppSpacing.s3),
          Card.outlined(
            margin: EdgeInsets.zero,
            clipBehavior: Clip.antiAlias,
            child: InkWell(
              onTap: () => context.go('/sv/nhom'),
              child: Padding(
                padding: const EdgeInsets.all(AppSpacing.s4),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(team.name, style: theme.textTheme.titleMedium),
                    if (team.projectName != null) Text(team.projectName!, style: theme.textTheme.bodyMedium),
                    const SizedBox(height: AppSpacing.s2),
                    TeamRequirementBar(memberCount: team.memberCount, maxMembers: team.maxSize),
                  ],
                ),
              ),
            ),
          ),
        ],
        if (home.pendingInvitationCount > 0)
          _Row(icon: Icons.mail_outline, title: 'Lời mời vào nhóm', count: home.pendingInvitationCount, path: '/sv/loi-moi'),
        if (home.pendingJoinRequestCount > 0)
          _Row(icon: Icons.mark_email_unread_outlined, title: 'Yêu cầu tham gia', count: home.pendingJoinRequestCount, path: '/sv/nhom/yeu-cau'),
        if (team == null && home.isTeamFormation) const _Suggestions(),
      ],
    );
  }

  /// Thẻ "việc nên làm" theo nextAction của API; null khi không có việc gì.
  static Widget? _nextAction(BuildContext context, HomeData h) {
    final (String title, String? description, String? button, String? path) = switch (h.nextAction) {
      'CREATE_OR_JOIN' => ('Bạn chưa có nhóm', 'Tạo nhóm mới hoặc xin vào một nhóm đang tuyển.', 'Tạo nhóm', '/sv/nhom/tao'),
      'INVITE_MEMBERS' => ('Nhóm cần thêm thành viên', 'Nhóm cần ${h.team?.minSize ?? 4}–${h.team?.maxSize ?? 6} người.', h.isLeader ? 'Mời thành viên' : null, '/sv/nhom/goi-y'),
      'ADD_SPECIALIZATION' => ('Nhóm cần thêm chuyên ngành', 'Nhóm cần ít nhất ${h.team?.minSpecializations ?? 2} chuyên ngành.', h.isLeader ? 'Gợi ý thành viên' : null, '/sv/nhom/goi-y'),
      'CHOOSE_LECTURER' => ('Chọn giảng viên', 'Thêm nguyện vọng giảng viên trước hạn.', 'Xem nhóm', '/sv/nhom'),
      'SUBMIT_CHANGE_REQUEST' => ('Gửi yêu cầu đổi nhóm', 'Nhóm có thay đổi chưa gửi Bộ môn.', 'Xem nhóm', '/sv/nhom'),
      'WAIT_REVIEW' => ('Đang chờ Bộ môn duyệt', 'Yêu cầu đổi nhóm đang được xem xét.', null, null),
      'OPEN_WORKSPACE' => ('Nhóm đã chốt', 'Làm việc cùng nhóm trong Dự án.', 'Mở dự án', '/sv/du-an'),
      _ => ('', null, null, null),
    };
    if (title.isEmpty) return null;
    final theme = Theme.of(context);
    return Card.outlined(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.s4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(title, style: theme.textTheme.titleMedium),
            if (description != null) Text(description, style: theme.textTheme.bodyMedium),
            if (button != null) ...[
              const SizedBox(height: AppSpacing.s3),
              Wrap(
                spacing: AppSpacing.s2,
                children: [
                  FilledButton(onPressed: () => path!.startsWith('/sv/nhom/') ? context.push(path) : context.go(path), child: Text(button)),
                  if (h.nextAction == 'CREATE_OR_JOIN') OutlinedButton(onPressed: () => context.go('/sv/kham-pha'), child: const Text('Khám phá')),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _Row extends StatelessWidget {
  const _Row({required this.icon, required this.title, required this.path, this.count});

  final IconData icon;
  final String title;
  final String path;
  final int? count;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(top: AppSpacing.s3),
        child: Card.outlined(
          margin: EdgeInsets.zero,
          child: ListTile(
            leading: count == null ? Icon(icon) : Badge.count(count: count!, child: Icon(icon)),
            title: Text(title),
            trailing: const Icon(Icons.chevron_right),
            onTap: () => context.push(path),
          ),
        ),
      );
}

/// "Nhóm phù hợp với bạn" – 3 gợi ý đầu của Matching (S07 của Nguyên), mức phù hợp chỉ để tham khảo.
class _Suggestions extends ConsumerWidget {
  const _Suggestions();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final items = ref.watch(teamsForMeProvider).value ?? const [];
    if (items.isEmpty) return const SizedBox.shrink();
    return Column(
      // stretch: thẻ gợi ý rộng hết hàng; start làm thẻ co theo nội dung (nhóm tên ngắn bị hẹp hơn).
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const SizedBox(height: AppSpacing.s4),
        Row(
          children: [
            Expanded(child: Text('Nhóm phù hợp với bạn', style: Theme.of(context).textTheme.titleMedium)),
            TextButton(onPressed: () => context.push('/sv/kham-pha/goi-y'), child: const Text('Xem tất cả')),
          ],
        ),
        for (final s in items.take(3))
          Padding(
            padding: const EdgeInsets.only(bottom: AppSpacing.s3),
            child: TeamSuggestionCard(suggestion: s, onTap: () => context.push('/sv/kham-pha/nhom/${s.team.id}')),
          ),
      ],
    );
  }
}
