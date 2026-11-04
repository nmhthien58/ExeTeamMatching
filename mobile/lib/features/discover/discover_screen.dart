import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/network/api_exception.dart';
import '../../core/phase.dart';
import '../../core/theme/app_tokens.dart';
import '../../core/view_state.dart';
import '../../shared/widgets/states.dart';
import '../../shared/widgets/team_requirement_bar.dart';
import '../my_team/my_team_api.dart' show domainsProvider;
import '../suggestions/suggestion_widgets.dart' show DomainChips;
import '../suggestions/suggestions_api.dart' show TeamCard, specializationsProvider;
import 'discover_api.dart';
import 'seeking_posts_screen.dart';
import 'teams_view_model.dart';

/// Khám phá: tab "Nhóm đang tuyển" (S05) và "Sinh viên tìm nhóm" (S10 – MB-17).
class DiscoverScreen extends StatelessWidget {
  const DiscoverScreen({super.key});

  @override
  Widget build(BuildContext context) => const DefaultTabController(
        length: 2,
        child: Column(
          children: [
            TabBar(tabs: [Tab(text: 'Nhóm đang tuyển'), Tab(text: 'Sinh viên tìm nhóm')]),
            Expanded(child: TabBarView(children: [TeamsTab(), SeekingPostsTab()])),
          ],
        ),
      );
}

/// S05 – nhóm đang tuyển (MB-14): tìm kiếm, bộ lọc S06, lối vào "Gợi ý cho bạn" (S07), danh sách thẻ nhóm phân trang.
/// Thẻ người ngoài xem không có chip trạng thái; nhóm đủ 6 không xuất hiện (API lọc).
/// View của [TeamsViewModel] (MVVM, MB-48): chỉ giữ ô tìm kiếm, mọi trạng thái tải nằm trong ViewModel.
class TeamsTab extends ConsumerStatefulWidget {
  const TeamsTab({super.key});

  @override
  ConsumerState<TeamsTab> createState() => _TeamsTabState();
}

class _TeamsTabState extends ConsumerState<TeamsTab> {
  final _searchController = TextEditingController();

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  TeamsViewModel get _vm => ref.read(teamsViewModelProvider.notifier);

  Future<void> _openFilter(TeamFilter current) async {
    final picked = await showModalBottomSheet<TeamFilter>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (_) => FilterSheet(initial: current),
    );
    if (picked != null) await _vm.applyFilter(picked);
  }

  /// Ngoài đợt lập nhóm, API trả 409 `round-closed`: hiện dialog rồi về Dự án / Trang chủ.
  Future<void> _onRoundClosed() async {
    final openProject = await showPhaseClosedDialog(context, closedAt: ref.read(shellPhaseProvider).studentPhase?.teamFormationDeadlineAt);
    if (mounted) context.go(openProject ? '/sv/du-an' : '/sv');
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    ref.listen(teamsViewModelProvider.select((s) => s.results), (_, next) {
      if (next case ViewError(error: ApiException(code: 'round-closed'))) _onRoundClosed();
    });
    final state = ref.watch(teamsViewModelProvider);
    final filter = state.filter;
    return RefreshIndicator(
      onRefresh: _vm.refresh,
      child: ListView(
        padding: const EdgeInsets.all(AppSpacing.s4),
        children: [
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: _searchController,
                  textInputAction: TextInputAction.search,
                  decoration: const InputDecoration(prefixIcon: Icon(Icons.search), hintText: 'Tên nhóm hoặc đề tài'),
                  onSubmitted: _vm.search,
                ),
              ),
              const SizedBox(width: AppSpacing.s2),
              IconButton.outlined(
                tooltip: 'Bộ lọc',
                onPressed: () => _openFilter(filter),
                icon: Badge.count(count: filter.count, isLabelVisible: filter.count > 0, child: const Icon(Icons.tune)),
              ),
            ],
          ),
          if (filter.count > 0)
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton.icon(
                onPressed: _vm.clearFilters,
                icon: const Icon(Icons.close, size: 18),
                label: Text('Xóa ${filter.count} bộ lọc'),
              ),
            ),
          const SizedBox(height: AppSpacing.s2),
          Card.outlined(
            margin: EdgeInsets.zero,
            child: ListTile(
              leading: const Icon(Icons.auto_awesome_outlined),
              title: const Text('Gợi ý cho bạn'),
              trailing: const Icon(Icons.chevron_right),
              onTap: () => context.push('/sv/kham-pha/goi-y'),
            ),
          ),
          const SizedBox(height: AppSpacing.s3),
          ...switch (state.results) {
            ViewLoading() => [const SkeletonList(count: 3)],
            ViewError(:final error) => [ErrorState(error: error, onRetry: _vm.refresh)],
            ViewEmpty() => [
                EmptyState(
                  icon: Icons.search_off,
                  title: 'Không tìm thấy nhóm',
                  description: 'Thử từ khóa khác hoặc bỏ bớt bộ lọc.',
                  action: state.canClear
                      ? OutlinedButton(
                          onPressed: () {
                            _searchController.clear();
                            _vm.clearFilters(includeQuery: true);
                          },
                          child: const Text('Xóa bộ lọc'),
                        )
                      : null,
                ),
              ],
            ViewData(value: final results) => [
                for (final team in results.items)
                  Padding(padding: const EdgeInsets.only(bottom: AppSpacing.s3), child: TeamListCard(team: team)),
                if (results.hasMore)
                  results.loadingMore
                      ? const Center(child: CircularProgressIndicator())
                      : OutlinedButton(onPressed: _vm.loadMore, child: const Text('Xem thêm')),
                if (results.loadMoreError != null) ErrorBanner(message: 'Không tải được trang tiếp theo.', onRetry: _vm.loadMore),
                if (!results.hasMore) Center(child: Text('Đã hết danh sách', style: theme.textTheme.bodyMedium)),
              ],
          },
        ],
      ),
    );
  }
}

