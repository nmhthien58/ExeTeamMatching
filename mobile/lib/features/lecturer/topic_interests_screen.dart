import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import 'lecturer_home_api.dart';
import 'lecturer_home_models.dart';

/// L07: danh sách đề tài cho giảng viên và mentor.
class TopicInterestsScreen extends ConsumerStatefulWidget {
  const TopicInterestsScreen({super.key, required this.advisorKind});

  final String advisorKind;

  @override
  ConsumerState<TopicInterestsScreen> createState() =>
      _TopicInterestsScreenState();
}

class _TopicInterestsScreenState extends ConsumerState<TopicInterestsScreen> {
  int _tab = 0;
  final Set<String> _pending = {};

  bool get _lecturer => widget.advisorKind == 'LECTURER';
  int get _interestTab => _lecturer ? 2 : 1;

  Future<void> _refresh() async {
    ref.invalidate(advisorTopicRecommendationsProvider(widget.advisorKind));
    ref.invalidate(advisorMyInterestsProvider(widget.advisorKind));
    if (_lecturer) ref.invalidate(lecturerPreferencesMeProvider);
    try {
      await ref.read(
        advisorTopicRecommendationsProvider(widget.advisorKind).future,
      );
    } catch (_) {
      // ErrorState displays the failed request and its retry action.
    }
  }

  Future<void> _toggle(TopicRecommendationItem item) async {
    if (!_pending.add(item.teamId)) return;
    setState(() {});
    try {
      final api = ref.read(lecturerHomeApiProvider);
      if (item.isInterested && item.myInterestId != null) {
        await api.withdrawInterest(item.myInterestId!);
      } else {
        await api.openInterest(item.teamId, advisorKind: widget.advisorKind);
      }
      _refresh();
      if (!mounted) return;
      ScaffoldMessenger.of(context).hideCurrentSnackBar();
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            item.isInterested ? 'Đã rút quan tâm' : 'Đã quan tâm đề tài',
          ),
          duration: const Duration(seconds: 5),
          action: SnackBarAction(
            label: 'Hoàn tác',
            onPressed: () async {
              try {
                if (item.isInterested) {
                  await api.openInterest(
                    item.teamId,
                    advisorKind: widget.advisorKind,
                  );
                } else {
                  final interests = await api.getMyTopicInterests(
                    advisorKind: widget.advisorKind,
                  );
                  final opened = interests
                      .where((i) => i.teamId == item.teamId)
                      .firstOrNull;
                  if (opened == null) throw StateError('Interest not found');
                  await api.withdrawInterest(opened.id);
                }
                _refresh();
              } catch (_) {
                if (mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(
                      content: Text('Không thể hoàn tác. Vui lòng thử lại.'),
                    ),
                  );
                }
              }
            },
          ),
        ),
      );
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Không cập nhật được quan tâm. Vui lòng thử lại.'),
          ),
        );
      }
    } finally {
      _pending.remove(item.teamId);
      if (mounted) setState(() {});
    }
  }

  @override
  Widget build(BuildContext context) {
    final recommendations = ref.watch(
      advisorTopicRecommendationsProvider(widget.advisorKind),
    );
    return Scaffold(
      appBar: AppBar(title: const Text('Đề tài')),
      body: RefreshIndicator(
        onRefresh: _refresh,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(AppSpacing.s4),
          children: [
            recommendations.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, _) => ErrorState(error: error, onRetry: _refresh),
              data: (response) => Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Đang quan tâm ${response.openInterestCount}/${response.interestLimit}',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  if (response.matchingAt != null)
                    Text('Ghép lúc ${_date(response.matchingAt!)}'),
                  if (!response.interestOpen)
                    const Padding(
                      padding: EdgeInsets.only(top: AppSpacing.s2),
                      child: Text(
                        'Ngoài giai đoạn quan tâm: bạn vẫn có thể xem đề tài.',
                      ),
                    ),
                  const SizedBox(height: AppSpacing.s4),
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
                  if (_tab == 0) ...[
                    const Text(
                      'Gợi ý dựa trên lĩnh vực chuyên môn. Quan tâm không giữ chỗ.',
                    ),
                    const SizedBox(height: AppSpacing.s3),
                    if (response.items.isEmpty)
                      const Center(
                        child: Text('Chưa có đề tài gợi ý phù hợp.'),
                      ),
                    for (final item in response.items)
                      _recommendation(item, response),
                  ] else if (_tab == 1 && _lecturer)
                    _preferences()
                  else
                    _interests(response),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _recommendation(
    TopicRecommendationItem item,
    TopicRecommendationsResponse response,
  ) {
    final canToggle =
        response.interestOpen &&
        !item.hasAdvisor &&
        (item.isInterested ||
            response.openInterestCount < response.interestLimit);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.s3),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            ListTile(
              contentPadding: EdgeInsets.zero,
              title: Text(item.teamName),
              subtitle: Text(
                '${item.projectName ?? 'Chưa đặt tên đề tài'} · ${item.memberCount}${item.maxMemberCount == null ? '' : '/${item.maxMemberCount}'} thành viên',
              ),
              onTap: () => _openDetail(item.teamId),
            ),
            if (item.matchingDomains.isNotEmpty)
              Wrap(
                spacing: AppSpacing.s2,
                children: [
                  for (final domain in item.matchingDomains)
                    Chip(
                      label: Text(
                        domain.name.isNotEmpty ? domain.name : domain.code,
                      ),
                    ),
                ],
              ),
            if (_lecturer && item.myPreferenceRank != null)
              Text('Nhóm chọn bạn: nguyện vọng ${item.myPreferenceRank}'),
            Text('${item.otherInterestedCount} người khác cũng quan tâm'),
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
              ),
          ],
        ),
      ),
    );
  }

  Widget _preferences() => ref
      .watch(lecturerPreferencesMeProvider)
      .when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ErrorState(error: error, onRetry: _refresh),
        data: (items) => items.isEmpty
            ? const Center(
                child: Text('Chưa có nhóm chọn bạn làm nguyện vọng.'),
              )
            : Column(
                children: [
                  for (final item in items)
                    Card(
                      child: ListTile(
                        title: Text(item.teamName),
                        subtitle: Text(
                          '${item.projectName ?? 'Chưa đặt tên đề tài'} · Nguyện vọng ${item.rank}',
                        ),
                        trailing: item.iAmInterested
                            ? const Icon(Icons.favorite)
                            : null,
                        onTap: () => _openDetail(item.teamId),
                      ),
                    ),
                ],
              ),
      );

  Widget _interests(TopicRecommendationsResponse response) => ref
      .watch(advisorMyInterestsProvider(widget.advisorKind))
      .when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ErrorState(error: error, onRetry: _refresh),
        data: (items) => items.isEmpty
            ? const Center(child: Text('Bạn chưa quan tâm đề tài nào.'))
            : Column(
                children: [
                  for (final item in items)
                    Card(
                      child: ListTile(
                        title: ref
                            .watch(advisorTeamDetailProvider(item.teamId))
                            .when(
                              loading: () => const Text('Đang tải đề tài…'),
                              error: (_, _) => const Text('Đề tài đã quan tâm'),
                              data: (team) => Text(
                                team['name']?.toString() ??
                                    'Đề tài đã quan tâm',
                              ),
                            ),
                        subtitle: const Text('Đang quan tâm'),
                        trailing: IconButton(
                          icon: const Icon(Icons.close),
                          tooltip: 'Rút quan tâm',
                          onPressed:
                              response.interestOpen &&
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
                        onTap: () => _openDetail(item.teamId),
                      ),
                    ),
                ],
              ),
      );

  void _openDetail(String teamId) =>
      context.push('${_lecturer ? '/gv' : '/mentor'}/de-tai/$teamId');

  String _date(DateTime value) {
    final date = value.toLocal();
    return '${date.day.toString().padLeft(2, '0')}/${date.month.toString().padLeft(2, '0')}/${date.year} '
        '${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}';
  }
}

