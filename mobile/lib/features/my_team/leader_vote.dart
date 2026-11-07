import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_exception.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import 'my_team_api.dart';

/// Đề nghị đổi nhóm trưởng (MB-63, BE-88). [myBallot] null = chưa bỏ phiếu.
class LeaderVote {
  const LeaderVote({
    required this.id,
    required this.status,
    required this.nomineeName,
    required this.proposerName,
    required this.expiresAt,
    required this.approveCount,
    required this.requiredApprovals,
    required this.canWithdraw,
    this.reason,
    this.myBallot,
  });

  final String id;
  final String status;
  final String nomineeName;
  final String proposerName;
  final String? reason;
  final DateTime expiresAt;
  final int approveCount;
  final int requiredApprovals;
  final bool? myBallot;
  final bool canWithdraw;

  bool get isOpen => status == 'OPEN';

  factory LeaderVote.fromJson(Map<String, dynamic> j) {
    String name(String key) => (j[key] as Map?)?['fullName'] as String? ?? 'Thành viên';
    return LeaderVote(
      id: j['id'] as String,
      status: j['status'] as String? ?? 'OPEN',
      nomineeName: name('nominee'),
      proposerName: name('proposedBy'),
      reason: j['reason'] as String?,
      expiresAt: DateTime.parse(j['expiresAt'] as String),
      approveCount: j['approveCount'] as int? ?? 0,
      requiredApprovals: j['requiredApprovals'] as int? ?? 1,
      myBallot: j['myBallot'] as bool?,
      canWithdraw: j['canWithdraw'] as bool? ?? false,
    );
  }
}

/// Thời gian còn lại của đề nghị, dạng "còn 5 giờ".
String leaderVoteTimeLeft(DateTime expiresAt, DateTime now) {
  final left = expiresAt.difference(now);
  if (left.inMinutes < 1) return 'sắp hết hạn';
  if (left.inHours < 1) return 'còn ${left.inMinutes} phút';
  return 'còn ${left.inHours} giờ';
}

/// Số phiếu cần: quá nửa số thành viên (nhóm 4 cần 3, nhóm 5 cần 3, nhóm 6 cần 4).
int requiredApprovals(int memberCount) => memberCount ~/ 2 + 1;

/// Câu kết quả sau khi bỏ phiếu; null khi đề nghị còn mở.
String? leaderVoteResult(LeaderVote vote) => switch (vote.status) {
  'PASSED' => '${vote.nomineeName} đã trở thành nhóm trưởng',
  'REJECTED' => 'Đề nghị không được thông qua',
  _ => null,
};

/// Đề nghị đang mở của nhóm; không có hoặc tải lỗi thì thẻ ở S15 không hiện.
final leaderVoteProvider = FutureProvider.autoDispose.family<LeaderVote?, String>(
  (ref, teamId) => ref.watch(myTeamApiProvider).currentLeaderVote(teamId),
);

/// Trạng thái thao tác trên đề nghị: đang gửi và câu lỗi gần nhất.
class LeaderVoteActionState {
  const LeaderVoteActionState({this.busy = false, this.error});

  final bool busy;
  final String? error;
}

/// ViewModel của thẻ đề nghị (S15) và bottom sheet đề nghị (S16). Trả câu cho SnackBar, null khi lỗi (lỗi ở [LeaderVoteActionState.error]).
class LeaderVoteViewModel extends Notifier<LeaderVoteActionState> {
  LeaderVoteViewModel(this.teamId);

  final String teamId;

  @override
  LeaderVoteActionState build() => const LeaderVoteActionState();

  MyTeamApi get _api => ref.read(myTeamApiProvider);

  Future<String?> open(String nomineeId, String reason) =>
      _run(() async {
        await _api.openLeaderVote(teamId, nomineeId, reason.trim());
        return 'Đã gửi đề nghị';
      });

  Future<String?> cast(String voteId, bool approve) =>
      _run(() async => leaderVoteResult(await _api.castLeaderBallot(teamId, voteId, approve)) ?? 'Đã ghi nhận phiếu của bạn');

  Future<String?> withdraw(String voteId) =>
      _run(() async {
        await _api.withdrawLeaderVote(teamId, voteId);
        return 'Đã rút đề nghị';
      });

  Future<String?> _run(Future<String> Function() call) async {
    state = const LeaderVoteActionState(busy: true);
    try {
      final message = await call();
      if (!ref.mounted) return message;
      state = const LeaderVoteActionState();
      ref.invalidate(leaderVoteProvider(teamId));
      refreshMyTeamFrom(ref);
      return message;
    } on ApiException catch (e) {
      if (ref.mounted) {
        state = LeaderVoteActionState(error: e.detail ?? e.title);
        ref.invalidate(leaderVoteProvider(teamId));
      }
      return null;
    }
  }
}

final leaderVoteViewModelProvider = NotifierProvider.autoDispose.family<LeaderVoteViewModel, LeaderVoteActionState, String>(
  LeaderVoteViewModel.new,
);

/// S15 – thẻ đề nghị đang mở: tiến độ phiếu, hạn còn lại, Đồng ý / Không đồng ý, đổi phiếu, rút đề nghị.
class LeaderVoteCard extends ConsumerWidget {
  const LeaderVoteCard({super.key, required this.teamId});