/// Thẻ nhóm cho người ngoài (S05): không có chip trạng thái, ghi "n/6 thành viên · còn k chỗ".
class TeamListCard extends StatelessWidget {
  const TeamListCard({super.key, required this.team});

  final TeamCard team;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final open = team.maxSize - team.memberCount;
    return Card.outlined(
      margin: EdgeInsets.zero,
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => context.push('/sv/kham-pha/nhom/${team.id}'),
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.s4),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(team.name, style: theme.textTheme.titleMedium, maxLines: 2, overflow: TextOverflow.ellipsis),
              if (team.projectName != null) Text(team.projectName!, style: theme.textTheme.bodyMedium, maxLines: 1, overflow: TextOverflow.ellipsis),
              const SizedBox(height: AppSpacing.s2),
              DomainChips(domains: team.domains),
              const SizedBox(height: AppSpacing.s2),
              TeamRequirementBar(memberCount: team.memberCount, maxMembers: team.maxSize),
              Text('${team.memberCount}/${team.maxSize} thành viên · còn $open chỗ', style: theme.textTheme.bodyMedium),
            ],
          ),
        ),
      ),
    );
  }
}

/// S06 – Bộ lọc và sắp xếp (bottom sheet): domain, chuyên ngành đang thiếu, số chỗ trống, sắp xếp; "Xóa tất cả" / "Áp dụng (n)".
class FilterSheet extends ConsumerStatefulWidget {
  const FilterSheet({super.key, required this.initial});

  final TeamFilter initial;

  @override
  ConsumerState<FilterSheet> createState() => _FilterSheetState();
}

class _FilterSheetState extends ConsumerState<FilterSheet> {
  late String? _domain = widget.initial.domain;
  late int? _specialization = widget.initial.missingSpecializationId;
  late int? _openSlots = widget.initial.minOpenSlots;
  late String _sort = widget.initial.sort;

  TeamFilter get _filter => TeamFilter(domain: _domain, missingSpecializationId: _specialization, minOpenSlots: _openSlots, sort: _sort);

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final domains = ref.watch(domainsProvider).value ?? const [];
    final specializations = ref.watch(specializationsProvider).value ?? const [];
    Widget section(String title, List<Widget> chips) => Padding(
          padding: const EdgeInsets.only(bottom: AppSpacing.s4),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(title, style: theme.textTheme.titleSmall),
              const SizedBox(height: AppSpacing.s2),
              Wrap(spacing: AppSpacing.s2, runSpacing: AppSpacing.s2, children: chips),
            ],
          ),
        );
    return SafeArea(
      child: ConstrainedBox(
        constraints: BoxConstraints(maxHeight: MediaQuery.sizeOf(context).height - 48),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Flexible(
              child: ListView(
                shrinkWrap: true,
                padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s4),
                children: [
                  section('Domain', [
                    for (final d in domains)
                      FilterChip(label: Text(d.code), tooltip: d.name, selected: _domain == d.code, onSelected: (on) => setState(() => _domain = on ? d.code : null)),
                  ]),
                  section('Chuyên ngành đang thiếu', [
                    for (final s in specializations)
                      FilterChip(label: Text(s.name), selected: _specialization == s.id, onSelected: (on) => setState(() => _specialization = on ? s.id : null)),
                  ]),
                  section('Số chỗ trống', [
                    for (final n in [1, 2, 3])
                      FilterChip(label: Text('Từ $n chỗ'), selected: _openSlots == n, onSelected: (on) => setState(() => _openSlots = on ? n : null)),
                  ]),
                  section('Sắp xếp', [
                    for (final (value, label) in const [('-createdAt', 'Mới nhất'), ('-openSlots', 'Nhiều chỗ trống'), ('name', 'Tên A–Z')])
                      ChoiceChip(label: Text(label), selected: _sort == value, onSelected: (_) => setState(() => _sort = value)),
                  ]),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.all(AppSpacing.s4),
              child: Row(
                children: [
                  TextButton(
                    onPressed: () => setState(() {
                      _domain = null;
                      _specialization = null;
                      _openSlots = null;
                      _sort = '-createdAt';
                    }),
                    child: const Text('Xóa tất cả'),
                  ),
                  const Spacer(),
                  FilledButton(onPressed: () => Navigator.pop(context, _filter), child: Text('Áp dụng (${_filter.count})')),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
