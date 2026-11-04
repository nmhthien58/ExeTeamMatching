import 'dart:async';

import 'package:dio/dio.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_api.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_models.dart';
import 'package:exe_team_matching/features/lecturer/topic_interests_view_model.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

TopicRecommendationItem _rec(String teamId, {bool interested = false, String? interestId, String course = 'EXE101'}) =>
    TopicRecommendationItem(
      teamId: teamId,
      teamName: 'Nhóm $teamId',
      courseCode: course,
      memberCount: 4,
      isInterested: interested,
      myInterestId: interestId,
    );

class _FakeApi extends LecturerHomeApi {
  _FakeApi() : super(Dio());

  final calls = <String>[];
  Object? error;
  Completer<void>? gate;
  List<TopicInterestItem> interests = const [];

  @override
  Future<void> openInterest(String teamId, {String advisorKind = 'LECTURER'}) async {
    calls.add('open:$teamId:$advisorKind');
    if (gate != null) await gate!.future;
    if (error != null) throw error!;
  }

  @override
  Future<void> withdrawInterest(String interestId) async {
    calls.add('withdraw:$interestId');
    if (error != null) throw error!;
  }

  @override
  Future<List<TopicInterestItem>> getMyTopicInterests({String advisorKind = 'LECTURER'}) async {
    calls.add('mine:$advisorKind');
    return interests;
  }

  @override
  Future<TopicRecommendationsResponse> getRecommendations({String advisorKind = 'LECTURER', String? courseCode, int page = 1, int pageSize = 20}) async =>
      const TopicRecommendationsResponse();
}

/// MB-49 – ViewModel của L07 và chi tiết đề tài, không dựng widget.
void main() {
  late _FakeApi api;
  late ProviderContainer container;

  setUp(() {
    api = _FakeApi();
    container = ProviderContainer(overrides: [lecturerHomeApiProvider.overrideWithValue(api)]);
    addTearDown(container.dispose);
    container.listen(topicInterestsViewModelProvider('MENTOR'), (_, _) {});
  });

  TopicInterestsViewModel vm() => container.read(topicInterestsViewModelProvider('MENTOR').notifier);

  test('lọc theo môn; null = tất cả', () {
    final items = [_rec('a'), _rec('b', course: 'EXE201')];
    expect(vm().byCourse(items, (i) => i.courseCode).length, 2);
    vm().selectCourse('EXE201');
    expect(vm().byCourse(items, (i) => i.courseCode).single.teamId, 'b');
    vm().selectCourse(null);
    expect(container.read(topicInterestsViewModelProvider('MENTOR')).course, isNull);
  });

  test('quan tâm: mở, đánh dấu đang gửi, bấm lần hai bị bỏ qua', () async {
    api.gate = Completer<void>();
    final first = vm().toggle(_rec('a'));
    expect(container.read(topicInterestsViewModelProvider('MENTOR')).pending, {'a'});
    expect(await vm().toggle(_rec('a')), isA<TopicToggleIgnored>());
    api.gate!.complete();
    expect(await first, isA<TopicToggled>().having((o) => o.wasInterested, 'wasInterested', false));
    expect(container.read(topicInterestsViewModelProvider('MENTOR')).pending, isEmpty);
    expect(api.calls.first, 'open:a:MENTOR');
  });

  test('rút quan tâm không có id: tìm quan tâm OPEN rồi rút', () async {
    api.interests = const [
      TopicInterestItem(id: 'i1', teamId: 'a', advisorId: 'm', advisorName: 'M', status: 'OPEN'),
    ];
    final outcome = await vm().toggle(_rec('a', interested: true));
    expect((outcome as TopicToggled).wasInterested, isTrue);
    expect(api.calls.take(2), ['mine:MENTOR', 'withdraw:i1']);
  });

  test('lỗi → TopicToggleFailed; hoàn tác lỗi → false', () async {
    api.error = Exception('x');
    expect(await vm().toggle(_rec('a', interested: true, interestId: 'i9')), isA<TopicToggleFailed>());
    expect(await vm().undo('a', wasInterested: true), isFalse);
  });

  test('hoàn tác: vừa rút thì mở lại, vừa mở thì rút', () async {
    expect(await vm().undo('a', wasInterested: true), isTrue);
    expect(api.calls.first, 'open:a:MENTOR');
  });

  test('chi tiết đề tài: toggle, đang gửi, canToggle', () async {
    const key = (teamId: 'a', advisorKind: 'LECTURER');
    container.listen(topicDetailViewModelProvider(key), (_, _) {});
    final detail = container.read(topicDetailViewModelProvider(key).notifier);
    final pending = detail.toggle(null);
    expect(container.read(topicDetailViewModelProvider(key)), isTrue);
    expect(await pending, isA<TopicToggled>());
    expect(container.read(topicDetailViewModelProvider(key)), isFalse);

    expect(TopicDetailViewModel.isInterested(null, _rec('a', interested: true)), isTrue);
    const full = TopicRecommendationsResponse(openInterestCount: 10, interestLimit: 10);
    expect(TopicDetailViewModel.canToggle(response: full, hasAdvisor: false, isInterested: false), isFalse);
    expect(TopicDetailViewModel.canToggle(response: full, hasAdvisor: false, isInterested: true), isTrue);
    expect(TopicDetailViewModel.canToggle(response: full, hasAdvisor: true, isInterested: true), isFalse);
  });
}
