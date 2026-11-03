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
  String _selectedCourse = 'EXE201';

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
      await ref
          .read(lecturerHomeApiProvider)
          .openInterest(teamId, advisorKind: 'LECTURER');
      ref.invalidate(lecturerMyTopicInterestsProvider);
      ref.invalidate(lecturerPreferencesMeProvider);
      ref.invalidate(lecturerTopicRecommendationsProvider(_selectedCourse));

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
                final myInterests = await ref
                    .read(lecturerHomeApiProvider)
                    .getMyTopicInterests();
                final current = myInterests
                    .where((i) => i.teamId == teamId && i.status == 'OPEN')
                    .firstOrNull;
                if (current == null) {
                  throw StateError('Không tìm thấy quan tâm');
                }
                await ref
                    .read(lecturerHomeApiProvider)
                    .withdrawInterest(current.id);
                ref.invalidate(lecturerMyTopicInterestsProvider);
                ref.invalidate(lecturerPreferencesMeProvider);
                ref.invalidate(
                  lecturerTopicRecommendationsProvider(_selectedCourse),
                );
              } catch (_) {
                if (!context.mounted) return;
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(
                    content: Text('Không thể hoàn tác. Vui lòng thử lại.'),
                  ),
                );
              }
            },
          ),
        ),
      );
    } catch (e) {
      if (!context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            e is Exception
                ? e.toString().replaceAll('Exception: ', '')
                : 'Không thể mở quan tâm',
          ),
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
      ref.invalidate(lecturerTopicRecommendationsProvider(_selectedCourse));

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
                await ref
                    .read(lecturerHomeApiProvider)
                    .openInterest(teamId, advisorKind: 'LECTURER');
                ref.invalidate(lecturerMyTopicInterestsProvider);
                ref.invalidate(lecturerPreferencesMeProvider);
                ref.invalidate(
                  lecturerTopicRecommendationsProvider(_selectedCourse),
                );
              } catch (_) {
                if (!context.mounted) return;
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(
                    content: Text('Không thể hoàn tác. Vui lòng thử lại.'),
                  ),
                );
              }
            },
          ),
        ),
      );
    } catch (e) {
      if (!context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            e is Exception
                ? e.toString().replaceAll('Exception: ', '')
                : 'Không thể rút quan tâm',
          ),
          backgroundColor: Theme.of(context).colorScheme.error,
        ),
      );
    }
  }

  String _formatDateTime(DateTime dt) {
    dt = dt.toLocal();
    return '${dt.day.toString().padLeft(2, '0')}/${dt.month.toString().padLeft(2, '0')}/${dt.year} ${dt.hour.toString().padLeft(2, '0')}:${dt.minute.toString().padLeft(2, '0')}';
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final user = ref.watch(authControllerProvider).value;
    final phases = ref.watch(phasesProvider).value;
    final shellPhase = ref.watch(shellPhaseProvider);
    final currentCoursePhase = phases
        ?.where(
          (p) => p.courseCode.toUpperCase() == _selectedCourse.toUpperCase(),
        )
        .firstOrNull;
    final isTeamFormation =
        currentCoursePhase?.isTeamFormation ?? shellPhase.isTeamFormation;

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
            ref.invalidate(
              lecturerTopicRecommendationsProvider(_selectedCourse),
            );
          } else {
            ref.invalidate(lecturerTeamsMeProvider(_selectedCourse));
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
                    style: theme.textTheme.titleMedium?.copyWith(
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
            const SizedBox(height: AppSpacing.s3),

            // Course Switcher: EXE201 | EXE101
            SegmentedButton<String>(
              segments: const [
                ButtonSegment(value: 'EXE201', label: Text('EXE201')),
                ButtonSegment(value: 'EXE101', label: Text('EXE101')),
              ],
              selected: {_selectedCourse},
              onSelectionChanged: (val) {
                setState(() => _selectedCourse = val.first);
              },
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
    final recommendationsAsync = ref.watch(
      lecturerTopicRecommendationsProvider(_selectedCourse),
    );

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        // Thẻ số liệu tổng quan 2 cột
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
                value:
                    '${myInterestsAsync.value?.where((i) => i.status == 'OPEN').length ?? 0}',
                subtitle: 'Đã mở quan tâm',
                icon: Icons.favorite_outline,
                color: Palette.infoDark,
                bgColor: Palette.infoContainer,
              ),
            ),
          ],
        ),
        const SizedBox(height: AppSpacing.s4),

        // Deadline matching banner (if available from recommendations)
        if (recommendationsAsync.value?.matchingAt != null) ...[
          Container(
            padding: const EdgeInsets.symmetric(
              horizontal: AppSpacing.s4,
              vertical: AppSpacing.s3,
            ),
            decoration: BoxDecoration(
              color: Palette.warningContainer,
              borderRadius: BorderRadius.circular(AppRadius.md),
            ),
            child: Row(
              children: [
                const Icon(Icons.alarm, size: 20, color: Palette.warning),
                const SizedBox(width: AppSpacing.s3),
                Expanded(
                  child: Text(
                    'Hạn chốt ghép cố vấn: ${_formatDateTime(recommendationsAsync.value!.matchingAt!)}',
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: Palette.warning,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: AppSpacing.s4),
        ],

        // Tab bar giữa "Nhóm chọn tôi" và "Đang quan tâm"
        Container(
          decoration: BoxDecoration(
            border: Border(
              bottom: BorderSide(color: theme.colorScheme.outlineVariant),
            ),
          ),
          child: TabBar(
            controller: _tabController,
            isScrollable: true,
            tabAlignment: TabAlignment.start,
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
                      _buildCountBadge(
                        context,
                        preferencesMeAsync.value!.length,
                        theme.colorScheme.primaryContainer,
                        theme.colorScheme.onPrimaryContainer,
                      ),
                    ],
                  ],
                ),
              ),
              Tab(
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    const Text('Đang quan tâm'),
                    if ((myInterestsAsync.value
                                ?.where((i) => i.status == 'OPEN')
                                .length ??
                            0) >
                        0) ...[
                      const SizedBox(width: 6),
                      _buildCountBadge(
                        context,
                        myInterestsAsync.value!
                            .where((i) => i.status == 'OPEN')
                            .length,
                        Palette.infoContainer,
                        Palette.infoDark,
                      ),
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
          height: MediaQuery.textScalerOf(context).scale(320),
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
                      description:
                          'Khi các nhóm xếp bạn vào nguyện vọng 1–3, thông tin sẽ hiển thị tại đây.',
                    );
                  }
                  return ListView.builder(
                    itemCount: preferences.length,
                    itemBuilder: (context, i) =>
                        _buildPreferenceMeCard(context, preferences[i]),
                  );
                },
              ),

              // Tab 2: Đang quan tâm
              myInterestsAsync.when(
                loading: () => const Center(child: CircularProgressIndicator()),
                error: (err, _) => ErrorState(
                  error: err,
                  onRetry: () =>
                      ref.invalidate(lecturerMyTopicInterestsProvider),
                ),
                data: (interests) {
                  final openInterests = interests
                      .where((i) => i.status == 'OPEN')
                      .toList();
                  if (openInterests.isEmpty) {
                    return const EmptyState(
                      icon: Icons.favorite_border,
                      title: 'Chưa quan tâm nhóm nào',
                      description:
                          'Khám phá các đề tài gợi ý phía dưới để mở quan tâm đến các nhóm phù hợp chuyên môn.',
                    );
                  }
                  return ListView.builder(
                    itemCount: openInterests.length,
                    itemBuilder: (context, i) =>
                        _buildOpenInterestCard(context, openInterests[i]),
                  );
                },
              ),
            ],
          ),
        ),

        const SizedBox(height: AppSpacing.s6),

        // Section: Đề tài gợi ý
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Expanded(
              child: Text(
                'Đề tài gợi ý cho bạn',
                style: theme.textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.bold,
                ),
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
            onRetry: () => ref.invalidate(
              lecturerTopicRecommendationsProvider(_selectedCourse),
            ),
          ),
          data: (response) {
            final recommendations = response.items;
            if (recommendations.isEmpty) {
              return const Card.outlined(
                child: Padding(
                  padding: EdgeInsets.all(AppSpacing.s6),
                  child: Center(
                    child: Text('Không có đề tài gợi ý phù hợp trong đợt này.'),
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
    final teamsMeAsync = ref.watch(lecturerTeamsMeProvider(_selectedCourse));
    final isExe201 = _selectedCourse == 'EXE201';
    final stats = statsAsync.value;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        // Thống kê tương tác từ Collaboration Service.
        if (statsAsync.isLoading) ...[
          const Padding(
            padding: EdgeInsets.symmetric(vertical: AppSpacing.s4),
            child: Center(child: CircularProgressIndicator()),
          ),
          const SizedBox(height: AppSpacing.s2),
        ] else if (statsAsync.hasError) ...[
          Container(
            padding: const EdgeInsets.all(AppSpacing.s3),
            decoration: BoxDecoration(
              color: Palette.neutralContainer,
              borderRadius: BorderRadius.circular(AppRadius.md),
              border: Border.all(color: Palette.border),
            ),
            child: Row(
              children: [
                const Icon(
                  Icons.sync_problem,
                  size: 24,
                  color: Palette.textSecondary,
                ),
                const SizedBox(width: AppSpacing.s3),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Chưa tải được số liệu tương tác học kỳ',
                        style: theme.textTheme.bodyMedium?.copyWith(
                          fontWeight: FontWeight.bold,
                          color: theme.colorScheme.onSurface,
                        ),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        'Vui lòng thử lại sau.',
                        style: theme.textTheme.bodySmall?.copyWith(
                          color: Palette.textSecondary,
                        ),
                      ),
                    ],
                  ),
                ),
                TextButton(
                  onPressed: () =>
                      ref.invalidate(lecturerSemesterStatsProvider),
                  child: const Text('Thử lại'),
                ),
              ],
            ),
          ),
          const SizedBox(height: AppSpacing.s4),
        ] else if (stats != null) ...[
          // Ba thẻ số liệu bấm được (Rule 22, 23, 25, 27)
          // Thẻ 3 chọn theo môn EXE201 (họp hôm nay) vs EXE101 (checkpoint quá hạn).
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
                  title: isExe201 ? 'Họp hôm nay' : 'Chưa trình bày checkpoint',
                  value: isExe201
                      ? (stats.meetingsTodayCount?.toString() ?? '—')
                      : '${stats.missingCheckpointsCount}',
                  subtitle: isExe201 && stats.meetingsTodayCount == null
                      ? 'Chưa có số liệu'
                      : isExe201
                      ? 'Buổi họp'
                      : 'Nhóm quá hạn CP',
                  icon: isExe201 ? Icons.event_available : Icons.flag_outlined,
                  color: isExe201 ? Palette.success : Palette.warning,
                  bgColor: isExe201
                      ? Palette.successContainer
                      : Palette.warningContainer,
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.s4),

          // Thẻ nhắc "Đăng lịch họp hằng tuần" (chỉ cho EXE201, Rule 27)
          if (isExe201 && !stats.hasPostedWeeklySchedule) ...[
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
                    const Icon(
                      Icons.event_note,
                      size: 36,
                      color: Palette.infoDark,
                    ),
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
        ],

        // Section: Nhóm đang hướng dẫn (từ API thật GET /api/lecturer-teams/me)
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Expanded(
              child: Text(
                'Nhóm đang hướng dẫn',
                style: theme.textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.bold,
                ),
              ),
            ),
            TextButton(
              onPressed: () => context.push('/gv/nhom'),
              child: const Text('Xem tất cả'),
            ),
          ],
        ),
        const SizedBox(height: AppSpacing.s2),

        teamsMeAsync.when(
          loading: () => const Center(
            child: Padding(
              padding: EdgeInsets.all(AppSpacing.s4),
              child: CircularProgressIndicator(),
            ),
          ),
          error: (err, _) => ErrorState(
            error: err,
            onRetry: () =>
                ref.invalidate(lecturerTeamsMeProvider(_selectedCourse)),
          ),
          data: (response) {
            final allTeams = [...response.instructing, ...response.mentoring];
            if (allTeams.isEmpty) {
              return const Card.outlined(
                child: Padding(
                  padding: EdgeInsets.all(AppSpacing.s6),
                  child: Center(
                    child: Text('Chưa có nhóm nào được phân công hướng dẫn.'),
                  ),
                ),
              );
            }

            return Column(
              children: [
                for (final team in allTeams)
                  _buildAdvisedTeamCard(context, team),
              ],
            );
          },
        ),
      ],
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
                  Container(
                    padding: const EdgeInsets.all(AppSpacing.s2),
                    decoration: BoxDecoration(
                      color: bgColor,
                      borderRadius: BorderRadius.circular(AppRadius.md),
                    ),
                    child: Icon(icon, color: color, size: 20),
                  ),
                  if (onTap != null)
                    Icon(
                      Icons.chevron_right,
                      size: 16,
                      color: theme.colorScheme.onSurfaceVariant,
                    ),
                ],
              ),
              const SizedBox(height: AppSpacing.s2),
              Text(
                value,
                style: theme.textTheme.headlineSmall?.copyWith(
                  fontWeight: FontWeight.bold,
                  color: theme.colorScheme.onSurface,
                ),
              ),
              const SizedBox(height: 2),
              Text(
                title,
                style: theme.textTheme.labelMedium?.copyWith(
                  fontWeight: FontWeight.bold,
                ),
              ),
              Text(
                subtitle,
                style: theme.textTheme.bodyMedium?.copyWith(
                  color: theme.colorScheme.onSurfaceVariant,
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

  Widget _buildCountBadge(
    BuildContext context,
    int count,
    Color bg,
    Color text,
  ) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(AppRadius.full),
      ),
      child: Text(
        '$count',
        style: Theme.of(context).textTheme.bodyMedium?.copyWith(
          color: text,
          fontWeight: FontWeight.bold,
        ),
      ),
    );
  }

  Widget _buildPreferenceMeCard(
    BuildContext context,
    LecturerPreferenceMe item,
  ) {
    final theme = Theme.of(context);
    return Card(
      elevation: 0,
      margin: const EdgeInsets.only(bottom: AppSpacing.s2),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.md),
        side: BorderSide(color: theme.colorScheme.outlineVariant),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.md),
        onTap: () => context.push('/gv/de-tai/${item.teamId}'),
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.s3),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  CircleAvatar(
                    radius: 16,
                    backgroundColor: theme.colorScheme.primaryContainer,
                    child: Text(
                      'NV${item.rank}',
                      style: theme.textTheme.labelSmall?.copyWith(
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
                          item.teamName,
                          style: theme.textTheme.titleSmall?.copyWith(
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                        if (item.iAmInterested)
                          Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 6,
                              vertical: 2,
                            ),
                            decoration: BoxDecoration(
                              color: Palette.successContainer,
                              borderRadius: BorderRadius.circular(AppRadius.sm),
                            ),
                            child: Text(
                              'Đôi bên quan tâm',
                              style: theme.textTheme.bodyMedium?.copyWith(
                                color: Palette.success,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ),
                        if (item.projectName != null) ...[
                          const SizedBox(height: 2),
                          Text(
                            item.projectName!,
                            style: theme.textTheme.bodySmall?.copyWith(
                              color: theme.colorScheme.onSurfaceVariant,
                            ),
                          ),
                        ],
                        const SizedBox(height: AppSpacing.s1),
                        Text(
                          '${item.courseCode} · ${item.memberCount} thành viên',
                          style: theme.textTheme.bodyMedium?.copyWith(
                            color: theme.colorScheme.onSurfaceVariant,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
              if (!item.iAmInterested)
                Align(
                  alignment: Alignment.centerRight,
                  child: FilledButton.tonal(
                    onPressed: () => _handleOpenInterestWithUndo(
                      context,
                      item.teamId,
                      item.teamName,
                    ),
                    child: const Text('Quan tâm'),
                  ),
                ),
            ],
          ),
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
        borderRadius: BorderRadius.circular(AppRadius.md),
        side: BorderSide(color: theme.colorScheme.outlineVariant),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.md),
        onTap: () => context.push('/gv/de-tai/${item.teamId}'),
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.s3),
          child: Row(
            children: [
              Container(
                padding: const EdgeInsets.all(8),
                decoration: BoxDecoration(
                  color: Palette.infoContainer,
                  borderRadius: BorderRadius.circular(AppRadius.md),
                ),
                child: const Icon(
                  Icons.favorite,
                  size: 20,
                  color: Palette.infoDark,
                ),
              ),
              const SizedBox(width: AppSpacing.s3),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    ref
                        .watch(advisorTeamDetailProvider(item.teamId))
                        .when(
                          loading: () => const Text('Đang tải nhóm…'),
                          error: (_, _) => const Text('Nhóm đã quan tâm'),
                          data: (team) => Text(
                            team['name']?.toString() ?? 'Nhóm đã quan tâm',
                            style: theme.textTheme.titleSmall?.copyWith(
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ),
                    const SizedBox(height: 2),
                    Text(
                      'Đang mở quan tâm · Chờ hệ thống ghép',
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: theme.colorScheme.onSurfaceVariant,
                      ),
                    ),
                  ],
                ),
              ),
              TextButton(
                onPressed: () => _handleWithdrawInterestWithUndo(
                  context,
                  item.id,
                  item.teamId,
                  'đã quan tâm',
                ),
                child: const Text('Rút'),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildRecommendationCard(
    BuildContext context,
    TopicRecommendationItem rec,
  ) {
    final theme = Theme.of(context);
    return Card(
      elevation: 0,
      margin: const EdgeInsets.only(bottom: AppSpacing.s2),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.md),
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
                          Expanded(
                            child: Text(
                              rec.teamName,
                              style: theme.textTheme.titleSmall?.copyWith(
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ),
                          if (rec.wasMyEXE101Team)
                            Container(
                              padding: const EdgeInsets.symmetric(
                                horizontal: 6,
                                vertical: 2,
                              ),
                              decoration: BoxDecoration(
                                color: Palette.infoContainer,
                                borderRadius: BorderRadius.circular(
                                  AppRadius.sm,
                                ),
                              ),
                              child: Text(
                                'Đề tài EXE101',
                                style: theme.textTheme.bodyMedium?.copyWith(
                                  color: Palette.infoDark,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                            ),
                        ],
                      ),
                      if (rec.projectName != null) ...[
                        const SizedBox(height: 2),
                        Text(
                          rec.projectName!,
                          style: theme.textTheme.bodyMedium?.copyWith(
                            color: theme.colorScheme.primary,
                            fontWeight: FontWeight.w500,
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.s2),
            Wrap(
              spacing: AppSpacing.s2,
              children: [
                Text(
                  '${rec.courseCode} · ${rec.memberCount} thành viên',
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: theme.colorScheme.onSurfaceVariant,
                  ),
                ),
                if (rec.otherInterestedCount > 0) ...[
                  Text(
                    '${rec.otherInterestedCount} người khác quan tâm',
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: Palette.warning,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                ],
              ],
            ),
            const SizedBox(height: AppSpacing.s2),
            Wrap(
              alignment: WrapAlignment.end,
              spacing: AppSpacing.s2,
              runSpacing: AppSpacing.s2,
              children: [
                OutlinedButton(
                  onPressed: () => context.push('/gv/de-tai/${rec.teamId}'),
                  child: const Text('Chi tiết'),
                ),
                if (rec.isInterested)
                  FilledButton.tonal(
                    style: FilledButton.styleFrom(
                      backgroundColor: Palette.successContainer,
                      foregroundColor: Palette.success,
                    ),
                    onPressed: () {
                      if (rec.myInterestId != null) {
                        _handleWithdrawInterestWithUndo(
                          context,
                          rec.myInterestId!,
                          rec.teamId,
                          rec.teamName,
                        );
                      }
                    },
                    child: const Text('Đã quan tâm'),
                  )
                else
                  FilledButton.tonal(
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

  Widget _buildAdvisedTeamCard(BuildContext context, AdvisedTeamItem team) {
    final theme = Theme.of(context);
    return Card(
      elevation: 0,
      margin: const EdgeInsets.only(bottom: AppSpacing.s2),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.md),
        side: BorderSide(color: theme.colorScheme.outlineVariant),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.md),
        onTap: () => context.push('/gv/de-tai/${team.teamId}'),
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.s3),
          child: Row(
            children: [
              CircleAvatar(
                radius: 20,
                backgroundColor: theme.colorScheme.primaryContainer,
                child: Text(
                  team.teamName.isNotEmpty ? team.teamName[0] : 'N',
                  style: TextStyle(
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
                      team.teamName,
                      style: theme.textTheme.titleSmall?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    if (team.projectName != null) ...[
                      const SizedBox(height: 2),
                      Text(
                        team.projectName!,
                        style: theme.textTheme.bodySmall?.copyWith(
                          color: theme.colorScheme.onSurfaceVariant,
                        ),
                      ),
                    ],
                    const SizedBox(height: AppSpacing.s1),
                    Text(
                      '${team.courseCode} · ${team.memberCount} thành viên · ${team.reason}',
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: theme.colorScheme.onSurfaceVariant,
                      ),
                    ),
                  ],
                ),
              ),
              const Icon(
                Icons.chevron_right,
                size: 20,
                color: Palette.textSecondary,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
