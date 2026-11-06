import 'dart:async';

import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/core/view_state.dart';
import 'package:exe_team_matching/features/discover/discover_api.dart';
import 'package:exe_team_matching/features/discover/teams_view_model.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

TeamCard _team(String id) => TeamCard(id: id, name: 'Nhóm $id', status: 'ACTIVE', memberCount: 3);

/// Service giả: trả trang theo [pages] (trang 1, 2, …), ghi lại mọi lời gọi; [gate] giữ một lời gọi chưa trả.
class _FakeApi extends DiscoverApi {
  _FakeApi({this.pages = const [], this.error}) : super(Dio());

  List<List<TeamCard>> pages;
  Object? error;
  Object? errorOnPage2;
  final calls = <(String, TeamFilter, int)>[];
  Completer<void>? gate;

  @override
  Future<TeamPage> search(String search, TeamFilter filter, int page) async {
    calls.add((search, filter, page));
    if (gate != null) await gate!.future;
    if (error != null) throw error!;
    if (page == 2 && errorOnPage2 != null) throw errorOnPage2!;
    final items = page <= pages.length ? pages[page - 1] : const <TeamCard>[];
    return TeamPage(items: items, page: page, totalPages: pages.length);
  }
}

/// MB-48 – ViewModel của S05 test riêng, không dựng widget.
void main() {
  late _FakeApi api;
  late ProviderContainer container;

  ProviderContainer create() {
    final c = ProviderContainer(overrides: [discoverApiProvider.overrideWithValue(api)]);
    addTearDown(c.dispose);
    c.listen(teamsViewModelProvider, (_, _) {}); // giữ provider autoDispose sống trong test
    return c;
  }

  Future<void> settle() => Future<void>.delayed(Duration.zero);

  TeamsState stateOf() => container.read(teamsViewModelProvider);
  TeamsViewModel vm() => container.read(teamsViewModelProvider.notifier);

  test('mở màn: ViewLoading rồi ViewData trang 1', () async {
    api = _FakeApi(pages: [
      [_team('a'), _team('b')],
      [_team('c')],
    ]);
    container = create();
    expect(stateOf().results, isA<ViewLoading<TeamResults>>());

    await settle();
    final results = stateOf().results as ViewData<TeamResults>;
    expect(results.value.items.map((t) => t.id), ['a', 'b']);
    expect(results.value.hasMore, isTrue);
  });

  test('không có nhóm nào: ViewEmpty', () async {
    api = _FakeApi(pages: [[]]);
    container = create();
    await settle();
    expect(stateOf().results, isA<ViewEmpty<TeamResults>>());
  });

  test('API lỗi: ViewError giữ nguyên lỗi (View tự nhận round-closed)', () async {
    const closed = ApiException(status: 409, code: 'round-closed', title: 'Đợt đã khóa');
    api = _FakeApi(error: closed);
    container = create();
    await settle();
    expect((stateOf().results as ViewError<TeamResults>).error, closed);
  });

  test('Xem thêm: nối trang 2, hết trang thì hasMore = false', () async {
    api = _FakeApi(pages: [
      [_team('a')],
      [_team('b')],
    ]);
    container = create();
    await settle();

    await vm().loadMore();
    final results = (stateOf().results as ViewData<TeamResults>).value;
    expect(results.items.map((t) => t.id), ['a', 'b']);
    expect(results.hasMore, isFalse);
    expect(api.calls.map((c) => c.$3), [1, 2]);
  });

  test('trang 2 lỗi: giữ danh sách cũ, có loadMoreError để hiện banner', () async {
    api = _FakeApi(pages: [
      [_team('a')],
      [_team('b')],
    ])..errorOnPage2 = Exception('mạng');
    container = create();
    await settle();

    await vm().loadMore();
    final results = (stateOf().results as ViewData<TeamResults>).value;
    expect(results.items.map((t) => t.id), ['a']);
    expect(results.loadMoreError, isNotNull);
    expect(results.loadingMore, isFalse);
  });

  test('tìm kiếm và bộ lọc gửi đúng query, luôn tải lại từ trang 1', () async {
    api = _FakeApi(pages: [
      [_team('a')],
    ]);
    container = create();
    await settle();

    await vm().search('  edu ');
    await vm().applyFilter(const TeamFilter(minOpenSlots: 2));
    final (query, filter, page) = api.calls.last;
    expect((query, filter.minOpenSlots, page), ('edu', 2, 1));
    expect(stateOf().canClear, isTrue);

    await vm().clearFilters(includeQuery: true);
    expect(stateOf().query, '');
    expect(stateOf().filter.count, 0);
    expect(stateOf().canClear, isFalse);
  });

  test('kết quả của lượt tìm cũ về muộn bị bỏ qua', () async {
    api = _FakeApi(pages: [
      [_team('a')],
    ]);
    container = create();
    await settle();

    api.gate = Completer<void>();
    final slow = vm().search('cũ');
    api.pages = [
      [_team('moi')],
    ];
    final fast = vm().search('mới');
    api.gate!.complete();
    await Future.wait([slow, fast]);

    final results = (stateOf().results as ViewData<TeamResults>).value;
    expect(stateOf().query, 'mới');
    expect(results.items.map((t) => t.id), ['moi']);
  });
}
