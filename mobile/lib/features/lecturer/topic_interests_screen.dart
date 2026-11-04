import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../lecturer_profile/lecturer_profile_screen.dart'
    show lecturerProfilePath, lecturerProfileProvider, LecturerProfile;
import 'lecturer_home_api.dart';
import 'lecturer_home_models.dart';
import 'topic_interests_view_model.dart';

/// L07: danh sách đề tài phù hợp cho giảng viên và mentor (Quy tắc 9, 19, 20, 26).
class TopicInterestsScreen extends ConsumerStatefulWidget {
  const TopicInterestsScreen({super.key, required this.advisorKind});

  final String advisorKind;

  @override
  ConsumerState<TopicInterestsScreen> createState() =>
      _TopicInterestsScreenState();
}

class _TopicInterestsScreenState extends ConsumerState<TopicInterestsScreen> {
  int _tab = 0;

  bool get _lecturer => widget.advisorKind == 'LECTURER';
  int get _interestTab => _lecturer ? 2 : 1;

  TopicInterestsViewModel get _vm =>
      ref.read(topicInterestsViewModelProvider(widget.advisorKind).notifier);

  /// `build()` đã watch ViewModel nên các hàm dựng con đọc giá trị mới nhất.
  TopicInterestsState get _state =>
      ref.read(topicInterestsViewModelProvider(widget.advisorKind));
  String? get _selectedCourse => _state.course;
  Set<String> get _pending => _state.pending;

  Future<void> _refresh() => _vm.refresh();

