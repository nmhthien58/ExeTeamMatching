import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/phase.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../suggestions/suggestions_api.dart';
import 'change_requests_api.dart';

const changeRequestListPath = '/sv/yeu-cau-doi-nhom';
const changeRequestCreatePath = '/sv/yeu-cau-doi-nhom/tao';
String changeRequestDetailPath(String id) => '/sv/yeu-cau-doi-nhom/$id';

/// S26 – danh sách yêu cầu của nhóm EXE201; chờ router chung đăng ký route.
class ChangeRequestListScreen extends ConsumerWidget {
  const ChangeRequestListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final course = ref.watch(myCourseProvider);
    final team = ref.watch(myTeamProvider);
    final round = ref.watch(openChangeRoundProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Yêu cầu đổi nhóm')),
      body: course.when(
        loading: () => const SkeletonList(),
        error: (e, _) => ErrorState(
          error: e,
          onRetry: () => ref.invalidate(myCourseProvider),
        ),
        data: (code) => code != 'EXE201'
            ? const EmptyState(
                icon: Icons.lock_outline,
                title: 'Chỉ áp dụng cho EXE201',
                description:
                    'Yêu cầu đổi nhóm chỉ mở với nhóm kế thừa ở EXE201.',
              )
            : team.when(
                loading: () => const SkeletonList(),
                error: (e, _) => ErrorState(
                  error: e,
                  onRetry: () => ref.invalidate(myTeamProvider),
                ),
                data: (mine) => mine == null
                    ? const EmptyState(
                        icon: Icons.groups_outlined,
                        title: 'Bạn chưa có nhóm',
                        description:
                            'Yêu cầu đổi nhóm sẽ hiện khi bạn là thành viên của nhóm EXE201.',
                      )
                    : _RequestList(
                        teamId: mine.card.id,
                        isLeader: mine.isLeader,
                        round: round,
                        ref: ref,
                      ),
              ),
      ),
    );
  }
}

class _RequestList extends StatefulWidget {
  const _RequestList({
    required this.teamId,
    required this.isLeader,
    required this.round,
    required this.ref,
  });
  final String teamId;
  final bool isLeader;
  final AsyncValue<bool> round;
  final WidgetRef ref;

  @override
  State<_RequestList> createState() => _RequestListState();
}

class _RequestListState extends State<_RequestList> {
  String? _status;
  String? _round;

  @override
  Widget build(BuildContext context) {
    final requests = widget.ref.watch(changeRequestsProvider(widget.teamId));
    return requests.when(
      loading: () => const SkeletonList(),
      error: (e, _) => ErrorState(
        error: e,
        onRetry: () =>
            widget.ref.invalidate(changeRequestsProvider(widget.teamId)),
      ),
      data: (items) {
        final rounds = items.map((item) => item.roundName).toSet().toList()
          ..sort();
        final visible = items
            .where(
              (item) =>
                  (_status == null || item.status == _status) &&
                  (_round == null || item.roundName == _round),
            )
            .toList();
        return RefreshIndicator(
          onRefresh: () async =>
              widget.ref.invalidate(changeRequestsProvider(widget.teamId)),
          child: ListView(
            padding: const EdgeInsets.all(AppSpacing.s4),
            children: [
              widget.round.when(
                loading: () => const LinearProgressIndicator(),
                error: (_, _) => const SizedBox.shrink(),
                data: (open) => open && widget.isLeader
                    ? Padding(
                        padding: const EdgeInsets.only(bottom: AppSpacing.s3),
                        child: FilledButton.icon(
                          onPressed: () =>
                              context.push(changeRequestCreatePath),
                          icon: const Icon(Icons.edit_note),
                          label: const Text('Tạo yêu cầu đổi nhóm'),
                        ),
                      )
                    : const SizedBox.shrink(),
              ),
              if (items.isNotEmpty) ...[
                DropdownButtonFormField<String>(
                  initialValue: _status,
                  decoration: const InputDecoration(labelText: 'Trạng thái'),
                  items: const [
                    DropdownMenuItem(
                      value: null,
                      child: Text('Tất cả trạng thái'),
                    ),
                    DropdownMenuItem(
                      value: 'PENDING',
                      child: Text('Chờ duyệt'),
                    ),
                    DropdownMenuItem(
                      value: 'APPROVED',
                      child: Text('Đã duyệt'),
                    ),
                    DropdownMenuItem(
                      value: 'REJECTED',
                      child: Text('Đã từ chối'),
                    ),
                    DropdownMenuItem(value: 'CANCELLED', child: Text('Đã hủy')),
                  ],
                  onChanged: (value) => setState(() => _status = value),
                ),
                const SizedBox(height: AppSpacing.s3),
                DropdownButtonFormField<String>(
                  initialValue: _round,
                  decoration: const InputDecoration(labelText: 'Đợt'),
                  items: [
                    const DropdownMenuItem(
                      value: null,
                      child: Text('Tất cả đợt'),
                    ),
                    for (final round in rounds)
                      DropdownMenuItem(value: round, child: Text(round)),
                  ],
                  onChanged: (value) => setState(() => _round = value),
                ),
                const SizedBox(height: AppSpacing.s3),
              ],
              if (items.isEmpty)
                const EmptyState(
                  icon: Icons.inbox_outlined,
                  title: 'Chưa có yêu cầu đổi nhóm',
                  description: 'Yêu cầu của nhóm bạn sẽ xuất hiện tại đây.',
                )
              else if (visible.isEmpty)
                const EmptyState(title: 'Không có yêu cầu phù hợp')
              else
                for (final request in visible)
                  Card.outlined(
                    child: ListTile(
                      title: Text(request.roundName),
                      subtitle: Text(
                        '${request.changedMemberCount} thay đổi · ${request.changedPercent.toStringAsFixed(0)}% · ${_date(request.createdAt)}',
                      ),
                      trailing: _StatusChip(status: request.status),
                      onTap: () =>
                          context.push(changeRequestDetailPath(request.id)),
                    ),
                  ),
            ],
          ),
        );
      },
    );
  }
}

