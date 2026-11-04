import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../discover/post_form_view_model.dart';
import '../suggestions/suggestions_api.dart' show CatalogRef, specializationsProvider;
import 'my_team_api.dart';

/// S21 – Đăng / sửa bài tuyển thành viên (MB-25), chỉ trưởng nhóm: tiêu đề, chuyên ngành cần tìm, mô tả yêu cầu;
/// số chỗ trống tính theo sĩ số nhóm. Nút Đóng bài khi đang có bài mở.
class RecruitmentPostScreen extends ConsumerWidget {
  const RecruitmentPostScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) => ref.watch(myTeamDetailProvider).when(
        loading: () => Scaffold(appBar: AppBar(title: const Text('Bài tuyển thành viên')), body: const SkeletonList()),
        error: (e, _) => Scaffold(appBar: AppBar(), body: ErrorState(error: e, onRetry: () => refreshMyTeam(ref))),
        data: (team) => team == null || !team.isLeader
            ? Scaffold(
                appBar: AppBar(title: const Text('Bài tuyển thành viên')),
                body: const EmptyState(icon: Icons.lock_outline, title: 'Chỉ trưởng nhóm đăng bài tuyển'),
              )
            : _RecruitmentForm(team: team),
      );
}

class _RecruitmentForm extends ConsumerStatefulWidget {
  const _RecruitmentForm({required this.team});

  final MyTeam team;

  @override
  ConsumerState<_RecruitmentForm> createState() => _RecruitmentFormState();
}

class _RecruitmentFormState extends ConsumerState<_RecruitmentForm> {
  late final _post = widget.team.recruitmentPost;
  late final _title = TextEditingController(text: _post?.title);
  late final _description = TextEditingController(text: _post?.description);
  late final Set<int> _specializationIds = {...?_post?.specializationIds};

  bool get _isEdit => _post != null;

  RecruitmentPostViewModel get _vm => ref.read(recruitmentPostViewModelProvider(widget.team.id).notifier);

  @override
  void dispose() {
    _title.dispose();
    _description.dispose();
    super.dispose();
  }

  void _handle(PostFormOutcome outcome) {
    if (outcome case PostFormDone(:final message) when mounted) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
      context.pop();
    }
  }

  Future<void> _save() async => _handle(
        await _vm.save(title: _title.text, description: _description.text, specializationIds: _specializationIds.toList(), isEdit: _isEdit),
      );

  Future<void> _close() async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Đóng bài tuyển?'),
        content: const Text('Nhóm sẽ không còn hiện mục "Đang tuyển" ở trang chi tiết nhóm.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy')),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Đóng bài')),
        ],
      ),
    );
    if (ok == true) _handle(await _vm.close());
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final specializations = ref.watch(specializationsProvider).value ?? const <CatalogRef>[];
    final openSlots = widget.team.maxSize - widget.team.memberCount;
    final state = ref.watch(recruitmentPostViewModelProvider(widget.team.id));
    final saving = state.saving;
    return Scaffold(
      appBar: AppBar(title: Text(_isEdit ? 'Sửa bài tuyển' : 'Đăng bài tuyển')),
      body: ListView(
        padding: const EdgeInsets.all(AppSpacing.s4),
        children: [
          if (state.generalError case final message?) ...[
            ErrorBanner(message: message, onRetry: saving ? null : _save),
            const SizedBox(height: AppSpacing.s4),
          ],
          Card.outlined(
            margin: EdgeInsets.zero,
            child: ListTile(
              leading: const Icon(Icons.event_seat_outlined),
              title: Text('Còn $openSlots chỗ trống'),
              subtitle: Text('${widget.team.memberCount}/${widget.team.maxSize} thành viên – tự cập nhật khi có người vào nhóm.'),
            ),
          ),
          const SizedBox(height: AppSpacing.s4),
          TextField(
            controller: _title,
            maxLength: 150,
            decoration: InputDecoration(labelText: 'Tiêu đề', hintText: 'Ví dụ: Cần 1 bạn Marketing', errorText: state.titleError),
            onChanged: _vm.titleChanged,
          ),
          const SizedBox(height: AppSpacing.s3),
          Text('Chuyên ngành cần tìm', style: theme.textTheme.titleSmall),
          const SizedBox(height: AppSpacing.s2),
          Wrap(
            spacing: AppSpacing.s2,
            runSpacing: AppSpacing.s2,
            children: [
              for (final s in specializations)
                FilterChip(
                  label: Text(s.name),
                  selected: _specializationIds.contains(s.id),
                  onSelected: (on) => setState(() => on ? _specializationIds.add(s.id) : _specializationIds.remove(s.id)),
                ),
            ],
          ),
          if (state.fieldError('specializationIds') case final message?)
            Padding(
              padding: const EdgeInsets.only(top: AppSpacing.s2),
              child: Text(message, style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.error)),
            ),
          const SizedBox(height: AppSpacing.s4),
          TextField(
            controller: _description,
            maxLength: 2000,
            minLines: 3,
            maxLines: 6,
            decoration: InputDecoration(labelText: 'Mô tả yêu cầu', alignLabelWithHint: true, errorText: state.fieldError('description')),
          ),
          const SizedBox(height: AppSpacing.s6),
          LoadingButton(label: _isEdit ? 'Lưu' : 'Đăng bài', loading: saving, onPressed: saving ? null : _save),
          if (_isEdit) ...[
            const SizedBox(height: AppSpacing.s2),
            TextButton.icon(onPressed: saving ? null : _close, icon: const Icon(Icons.campaign_outlined), label: const Text('Đóng bài')),
          ],
        ],
      ),
    );
  }
}
