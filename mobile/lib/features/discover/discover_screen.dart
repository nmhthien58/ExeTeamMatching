import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/network/api_exception.dart';
import '../../core/phase.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../../shared/widgets/team_requirement_bar.dart';
import '../my_team/my_team_api.dart' show domainsProvider;
import '../suggestions/suggestion_widgets.dart' show DomainChips;
import '../suggestions/suggestions_api.dart' show TeamCard, specializationsProvider;
import 'discover_api.dart';

/// S05 – Khám phá, nhóm đang tuyển (MB-14): tìm kiếm, bộ lọc S06, lối vào "Gợi ý cho bạn" (S07), danh sách thẻ nhóm phân trang.
/// Thẻ người ngoài xem không có chip trạng thái; nhóm đủ 6 không xuất hiện (API lọc). Tab "Sinh viên tìm nhóm" (S10) làm ở MB-17.
class DiscoverScreen extends ConsumerStatefulWidget {
  const DiscoverScreen({super.key});

  @override
  ConsumerState<DiscoverScreen> createState() => _DiscoverScreenState();
}

class _DiscoverScreenState extends ConsumerState<DiscoverScreen> {
  final _searchController = TextEditingController();
  TeamFilter _filter = const TeamFilter();
  final List<TeamCard> _items = [];
  int _page = 0;
  bool _hasMore = true;
  bool _loading = false;
  Object? _error;

  @override
  void initState() {
    super.initState();
    _load(reset: true);
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _load({bool reset = false}) async {
    if (_loading) return;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final next = reset ? 1 : _page + 1;
      final page = await ref.read(discoverApiProvider).search(_searchController.text.trim(), _filter, next);
      if (!mounted) return;
      setState(() {
        if (reset) _items.clear();
        _items.addAll(page.items);
        _page = page.page;
        _hasMore = page.hasMore;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() => _error = e);
      if (e.code == 'round-closed') {
        final openProject = await showPhaseClosedDialog(context, closedAt: ref.read(shellPhaseProvider).studentPhase?.teamFormationDeadlineAt);
        if (mounted) context.go(openProject ? '/sv/du-an' : '/sv');
      }
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _openFilter() async {
    final picked = await showModalBottomSheet<TeamFilter>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (_) => FilterSheet(initial: _filter),
    );
    if (picked == null) return;
    setState(() => _filter = picked);
    await _load(reset: true);
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return RefreshIndicator(
      onRefresh: () => _load(reset: true),
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
                  onSubmitted: (_) => _load(reset: true),
                ),
              ),
              const SizedBox(width: AppSpacing.s2),
              IconButton.outlined(
                tooltip: 'Bộ lọc',
                onPressed: _openFilter,
                icon: Badge.count(count: _filter.count, isLabelVisible: _filter.count > 0, child: const Icon(Icons.tune)),
              ),
            ],
          ),
          if (_filter.count > 0)
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton.icon(
                onPressed: () {
                  setState(() => _filter = const TeamFilter());
                  _load(reset: true);
                },
                icon: const Icon(Icons.close, size: 18),
                label: Text('Xóa ${_filter.count} bộ lọc'),
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
          if (_error != null && _items.isEmpty)
            ErrorState(error: _error!, onRetry: () => _load(reset: true))
          else if (_loading && _items.isEmpty)
            const SkeletonList(count: 3)
          else if (_items.isEmpty)
            EmptyState(
              icon: Icons.search_off,
              title: 'Không tìm thấy nhóm',
              description: 'Thử từ khóa khác hoặc bỏ bớt bộ lọc.',
              action: _filter.count > 0 || _searchController.text.isNotEmpty
                  ? OutlinedButton(
                      onPressed: () {
                        _searchController.clear();
                        setState(() => _filter = const TeamFilter());
                        _load(reset: true);
                      },
                      child: const Text('Xóa bộ lọc'),
                    )
                  : null,
            )
          else ...[
            for (final team in _items)
              Padding(padding: const EdgeInsets.only(bottom: AppSpacing.s3), child: TeamListCard(team: team)),
            if (_hasMore)
              _loading
                  ? const Center(child: CircularProgressIndicator())
                  : OutlinedButton(onPressed: _load, child: const Text('Xem thêm')),
            if (_error != null) ErrorBanner(message: 'Không tải được trang tiếp theo.', onRetry: _load),
          ],
          if (!_hasMore && _items.isNotEmpty)
            Center(child: Text('Đã hết danh sách', style: theme.textTheme.bodyMedium)),
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