  Future<void> _toggle(TopicRecommendationItem item) async {
    final vm = _vm;
    final outcome = await vm.toggle(item);
    if (!mounted) return;
    switch (outcome) {
      case TopicToggleIgnored():
        return;
      case TopicToggleFailed():
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Không cập nhật được quan tâm. Vui lòng thử lại.'),
          ),
        );
      case TopicToggled(:final wasInterested):
        ScaffoldMessenger.of(context).hideCurrentSnackBar();
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              wasInterested
                  ? 'Đã rút quan tâm'
                  : 'Đã quan tâm đề tài ${item.projectName ?? item.teamName}',
            ),
            duration: const Duration(seconds: 5),
            action: SnackBarAction(
              label: 'Hoàn tác',
              onPressed: () async {
                final undone = await vm.undo(
                  item.teamId,
                  wasInterested: wasInterested,
                );
                if (!undone && mounted) {
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
    }
  }

  @override
  Widget build(BuildContext context) {
    ref.watch(topicInterestsViewModelProvider(widget.advisorKind));
    final recommendations = ref.watch(
      advisorTopicRecommendationsProvider(widget.advisorKind),
    );
    final profileAsync = _lecturer ? ref.watch(lecturerProfileProvider) : null;

    return Scaffold(
      appBar: AppBar(title: const Text('Đề tài')),
      body: RefreshIndicator(
        onRefresh: _refresh,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(AppSpacing.s4),
          children: [
            if (_lecturer && profileAsync != null && profileAsync.isLoading && !recommendations.hasError)
              const Center(
                child: Padding(
                  padding: EdgeInsets.all(AppSpacing.s6),
                  child: CircularProgressIndicator(),
                ),
              )
            else
              recommendations.when(
                loading: () => const Center(
                  child: Padding(
                    padding: EdgeInsets.all(AppSpacing.s6),
                    child: CircularProgressIndicator(),
                  ),
                ),
                error: (error, _) => ErrorState(error: error, onRetry: _refresh),
                data: (response) => _buildContent(context, response, profileAsync?.asData?.value),
              ),
          ],
        ),
      ),
    );
  }

  Widget _buildContent(
    BuildContext context,
    TopicRecommendationsResponse response,
    LecturerProfile? profile,
  ) {
    final theme = Theme.of(context);

    // Check if lecturer hasn't declared expertise domains (L06)
    if (_lecturer) {
      if (profile != null && profile.domains.isEmpty) {
        return EmptyState(
          icon: Icons.school_outlined,
          title: 'Chưa khai báo chuyên môn',
          description: 'Cập nhật chuyên môn để xem đề tài phù hợp.',
          action: FilledButton(
            onPressed: () async {
              await context.push(lecturerProfilePath);
              _refresh();
            },
            child: const Text('Cập nhật chuyên môn'),
          ),
        );
      }
    }

    final matchingText = _lecturer
        ? (response.matchingAt != null
            ? ' · Ghép lúc ${_date(response.matchingAt!)}'
            : '')
        : (response.matchingAt != null
            ? ' · Ghép lúc ${_date(response.matchingAt!)}'
            : ' · Ghép tuần 6');

    final items = _vm.byCourse(response.items, (i) => i.courseCode);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        // Header limit banner
        Text(
          'Đang quan tâm ${response.openInterestCount}/${response.interestLimit}$matchingText',
          style: theme.textTheme.titleMedium?.copyWith(
            fontWeight: FontWeight.bold,
          ),
        ),
        if (!response.interestOpen)
          const Padding(
            padding: EdgeInsets.only(top: AppSpacing.s2),
            child: Text(
              'Ngoài giai đoạn quan tâm: bạn vẫn có thể xem đề tài.',
            ),
          ),
        const SizedBox(height: AppSpacing.s3),

        // Course filter chips
        Wrap(
          spacing: AppSpacing.s2,
          runSpacing: AppSpacing.s2,
          children: [
            FilterChip(
              label: const Text('Tất cả môn'),
              selected: _selectedCourse == null,
              onSelected: (_) => _vm.selectCourse(null),
            ),
            FilterChip(
              label: const Text('EXE101'),
              selected: _selectedCourse == 'EXE101',
              onSelected: (_) => _vm.selectCourse('EXE101'),
            ),
            FilterChip(
              label: const Text('EXE201'),
              selected: _selectedCourse == 'EXE201',
              onSelected: (_) => _vm.selectCourse('EXE201'),
            ),
          ],
        ),
        const SizedBox(height: AppSpacing.s3),

        // Tabs
        SegmentedButton<int>(
          segments: [
            const ButtonSegment(value: 0, label: Text('Gợi ý')),
            if (_lecturer)
              const ButtonSegment(
                value: 1,
                label: Text('Nhóm chọn tôi'),
              ),
            ButtonSegment(
              value: _interestTab,
              label: const Text('Đã quan tâm'),
            ),
          ],
          selected: {_tab},
          onSelectionChanged: (selection) =>
              setState(() => _tab = selection.first),
        ),
        const SizedBox(height: AppSpacing.s3),

        // Tab Content
        if (_tab == 0) ...[
          const Text(
            'Gợi ý dựa trên lĩnh vực chuyên môn. Quan tâm không giữ chỗ.',
          ),
          const SizedBox(height: AppSpacing.s3),
          if (items.isEmpty)
            EmptyState(
              icon: Icons.school_outlined,
              title: (_lecturer && (profile != null && profile.domains.isEmpty))
                  ? 'Chưa khai báo chuyên môn'
                  : 'Chưa có đề tài gợi ý phù hợp',
              description: _lecturer
                  ? ((profile != null && profile.domains.isEmpty)
                      ? 'Cập nhật chuyên môn để xem đề tài phù hợp.'
                      : 'Chưa có đề tài nào phù hợp với chuyên môn của bạn trong đợt này. Bạn có thể cập nhật chuyên môn để xem thêm gợi ý.')
                  : 'Chưa có đề tài nào phù hợp với lĩnh vực của bạn trong đợt này.',
              action: _lecturer
                  ? FilledButton(
                      onPressed: () async {
                        await context.push(lecturerProfilePath);
                        _refresh();
                      },
                      child: const Text('Cập nhật chuyên môn'),
                    )
                  : null,
            ),
          for (final item in items) _recommendation(item, response),
        ] else if (_tab == 1 && _lecturer)
          _preferences(response)
        else
          _interests(response),
      ],
    );
  }

  Widget _recommendation(
    TopicRecommendationItem item,
    TopicRecommendationsResponse response,
  ) {
    final canToggle = response.interestOpen &&
        !item.hasAdvisor &&
        (item.isInterested ||
            response.openInterestCount < response.interestLimit);
    final memberText =
        '${item.projectName ?? 'Chưa đặt tên đề tài'} · ${item.memberCount}${item.maxMemberCount == null ? '' : '/${item.maxMemberCount}'} thành viên';

    return Card(
      margin: const EdgeInsets.only(bottom: AppSpacing.s3),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.md),
        side: BorderSide(
          color: item.isInterested
              ? Theme.of(context).colorScheme.primary
              : Theme.of(context).colorScheme.outlineVariant,
        ),
      ),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.s3),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            ListTile(
              contentPadding: EdgeInsets.zero,
              title: Text(
                item.teamName,
                style: const TextStyle(fontWeight: FontWeight.bold),
              ),
              subtitle: Text(memberText),
              onTap: () => _openDetail(item.teamId, item.wasMyEXE101Team),
            ),
            if (item.wasMyEXE101Team) ...[
              Wrap(
                children: [
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 8,
                      vertical: 2,
                    ),
                    decoration: BoxDecoration(
                      color: Palette.infoContainer,
                      borderRadius: BorderRadius.circular(AppRadius.sm),
                    ),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        const Icon(
                          Icons.history_edu,
                          size: 14,
                          color: Palette.infoDark,
                        ),
                        const SizedBox(width: 4),
                        // Flexible: thẻ hẹp + chữ 130% thì nhãn xuống dòng thay vì tràn (flutter-handoff §5).
                        Flexible(
                          child: Text(
                            'Đã hướng dẫn ở EXE101',
                            style: Theme.of(context).textTheme.labelSmall?.copyWith(
                                  color: Palette.infoDark,
                                  fontWeight: FontWeight.bold,
                                ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
              const SizedBox(height: AppSpacing.s2),
            ],
            if (item.matchingDomains.isNotEmpty)
              Wrap(
                spacing: AppSpacing.s2,
                runSpacing: AppSpacing.s1,
                children: [
                  for (final domain in item.matchingDomains)
                    Chip(
                      materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
                      visualDensity: VisualDensity.compact,
                      label: Text(
                        domain.name.isNotEmpty ? domain.name : domain.code,
                      ),
                    ),
                ],
              ),
            if (_lecturer && item.myPreferenceRank != null)
              Padding(
                padding: const EdgeInsets.only(top: AppSpacing.s1),
                child: Text('Nhóm chọn bạn: nguyện vọng ${item.myPreferenceRank}'),
              ),
            const SizedBox(height: AppSpacing.s1),
            Text('${item.otherInterestedCount} người khác cũng quan tâm'),
            const SizedBox(height: AppSpacing.s2),
            if (item.hasAdvisor)
              Text(_lecturer ? 'Đã có giảng viên' : 'Đã có mentor')
            else
              Align(
                alignment: Alignment.centerRight,
                child: TextButton(
                  onPressed: canToggle && !_pending.contains(item.teamId)
                      ? () => _toggle(item)
                      : null,
                  child: Text(
                    item.isInterested ? 'Đã quan tâm · Rút' : 'Quan tâm',
                  ),
                ),
              ),
            if (!canToggle && !item.hasAdvisor)
              Text(
                !response.interestOpen
                    ? 'Đã hết giai đoạn quan tâm'
                    : 'Đã đạt giới hạn quan tâm',
                style: const TextStyle(color: Palette.warning),
              ),
          ],
        ),
      ),
    );
  }

  Widget _preferences(TopicRecommendationsResponse response) => ref
      .watch(lecturerPreferencesMeProvider)
      .when(
        loading: () => const Center(
          child: Padding(
            padding: EdgeInsets.all(AppSpacing.s4),
            child: CircularProgressIndicator(),
          ),
        ),
        error: (error, _) => ErrorState(error: error, onRetry: _refresh),
        data: (items) {
          final filtered = _vm.byCourse(items, (i) => i.courseCode);

          if (filtered.isEmpty) {
            return const Center(
              child: Padding(
                padding: EdgeInsets.all(AppSpacing.s4),
                child: Text('Chưa có nhóm chọn bạn làm nguyện vọng.'),
              ),
            );
          }

          return Column(
            children: [
              for (final item in filtered)
                Card(
                  margin: const EdgeInsets.only(bottom: AppSpacing.s2),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(AppRadius.md),
                    side: BorderSide(
                      color: item.iAmInterested
                          ? Theme.of(context).colorScheme.primary
                          : Theme.of(context).colorScheme.outlineVariant,
                    ),
                  ),
                  child: ListTile(
                    title: Text(
                      item.teamName,
                      style: const TextStyle(fontWeight: FontWeight.bold),
                    ),
                    subtitle: Text(
                      '${item.projectName ?? 'Chưa đặt tên đề tài'} · Nguyện vọng ${item.rank}',
                    ),
                    trailing: item.iAmInterested
                        ? const Icon(Icons.favorite, color: Palette.primary)
                        : null,
                    onTap: () => _openDetail(item.teamId, item.wasMyEXE101Team),
                  ),
                ),
            ],
          );
        },
      );

  Widget _interests(TopicRecommendationsResponse response) => ref
      .watch(advisorMyInterestsProvider(widget.advisorKind))
      .when(
        loading: () => const Center(
          child: Padding(
            padding: EdgeInsets.all(AppSpacing.s4),
            child: CircularProgressIndicator(),
          ),
        ),
        error: (error, _) => ErrorState(error: error, onRetry: _refresh),
        data: (items) {
          if (items.isEmpty) {
            return const Center(
              child: Padding(
                padding: EdgeInsets.all(AppSpacing.s4),
                child: Text('Bạn chưa quan tâm đề tài nào.'),
              ),
            );
          }

          return Column(
            children: [
              for (final item in items)
                Card(
                  margin: const EdgeInsets.only(bottom: AppSpacing.s2),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(AppRadius.md),
                    side: BorderSide(
                      color: Theme.of(context).colorScheme.outlineVariant,
                    ),
                  ),
                  child: ListTile(
                    title: ref.watch(advisorTeamDetailProvider(item.teamId)).when(
                          loading: () => const Text('Đang tải đề tài…'),
                          error: (_, _) => Text(
                            item.advisorName.isNotEmpty
                                ? item.advisorName
                                : 'Đề tài đã quan tâm',
                          ),
                          data: (team) => Text(
                            team['name']?.toString() ?? 'Đề tài đã quan tâm',
                            style: const TextStyle(fontWeight: FontWeight.bold),
                          ),
                        ),
                    subtitle: const Text('Đang quan tâm'),
                    trailing: IconButton(
                      icon: const Icon(Icons.close),
                      tooltip: 'Rút quan tâm',
                      onPressed: response.interestOpen &&
                              !_pending.contains(item.teamId)
                          ? () => _toggle(
                                TopicRecommendationItem(
                                  teamId: item.teamId,
                                  teamName: '',
                                  courseCode: '',
                                  memberCount: 0,
                                  isInterested: true,
                                  myInterestId: item.id,
                                ),
                              )
                          : null,
                    ),
                    onTap: () => _openDetail(item.teamId, item.wasPreviousInstructor),
                  ),
                ),
            ],
          );
        },
      );

  void _openDetail(String teamId, [bool? wasMyEXE101Team]) =>
      context.push(
        '${_lecturer ? '/gv' : '/mentor'}/de-tai/$teamId',
        extra: {'wasMyEXE101Team': wasMyEXE101Team},
      );

  String _date(DateTime value) {
    final date = value.toLocal();
    return '${date.day.toString().padLeft(2, '0')}/${date.month.toString().padLeft(2, '0')}/${date.year} '
        '${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}';
  }
}

