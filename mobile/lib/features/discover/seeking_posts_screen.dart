import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/network/api_exception.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../my_team/my_team_api.dart' show domainsProvider, myTeamDetailProvider;
import '../suggestions/suggestion_widgets.dart' show showInviteSheet;
import '../suggestions/suggestions_api.dart' show CatalogRef, specializationsProvider;
import 'posts_api.dart';

/// S10 – tab "Sinh viên tìm nhóm" của Khám phá (MB-17): bài của mình (→ S11), lọc chuyên ngành, danh sách bài phân trang.
/// Trưởng nhóm còn chỗ thấy nút "Mời vào nhóm" (design-decisions B2).
// shortcut: chạm thẻ chưa mở S04 (hồ sơ sinh viên khác – MB-13 của Nam); nối route khi S04 xong.
class SeekingPostsTab extends ConsumerStatefulWidget {
  const SeekingPostsTab({super.key});

  @override
  ConsumerState<SeekingPostsTab> createState() => _SeekingPostsTabState();
}

class _SeekingPostsTabState extends ConsumerState<SeekingPostsTab> {
  int? _specializationId;
  final List<SeekingPost> _items = [];
  final Set<String> _invited = {};
  int _page = 0;
  bool _hasMore = true;
  bool _loading = false;
  Object? _error;

  @override
  void initState() {
    super.initState();
    _load(reset: true);
  }

  Future<void> _load({bool reset = false}) async {
    if (_loading) return;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final page = await ref.read(postsApiProvider).seeking(specializationId: _specializationId, page: reset ? 1 : _page + 1);
      if (!mounted) return;
      setState(() {
        if (reset) _items.clear();
        _items.addAll(page.items);
        _page = page.page;
        _hasMore = page.hasMore;
      });
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _refresh() async {
    ref.invalidate(mySeekingPostProvider);
    await _load(reset: true);
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final myTeam = ref.watch(myTeamDetailProvider).value;
    final canInvite = myTeam != null && myTeam.isLeader && myTeam.isOpen && !myTeam.isFull;
    final specializations = ref.watch(specializationsProvider).value ?? const <CatalogRef>[];
    final domains = {for (final d in ref.watch(domainsProvider).value ?? const <CatalogRef>[]) d.id: d};
    return RefreshIndicator(
      onRefresh: _refresh,
      child: ListView(
        padding: const EdgeInsets.all(AppSpacing.s4),
        children: [
          if (myTeam == null) ...[const _MyPostCard(), const SizedBox(height: AppSpacing.s3)],
          if (specializations.isNotEmpty)
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: [
                  for (final s in [null, ...specializations])
                    Padding(
                      padding: const EdgeInsets.only(right: AppSpacing.s2),
                      child: ChoiceChip(
                        label: Text(s?.name ?? 'Tất cả'),
                        selected: _specializationId == s?.id,
                        onSelected: (_) {
                          setState(() => _specializationId = s?.id);
                          _load(reset: true);
                        },
                      ),
                    ),
                ],
              ),
            ),
          const SizedBox(height: AppSpacing.s3),
          if (_error != null && _items.isEmpty)
            ErrorState(error: _error!, onRetry: () => _load(reset: true))
          else if (_loading && _items.isEmpty)
            const SkeletonList(count: 3)
          else if (_items.isEmpty)
            const EmptyState(
              icon: Icons.person_search_outlined,
              title: 'Chưa có sinh viên đang tìm nhóm',
              description: 'Kéo xuống để làm mới, hoặc thử chuyên ngành khác.',
            )
          else ...[
            for (final post in _items)
              Padding(
                padding: const EdgeInsets.only(bottom: AppSpacing.s3),
                child: SeekingPostCard(
                  post: post,
                  domains: [for (final id in post.domainIds) ?domains[id]],
                  action: !canInvite
                      ? null
                      : _invited.contains(post.student.id)
                          ? const Chip(avatar: Icon(Icons.check, size: 18), label: Text('Đã mời'))
                          : OutlinedButton.icon(
                              icon: const Icon(Icons.person_add_alt_outlined),
                              label: const Text('Mời vào nhóm'),
                              onPressed: () async {
                                if (await showInviteSheet(context, teamId: myTeam.id, student: post.student) && mounted) {
                                  setState(() => _invited.add(post.student.id));
                                }
                              },
                            ),
                ),
              ),
            if (_hasMore)
              _loading ? const Center(child: CircularProgressIndicator()) : OutlinedButton(onPressed: _load, child: const Text('Xem thêm')),
            if (_error != null) ErrorBanner(message: 'Không tải được trang tiếp theo.', onRetry: _load),
            if (!_hasMore) Center(child: Text('Đã hết danh sách', style: theme.textTheme.bodyMedium)),
          ],
        ],
      ),
    );
  }
}

