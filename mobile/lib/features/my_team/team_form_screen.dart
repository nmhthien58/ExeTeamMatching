import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/network/api_exception.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../suggestions/suggestions_api.dart' show CatalogRef;
import 'my_team_api.dart';

/// S13 Tạo nhóm (MB-19) và S16 Sửa thông tin nhóm (MB-21): tên nhóm, tên đề tài, mô tả ý tưởng, domain (S14 – bottom sheet chọn nhiều).
/// [team] null = tạo mới. Lỗi gửi → banner lỗi, giữ nội dung; lỗi theo ô từ ProblemDetails hiện dưới ô đó.
class TeamFormScreen extends ConsumerStatefulWidget {
  const TeamFormScreen({super.key, this.team});

  final MyTeam? team;

  @override
  ConsumerState<TeamFormScreen> createState() => _TeamFormScreenState();
}

class _TeamFormScreenState extends ConsumerState<TeamFormScreen> {
  late final _name = TextEditingController(text: widget.team?.name);
  late final _project = TextEditingController(text: widget.team?.projectName);
  late final _idea = TextEditingController(text: widget.team?.ideaDescription);
  late List<CatalogRef> _domains = [...?widget.team?.domains];
  bool _saving = false;
  bool _submitted = false;
  ApiException? _error;

  bool get _isEdit => widget.team != null;

  @override
  void dispose() {
    _name.dispose();
    _project.dispose();
    _idea.dispose();
    super.dispose();
  }

  String? get _nameError {
    if (_error?.fieldErrors['name'] case [final first, ...]) return first;
    if (!_submitted) return null;
    return _name.text.trim().length < 2 ? 'Tên nhóm cần ít nhất 2 ký tự.' : null;
  }

  String? get _domainError {
    if (_error?.fieldErrors['domainIds'] case [final first, ...]) return first;
    return _submitted && _domains.isEmpty ? 'Chọn ít nhất một domain.' : null;
  }

  Future<void> _pickDomains() async {
    final picked = await showModalBottomSheet<List<CatalogRef>>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (_) => _DomainSheet(selected: _domains),
    );
    if (picked != null) setState(() => _domains = picked);
  }

  Future<void> _save() async {
    setState(() => _submitted = true);
    if (_nameError != null || _domainError != null) return;
    setState(() {
      _saving = true;
      _error = null;
    });
    final input = TeamInput(
      name: _name.text.trim(),
      projectName: _project.text.trim(),
      ideaDescription: _idea.text.trim(),
      domainIds: [for (final d in _domains) d.id],
      primaryDomainId: _domains.first.id,
    );
    try {
      final api = ref.read(myTeamApiProvider);
      _isEdit ? await api.update(widget.team!.id, input) : await api.create(input);
      if (!mounted) return;
      refreshMyTeam(ref);
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(_isEdit ? 'Đã lưu' : 'Đã tạo nhóm')));
      // Sau khi gửi thành công, Back không quay lại form (flutter-handoff mục 3).
      _isEdit ? context.pop() : context.go('/sv/nhom');
    } on ApiException catch (e) {
      setState(() {
        _saving = false;
        _error = e;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final generalError = _error != null && _error!.fieldErrors.isEmpty;
    return Scaffold(
      appBar: AppBar(title: Text(_isEdit ? 'Sửa thông tin' : 'Tạo nhóm')),
      body: ListView(
        padding: const EdgeInsets.all(AppSpacing.s4),
        children: [
          if (generalError) ...[
            ErrorBanner(message: _error!.detail ?? _error!.title, onRetry: _saving ? null : _save),
            const SizedBox(height: AppSpacing.s4),
          ],
          TextField(
            controller: _name,
            maxLength: 100,
            decoration: InputDecoration(labelText: 'Tên nhóm', errorText: _nameError),
            onChanged: (_) => setState(() => _error = null),
          ),
          const SizedBox(height: AppSpacing.s3),
          TextField(
            controller: _project,
            maxLength: 200,
            enabled: !(widget.team?.isInherited ?? false),
            decoration: InputDecoration(
              labelText: 'Tên đề tài',
              helperText: widget.team?.isInherited ?? false ? 'Đổi đề tài qua yêu cầu đổi nhóm.' : 'Có thể bổ sung sau.',
              errorText: _error?.fieldErrors['projectName']?.first,
            ),
          ),
          const SizedBox(height: AppSpacing.s3),
          if (widget.team?.isInherited ?? false)
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton.icon(
                onPressed: () => context.push('/sv/yeu-cau-doi-nhom'),
                icon: const Icon(Icons.published_with_changes_outlined, size: 18),
                label: const Text('Mở yêu cầu đổi nhóm'),
              ),
            ),
          TextField(
            controller: _idea,
            maxLength: 4000,
            minLines: 3,
            maxLines: 6,
            decoration: const InputDecoration(labelText: 'Mô tả ý tưởng', alignLabelWithHint: true),
          ),
          const SizedBox(height: AppSpacing.s3),
          Text('Domain', style: theme.textTheme.titleSmall),
          const SizedBox(height: AppSpacing.s2),
          Wrap(
            spacing: AppSpacing.s2,
            runSpacing: AppSpacing.s2,
            children: [
              for (final (i, d) in _domains.indexed)
                InputChip(
                  label: Text(i == 0 ? '${d.code} · ${d.name} (chính)' : '${d.code} · ${d.name}'),
                  onDeleted: () => setState(() => _domains = [..._domains]..removeAt(i)),
                  deleteButtonTooltipMessage: 'Bỏ ${d.code}',
                ),
              ActionChip(avatar: const Icon(Icons.add, size: 18), label: const Text('Chọn domain'), onPressed: _pickDomains),
            ],
          ),
          if (_domainError != null)
            Padding(
              padding: const EdgeInsets.only(top: AppSpacing.s2),
              child: Text(_domainError!, style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.error)),
            ),
          const SizedBox(height: AppSpacing.s6),
          LoadingButton(label: _isEdit ? 'Lưu' : 'Tạo nhóm', loading: _saving, onPressed: _saving ? null : _save),
        ],
      ),
    );
  }
}

