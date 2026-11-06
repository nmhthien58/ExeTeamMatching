import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/firebase/remote_config_service.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../../shared/widgets/team_requirement_bar.dart';
import '../my_team/my_team_api.dart';
import '../my_team/my_team_screen.dart' show teamStatusChip;
import '../suggestions/suggestion_widgets.dart' show DomainChips;
import '../workspace/workspace_widgets.dart';
import '../invitations/invitations_api.dart' show myJoinRequestsProvider;
import '../invitations/invitations_screen.dart' show confirmCancelJoinRequest;
import '../invitations/invitations_view_model.dart' show sentJoinRequestsViewModelProvider;
import 'discover_api.dart';
import 'idea_suggest_sheet.dart';
import 'join_request_view_model.dart';

/// S08 – Chi tiết nhóm xem từ bên ngoài (MB-16): tên + trạng thái, domain, ý tưởng, thanh điều kiện đầy đủ, thành viên + chuyên ngành,
/// bài tuyển; nút chính theo quan hệ: Xin tham gia (S09) · đã gửi yêu cầu · được mời (→ S12) · nhóm của bạn. Không hiện liên hệ.
class TeamDetailScreen extends ConsumerWidget {
  const TeamDetailScreen({super.key, required this.teamId});

  final String teamId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final team = ref.watch(teamDetailProvider(teamId));
    return Scaffold(
      appBar: AppBar(title: const Text('Chi tiết nhóm')),
      body: team.when(
        loading: () => const SkeletonList(),
        error: (e, _) => ErrorState(error: e, onRetry: () => ref.invalidate(teamDetailProvider(teamId))),
        data: (t) => _Detail(team: t),
      ),
    );
  }
}

class _Detail extends ConsumerWidget {
  const _Detail({required this.team});

  final MyTeam team;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final (label, tone, icon) = teamStatusChip(team.status);
    return Column(
      children: [
        Expanded(
          child: ListView(
            padding: const EdgeInsets.all(AppSpacing.s4),
            children: [
              Wrap(
                alignment: WrapAlignment.spaceBetween,
                crossAxisAlignment: WrapCrossAlignment.center,
                spacing: AppSpacing.s2,
                runSpacing: AppSpacing.s2,
                children: [Text(team.name, style: theme.textTheme.headlineSmall), StatusChip(label: label, tone: tone, icon: icon)],
              ),
              if (team.projectName != null) Text(team.projectName!, style: theme.textTheme.titleMedium),
              const SizedBox(height: AppSpacing.s2),
              DomainChips(domains: team.domains),
              if (team.ideaDescription != null) ...[const SizedBox(height: AppSpacing.s3), Text(team.ideaDescription!)],
              const SizedBox(height: AppSpacing.s3),
              Card.outlined(
                margin: EdgeInsets.zero,
                child: Padding(
                  padding: const EdgeInsets.all(AppSpacing.s4),
                  child: TeamRequirementBar(
                    memberCount: team.memberCount,
                    maxMembers: team.maxSize,
                    minMembers: team.minSize,
                    specializationCount: team.specializationCount,
                    minSpecializations: team.minSpecializations,
                    full: true,
                  ),
                ),
              ),
              const SizedBox(height: AppSpacing.s3),
              Text('Thành viên', style: theme.textTheme.titleSmall),
              for (final m in team.members)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  leading: Avatar(name: m.fullName),
                  title: Text(m.fullName),
                  subtitle: Text(m.specialization?.name ?? ''),
                  trailing: m.isLeader ? const StatusChip(label: 'Trưởng nhóm', tone: StatusTone.info) : null,
                ),
              if (team.recruitment != null) ...[
                const SizedBox(height: AppSpacing.s3),
                Card.outlined(
                  margin: EdgeInsets.zero,
                  child: ListTile(leading: const Icon(Icons.campaign_outlined), title: const Text('Đang tuyển'), subtitle: Text(team.recruitment!)),
                ),
              ],
            ],
          ),
        ),
        SafeArea(child: Padding(padding: const EdgeInsets.all(AppSpacing.s4), child: _Action(team: team))),
      ],
    );
  }
}

/// Nút chính theo trạng thái (flutter-handoff S08); nút khóa luôn kèm lý do.
class _Action extends ConsumerWidget {
  const _Action({required this.team});