  final String teamId;

  Future<void> _run(BuildContext context, Future<String?> Function() call) async {
    final message = await call();
    if (message == null || !context.mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final vote = ref.watch(leaderVoteProvider(teamId)).value;
    if (vote == null) return const SizedBox.shrink();
    final theme = Theme.of(context);
    final action = ref.watch(leaderVoteViewModelProvider(teamId));
    final vm = ref.read(leaderVoteViewModelProvider(teamId).notifier);
    final progress = vote.requiredApprovals == 0 ? 0.0 : (vote.approveCount / vote.requiredApprovals).clamp(0.0, 1.0);
    final voted = vote.myBallot;
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.s4),
      child: Card.outlined(
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.s4),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('${vote.proposerName} đề nghị ${vote.nomineeName} làm nhóm trưởng', style: theme.textTheme.titleMedium),
              if (vote.reason case final reason?) ...[
                const SizedBox(height: AppSpacing.s1),
                Text(reason, style: theme.textTheme.bodyMedium),
              ],
              const SizedBox(height: AppSpacing.s3),
              Semantics(
                label: '${vote.approveCount} trên ${vote.requiredApprovals} phiếu đồng ý cần thiết',
                child: LinearProgressIndicator(value: progress),
              ),
              const SizedBox(height: AppSpacing.s2),
              Text(
                '${vote.approveCount}/${vote.requiredApprovals} phiếu đồng ý · ${leaderVoteTimeLeft(vote.expiresAt, DateTime.now())}',
                style: theme.textTheme.bodySmall,
              ),
              if (action.error case final error?) ...[
                const SizedBox(height: AppSpacing.s3),
                ErrorBanner(message: error),
              ],
              if (voted != null) ...[
                const SizedBox(height: AppSpacing.s2),
                Text('Bạn đã chọn: ${voted ? 'Đồng ý' : 'Không đồng ý'}', style: theme.textTheme.bodyMedium),
              ],
              const SizedBox(height: AppSpacing.s3),
              Wrap(
                spacing: AppSpacing.s2,
                runSpacing: AppSpacing.s2,
                children: [
                  if (voted != true)
                    FilledButton(
                      onPressed: action.busy ? null : () => _run(context, () => vm.cast(vote.id, true)),
                      child: Text(voted == null ? 'Đồng ý' : 'Đổi sang Đồng ý'),
                    ),
                  if (voted != false)
                    OutlinedButton(
                      onPressed: action.busy ? null : () => _run(context, () => vm.cast(vote.id, false)),
                      child: Text(voted == null ? 'Không đồng ý' : 'Đổi sang Không đồng ý'),
                    ),
                  if (vote.canWithdraw)
                    TextButton(
                      onPressed: action.busy ? null : () => _run(context, () => vm.withdraw(vote.id)),
                      child: const Text('Rút đề nghị'),
                    ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// S16 – bottom sheet đề nghị làm nhóm trưởng. Trả câu cho SnackBar khi gửi được.
Future<String?> showProposeLeaderSheet(BuildContext context, {required MyTeam team, required Member nominee}) =>
    showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      builder: (_) => _ProposeLeaderSheet(team: team, nominee: nominee),
    );

class _ProposeLeaderSheet extends ConsumerStatefulWidget {
  const _ProposeLeaderSheet({required this.team, required this.nominee});

  final MyTeam team;
  final Member nominee;

  @override
  ConsumerState<_ProposeLeaderSheet> createState() => _ProposeLeaderSheetState();
}

class _ProposeLeaderSheetState extends ConsumerState<_ProposeLeaderSheet> {
  final _reason = TextEditingController();

  @override
  void dispose() {
    _reason.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final message = await ref.read(leaderVoteViewModelProvider(widget.team.id).notifier).open(widget.nominee.id, _reason.text);
    if (message != null && mounted) Navigator.pop(context, message);
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final state = ref.watch(leaderVoteViewModelProvider(widget.team.id));
    final required = requiredApprovals(widget.team.memberCount);
    return Padding(
      padding: EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s4, AppSpacing.s4, AppSpacing.s4 + MediaQuery.viewInsetsOf(context).bottom),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text('Đề nghị làm nhóm trưởng', style: theme.textTheme.titleLarge),
          const SizedBox(height: AppSpacing.s2),
          Text('Người được đề cử: ${widget.nominee.fullName}', style: theme.textTheme.bodyMedium),
          const SizedBox(height: AppSpacing.s3),
          TextField(
            controller: _reason,
            maxLength: 300,
            minLines: 2,
            maxLines: 4,
            decoration: const InputDecoration(labelText: 'Lý do (không bắt buộc)', border: OutlineInputBorder()),
          ),
          Text('Cần $required/${widget.team.memberCount} thành viên đồng ý trong 48 giờ.', style: theme.textTheme.bodySmall),
          if (state.error case final error?) ...[
            const SizedBox(height: AppSpacing.s3),
            ErrorBanner(message: error),
          ],
          const SizedBox(height: AppSpacing.s3),
          FilledButton(onPressed: state.busy ? null : _send, child: const Text('Gửi đề nghị')),
        ],
      ),
    );
  }
}
