import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/features/invitations/invitations_api.dart';
import 'package:exe_team_matching/features/invitations/invitations_view_model.dart';
import 'package:exe_team_matching/features/my_team/my_team_api.dart' show MemberChange;
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

const _error = ApiException(status: 409, code: 'x', title: 'Lỗi', detail: 'Lời mời đã hết hạn');

final _invitation = MyRequest(id: 'i1', team: null, status: 'PENDING', expiresAt: DateTime.utc(2026, 12));

class _FakeApi extends InvitationsApi {
  _FakeApi() : super(Dio());

  Object? error;
  bool applied = true;
  final calls = <String>[];

  Future<T> _do<T>(String call, T value) async {
    calls.add(call);
    if (error != null) throw error!;
    return value;
  }

  @override
  Future<List<MyRequest>> invitations() => _do('list', [_invitation]);

  @override
  Future<List<MyRequest>> joinRequests() => _do('requests', [_invitation]);

  @override
  Future<MemberChange> accept(String invitationId) => _do('accept $invitationId', MemberChange(applied: applied));

  @override
  Future<void> decline(String invitationId) => _do('decline $invitationId', null);

  @override
  Future<void> cancelJoinRequest(String joinRequestId) => _do('cancel $joinRequestId', null);
}

/// MB-49 – ViewModel của S12 test riêng, không dựng widget.
void main() {
  late _FakeApi api;
  late ProviderContainer c;

  setUp(() {
    api = _FakeApi();
    c = ProviderContainer(overrides: [invitationsApiProvider.overrideWithValue(api)]);
    addTearDown(c.dispose);
  });

  group('ReceivedInvitationsViewModel', () {
    setUp(() => c.listen(receivedInvitationsViewModelProvider, (_, _) {}));
    ReceivedInvitationsViewModel vm() => c.read(receivedInvitationsViewModelProvider.notifier);

    test('chấp nhận: vào nhóm ngay hoặc chờ Bộ môn duyệt', () async {
      expect((await vm().accept(_invitation) as AcceptJoined).message, 'Đã vào nhóm');
      api.applied = false;
      expect((await vm().accept(_invitation) as AcceptJoined).message, 'Đã ghi nhận, chờ Bộ môn duyệt');
      expect(c.read(receivedInvitationsViewModelProvider).busyId, isNull);
    });

    test('từ chối gọi API, đang xử lý thì busyId là id lời mời', () async {
      final pending = vm().decline(_invitation);
      expect(c.read(receivedInvitationsViewModelProvider).busyId, 'i1');
      await pending;
      expect(api.calls, ['decline i1']);
      expect(c.read(receivedInvitationsViewModelProvider).busyId, isNull);
    });

    test('lỗi: AcceptFailed, state có câu lỗi', () async {
      api.error = _error;
      expect(await vm().accept(_invitation), isA<AcceptFailed>());
      expect(c.read(receivedInvitationsViewModelProvider).error, 'Lời mời đã hết hạn');
    });
  });

  group('SentJoinRequestsViewModel', () {
    test('hủy yêu cầu; lỗi thì state là câu lỗi', () async {
      c.listen(sentJoinRequestsViewModelProvider, (_, _) {});
      final vm = c.read(sentJoinRequestsViewModelProvider.notifier);
      await vm.cancel(_invitation);
      expect(c.read(sentJoinRequestsViewModelProvider), isNull);
      api.error = _error;
      await vm.cancel(_invitation);
      expect(c.read(sentJoinRequestsViewModelProvider), 'Lời mời đã hết hạn');
      expect(api.calls, ['cancel i1', 'cancel i1']);
    });
  });
}