  final MyTeam team;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final myTeam = ref.watch(myTeamDetailProvider).value;
    Widget reason(String text) => Text(text, textAlign: TextAlign.center, style: Theme.of(context).textTheme.bodyMedium);
    return switch (team.relation) {
      'LEADER' || 'MEMBER' => OutlinedButton(onPressed: () => context.go('/sv/nhom'), child: const Text('Đây là nhóm của bạn')),
      'REQUESTED' => _Requested(team: team),
      'INVITED' => FilledButton(onPressed: () => context.push('/sv/loi-moi'), child: const Text('Xem lời mời')),
      _ when myTeam != null => reason('Bạn đã có nhóm.'),
      _ when team.isFull => reason('Nhóm đã đủ ${team.maxSize} người.'),
      _ when !team.isOpen => reason('Nhóm không còn nhận thành viên.'),
      _ => FilledButton(
          onPressed: () async {
            final sent = await showModalBottomSheet<bool>(
              context: context,
              isScrollControlled: true,
              showDragHandle: true,
              builder: (_) => JoinRequestSheet(teamId: team.id),
            );
            if (sent == true && context.mounted) {
              ref.invalidate(teamDetailProvider(team.id));
              ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Đã gửi yêu cầu')));
            }
          },
          style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(48)),
          child: const Text('Xin tham gia'),
        ),
    };
  }
}

/// Đã gửi yêu cầu đang chờ: trạng thái + nút Hủy yêu cầu (hỏi xác nhận). Mã yêu cầu lấy từ `GET /api/join-requests/me`.
class _Requested extends ConsumerStatefulWidget {
  const _Requested({required this.team});

  final MyTeam team;

  @override
  ConsumerState<_Requested> createState() => _RequestedState();
}

class _RequestedState extends ConsumerState<_Requested> {
  bool _cancelling = false;

  Future<void> _cancel(String joinRequestId) async {
    if (!await confirmCancelJoinRequest(context, widget.team.name) || !mounted) return;
    setState(() => _cancelling = true);
    final error = await ref.read(sentJoinRequestsViewModelProvider.notifier).cancelById(joinRequestId);
    if (!mounted) return;
    setState(() => _cancelling = false);
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error ?? 'Đã hủy yêu cầu')));
    if (error == null) ref.invalidate(teamDetailProvider(widget.team.id));
  }

  @override
  Widget build(BuildContext context) {
    final pending = ref.watch(myJoinRequestsProvider).value?.where((r) => r.team?.id == widget.team.id && r.status == 'PENDING').firstOrNull;
    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Center(child: StatusChip(label: 'Đã gửi yêu cầu · chờ trưởng nhóm', tone: StatusTone.warning, icon: Icons.schedule)),
        const SizedBox(height: AppSpacing.s3),
        OutlinedButton(
          onPressed: pending == null || _cancelling ? null : () => _cancel(pending.id),
          style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(48)),
          child: Text(_cancelling ? 'Đang hủy…' : 'Hủy yêu cầu'),
        ),
      ],
    );
  }
}

/// S09 – Gửi yêu cầu tham gia (bản 4): lời nhắn, IdeaListField (1–3 ý tưởng hoặc "Chưa có ý tưởng" – quy tắc 17), nhắc hạn 72 giờ.
/// Lỗi gửi không đóng sheet và giữ toàn bộ nội dung.
class JoinRequestSheet extends ConsumerStatefulWidget {
  const JoinRequestSheet({super.key, required this.teamId});

  final String teamId;

  @override
  ConsumerState<JoinRequestSheet> createState() => _JoinRequestSheetState();
}

class _JoinRequestSheetState extends ConsumerState<JoinRequestSheet> {
  final _message = TextEditingController();
  final List<(TextEditingController, TextEditingController)> _ideas = [];

  /// Ô tiêu đề của các ý tưởng do AI điền, để hiện nhãn "Gợi ý của AI".
  final Set<TextEditingController> _aiIdeas = {};

  @override
  void dispose() {
    _message.dispose();
    for (final (title, description) in _ideas) {
      title.dispose();
      description.dispose();
    }
    super.dispose();
  }

  JoinRequestViewModel get _vm => ref.read(joinRequestViewModelProvider(widget.teamId).notifier);

