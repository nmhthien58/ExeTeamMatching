import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../workspace/workspace_widgets.dart';
import 'suggestion_widgets.dart';
import 'suggestions_api.dart';
import 'suggestions_view_model.dart';

/// S19 – Tìm và mời sinh viên (MB-23): tìm theo tên / MSSV, lọc chuyên ngành, chỉ sinh viên chưa có nhóm, lời nhắn.
/// Trạng thái: kết quả · không tìm thấy · nhóm đã đủ 6 người · đang gửi.
class InviteStudentsScreen extends ConsumerStatefulWidget {
  const InviteStudentsScreen({super.key});

  @override
  ConsumerState<InviteStudentsScreen> createState() => _InviteStudentsScreenState();
}

class _InviteStudentsScreenState extends ConsumerState<InviteStudentsScreen> {
  final _search = TextEditingController();
  Timer? _debounce;

  InviteStudentsViewModel get _vm => ref.read(inviteStudentsViewModelProvider.notifier);

  @override
  void dispose() {
    _debounce?.cancel();
    _search.dispose();
    super.dispose();
  }

  void _onSearch(String text) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 300), () => _vm.search(text));
  }

  Future<void> _invite(TeamDetail team, StudentRef student) async {
    if (!await showInviteSheet(context, teamId: team.card.id, student: student) || !mounted) return;
    _vm.markInvited(student.id);
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(const SnackBar(content: Text('Đã gửi lời mời')));
  }

  @override
  Widget build(BuildContext context) {
    final team = ref.watch(myTeamProvider).value;
    final specializations = ref.watch(specializationsProvider).value ?? const [];
    final state = ref.watch(inviteStudentsViewModelProvider);
    final results = ref.watch(studentSearchProvider(state.searchQuery));
    final isLeader = team?.isLeader ?? false;
    final isFull = team?.isFull ?? false;

    return Scaffold(
      appBar: AppBar(title: const Text('Mời sinh viên')),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s3, AppSpacing.s4, 0),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (isFull) const Padding(padding: EdgeInsets.only(bottom: AppSpacing.s3), child: StatusChip(label: 'Nhóm đã đủ 6 người', tone: StatusTone.success)),
                if (team != null && !isLeader)
                  Padding(padding: const EdgeInsets.only(bottom: AppSpacing.s3), child: Text('Chỉ trưởng nhóm gửi được lời mời.', style: Theme.of(context).textTheme.bodyMedium)),
                TextField(
                  controller: _search,
                  onChanged: _onSearch,
                  textInputAction: TextInputAction.search,
                  decoration: const InputDecoration(prefixIcon: Icon(Icons.search), hintText: 'Tên hoặc MSSV'),
                ),
                const SizedBox(height: AppSpacing.s2),
                SingleChildScrollView(
                  scrollDirection: Axis.horizontal,
                  child: Row(
                    children: [
                      for (final s in specializations)
                        Padding(
                          padding: const EdgeInsets.only(right: AppSpacing.s2),
                          child: FilterChip(
                            label: Text(s.name),
                            selected: state.specializationId == s.id,
                            onSelected: (v) => _vm.toggleSpecialization(s.id, v),
                          ),
                        ),
                    ],
                  ),
                ),
              ],
            ),
          ),
          Expanded(
            child: results.when(
              skipLoadingOnReload: true,
              loading: () => const SkeletonList(count: 3),
              error: (e, _) => ErrorState(error: e, onRetry: _vm.retry),
              data: (students) => students.isEmpty
                  ? const EmptyState(
                      icon: Icons.search_off,
                      title: 'Không tìm thấy sinh viên chưa có nhóm',
                      description: 'Thử tên khác, MSSV đầy đủ hoặc bỏ lọc chuyên ngành.',
                    )
                  : ListView.separated(
                      padding: const EdgeInsets.all(AppSpacing.s4),
                      itemCount: students.length,
                      separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.s3),
                      itemBuilder: (_, i) {
                        final student = students[i];
                        return StudentCard(
                          student: student,
                          action: !isLeader
                              ? null
                              : state.invited.contains(student.id)
                                  ? const StatusChip(label: 'Đã mời', tone: StatusTone.warning)
                                  : OutlinedButton.icon(
                                      onPressed: isFull ? null : () => _invite(team!, student),
                                      icon: const Icon(Icons.person_add_alt, size: 20),
                                      label: const Text('Gửi lời mời'),
                                    ),
                        );
                      },
                    ),
            ),
          ),
        ],
      ),
    );
  }
}