/// S16 – mở form với dữ liệu nhóm hiện tại.
class EditTeamScreen extends ConsumerWidget {
  const EditTeamScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) => ref.watch(myTeamDetailProvider).when(
        loading: () => Scaffold(appBar: AppBar(title: const Text('Sửa thông tin')), body: const SkeletonList()),
        error: (e, _) => Scaffold(appBar: AppBar(), body: ErrorState(error: e, onRetry: () => refreshMyTeam(ref))),
        data: (t) => t == null
            ? Scaffold(appBar: AppBar(), body: const EmptyState(icon: Icons.group_off_outlined, title: 'Bạn chưa có nhóm'))
            : TeamFormScreen(team: t),
      );
}

/// S14 – Chọn domain D01–D18 có tìm kiếm; domain chọn đầu tiên là domain chính.
class _DomainSheet extends ConsumerStatefulWidget {
  const _DomainSheet({required this.selected});

  final List<CatalogRef> selected;

  @override
  ConsumerState<_DomainSheet> createState() => _DomainSheetState();
}

class _DomainSheetState extends ConsumerState<_DomainSheet> {
  late final List<CatalogRef> _picked = [...widget.selected];
  String _search = '';

  @override
  Widget build(BuildContext context) {
    final domains = ref.watch(domainsProvider);
    return SafeArea(
      child: ConstrainedBox(
        constraints: BoxConstraints(maxHeight: MediaQuery.sizeOf(context).height - 48),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s4),
              child: TextField(
                decoration: const InputDecoration(prefixIcon: Icon(Icons.search), hintText: 'Tìm domain'),
                onChanged: (v) => setState(() => _search = v.trim().toLowerCase()),
              ),
            ),
            Flexible(
              child: domains.when(
                loading: () => const SkeletonList(),
                error: (e, _) => ErrorState(error: e, onRetry: () => ref.invalidate(domainsProvider)),
                data: (all) => ListView(
                  shrinkWrap: true,
                  children: [
                    for (final d in all.where((d) => _search.isEmpty || '${d.code} ${d.name}'.toLowerCase().contains(_search)))
                      CheckboxListTile(
                        value: _picked.any((p) => p.id == d.id),
                        title: Text('${d.code} · ${d.name}'),
                        onChanged: (on) => setState(() => on! ? _picked.add(d) : _picked.removeWhere((p) => p.id == d.id)),
                      ),
                  ],
                ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.all(AppSpacing.s4),
              child: Row(
                children: [
                  TextButton(onPressed: () => Navigator.pop(context), child: const Text('Hủy')),
                  const Spacer(),
                  FilledButton(onPressed: () => Navigator.pop(context, _picked), child: Text('Chọn (${_picked.length})')),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