  /// MB-56: mở "Gợi ý bằng AI"; dùng gợi ý thì thêm một ý tưởng sửa được.
  Future<void> _suggestWithAi() async {
    final fields = await showModalBottomSheet<(String, String)>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (_) => const IdeaSuggestSheet(),
    );
    if (fields == null || !mounted) return;
    final title = TextEditingController(text: fields.$1);
    setState(() {
      _ideas.add((title, TextEditingController(text: fields.$2)));
      _aiIdeas.add(title);
    });
  }

  Future<void> _send() async {
    final outcome = await _vm.send(message: _message.text, ideas: [for (final (t, d) in _ideas) (t.text, d.text)]);
    if (outcome is JoinRequestSent && mounted) Navigator.pop(context, true);
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final state = ref.watch(joinRequestViewModelProvider(widget.teamId));
    final noIdea = state.noIdea;
    final sending = state.sending;
    return SafeArea(
      child: Padding(
        padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(context).bottom),
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
                    Text('Xin tham gia', style: theme.textTheme.titleLarge),
                    const SizedBox(height: AppSpacing.s3),
                    if (state.error != null) ...[ErrorBanner(message: state.error!, onRetry: sending ? null : _send), const SizedBox(height: AppSpacing.s3)],
                    TextField(
                      controller: _message,
                      maxLength: 500,
                      minLines: 2,
                      maxLines: 4,
                      decoration: const InputDecoration(labelText: 'Lời nhắn cho trưởng nhóm', alignLabelWithHint: true),
                    ),
                    Text('Ý tưởng của bạn', style: theme.textTheme.titleSmall),
                    if (!noIdea)
                      for (final (i, (title, description)) in _ideas.indexed)
                        Card.outlined(
                          margin: const EdgeInsets.only(top: AppSpacing.s2),
                          child: Padding(
                            padding: const EdgeInsets.all(AppSpacing.s3),
                            child: Column(
                              children: [
                                if (_aiIdeas.contains(title))
                                  const Align(alignment: Alignment.centerLeft, child: AiSuggestionLabel()),
                                Row(
                                  children: [
                                    Expanded(
                                      child: TextField(controller: title, maxLength: 80, decoration: InputDecoration(labelText: 'Ý tưởng ${i + 1}')),
                                    ),
                                    IconButton(
                                      tooltip: 'Xóa ý tưởng ${i + 1}',
                                      icon: const Icon(Icons.delete_outline),
                                      onPressed: () => setState(() => _ideas.removeAt(i)),
                                    ),
                                  ],
                                ),
                                TextField(controller: description, maxLength: 300, maxLines: 3, decoration: const InputDecoration(labelText: 'Mô tả ngắn')),
                              ],
                            ),
                          ),
                        ),
                    if (!noIdea && _ideas.length < 3)
                      Wrap(
                        spacing: AppSpacing.s2,
                        children: [
                          TextButton.icon(
                            onPressed: () => setState(() => _ideas.add((TextEditingController(), TextEditingController()))),
                            icon: const Icon(Icons.add),
                            label: const Text('Thêm ý tưởng'),
                          ),
                          if (ref.watch(remoteConfigProvider).aiIdeaEnabled)
                            TextButton.icon(
                              onPressed: _suggestWithAi,
                              icon: const Icon(Icons.auto_awesome),
                              label: const Text('Gợi ý bằng AI'),
                            ),
                        ],
                      ),
                    CheckboxListTile(
                      contentPadding: EdgeInsets.zero,
                      value: noIdea,
                      title: const Text('Chưa có ý tưởng'),
                      onChanged: (v) => _vm.setNoIdea(v ?? false),
                    ),
                    if (state.ideasMissing)
                      Text('Thêm ít nhất 1 ý tưởng hoặc chọn "Chưa có ý tưởng".', style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.error)),
                    const SizedBox(height: AppSpacing.s2),
                    Text('Yêu cầu hết hạn sau 72 giờ nếu trưởng nhóm chưa trả lời.', style: theme.textTheme.bodyMedium),
                  ],
                ),
              ),
              Padding(
                padding: const EdgeInsets.all(AppSpacing.s4),
                child: Row(
                  children: [
                    TextButton(onPressed: sending ? null : () => Navigator.pop(context), child: const Text('Hủy')),
                    const Spacer(),
                    LoadingButton(label: 'Xin tham gia', expand: false, loading: sending, onPressed: sending ? null : _send),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