/// Lối vào S11 cho sinh viên chưa có nhóm: chưa đăng → nút Đăng bài; đã đăng → tóm tắt + Sửa.
class _MyPostCard extends ConsumerWidget {
  const _MyPostCard();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final mine = ref.watch(mySeekingPostProvider).value;
    return Card.outlined(
      margin: EdgeInsets.zero,
      child: ListTile(
        leading: Icon(mine == null ? Icons.campaign_outlined : Icons.check_circle_outline),
        title: Text(mine == null ? 'Đăng bài "Tôi đang tìm nhóm"' : 'Bạn đang tìm nhóm'),
        subtitle: Text(mine?.title ?? 'Trưởng nhóm thấy vai trò bạn muốn và có thể mời bạn.'),
        trailing: Text(mine == null ? 'Đăng bài' : 'Sửa'),
        onTap: () => context.push('/sv/kham-pha/tim-nhom'),
      ),
    );
  }
}

/// Thẻ bài tìm nhóm: họ tên, MSSV · chuyên ngành, vai trò mong muốn, có thể đóng góp, kỹ năng, domain quan tâm.
class SeekingPostCard extends StatelessWidget {
  const SeekingPostCard({super.key, required this.post, this.domains = const [], this.action});

  final SeekingPost post;
  final List<CatalogRef> domains;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final student = post.student;
    return Card.outlined(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.s4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(student.fullName, style: theme.textTheme.titleMedium),
            Text([student.studentCode, ?student.specialization?.name].join(' · '), style: theme.textTheme.bodyMedium),
            const SizedBox(height: AppSpacing.s2),
            Text('Vai trò: ${post.title}', style: theme.textTheme.bodyLarge),
            if (post.description != null) ...[
              const SizedBox(height: AppSpacing.s1),
              Text(post.description!, style: theme.textTheme.bodyMedium, maxLines: 3, overflow: TextOverflow.ellipsis),
            ],
            if (post.skills.isNotEmpty || domains.isNotEmpty) ...[
              const SizedBox(height: AppSpacing.s2),
              Wrap(
                spacing: AppSpacing.s2,
                runSpacing: AppSpacing.s2,
                children: [
                  for (final s in post.skills) Chip(label: Text('${s.skill} ${s.level}/5')),
                  for (final d in domains) Tooltip(message: d.name, child: Chip(avatar: const Icon(Icons.category_outlined, size: 18), label: Text(d.code))),
                ],
              ),
            ],
            if (action != null) ...[const SizedBox(height: AppSpacing.s2), Align(alignment: Alignment.centerRight, child: action)],
          ],
        ),
      ),
    );
  }
}

/// S11 – Đăng / sửa bài "Tôi đang tìm nhóm" (MB-17): vai trò mong muốn, có thể đóng góp, domain quan tâm; nút Gỡ bài.
/// Kỹ năng hiện theo hồ sơ (S02), không sửa ở đây.
class SeekingPostFormScreen extends ConsumerWidget {
  const SeekingPostFormScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) => ref.watch(mySeekingPostProvider).when(
        loading: () => Scaffold(appBar: AppBar(title: const Text('Tôi đang tìm nhóm')), body: const SkeletonList()),
        error: (e, _) => Scaffold(appBar: AppBar(), body: ErrorState(error: e, onRetry: () => ref.invalidate(mySeekingPostProvider))),
        data: (post) => _SeekingPostForm(post: post),
      );
}

class _SeekingPostForm extends ConsumerStatefulWidget {
  const _SeekingPostForm({required this.post});

  final SeekingPost? post;

  @override
  ConsumerState<_SeekingPostForm> createState() => _SeekingPostFormState();
}