/// Màn hình chi tiết đề tài cho giảng viên và mentor.
class TopicDetailScreen extends ConsumerStatefulWidget {
  const TopicDetailScreen({
    super.key,
    required this.teamId,
    required this.advisorKind,
    this.wasMyEXE101Team,
  });

  final String teamId;
  final String advisorKind;
  final bool? wasMyEXE101Team;

  @override
  ConsumerState<TopicDetailScreen> createState() => _TopicDetailScreenState();
}

class _TopicDetailScreenState extends ConsumerState<TopicDetailScreen> {
  bool get _lecturer => widget.advisorKind == 'LECTURER';

  TopicDetailKey get _key =>
      (teamId: widget.teamId, advisorKind: widget.advisorKind);

  Future<void> _toggleInterest(
    TopicRecommendationItem? rec,
    TopicInterestItem? currentInterest,
  ) async {
    final vm = ref.read(topicDetailViewModelProvider(_key).notifier);
    final outcome = await vm.toggle(currentInterest);
    if (!mounted) return;
    switch (outcome) {
      case TopicToggleIgnored():
        return;
      case TopicToggleFailed():
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Không cập nhật được quan tâm. Vui lòng thử lại.'),
          ),
        );
      case TopicToggled(:final wasInterested):
        ScaffoldMessenger.of(context).hideCurrentSnackBar();
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              wasInterested ? 'Đã rút quan tâm' : 'Đã quan tâm đề tài',
            ),
            duration: const Duration(seconds: 5),
            action: SnackBarAction(
              label: 'Hoàn tác',
              onPressed: () => vm.undo(wasInterested: wasInterested),
            ),
          ),
        );
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final submitting = ref.watch(topicDetailViewModelProvider(_key));
    final teamAsync = ref.watch(advisorTeamDetailProvider(widget.teamId));
    final recAsync = ref.watch(
      advisorTopicRecommendationsProvider(widget.advisorKind),
    );
    final interestsAsync = ref.watch(
      advisorMyInterestsProvider(widget.advisorKind),
    );

    return Scaffold(
      appBar: AppBar(title: const Text('Chi tiết đề tài')),
      body: teamAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ErrorState(
          error: error,
          onRetry: () =>
              ref.invalidate(advisorTeamDetailProvider(widget.teamId)),
        ),
        data: (team) {
          final project = team['project'] as Map?;
          final members = team['members'] as List? ?? const [];
          final domains = team['domains'] as List? ?? const [];
          final teamName = team['name']?.toString() ?? 'Nhóm';
          final projectName =
              team['projectName']?.toString() ?? 'Chưa đặt tên đề tài';
          final courseCode = team['courseCode']?.toString() ?? 'EXE101';
          final ideaDescription = team['ideaDescription']?.toString() ??
              project?['description']?.toString() ??
              'Nhóm chưa cập nhật mô tả ý tưởng.';

          final rec = recAsync.asData?.value.items
              .where((i) => i.teamId == widget.teamId)
              .firstOrNull;
          final recResponse = recAsync.asData?.value;

          final myInterest = interestsAsync.asData?.value
              .where(
                (i) => i.teamId == widget.teamId && i.status == 'OPEN',
              )
              .firstOrNull;
          final isInterested =
              TopicDetailViewModel.isInterested(myInterest, rec);

          final hasAdvisor = rec?.hasAdvisor == true ||
              (_lecturer
                  ? team['lecturerName'] != null
                  : team['mentorName'] != null);
          final canToggle = TopicDetailViewModel.canToggle(
            response: recResponse,
            hasAdvisor: hasAdvisor,
            isInterested: isInterested,
          );

          return ListView(
            padding: const EdgeInsets.all(AppSpacing.s4),
            children: [
              Text(
                projectName,
                style: theme.textTheme.headlineSmall?.copyWith(
                  fontWeight: FontWeight.bold,
                ),
              ),
              const SizedBox(height: AppSpacing.s1),
              Text(
                '$teamName · $courseCode · ${members.length} thành viên',
                style: theme.textTheme.bodyMedium?.copyWith(
                  color: theme.colorScheme.onSurfaceVariant,
                ),
              ),
              const SizedBox(height: AppSpacing.s3),

              Wrap(
                spacing: AppSpacing.s2,
                runSpacing: AppSpacing.s2,
                children: [
                  if (_lecturer)
                    ref.watch(lecturerPreferencesMeProvider).when(
                          loading: () => const SizedBox.shrink(),
                          error: (_, _) => const SizedBox.shrink(),
                          data: (prefs) {
                            final rank = prefs
                                .where((p) => p.teamId == widget.teamId)
                                .firstOrNull
                                ?.rank;
                            if (rank == null) return const SizedBox.shrink();
                            return Container(
                              padding: const EdgeInsets.symmetric(
                                horizontal: 10,
                                vertical: 4,
                              ),
                              decoration: BoxDecoration(
                                color: Palette.infoContainer,
                                borderRadius:
                                    BorderRadius.circular(AppRadius.sm),
                              ),
                              child: Text(
                                'Nhóm xếp bạn ở nguyện vọng $rank',
                                style: theme.textTheme.labelMedium?.copyWith(
                                  color: Palette.infoDark,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                            );
                          },
                        ),
                  if (widget.wasMyEXE101Team == true ||
                      rec?.wasMyEXE101Team == true ||
                      team['wasMyEXE101Team'] == true ||
                      team['wasMyExe101Team'] == true ||
                      team['isProjectContinued'] == true ||
                      (project != null && project['originCourseCode'] == 'EXE101'))
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 10,
                        vertical: 4,
                      ),
                      decoration: BoxDecoration(
                        color: Palette.infoContainer,
                        borderRadius: BorderRadius.circular(AppRadius.sm),
                      ),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          const Icon(
                            Icons.history_edu,
                            size: 16,
                            color: Palette.infoDark,
                          ),
                          const SizedBox(width: 4),
                          // Flexible: chữ 130% thì nhãn xuống dòng thay vì tràn (flutter-handoff §5).
                          Flexible(
                            child: Text(
                              'Đã hướng dẫn ở EXE101',
                              style: theme.textTheme.labelMedium?.copyWith(
                                color: Palette.infoDark,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  if (hasAdvisor)
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 10,
                        vertical: 4,
                      ),
                      decoration: BoxDecoration(
                        color: theme.colorScheme.surfaceContainerHighest,
                        borderRadius: BorderRadius.circular(AppRadius.sm),
                      ),
                      child: Text(
                        _lecturer ? 'Đã có giảng viên' : 'Đã có mentor',
                        style: theme.textTheme.labelMedium?.copyWith(
                          color: theme.colorScheme.onSurfaceVariant,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ),
                ],
              ),
              const SizedBox(height: AppSpacing.s4),

              if (rec != null) ...[
                Row(
                  children: [
                    Icon(
                      Icons.groups_outlined,
                      size: 18,
                      color: theme.colorScheme.onSurfaceVariant,
                    ),
                    const SizedBox(width: AppSpacing.s2),
                    Text(
                      '${rec.otherInterestedCount} người khác cũng quan tâm',
                      style: theme.textTheme.bodyMedium?.copyWith(
                        color: theme.colorScheme.onSurfaceVariant,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: AppSpacing.s4),
              ],

              Text(
                'Mô tả ý tưởng',
                style: theme.textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.bold,
                ),
              ),
              const SizedBox(height: AppSpacing.s1),
              Text(ideaDescription, style: theme.textTheme.bodyMedium),
              const SizedBox(height: AppSpacing.s4),

              if (domains.isNotEmpty) ...[
                Text(
                  'Lĩnh vực đề tài',
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
                ),
                const SizedBox(height: AppSpacing.s2),
                Wrap(
                  spacing: AppSpacing.s2,
                  runSpacing: AppSpacing.s2,
                  children: [
                    for (final domain in domains)
                      Chip(
                        label: Text(
                          (domain as Map)['name']?.toString() ??
                              domain['code']?.toString() ??
                              'Lĩnh vực',
                        ),
                      ),
                  ],
                ),
                const SizedBox(height: AppSpacing.s4),
              ],

              Text(
                'Thành viên (${members.length})',
                style: theme.textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.bold,
                ),
              ),
              const SizedBox(height: AppSpacing.s2),
              for (final member in members) ...[
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  leading: CircleAvatar(
                    child: Text(
                      ((member as Map)['fullName']?.toString() ?? 'T')
                          .characters
                          .first,
                    ),
                  ),
                  // Wrap: tên dài hoặc chữ 130% thì nhãn "Trưởng nhóm" xuống dòng thay vì tràn (flutter-handoff §5).
                  title: Wrap(
                    crossAxisAlignment: WrapCrossAlignment.center,
                    children: [
                      Text(
                        member['fullName']?.toString() ?? 'Thành viên',
                        style: const TextStyle(fontWeight: FontWeight.w600),
                      ),
                      if (member['isLeader'] == true ||
                          member['role'] == 'LEADER') ...[
                        const SizedBox(width: AppSpacing.s2),
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 6,
                            vertical: 1,
                          ),
                          decoration: BoxDecoration(
                            color: theme.colorScheme.primaryContainer,
                            borderRadius: BorderRadius.circular(AppRadius.sm),
                          ),
                          child: Text(
                            'Trưởng nhóm',
                            style: theme.textTheme.labelSmall?.copyWith(
                              color: theme.colorScheme.onPrimaryContainer,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ),
                      ],
                    ],
                  ),
                  subtitle: Text(
                    (member['specialization'] as Map?)?['name']?.toString() ??
                        '',
                  ),
                ),
                const Divider(height: 1),
              ],

              const SizedBox(height: AppSpacing.s6),

              if (!hasAdvisor)
                isInterested
                    ? FilledButton.tonalIcon(
                        onPressed: submitting
                            ? null
                            : () => _toggleInterest(rec, myInterest),
                        icon: const Icon(Icons.check),
                        label: const Text('Đã quan tâm · Rút lại'),
                      )
                    : FilledButton.icon(
                        onPressed: (canToggle && !submitting)
                            ? () => _toggleInterest(rec, myInterest)
                            : null,
                        icon: const Icon(Icons.star_outline),
                        label: const Text('Quan tâm đề tài này'),
                      ),
              if (!hasAdvisor && !canToggle && !isInterested) ...[
                const SizedBox(height: AppSpacing.s2),
                Text(
                  recResponse?.interestOpen != true
                      ? 'Đã hết giai đoạn quan tâm đề tài.'
                      : 'Đã đạt giới hạn quan tâm.',
                  style: const TextStyle(color: Palette.warning),
                ),
              ],
            ],
          );
        },
      ),
    );
  }
}
