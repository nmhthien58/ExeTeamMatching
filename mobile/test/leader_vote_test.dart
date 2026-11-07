import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/features/my_team/leader_vote.dart';
import 'package:exe_team_matching/features/my_team/my_team_api.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

LeaderVote _vote({bool? myBallot, bool canWithdraw = false, String status = 'OPEN'}) => LeaderVote(
  id: 'v1',
  status: status,
  nomineeName: 'Bình',
  proposerName: 'An',
  reason: 'Bạn ấy sát sao hơn',
  expiresAt: DateTime.now().add(const Duration(hours: 5, minutes: 30)),
  approveCount: 2,
  requiredApprovals: 3,
  myBallot: myBallot,
  canWithdraw: canWithdraw,
);

class _FakeApi extends MyTeamApi {
  _FakeApi(this.vote) : super(Dio());

  LeaderVote? vote;
  final ballots = <bool>[];
  ApiException? failure;
  bool withdrawn = false;

  @override
  Future<LeaderVote?> currentLeaderVote(String teamId) async => vote;

  @override
  Future<LeaderVote> castLeaderBallot(String teamId, String voteId, bool approve) async {
    if (failure case final f?) throw f;
    ballots.add(approve);
    return _vote(status: 'PASSED');
  }

  @override
  Future<void> withdrawLeaderVote(String teamId, String voteId) async {
    withdrawn = true;
    vote = null;
  }
}

Future<void> _pump(WidgetTester tester, _FakeApi api) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: [myTeamApiProvider.overrideWithValue(api)],
      child: const MaterialApp(home: Scaffold(body: SingleChildScrollView(child: LeaderVoteCard(teamId: 't1')))),
    ),
  );
  await tester.pumpAndSettle();
}

/// MB-63 – thẻ đề nghị đổi nhóm trưởng ở S15.
void main() {
  test('số phiếu cần là quá nửa số thành viên', () {
    expect([for (final n in [2, 4, 5, 6]) requiredApprovals(n)], [2, 3, 3, 4]);
  });

  test('đọc đề nghị từ API', () {
    final vote = LeaderVote.fromJson({
      'id': 'v1', 'status': 'OPEN', 'nominee': {'studentId': 's2', 'fullName': 'Bình'}, 'proposedBy': {'studentId': 's1', 'fullName': 'An'},
      'expiresAt': '2026-11-09T11:00:00Z', 'approveCount': 1, 'requiredApprovals': 3, 'myBallot': null, 'canWithdraw': true,
    });
    expect((vote.nomineeName, vote.proposerName, vote.myBallot, vote.canWithdraw), ('Bình', 'An', null, true));
  });

  test('thời gian còn lại', () {
    final now = DateTime(2026, 11, 7, 10);
    expect(leaderVoteTimeLeft(now.add(const Duration(hours: 5, minutes: 30)), now), 'còn 5 giờ');
    expect(leaderVoteTimeLeft(now.add(const Duration(minutes: 20)), now), 'còn 20 phút');
    expect(leaderVoteTimeLeft(now, now), 'sắp hết hạn');
  });

  testWidgets('không có đề nghị thì không hiện thẻ', (tester) async {
    await _pump(tester, _FakeApi(null));
    expect(find.text('Đồng ý'), findsNothing);
  });

  testWidgets('chưa bỏ phiếu: hiện tiến độ, bỏ phiếu rồi báo kết quả', (tester) async {
    final api = _FakeApi(_vote());
    await _pump(tester, api);

    expect(find.text('An đề nghị Bình làm nhóm trưởng'), findsOneWidget);
    expect(find.textContaining('2/3 phiếu đồng ý'), findsOneWidget);
    expect(find.text('Rút đề nghị'), findsNothing);

    await tester.tap(find.text('Đồng ý'));
    await tester.pumpAndSettle();

    expect(api.ballots, [true]);
  });

  testWidgets('đã đồng ý thì chỉ còn nút đổi phiếu; người đề nghị rút được', (tester) async {
    final api = _FakeApi(_vote(myBallot: true, canWithdraw: true));
    await _pump(tester, api);

    expect(find.text('Bạn đã chọn: Đồng ý'), findsOneWidget);
    expect(find.text('Đồng ý'), findsNothing);
    expect(find.text('Đổi sang Không đồng ý'), findsOneWidget);

    await tester.tap(find.text('Rút đề nghị'));
    await tester.pumpAndSettle();

    expect(api.withdrawn, isTrue);
    expect(find.text('Đổi sang Không đồng ý'), findsNothing);
  });

  testWidgets('bỏ phiếu lỗi thì báo lỗi, không mất thẻ', (tester) async {
    final api = _FakeApi(_vote())..failure = const ApiException(status: 409, code: 'leader-vote-closed', title: 'Đề nghị đã kết thúc');
    await _pump(tester, api);

    await tester.tap(find.text('Không đồng ý'));
    await tester.pumpAndSettle();

    expect(find.text('Đề nghị đã kết thúc'), findsOneWidget);
    expect(find.text('An đề nghị Bình làm nhóm trưởng'), findsOneWidget);
  });
}
