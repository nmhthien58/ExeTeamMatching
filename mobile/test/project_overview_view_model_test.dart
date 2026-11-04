import 'package:exe_team_matching/core/providers.dart';
import 'package:exe_team_matching/features/workspace/project_overview_view_model.dart';
import 'package:exe_team_matching/features/workspace/workspace_models.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/workspace_fakes.dart';

/// MB-49 – ViewModel của C01, không dựng widget.
void main() {
  late FakeCollabApi api;

  ProviderContainer create() {
    final c = ProviderContainer(overrides: [collabApiOverride(api), authOverride()]);
    addTearDown(c.dispose);
    return c;
  }

  Future<void> settle() async {
    for (var i = 0; i < 5; i++) {
      await Future<void>.delayed(Duration.zero);
    }
  }

  test('đang tải rồi tính số liệu theo người xem', () async {
    api = FakeCollabApi()..board = [task('a', assignees: [me]), task('b', status: TaskStatus.done, assignees: [me])];
    final c = create();
    const key = (workspaceId: 'w1', hasRecentActivity: true);
    c.listen(projectOverviewViewModelProvider(key), (_, _) {});
    expect(c.read(projectOverviewViewModelProvider(key)).loading, isTrue);
    await c.read(authControllerProvider.future);
    await settle();
    final state = c.read(projectOverviewViewModelProvider(key));
    expect((state.loading, state.stats.mine, state.isNewTeam), (false, 1, false));
  });

  test('chưa có việc và chưa có hoạt động → nhóm mới', () async {
    api = FakeCollabApi()..board = const [];
    final c = create();
    const key = (workspaceId: 'w1', hasRecentActivity: false);
    c.listen(projectOverviewViewModelProvider(key), (_, _) {});
    await settle();
    expect(c.read(projectOverviewViewModelProvider(key)).isNewTeam, isTrue);
    await c.read(projectOverviewViewModelProvider(key).notifier).refresh();
    await settle();
    expect(c.read(projectOverviewViewModelProvider(key)).isNewTeam, isTrue);
  });
}
