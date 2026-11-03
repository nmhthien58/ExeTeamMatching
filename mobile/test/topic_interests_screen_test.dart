import 'package:dio/dio.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_api.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_models.dart';
import 'package:exe_team_matching/features/lecturer/topic_interests_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

class _Api extends LecturerHomeApi {
  _Api() : super(Dio());

  String? openedKind;
  String? openedTeam;
  bool interestOpen = true;
  int interestCount = 0;

  @override
  Future<TopicRecommendationsResponse> getRecommendations({
    String advisorKind = 'LECTURER',
    String? courseCode,
    int page = 1,
    int pageSize = 20,
  }) async => TopicRecommendationsResponse(
    openInterestCount: interestCount,
    interestLimit: 2,
    interestOpen: interestOpen,
    items: const [
      TopicRecommendationItem(
        teamId: 'team-1',
        teamName: 'Nhóm Mây',
        courseCode: 'EXE101',
        projectName: 'Dự án Mây',
        memberCount: 4,
        maxMemberCount: 6,
        otherInterestedCount: 2,
      ),
    ],
  );

  @override
  Future<List<TopicInterestItem>> getMyTopicInterests({
    String advisorKind = 'LECTURER',
  }) async => [];

  @override
  Future<List<LecturerPreferenceMe>> getMyPreferences() async => [];

  @override
  Future<void> openInterest(
    String teamId, {
    String advisorKind = 'LECTURER',
  }) async {
    openedTeam = teamId;
    openedKind = advisorKind;
  }
}

void main() {
  test('L07 reads the group size from the Team API response', () {
    final response = TopicRecommendationsResponse.fromJson({
      'items': [
        {
          'team': {
            'id': 'team-1',
            'name': 'Nhóm Mây',
            'courseCode': 'EXE101',
            'memberCount': 4,
            'requirement': {'maxSize': 6},
          },
        },
      ],
    });

    expect(response.items.single.maxMemberCount, 6);
  });

  testWidgets(
    'mentor sees recommendations without lecturer preference tab and can open interest',
    (tester) async {
      tester.view.physicalSize = const Size(900, 1700);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      final api = _Api();
      await tester.pumpWidget(
        ProviderScope(
          overrides: [lecturerHomeApiProvider.overrideWithValue(api)],
          child: const MaterialApp(
            home: TopicInterestsScreen(advisorKind: 'MENTOR'),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Nhóm Mây'), findsOneWidget);
      expect(find.text('Dự án Mây · 4/6 thành viên'), findsOneWidget);
      expect(find.text('Nhóm chọn tôi'), findsNothing);
      expect(find.text('2 người khác cũng quan tâm'), findsOneWidget);
      await tester.tap(find.text('Quan tâm'));
      await tester.pumpAndSettle();
      expect(api.openedTeam, 'team-1');
      expect(api.openedKind, 'MENTOR');
    },
  );

  testWidgets(
    'lecturer sees preference tab and cannot open interest when limit is reached',
    (tester) async {
      tester.view.physicalSize = const Size(900, 1700);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      final api = _Api()..interestCount = 2;
      await tester.pumpWidget(
        ProviderScope(
          overrides: [lecturerHomeApiProvider.overrideWithValue(api)],
          child: const MaterialApp(
            home: TopicInterestsScreen(advisorKind: 'LECTURER'),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Nhóm chọn tôi'), findsOneWidget);
      expect(find.text('Đã đạt giới hạn quan tâm'), findsOneWidget);
      await tester.tap(find.text('Nhóm chọn tôi'));
      await tester.pumpAndSettle();
      expect(
        find.text('Chưa có nhóm chọn bạn làm nguyện vọng.'),
        findsOneWidget,
      );
      expect(api.openedTeam, isNull);
    },
  );

  for (final size in const [Size(360, 800), Size(412, 915)]) {
    testWidgets('L07 fits ${size.width}dp at 130% text scale', (tester) async {
      tester.view.physicalSize = size;
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      await tester.pumpWidget(
        ProviderScope(
          overrides: [lecturerHomeApiProvider.overrideWithValue(_Api())],
          child: MaterialApp(
            builder: (context, child) => MediaQuery(
              data: MediaQuery.of(
                context,
              ).copyWith(textScaler: const TextScaler.linear(1.3)),
              child: child!,
            ),
            home: const TopicInterestsScreen(advisorKind: 'LECTURER'),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
    });
  }
}