/// S27/S28 – chọn các thay đổi mốc, thêm đề tài nếu cần, rồi gửi lý do xác nhận.
class CreateChangeRequestScreen extends ConsumerStatefulWidget {
  const CreateChangeRequestScreen({super.key});
  @override
  ConsumerState<CreateChangeRequestScreen> createState() =>
      _CreateChangeRequestScreenState();
}

class _CreateChangeRequestScreenState
    extends ConsumerState<CreateChangeRequestScreen> {
  final _name = TextEditingController();
  final _description = TextEditingController();
  final _reason = TextEditingController();
  final _selected = <String>{};
  String? _error;
  bool _busy = false;
  int _step = 0;

  @override
  void dispose() {
    _name.dispose();
    _description.dispose();
    _reason.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final course = ref.watch(myCourseProvider);
    final team = ref.watch(myTeamProvider);
    return Scaffold(
      appBar: AppBar(
        title: Text(
          _step == 0 ? 'Chọn thay đổi · Bước 1/2' : 'Lý do · Bước 2/2',
        ),
      ),
      body: course.when(
        loading: () => const SkeletonList(),
        error: (e, _) => ErrorState(
          error: e,
          onRetry: () => ref.invalidate(myCourseProvider),
        ),
        data: (code) => code != 'EXE201'
            ? const EmptyState(
                icon: Icons.lock_outline,
                title: 'Chỉ áp dụng cho EXE201',
                description:
                    'Yêu cầu đổi nhóm chỉ mở với nhóm kế thừa ở EXE201.',
              )
            : team.when(
                loading: () => const SkeletonList(),
                error: (e, _) => ErrorState(
                  error: e,
                  onRetry: () => ref.invalidate(myTeamProvider),
                ),
                data: (mine) => mine == null
                    ? const EmptyState(
                        icon: Icons.groups_outlined,
                        title: 'Bạn chưa có nhóm',
                        description:
                            'Bạn cần thuộc một nhóm EXE201 để tạo yêu cầu.',
                      )
                    : !mine.isLeader
                    ? const EmptyState(
                        icon: Icons.lock_outline,
                        title: 'Chỉ trưởng nhóm tạo được yêu cầu',
                        description:
                            'Bạn vẫn có thể theo dõi yêu cầu đổi nhóm của nhóm mình.',
                      )
                    : _content(mine),
              ),
      ),
    );
  }

  Widget _content(TeamDetail team) {
    final round = ref.watch(openChangeRoundProvider);
    if (round.isLoading) return const SkeletonList();
    if (round.hasError) {
      return ErrorState(
        error: round.error!,
        onRetry: () => ref.invalidate(openChangeRoundProvider),
      );
    }
    if (round.value != true) {
      return const EmptyState(
        icon: Icons.event_busy,
        title: 'Đợt đổi nhóm đã đóng',
        description: 'Bạn chỉ có thể gửi yêu cầu trong đợt đổi nhóm đang mở.',
      );
    }
    final pending = ref.watch(pendingChangesProvider(team.card.id));
    return pending.when(
      loading: () => const SkeletonList(),
      error: (e, _) => ErrorState(
        error: e,
        onRetry: () => ref.invalidate(pendingChangesProvider(team.card.id)),
      ),
      data: (data) =>
          _step == 0 ? _selectStep(team, data) : _reasonStep(team, data),
    );
  }

  Widget _selectStep(TeamDetail team, PendingChanges data) {
    final items = data.pendingItems;
    final projected = _projectedMembers(team, data, _selected);
    final count = projected.length;
    final specializationCount = projected.values
        .map((member) => member.specialization?.id)
        .whereType<int>()
        .toSet()
        .length;
    final scheme = Theme.of(context).colorScheme;
    return ListView(
      padding: const EdgeInsets.all(AppSpacing.s4),
      children: [
        Text(
          'Nhóm mốc EXE101: ${data.baselineMembers.length} thành viên',
          style: Theme.of(context).textTheme.titleMedium,
        ),
        Text('Sau thay đổi đã chọn: $count thành viên'),
        if (data.currentProjectName != null)
          Text('Đề tài hiện tại: ${data.currentProjectName}'),
        const SizedBox(height: AppSpacing.s3),
        for (final item in items)
          Card.outlined(
            child: CheckboxListTile(
              value: _selected.contains(item.id),
              onChanged: (value) => setState(
                () => value == true
                    ? _selected.add(item.id)
                    : _selected.remove(item.id),
              ),
              title: Text(item.title),
              controlAffinity: ListTileControlAffinity.leading,
            ),
          ),
        const SizedBox(height: AppSpacing.s2),
        Text(
          'Đổi đề tài (không bắt buộc)',
          style: Theme.of(context).textTheme.titleMedium,
        ),
        TextField(
          controller: _name,
          maxLength: 200,
          onChanged: (_) => setState(() {}),
          decoration: const InputDecoration(
            labelText: 'Tên đề tài mới',
            border: OutlineInputBorder(),
          ),
        ),
        const SizedBox(height: AppSpacing.s2),
        TextField(
          controller: _description,
          maxLength: 4000,
          minLines: 2,
          maxLines: 4,
          decoration: const InputDecoration(
            labelText: 'Mô tả đề tài mới',
            border: OutlineInputBorder(),
          ),
        ),
        if (count < 4)
          _Notice(
            icon: Icons.error_outline,
            color: scheme.error,
            text: 'Nhóm còn dưới 4 người. Không thể gửi yêu cầu.',
          )
        else if (count > 6)
          _Notice(
            icon: Icons.error_outline,
            color: scheme.error,
            text: 'Nhóm vượt quá 6 người. Không thể gửi yêu cầu.',
          )
        else if (specializationCount < 2)
          _Notice(
            icon: Icons.warning_amber,
            color: scheme.tertiary,
            text:
                'Nhóm chưa có đủ 2 chuyên ngành. Bộ môn sẽ kiểm tra khi chốt nhóm.',
          ),
        const SizedBox(height: AppSpacing.s4),
        FilledButton(
          onPressed:
              _selected.isEmpty && _name.text.trim().isEmpty ||
                  count < 4 ||
                  count > 6 ||
                  (_name.text.trim().isNotEmpty && _name.text.trim().length < 2)
              ? null
              : () => setState(() => _step = 1),
          child: const Text('Tiếp tục'),
        ),
        const SizedBox(height: AppSpacing.s2),
        Text(
          'Chọn những thay đổi cần đưa vào yêu cầu. Đề tài mới sẽ được gửi cùng yêu cầu này.',
          style: Theme.of(context).textTheme.bodyMedium,
        ),
      ],
    );
  }

  Widget _reasonStep(TeamDetail team, PendingChanges data) {
    final memberChangeCount = data.pendingItems
        .where(
          (item) =>
              (item.action == 'ADD' || item.action == 'REMOVE') &&
              _selected.contains(item.id),
        )
        .length;
    final total = data.baselineMembers.length;
    final percent = total == 0 ? 0 : (100 * memberChangeCount / total);
    return ListView(
      padding: const EdgeInsets.all(AppSpacing.s4),
      children: [
        Row(
          children: [
            Expanded(
              child: _MetricCard(
                label: 'Thành viên thay đổi',
                value: '$memberChangeCount',
              ),
            ),
            const SizedBox(width: AppSpacing.s3),
            Expanded(
              child: _MetricCard(
                label: 'Tỷ lệ thay đổi',
                value: '${percent.toStringAsFixed(0)}%',
              ),
            ),
          ],
        ),
        const SizedBox(height: AppSpacing.s4),
        Text(
          'Xem lại thay đổi',
          style: Theme.of(context).textTheme.titleMedium,
        ),
        for (final item in data.pendingItems.where(
          (i) => _selected.contains(i.id),
        ))
          ListTile(
            leading: const Icon(Icons.change_circle_outlined),
            title: Text(item.title),
          ),
        if (_name.text.trim().isNotEmpty)
          ListTile(
            leading: const Icon(Icons.topic_outlined),
            title: Text('Đổi đề tài: ${_name.text.trim()}'),
          ),
        const SizedBox(height: AppSpacing.s3),
        TextField(
          controller: _reason,
          maxLength: 1000,
          minLines: 3,
          maxLines: 6,
          decoration: InputDecoration(
            labelText: 'Lý do thay đổi',
            helperText: 'Nhập ít nhất 10 ký tự.',
            errorText: _error,
            border: const OutlineInputBorder(),
          ),
        ),
        const SizedBox(height: AppSpacing.s4),
        if (_error != null) ErrorBanner(message: _error!),
        FilledButton(
          onPressed: _busy ? null : () => _submit(team, data),
          child: Text(_busy ? 'Đang gửi…' : 'Gửi yêu cầu đổi nhóm'),
        ),
        TextButton(
          onPressed: _busy ? null : () => setState(() => _step = 0),
          child: const Text('Quay lại'),
        ),
      ],
    );
  }

  Future<void> _submit(TeamDetail team, PendingChanges data) async {
    final reason = _reason.text.trim();
    if (reason.length < 10) {
      setState(() => _error = 'Lý do cần ít nhất 10 ký tự.');
      return;
    }
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Gửi yêu cầu đổi nhóm?'),
        content: const Text(
          'Bộ môn sẽ xem xét các thay đổi trước khi áp dụng.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Hủy'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Gửi yêu cầu'),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final api = ref.read(changeRequestsApiProvider);
      final itemIds = [..._selected];
      if (_name.text.trim().isNotEmpty) {
        final oldProjectIds = data.pendingItems
            .where((item) => item.action == 'PROJECT_CHANGE')
            .map((item) => item.id)
            .toSet();
        itemIds.removeWhere(oldProjectIds.contains);
        final projectChange = await api.setProjectChange(
          team.card.id,
          name: _name.text.trim(),
          description: _description.text,
        );
        if (!itemIds.contains(projectChange.id)) {
          itemIds.add(projectChange.id);
        }
      }
      final request = await api.create(
        team.card.id,
        itemIds: itemIds,
        reason: reason,
      );
      ref.invalidate(changeRequestsProvider(team.card.id));
      if (mounted) {
        context.go(changeRequestDetailPath(request.id));
      }
    } catch (e) {
      if (mounted) {
        setState(
          () =>
              _error = 'Không gửi được yêu cầu. Nội dung đã nhập được giữ lại.',
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }
}

/// S29 – dòng thời gian và trạng thái xét duyệt.
class ChangeRequestDetailScreen extends ConsumerWidget {
  const ChangeRequestDetailScreen({required this.requestId, super.key});
  final String requestId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(changeRequestDetailProvider(requestId));
    return Scaffold(
      appBar: AppBar(title: const Text('Chi tiết yêu cầu')),
      body: detail.when(
        loading: () => const SkeletonList(),
        error: (e, _) => ErrorState(
          error: e,
          onRetry: () => ref.invalidate(changeRequestDetailProvider(requestId)),
        ),
        data: (value) {
          final request = value.request;
          return RefreshIndicator(
            onRefresh: () async =>
                ref.invalidate(changeRequestDetailProvider(requestId)),
            child: ListView(
              padding: const EdgeInsets.all(AppSpacing.s4),
              children: [
                _StatusChip(status: request.status),
                const SizedBox(height: AppSpacing.s3),
                _Timeline(request: request),
                Row(
                  children: [
                    Expanded(
                      child: _MetricCard(
                        label: 'Thành viên thay đổi',
                        value: '${request.changedMemberCount}',
                      ),
                    ),
                    const SizedBox(width: AppSpacing.s3),
                    Expanded(
                      child: _MetricCard(
                        label: 'Tỷ lệ thay đổi',
                        value: '${request.changedPercent.toStringAsFixed(0)}%',
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: AppSpacing.s3),
                Text('Lý do', style: Theme.of(context).textTheme.titleMedium),
                Text(request.reason),
                const SizedBox(height: AppSpacing.s3),
                Text(
                  'Thay đổi',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                for (final item in value.items)
                  ListTile(
                    leading: const Icon(Icons.change_circle_outlined),
                    title: Text(item.title),
                  ),
                if (request.status == 'REJECTED' && request.reviewNote != null)
                  _Notice(
                    icon: Icons.info_outline,
                    color: Theme.of(context).colorScheme.error,
                    text: 'Ghi chú Bộ môn: ${request.reviewNote}',
                  ),
                if (request.status == 'REJECTED')
                  FilledButton(
                    onPressed: () => context.push(changeRequestCreatePath),
                    child: const Text('Tạo yêu cầu mới'),
                  ),
              ],
            ),
          );
        },
      ),
    );
  }
}

class _Timeline extends StatelessWidget {
  const _Timeline({required this.request});
  final ChangeRequest request;
  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      children: [
        ListTile(
          leading: Icon(Icons.check_circle, color: theme.colorScheme.primary),
          title: const Text('Đã gửi'),
          subtitle: Text(_date(request.createdAt)),
        ),
        ListTile(
          leading: Icon(
            request.status == 'PENDING'
                ? Icons.hourglass_top
                : Icons.check_circle,
            color: request.status == 'PENDING'
                ? theme.colorScheme.tertiary
                : theme.colorScheme.primary,
          ),
          title: const Text('Bộ môn xét duyệt'),
          subtitle: Text(
            request.status == 'CANCELLED'
                ? 'Yêu cầu đã hủy'
                : request.reviewedAt == null
                ? 'Đang chờ duyệt'
                : '${request.reviewedByName ?? 'Bộ môn'} · ${_date(request.reviewedAt!)}',
          ),
        ),
        if (request.status != 'PENDING')
          ListTile(
            leading: Icon(
              request.status == 'APPROVED'
                  ? Icons.task_alt
                  : Icons.cancel_outlined,
              color: request.status == 'APPROVED'
                  ? theme.colorScheme.primary
                  : theme.colorScheme.error,
            ),
            title: Text(
              request.status == 'CANCELLED'
                  ? 'Đã hủy'
                  : request.status == 'APPROVED'
                  ? 'Đã duyệt'
                  : 'Đã từ chối',
            ),
            subtitle: request.reviewedAt == null
                ? null
                : Text(_date(request.reviewedAt!)),
          ),
      ],
    );
  }
}

class _MetricCard extends StatelessWidget {
  const _MetricCard({required this.label, required this.value});
  final String label;
  final String value;
  @override
  Widget build(BuildContext context) => Card.outlined(
    child: Padding(
      padding: const EdgeInsets.all(AppSpacing.s3),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: Theme.of(context).textTheme.bodyMedium),
          const SizedBox(height: AppSpacing.s1),
          Text(value, style: Theme.of(context).textTheme.titleLarge),
        ],
      ),
    ),
  );
}

class _Notice extends StatelessWidget {
  const _Notice({required this.icon, required this.color, required this.text});
  final IconData icon;
  final Color color;
  final String text;
  @override
  Widget build(BuildContext context) => Card(
    color: color.withValues(alpha: .08),
    child: Padding(
      padding: const EdgeInsets.all(AppSpacing.s3),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, color: color),
          const SizedBox(width: AppSpacing.s2),
          Expanded(
            child: Text(text, style: Theme.of(context).textTheme.bodyMedium),
          ),
        ],
      ),
    ),
  );
}

