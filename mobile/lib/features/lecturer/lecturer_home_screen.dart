import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/phase.dart';
import '../../core/providers.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import 'lecturer_home_api.dart';
import 'lecturer_home_models.dart';

class LecturerHomeScreen extends ConsumerStatefulWidget {
  const LecturerHomeScreen({super.key});

  @override
  ConsumerState<LecturerHomeScreen> createState() => _LecturerHomeScreenState();
}

class _LecturerHomeScreenState extends ConsumerState<LecturerHomeScreen>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 2, vsync: this);
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  Future<void> _handleOpenInterestWithUndo(
    BuildContext context,
    String teamId,
    String teamName,
  ) async {
    try {
      await ref.read(lecturerHomeApiProvider).openInterest(teamId);
      ref.invalidate(lecturerMyTopicInterestsProvider);
      ref.invalidate(lecturerPreferencesMeProvider);
      ref.invalidate(lecturerTopicRecommendationsProvider);

      if (!context.mounted) return;
      ScaffoldMessenger.of(context).hideCurrentSnackBar();
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Đã mở quan tâm nhóm "$teamName"'),
          duration: const Duration(seconds: 5),
          action: SnackBarAction(
            label: 'Hoàn tác',
            onPressed: () async {
              try {
                final myInterests = await ref.read(lecturerHomeApiProvider).getMyTopicInterests();
                final current = myInterests.where((i) => i.teamId == teamId && i.status == 'OPEN').firstOrNull;
                if (current != null) {
                  await ref.read(lecturerHomeApiProvider).withdrawInterest(current.id);
                  ref.invalidate(lecturerMyTopicInterestsProvider);
                  ref.invalidate(lecturerPreferencesMeProvider);
                }
              } catch (_) {}
            },
          ),
        ),
      );
    } catch (e) {
      if (!context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(e is Exception ? e.toString().replaceAll('Exception: ', '') : 'Không thể mở quan tâm'),
          backgroundColor: Theme.of(context).colorScheme.error,
        ),
      );
    }
  }

  Future<void> _handleWithdrawInterestWithUndo(
    BuildContext context,
    String interestId,
    String teamId,
    String teamName,
  ) async {
    try {
      await ref.read(lecturerHomeApiProvider).withdrawInterest(interestId);
      ref.invalidate(lecturerMyTopicInterestsProvider);
      ref.invalidate(lecturerPreferencesMeProvider);
      ref.invalidate(lecturerTopicRecommendationsProvider);

      if (!context.mounted) return;
      ScaffoldMessenger.of(context).hideCurrentSnackBar();
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Đã rút quan tâm nhóm "$teamName"'),
          duration: const Duration(seconds: 5),
          action: SnackBarAction(
            label: 'Hoàn tác',
            onPressed: () async {
              try {
                await ref.read(lecturerHomeApiProvider).openInterest(teamId);
                ref.invalidate(lecturerMyTopicInterestsProvider);
                ref.invalidate(lecturerPreferencesMeProvider);
              } catch (_) {}
            },
          ),
        ),
      );
    } catch (e) {
      if (!context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(e is Exception ? e.toString().replaceAll('Exception: ', '') : 'Không thể rút quan tâm'),
          backgroundColor: Theme.of(context).colorScheme.error,
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final user = ref.watch(authControllerProvider).value;
    final shellPhase = ref.watch(shellPhaseProvider);
    final isTeamFormation = shellPhase.isTeamFormation;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Trang chủ'),
        actions: [
          IconButton(
            icon: const Icon(Icons.notifications_outlined),
            tooltip: 'Thông báo',
            onPressed: () => context.push('/gv/thong-bao'),
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(phasesProvider);
          if (isTeamFormation) {
            ref.invalidate(lecturerPreferencesMeProvider);
            ref.invalidate(lecturerMyTopicInterestsProvider);
            ref.invalidate(lecturerTopicRecommendationsProvider);
          } else {
            ref.invalidate(lecturerSemesterStatsProvider);
          }
        },
        child: ListView(
          padding: const EdgeInsets.all(AppSpacing.s4),
          children: [
            // Lời chào & vai trò
            Row(
              children: [
                CircleAvatar(
                  radius: 24,
                  backgroundColor: theme.colorScheme.primaryContainer,
                  child: Text(
                    (user?.fullName != null && user!.fullName.isNotEmpty)
                        ? user.fullName[0]
                        : 'G',
                    style: TextStyle(
                      fontSize: 18,
                      fontWeight: FontWeight.bold,
                      color: theme.colorScheme.primary,
                    ),
                  ),
                ),
                const SizedBox(width: AppSpacing.s3),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Xin chào, ${user?.fullName ?? 'Thầy/Cô'}',
                        style: theme.textTheme.titleMedium?.copyWith(
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      Text(
                        isTeamFormation
                            ? 'Đợt lập nhóm · Chọn nguyện vọng & quan tâm'
                            : 'Học kỳ đang diễn ra · Theo dõi & hướng dẫn',
                        style: theme.textTheme.bodySmall?.copyWith(
                          color: theme.colorScheme.onSurfaceVariant,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.s4),

            if (isTeamFormation)
              _buildTeamFormationView(context)
            else
              _buildInSemesterView(context),
          ],
        ),
      ),
    );
  }

  // ===================== GIAI ĐOẠN LẬP NHÓM =====================
  Widget _buildTeamFormationView(BuildContext context) {
    final theme = Theme.of(context);
    final preferencesMeAsync = ref.watch(lecturerPreferencesMeProvider);
    final myInterestsAsync = ref.watch(lecturerMyTopicInterestsProvider);
    final recommendationsAsync = ref.watch(lecturerTopicRecommendationsProvider);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        // Thẻ số liệu tổng quan 2 cột (DESIGN.md 1.5, chiều cao bằng nhau)
        Row(
          children: [
            Expanded(
              child: _buildMetricCard(
                context,
                title: 'Nhóm chọn tôi',
                value: '${preferencesMeAsync.value?.length ?? 0}',
                subtitle: 'Xếp bạn vào NV1–3',
                icon: Icons.how_to_reg_outlined,
                color: theme.colorScheme.primary,
                bgColor: theme.colorScheme.primaryContainer,
              ),
            ),
            const SizedBox(width: AppSpacing.s3),
            Expanded(
              child: _buildMetricCard(
                context,
                title: 'Đang quan tâm',
                value: '${myInterestsAsync.value?.where((i) => i.status == 'OPEN').length ?? 0}',
                subtitle: 'Đã mở quan tâm',
                icon: Icons.favorite_outline,
                color: Palette.infoDark,
                bgColor: Palette.infoContainer,
              ),
            ),
          ],
        ),
        const SizedBox(height: AppSpacing.s4),

        // Tab bar giữa "Nhóm chọn tôi" và "Đang quan tâm"
        Container(
          decoration: BoxDecoration(
            border: Border(bottom: BorderSide(color: theme.colorScheme.outlineVariant)),
          ),
          child: TabBar(
            controller: _tabController,
            labelColor: theme.colorScheme.primary,
            indicatorColor: theme.colorScheme.primary,
            unselectedLabelColor: theme.colorScheme.onSurfaceVariant,
            tabs: [
              Tab(
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    const Text('Nhóm chọn tôi'),
                    if ((preferencesMeAsync.value?.length ?? 0) > 0) ...[
                      const SizedBox(width: 6),
                      _buildCountBadge(context, preferencesMeAsync.value!.length, theme.colorScheme.primaryContainer, theme.colorScheme.onPrimaryContainer),
                    ],
                  ],
                ),
              ),
              Tab(
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    const Text('Đang quan tâm'),
                    if ((myInterestsAsync.value?.where((i) => i.status == 'OPEN').length ?? 0) > 0) ...[
                      const SizedBox(width: 6),
                      _buildCountBadge(context, myInterestsAsync.value!.where((i) => i.status == 'OPEN').length, Palette.infoContainer, Palette.infoDark),
                    ],
                  ],
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: AppSpacing.s3),

        // Nội dung của tab
        SizedBox(
          height: 320,
          child: TabBarView(
            controller: _tabController,
            children: [
              // Tab 1: Nhóm chọn tôi
              preferencesMeAsync.when(
                loading: () => const Center(child: CircularProgressIndicator()),
                error: (err, _) => ErrorState(
                  error: err,
                  onRetry: () => ref.invalidate(lecturerPreferencesMeProvider),
                ),
                data: (preferences) {
                  if (preferences.isEmpty) {
                    return const EmptyState(
                      icon: Icons.groups_outlined,
                      title: 'Chưa có nhóm nào chọn bạn',
                      description: 'Khi các nhóm xếp bạn vào nguyện vọng 1–3, thông tin sẽ hiển thị tại đây.',
                    );
                  }
                  return ListView.builder(
                    itemCount: preferences.length,
                    itemBuilder: (context, i) => _buildPreferenceMeCard(context, preferences[i]),
                  );
                },
              ),

              // Tab 2: Đang quan tâm
              myInterestsAsync.when(
                loading: () => const Center(child: CircularProgressIndicator()),
                error: (err, _) => ErrorState(
                  error: err,
                  onRetry: () => ref.invalidate(lecturerMyTopicInterestsProvider),
                ),
                data: (interests) {
                  final openInterests = interests.where((i) => i.status == 'OPEN').toList();
                  if (openInterests.isEmpty) {
                    return const EmptyState(
                      icon: Icons.favorite_border,
                      title: 'Chưa quan tâm nhóm nào',
                      description: 'Khám phá các đề tài gợi ý phía dưới để mở quan tâm đến các nhóm phù hợp chuyên môn.',
                    );
                  }
                  return ListView.builder(
                    itemCount: openInterests.length,
                    itemBuilder: (context, i) => _buildOpenInterestCard(context, openInterests[i]),
                  );
                },
              ),
            ],
          ),
        ),

        const SizedBox(height: AppSpacing.s6),

        // Section: Đề tài gợi ý mới
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Text(
              'Đề tài gợi ý cho bạn',
              style: theme.textTheme.titleMedium?.copyWith(
                fontWeight: FontWeight.bold,
              ),
            ),
            TextButton(
              onPressed: () => context.push('/gv/de-tai'),
              child: const Text('Xem tất cả'),
            ),
          ],
        ),
        const SizedBox(height: AppSpacing.s2),

        recommendationsAsync.when(
          loading: () => const Center(
            child: Padding(
              padding: EdgeInsets.all(AppSpacing.s4),
              child: CircularProgressIndicator(),
            ),
          ),
          error: (err, _) => ErrorState(
            error: err,
            onRetry: () => ref.invalidate(lecturerTopicRecommendationsProvider),
          ),
          data: (recommendations) {
            if (recommendations.isEmpty) {
              return const Card.outlined(
                child: Padding(
                  padding: EdgeInsets.all(AppSpacing.s4),
                  child: Center(
                    child: Text('Chưa có đề tài gợi ý mới trong học kỳ này.'),
                  ),
                ),
              );
            }

            return Column(
              children: [
                for (final rec in recommendations.take(5))
                  _buildRecommendationCard(context, rec),
              ],
            );
          },
        ),
      ],
    );
  }

  // ===================== GIAI ĐOẠN HỌC KỲ =====================
  Widget _buildInSemesterView(BuildContext context) {
    final theme = Theme.of(context);
    final statsAsync = ref.watch(lecturerSemesterStatsProvider);

    return statsAsync.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (err, _) => ErrorState(
        error: err,
        onRetry: () => ref.invalidate(lecturerSemesterStatsProvider),
      ),
      data: (stats) {
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Ba thẻ số liệu bấm được (Rule 22, 23, 25, 27)
            Row(
              children: [
                Expanded(
                  child: _buildMetricCard(
                    context,
                    title: 'Cần trả lời',
                    value: '${stats.needsReplyCount}',
                    subtitle: 'Câu hỏi / tin nhắn',
                    icon: Icons.question_answer_outlined,
                    color: Palette.warning,
                    bgColor: Palette.warningContainer,
                    onTap: () => context.push('/gv/tin-nhan'),
                  ),
                ),
                const SizedBox(width: AppSpacing.s2),
                Expanded(
                  child: _buildMetricCard(
                    context,
                    title: 'Trễ hạn nộp',
                    value: '${stats.overdueSubmissionsCount}',
                    subtitle: 'Việc / bài tập',
                    icon: Icons.assignment_late_outlined,
                    color: Palette.error,
                    bgColor: Palette.errorContainer,
                  ),
                ),
                const SizedBox(width: AppSpacing.s2),
                Expanded(
                  child: _buildMetricCard(
                    context,
                    title: stats.meetingsTodayCount > 0 ? 'Họp hôm nay' : 'Thiếu CP',
                    value: stats.meetingsTodayCount > 0 ? '${stats.meetingsTodayCount}' : '${stats.missingCheckpointsCount}',
                    subtitle: stats.meetingsTodayCount > 0 ? 'Buổi họp' : 'Nhóm chưa CP',
                    icon: stats.meetingsTodayCount > 0 ? Icons.event_available : Icons.flag_outlined,
                    color: Palette.success,
                    bgColor: Palette.successContainer,
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.s4),

            // Thẻ nhắc "Đăng lịch họp hằng tuần" (nếu chưa đăng, Rule 27)
            if (!stats.hasPostedWeeklySchedule) ...[
              Card(
                elevation: 0,
                color: Palette.infoContainer,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(AppRadius.lg),
                  side: const BorderSide(color: Palette.info),
                ),
                child: Padding(
                  padding: const EdgeInsets.all(AppSpacing.s4),
                  child: Row(
                    children: [
                      const Icon(Icons.event_note, size: 36, color: Palette.infoDark),
                      const SizedBox(width: AppSpacing.s3),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              'Đăng lịch họp hằng tuần (C09)',
                              style: theme.textTheme.titleSmall?.copyWith(
                                fontWeight: FontWeight.bold,
                                color: Palette.infoDark,
                              ),
                            ),
                            const SizedBox(height: 2),
                            Text(
                              'Thêm các khung giờ rảnh để các nhóm EXE201 đăng ký lịch hướng dẫn hằng tuần.',
                              style: theme.textTheme.bodySmall?.copyWith(
                                color: theme.colorScheme.onSurfaceVariant,
                              ),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(width: AppSpacing.s2),
                      FilledButton.tonal(
                        onPressed: () => context.push('/gv/lich'),
                        child: const Text('Đăng lịch'),
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: AppSpacing.s4),
            ],

            // Section: Nhóm đang hướng dẫn
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  'Nhóm đang hướng dẫn',
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
                ),
                TextButton(
                  onPressed: () => context.push('/gv/nhom'),
                  child: const Text('Xem tất cả'),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.s2),

            if (stats.assignedTeams.isEmpty)
              const Card.outlined(
                child: Padding(
                  padding: EdgeInsets.all(AppSpacing.s6),
                  child: Center(
                    child: Text('Chưa có nhóm nào được phân công hướng dẫn.'),
                  ),
                ),
              )
            else
              Column(
                children: [
                  for (final team in stats.assignedTeams)
                    _buildAssignedTeamCard(context, team),
                ],
              ),
          ],
        );
      },
    );
  }

  // ===================== HELPER WIDGETS =====================
  Widget _buildMetricCard(
    BuildContext context, {
    required String title,
    required String value,
    required String subtitle,
    required IconData icon,
    required Color color,
    required Color bgColor,
    VoidCallback? onTap,
  }) {
    final theme = Theme.of(context);
    return Card(
      elevation: 0,
      margin: EdgeInsets.zero,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.lg),
        side: BorderSide(color: theme.colorScheme.outlineVariant),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.lg),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.s3),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(
                    title,
                    style: theme.textTheme.labelMedium?.copyWith(
                      color: theme.colorScheme.onSurfaceVariant,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                  Container(
                    padding: const EdgeInsets.all(4),
                    decoration: BoxDecoration(
                      color: bgColor,
                      shape: BoxShape.circle,
                    ),
                    child: Icon(icon, size: 16, color: color),
                  ),
                ],
              ),
              const SizedBox(height: AppSpacing.s1),
              Text(
                value,
                style: theme.textTheme.headlineSmall?.copyWith(
                  fontWeight: FontWeight.bold,
                  color: color,
                ),
              ),
              Text(
                subtitle,
                style: theme.textTheme.labelSmall?.copyWith(
                  color: theme.colorScheme.onSurfaceVariant,
                  fontSize: 10,
                ),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildCountBadge(BuildContext context, int count, Color bgColor, Color textColor) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 1),
      decoration: BoxDecoration(
        color: bgColor,
        borderRadius: BorderRadius.circular(AppRadius.full),
      ),
      child: Text(
        '$count',
        style: TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.bold,
          color: textColor,
        ),
      ),
    );
  }

  Widget _buildPreferenceMeCard(BuildContext context, LecturerPreferenceMe item) {
    final theme = Theme.of(context);
    final rankTone = item.rank == 1
        ? theme.colorScheme.primaryContainer
        : Palette.neutralContainer;
    final rankColor = item.rank == 1
        ? theme.colorScheme.onPrimaryContainer
        : Palette.textSecondary;

    return Card(
      elevation: 0,
      margin: const EdgeInsets.only(bottom: AppSpacing.s2),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.lg),
        side: BorderSide(color: theme.colorScheme.outlineVariant),
      ),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.s3),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Flexible(
                            child: Text(
                              item.teamName,
                              style: theme.textTheme.titleSmall?.copyWith(
                                fontWeight: FontWeight.bold,
                              ),
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                          const SizedBox(width: 6),
                          Container(
                            padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 1),
                            decoration: BoxDecoration(
                              color: theme.colorScheme.surfaceContainerHighest,
                              borderRadius: BorderRadius.circular(AppRadius.sm),
                            ),
                            child: Text(
                              item.courseCode,
                              style: theme.textTheme.labelSmall?.copyWith(fontSize: 10),
                            ),
                          ),
                        ],
                      ),
                      if (item.projectName != null) ...[
                        const SizedBox(height: 2),
                        Text(
                          item.projectName!,
                          style: theme.textTheme.bodySmall?.copyWith(
                            color: theme.colorScheme.onSurfaceVariant,
                          ),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ],
                    ],
                  ),
                ),
                // Badge Nguyện vọng
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                  decoration: BoxDecoration(
                    color: rankTone,
                    borderRadius: BorderRadius.circular(AppRadius.full),
                  ),
                  child: Text(
                    'Nguyện vọng ${item.rank}',
                    style: TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.bold,
                      color: rankColor,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.s2),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  '${item.memberCount} thành viên',
                  style: theme.textTheme.labelSmall?.copyWith(
                    color: theme.colorScheme.onSurfaceVariant,
                  ),
                ),
                if (item.iAmInterested)
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                    decoration: BoxDecoration(
                      color: Palette.successContainer,
                      borderRadius: BorderRadius.circular(AppRadius.full),
                    ),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        const Icon(Icons.check_circle, size: 12, color: Palette.success),
                        const SizedBox(width: 4),
                        Text(
                          'Hai bên cùng chọn',
                          style: theme.textTheme.labelSmall?.copyWith(
                            color: Palette.success,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ],
                    ),
                  )
                else
                  FilledButton.tonal(
                    style: FilledButton.styleFrom(
                      padding: const EdgeInsets.symmetric(horizontal: 10),
                      visualDensity: VisualDensity.compact,
                    ),
                    onPressed: () => _handleOpenInterestWithUndo(
                      context,
                      item.teamId,
                      item.teamName,
                    ),
                    child: const Text('Quan tâm lại'),
                  ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildOpenInterestCard(BuildContext context, TopicInterestItem item) {
    final theme = Theme.of(context);
    return Card(
      elevation: 0,
      margin: const EdgeInsets.only(bottom: AppSpacing.s2),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.lg),
        side: BorderSide(color: theme.colorScheme.outlineVariant),
      ),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.s3),
        child: Row(
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    item.advisorName.isNotEmpty ? item.advisorName : 'Nhóm đề tài',
                    style: theme.textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    'Đã mở quan tâm',
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: Palette.infoDark,
                    ),
                  ),
                ],
              ),
            ),
            OutlinedButton(
              style: OutlinedButton.styleFrom(
                visualDensity: VisualDensity.compact,
                padding: const EdgeInsets.symmetric(horizontal: 10),
              ),
              onPressed: () => _handleWithdrawInterestWithUndo(
                context,
                item.id,
                item.teamId,
                item.advisorName,
              ),
              child: const Text('Rút quan tâm'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildRecommendationCard(BuildContext context, TopicRecommendationItem rec) {
    final theme = Theme.of(context);
    return Card(
      elevation: 0,
      margin: const EdgeInsets.only(bottom: AppSpacing.s2),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.lg),
        side: BorderSide(color: theme.colorScheme.outlineVariant),
      ),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.s3),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    rec.projectName ?? rec.teamName,
                    style: theme.textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
                if (rec.isInheritedFromExe101)
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                    decoration: BoxDecoration(
                      color: Palette.infoContainer,
                      borderRadius: BorderRadius.circular(AppRadius.sm),
                    ),
                    child: Text(
                      'Kế thừa EXE101',
                      style: theme.textTheme.labelSmall?.copyWith(
                        color: Palette.infoDark,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
              ],
            ),
            if (rec.projectDescription != null) ...[
              const SizedBox(height: 4),
              Text(
                rec.projectDescription!,
                style: theme.textTheme.bodySmall?.copyWith(
                  color: theme.colorScheme.onSurfaceVariant,
                ),
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
            ],
            const SizedBox(height: AppSpacing.s2),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  '${rec.teamName} · ${rec.memberCount} TV · Trùng ${rec.matchingDomainCount} lĩnh vực',
                  style: theme.textTheme.labelSmall?.copyWith(
                    color: theme.colorScheme.onSurfaceVariant,
                  ),
                ),
                FilledButton.tonal(
                  style: FilledButton.styleFrom(
                    padding: const EdgeInsets.symmetric(horizontal: 12),
                    visualDensity: VisualDensity.compact,
                  ),
                  onPressed: () => _handleOpenInterestWithUndo(
                    context,
                    rec.teamId,
                    rec.teamName,
                  ),
                  child: const Text('Quan tâm'),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildAssignedTeamCard(BuildContext context, LecturerAssignedTeam team) {
    final theme = Theme.of(context);
    return Card(
      elevation: 0,
      margin: const EdgeInsets.only(bottom: AppSpacing.s2),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.lg),
        side: BorderSide(color: theme.colorScheme.outlineVariant),
      ),
      child: ListTile(
        onTap: () {
          final ws = team.workspaceId ?? team.id;
          context.push('/gv/du-an/$ws');
        },
        title: Row(
          children: [
            Flexible(
              child: Text(
                team.name,
                style: const TextStyle(fontWeight: FontWeight.bold),
                overflow: TextOverflow.ellipsis,
              ),
            ),
            const SizedBox(width: 6),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 1),
              decoration: BoxDecoration(
                color: theme.colorScheme.surfaceContainerHighest,
                borderRadius: BorderRadius.circular(AppRadius.sm),
              ),
              child: Text(
                team.courseCode,
                style: const TextStyle(fontSize: 10),
              ),
            ),
          ],
        ),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (team.projectName != null)
              Text(
                team.projectName!,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
            Text(
              '${team.memberCount} thành viên${team.latestCheckpoint != null ? ' · ${team.latestCheckpoint}' : ''}',
              style: theme.textTheme.bodySmall?.copyWith(
                color: theme.colorScheme.onSurfaceVariant,
              ),
            ),
          ],
        ),
        trailing: const Icon(Icons.chevron_right),
      ),
    );
  }
}
