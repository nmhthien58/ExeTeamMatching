import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/firebase/analytics_service.dart';
import 'package:exe_team_matching/core/firebase/remote_config_service.dart';
import 'package:exe_team_matching/features/discover/discover_api.dart';
import 'package:exe_team_matching/features/discover/teams_view_model.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_view_model.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/firebase_fakes.dart';

TeamCard _team(String id) => TeamCard(id: id, name: 'Nhóm $id', status: 'ACTIVE', memberCount: 3);

class _OnePageApi extends DiscoverApi {
  _OnePageApi() : super(Dio());

  @override
  Future<TeamPage> search(String search, TeamFilter filter, int page) async =>
      TeamPage(items: [_team('a'), _team('b')], page: 1, totalPages: 1);
}

/// MB-50/51 – chỗ gắn sự kiện Analytics và Remote Config trong ViewModel (analytics-plan.md).
void main() {
  test('S05: mở màn không ghi search; tìm kiếm ghi search với từ khóa và số kết quả', () async {
    final analytics = FakeAnalyticsService();
    final c = ProviderContainer(overrides: [
      discoverApiProvider.overrideWithValue(_OnePageApi()),
      analyticsServiceProvider.overrideWithValue(analytics),
    ]);
    addTearDown(c.dispose);
    c.listen(teamsViewModelProvider, (_, _) {});
    await Future<void>.delayed(Duration.zero);
    expect(analytics.names, isEmpty);

    await c.read(teamsViewModelProvider.notifier).search('edu');
    await Future<void>.delayed(Duration.zero);
    expect(analytics.names, ['search']);
    expect(analytics.events.single.parameters['search_term'], 'edu');
  });

  test('S07: cắt theo suggestions_max_items, đổi Remote Config thì danh sách đổi theo', () async {
    final analytics = FakeAnalyticsService();
    final config = FakeRemoteConfigService(const AppRemoteConfig(suggestionsMaxItems: 3));
    final all = [for (var i = 0; i < 7; i++) TeamSuggestion(team: _team('$i'), score: 0.9, reasons: const [])];
    final c = ProviderContainer(overrides: [
      teamsForMeProvider.overrideWith((ref) async => all),
      remoteConfigServiceProvider.overrideWithValue(config),
      analyticsServiceProvider.overrideWithValue(analytics),
    ]);
    addTearDown(c.dispose);
    c.listen(suggestedTeamsProvider, (_, _) {});

    expect(await c.read(suggestedTeamsProvider.future), hasLength(3));
    expect(analytics.names, ['view_suggestions']);

    config.push(const AppRemoteConfig(suggestionsMaxItems: 5));
    await Future<void>.delayed(Duration.zero);
    expect(await c.read(suggestedTeamsProvider.future), hasLength(5));
  });
}
