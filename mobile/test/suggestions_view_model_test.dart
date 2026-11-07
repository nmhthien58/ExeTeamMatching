import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_view_model.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/suggestions_fakes.dart';

class _Api extends FakeSuggestionsApi {
  Object? error;
  final sent = <String>[];

  @override
  Future<void> invite(String teamId, String studentId, {String? message}) async {
    sent.add('$teamId $studentId $message');
    if (error != null) throw error!;
  }
}

/// MB-49 – ViewModel của S18, S19 và sheet gửi lời mời, không dựng widget.
void main() {
  late _Api api;
  late ProviderContainer container;

  setUp(() {
    api = _Api();
    container = ProviderContainer(overrides: [suggestionsApiProvider.overrideWithValue(api)]);
    addTearDown(container.dispose);
  });

  group('gửi lời mời', () {
    const key = (teamId: 't1', studentId: 's9');
    InviteViewModel vm() => container.read(inviteViewModelProvider(key).notifier);
    InviteState state() => container.read(inviteViewModelProvider(key));

    setUp(() => container.listen(inviteViewModelProvider(key), (_, _) {}));

    test('gửi được: cắt khoảng trắng lời nhắn', () async {
      final future = vm().send('  Vào nhóm mình nhé  ');
      expect(state().sending, isTrue);
      expect(await future, isTrue);
      expect(api.sent, ['t1 s9 Vào nhóm mình nhé']);
      expect(state().sending, isFalse);
    });

    test('lỗi nghiệp vụ → câu riêng; mã lạ → detail', () async {
      api.error = const ApiException(status: 409, code: 'team-full', title: 'Conflict');
      expect(await vm().send(''), isFalse);
      expect(state().error, 'Nhóm đã đủ người.');
      api.error = const ApiException(status: 409, code: 'other', title: 'Conflict', detail: 'Chi tiết');
      await vm().send('');
      expect((state().error, state().sending), ('Chi tiết', false));
    });
  });

  test('S18: ghi nhận đã mời trong phiên', () {
    container.listen(suggestedMembersViewModelProvider, (_, _) {});
    container.read(suggestedMembersViewModelProvider.notifier)
      ..markInvited('a')
      ..markInvited('b');
    expect(container.read(suggestedMembersViewModelProvider), {'a', 'b'});
  });

  test('S19: từ khóa, lọc chuyên ngành bật / tắt, đã mời', () {
    container.listen(inviteStudentsViewModelProvider, (_, _) {});
    final vm = container.read(inviteStudentsViewModelProvider.notifier);
    vm
      ..search('  Vy ')
      ..toggleSpecialization(3, true)
      ..markInvited('s1');
    var state = container.read(inviteStudentsViewModelProvider);
    expect(state.searchQuery, (search: 'Vy', specializationId: 3));
    expect(state.invited, {'s1'});
    vm.toggleSpecialization(3, false);
    state = container.read(inviteStudentsViewModelProvider);
    expect((state.specializationId, state.query), (null, 'Vy'));
    expect(state.invited, {'s1'});
  });
}