class _StatusChip extends StatelessWidget {
  const _StatusChip({required this.status});
  final String status;
  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final (label, color, icon) = switch (status) {
      'CANCELLED' => (
        'Đã hủy',
        theme.colorScheme.onSurfaceVariant,
        Icons.block,
      ),
      'APPROVED' => (
        'Đã duyệt',
        theme.colorScheme.primary,
        Icons.check_circle_outline,
      ),
      'REJECTED' => (
        'Đã từ chối',
        theme.colorScheme.error,
        Icons.cancel_outlined,
      ),
      _ => ('Chờ duyệt', theme.colorScheme.tertiary, Icons.hourglass_top),
    };
    return Chip(
      avatar: Icon(icon, size: 18, color: color),
      label: Text(label),
    );
  }
}

String _date(DateTime value) {
  final local = value.toLocal();
  return '${local.day.toString().padLeft(2, '0')}/${local.month.toString().padLeft(2, '0')}/${local.year} ${local.hour.toString().padLeft(2, '0')}:${local.minute.toString().padLeft(2, '0')}';
}

Map<String, StudentRef> _projectedMembers(
  TeamDetail team,
  PendingChanges data,
  Set<String> selected,
) {
  final members = {for (final member in team.members) member.id: member};
  for (final item in data.pendingItems) {
    if (!selected.contains(item.id)) continue;
    final student = item.student;
    if (student == null) continue;
    if (item.action == 'ADD') members[student.id] = student;
    if (item.action == 'REMOVE') members.remove(student.id);
  }
  return members;
}