class _SeekingPostFormState extends ConsumerState<_SeekingPostForm> {
  late final _title = TextEditingController(text: widget.post?.title);
  late final _description = TextEditingController(text: widget.post?.description);
  late final Set<int> _domainIds = {...?widget.post?.domainIds};
  bool _saving = false;
  bool _submitted = false;
  ApiException? _error;

  bool get _isEdit => widget.post != null;

  @override
  void dispose() {
    _title.dispose();
    _description.dispose();
    super.dispose();
  }

  String? get _titleError {
    if (_error?.fieldErrors['title'] case [final first, ...]) return first;
    return _submitted && _title.text.trim().isEmpty ? 'Nhập vai trò mong muốn.' : null;
  }

  Future<void> _run(Future<void> Function(PostsApi api) action, String done) async {
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await action(ref.read(postsApiProvider));
      if (!mounted) return;
      ref.invalidate(mySeekingPostProvider);
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(done)));
      context.pop();
    } on ApiException catch (e) {
      setState(() {
        _saving = false;
        _error = e;
      });
    }
  }

  Future<void> _save() async {
    setState(() => _submitted = true);
    if (_titleError != null) return;
    await _run(
      (api) => api.saveMySeeking(title: _title.text.trim(), description: _description.text.trim(), domainIds: _domainIds.toList()),
      _isEdit ? 'Đã lưu bài' : 'Đã đăng bài',
    );
  }

  Future<void> _remove() async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Gỡ bài tìm nhóm?'),
        content: const Text('Trưởng nhóm sẽ không thấy bạn trong danh sách Sinh viên tìm nhóm nữa.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy')),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Gỡ bài')),
        ],
      ),
    );
    if (ok == true) await _run((api) => api.removeMySeeking(), 'Đã gỡ bài');
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final domains = ref.watch(domainsProvider).value ?? const <CatalogRef>[];
    final generalError = _error != null && _error!.fieldErrors.isEmpty;
    return Scaffold(
      appBar: AppBar(title: Text(_isEdit ? 'Sửa bài tìm nhóm' : 'Tôi đang tìm nhóm')),
      body: ListView(
        padding: const EdgeInsets.all(AppSpacing.s4),
        children: [
          if (generalError) ...[
            ErrorBanner(message: postErrors[_error!.code] ?? _error!.detail ?? _error!.title, onRetry: _saving ? null : _save),
            const SizedBox(height: AppSpacing.s4),
          ],
          TextField(
            controller: _title,
            maxLength: 150,
            decoration: InputDecoration(labelText: 'Vai trò mong muốn', hintText: 'Ví dụ: Lập trình mobile, thiết kế UI', errorText: _titleError),
            onChanged: (_) => setState(() => _error = null),
          ),
          const SizedBox(height: AppSpacing.s3),
          TextField(
            controller: _description,
            maxLength: 2000,
            minLines: 3,
            maxLines: 6,
            decoration: InputDecoration(
              labelText: 'Bạn có thể đóng góp gì',
              alignLabelWithHint: true,
              errorText: _error?.fieldErrors['description']?.first,
            ),
          ),
          const SizedBox(height: AppSpacing.s3),
          Text('Domain quan tâm', style: theme.textTheme.titleSmall),
          const SizedBox(height: AppSpacing.s2),
          Wrap(
            spacing: AppSpacing.s2,
            runSpacing: AppSpacing.s2,
            children: [
              for (final d in domains)
                FilterChip(
                  label: Text(d.code),
                  tooltip: d.name,
                  selected: _domainIds.contains(d.id),
                  onSelected: (on) => setState(() => on ? _domainIds.add(d.id) : _domainIds.remove(d.id)),
                ),
            ],
          ),
          const SizedBox(height: AppSpacing.s3),
          Text('Kỹ năng lấy theo hồ sơ của bạn.', style: theme.textTheme.bodyMedium),
          const SizedBox(height: AppSpacing.s6),
          LoadingButton(label: _isEdit ? 'Lưu' : 'Đăng bài', loading: _saving, onPressed: _saving ? null : _save),
          if (_isEdit) ...[
            const SizedBox(height: AppSpacing.s2),
            TextButton.icon(onPressed: _saving ? null : _remove, icon: const Icon(Icons.delete_outline), label: const Text('Gỡ bài')),
          ],
        ],
      ),
    );
  }
}