class TopicDetailScreen extends ConsumerWidget {
  const TopicDetailScreen({
    super.key,
    required this.teamId,
    required this.advisorKind,
  });
  final String teamId;
  final String advisorKind;

  @override
  Widget build(BuildContext context, WidgetRef ref) => Scaffold(
    appBar: AppBar(title: const Text('Chi tiết đề tài')),
    body: ref
        .watch(advisorTeamDetailProvider(teamId))
        .when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (error, _) => ErrorState(
            error: error,
            onRetry: () => ref.invalidate(advisorTeamDetailProvider(teamId)),
          ),
          data: (team) {
            final project = team['project'] as Map?;
            final members = team['members'] as List? ?? const [];
            final domains = team['domains'] as List? ?? const [];
            return ListView(
              padding: const EdgeInsets.all(AppSpacing.s4),
              children: [
                Text(
                  team['name']?.toString() ?? 'Nhóm',
                  style: Theme.of(context).textTheme.headlineSmall,
                ),
                const SizedBox(height: AppSpacing.s3),
                Text(team['projectName']?.toString() ?? 'Chưa đặt tên đề tài'),
                const SizedBox(height: AppSpacing.s3),
                Text(
                  team['ideaDescription']?.toString() ??
                      project?['description']?.toString() ??
                      'Chưa có mô tả ý tưởng.',
                ),
                if (advisorKind == 'LECTURER')
                  ref
                      .watch(lecturerPreferencesMeProvider)
                      .when(
                        loading: () => const SizedBox.shrink(),
                        error: (_, _) => const SizedBox.shrink(),
                        data: (preferences) {
                          final rank = preferences
                              .where((p) => p.teamId == teamId)
                              .firstOrNull
                              ?.rank;
                          return rank == null
                              ? const SizedBox.shrink()
                              : Padding(
                                  padding: const EdgeInsets.only(
                                    top: AppSpacing.s3,
                                  ),
                                  child: Text(
                                    'Nhóm xếp bạn ở nguyện vọng $rank',
                                  ),
                                );
                        },
                      ),
                if (domains.isNotEmpty) ...[
                  const SizedBox(height: AppSpacing.s4),
                  Text(
                    'Lĩnh vực',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  Wrap(
                    spacing: AppSpacing.s2,
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
                ],
                const SizedBox(height: AppSpacing.s4),
                Text(
                  'Thành viên (${members.length})',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                for (final member in members)
                  ListTile(
                    title: Text(
                      (member as Map)['fullName']?.toString() ?? 'Thành viên',
                    ),
                    subtitle: Text(
                      (member['specialization'] as Map?)?['name']?.toString() ??
                          '',
                    ),
                  ),
              ],
            );
          },
        ),
  );
}
