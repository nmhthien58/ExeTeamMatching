import 'package:dio/dio.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_api.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_models.dart';
import 'package:exe_team_matching/features/lecturer/topic_interests_screen.dart';
import 'package:exe_team_matching/features/lecturer_profile/lecturer_profile_screen.dart'
    show lecturerProfileProvider, LecturerProfile;
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

class _Api extends LecturerHomeApi {
  _Api() : super(Dio());

  String? openedKind;
  String? openedTeam;
  String? withdrawnId;
  bool interestOpen = true;
  int interestCount = 0;
  List<TopicRecommendationItem>? customItems;
  List<TopicInterestItem>? customInterests;

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
    items: customItems ?? const [
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

  final allCalls = <String?>[];

  @override
  Future<TopicRecommendationsResponse> getAllTopics({
    String advisorKind = 'LECTURER',
    String? courseCode,
    int page = 1,
    int pageSize = 100,
  }) async {
    allCalls.add(courseCode);
    return TopicRecommendationsResponse(
      openInterestCount: interestCount,
      interestLimit: 2,
      interestOpen: interestOpen,
      items: [
        if (courseCode == 'EXE201')
          const TopicRecommendationItem(
            teamId: 'team-inherited',
            teamName: 'Nhóm Kế Thừa',
            courseCode: 'EXE201',
            projectName: 'Đề tài kế thừa',
            memberCount: 5,
            maxMemberCount: 6,
          ),
      ],
    );
  }

  @override
  Future<List<TopicInterestItem>> getMyTopicInterests({
    String advisorKind = 'LECTURER',
  }) async => customInterests ?? (openedTeam != null ? [
    TopicInterestItem(
      id: 'interest-1',
      teamId: openedTeam!,
      advisorId: 'adv-1',
      advisorName: 'Cố vấn 1',
      status: 'OPEN',
      advisorRole: advisorKind,
    ),
  ] : []);

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

  @override
  Future<void> withdrawInterest(String id) async {
    withdrawnId = id;
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

  testWidgets(
    'lecturer sees teams outside suggestions in "Tất cả nhóm" and can open interest there',
    (tester) async {
      tester.view.physicalSize = const Size(900, 1700);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      final api = _Api();
      await tester.pumpWidget(
        ProviderScope(
          overrides: [lecturerHomeApiProvider.overrideWithValue(api)],
          child: const MaterialApp(
            home: TopicInterestsScreen(advisorKind: 'LECTURER'),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Nhóm Kế Thừa'), findsNothing);
      expect(api.allCalls, isEmpty); // chỉ tải khi mở tab
      await tester.tap(find.text('Tất cả nhóm'));
      await tester.pumpAndSettle();

      expect(api.allCalls, containsAll(['EXE101', 'EXE201']));
      expect(find.text('Nhóm Kế Thừa'), findsOneWidget);
      await tester.tap(find.text('Quan tâm'));
      await tester.pumpAndSettle();
      expect(api.openedTeam, 'team-inherited');
      expect(api.openedKind, 'LECTURER');
    },
  );

  testWidgets('renders Rule 26 badge when wasMyEXE101Team is true', (tester) async {
    final api = _Api()
      ..customItems = const [
        TopicRecommendationItem(
          teamId: 'team-exe101',
          teamName: 'Nhóm Cũ',
          courseCode: 'EXE201',
          projectName: 'Dự án Cũ',
          memberCount: 5,
          maxMemberCount: 6,
          wasMyEXE101Team: true,
        ),
      ];
    await tester.pumpWidget(
      ProviderScope(
        overrides: [lecturerHomeApiProvider.overrideWithValue(api)],
        child: const MaterialApp(
          home: TopicInterestsScreen(advisorKind: 'LECTURER'),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Đã hướng dẫn ở EXE101'), findsOneWidget);
  });

  testWidgets('renders student preference rank chip when myPreferenceRank is set', (tester) async {
    final api = _Api()
      ..customItems = const [
        TopicRecommendationItem(
          teamId: 'team-pref',
          teamName: 'Nhóm Chọn',
          courseCode: 'EXE101',
          projectName: 'Dự án AI',
          memberCount: 4,
          maxMemberCount: 5,
          myPreferenceRank: 1,
        ),
      ];
    await tester.pumpWidget(
      ProviderScope(
        overrides: [lecturerHomeApiProvider.overrideWithValue(api)],
        child: const MaterialApp(
          home: TopicInterestsScreen(advisorKind: 'LECTURER'),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Nhóm chọn bạn: nguyện vọng 1'), findsOneWidget);
  });

  testWidgets('renders matched advisor status and disables interest action', (tester) async {
    final api = _Api()
      ..customItems = const [
        TopicRecommendationItem(
          teamId: 'team-matched',
          teamName: 'Nhóm Đã Ghép',
          courseCode: 'EXE101',
          projectName: 'Dự án X',
          memberCount: 5,
          hasAdvisor: true,
        ),
      ];
    await tester.pumpWidget(
      ProviderScope(
        overrides: [lecturerHomeApiProvider.overrideWithValue(api)],
        child: const MaterialApp(
          home: TopicInterestsScreen(advisorKind: 'LECTURER'),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Đã có giảng viên'), findsOneWidget);
    expect(find.text('Quan tâm'), findsNothing);
  });

  testWidgets('shows out-of-phase banner and disables button when interestOpen is false', (tester) async {
    final api = _Api()..interestOpen = false;
    await tester.pumpWidget(
      ProviderScope(
        overrides: [lecturerHomeApiProvider.overrideWithValue(api)],
        child: const MaterialApp(
          home: TopicInterestsScreen(advisorKind: 'LECTURER'),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Ngoài giai đoạn quan tâm: bạn vẫn có thể xem đề tài.'), findsOneWidget);
    expect(find.text('Đã hết giai đoạn quan tâm'), findsOneWidget);
  });

  testWidgets('tapping Quan tâm shows 5s SnackBar with Hoàn tác, and tapping Hoàn tác withdraws', (tester) async {
    final api = _Api();
    await tester.pumpWidget(
      ProviderScope(
        overrides: [lecturerHomeApiProvider.overrideWithValue(api)],
        child: const MaterialApp(
          home: TopicInterestsScreen(advisorKind: 'LECTURER'),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('Quan tâm'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 100));

    expect(find.text('Hoàn tác'), findsOneWidget);
    expect(api.openedTeam, 'team-1');

    await tester.tap(find.text('Hoàn tác'));
    await tester.pumpAndSettle();

    expect(api.withdrawnId, 'interest-1');
  });

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

  testWidgets('shows EmptyState with CTA Cập nhật chuyên môn when lecturer has no domains', (tester) async {
    final api = _Api()..customItems = [];
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          lecturerHomeApiProvider.overrideWithValue(api),
          lecturerProfileProvider.overrideWith((ref) async => const LecturerProfile(
            fullName: 'Thầy Test',
            email: 'test@fpt.edu.vn',
            domains: [],
          )),
        ],
        child: const MaterialApp(
          home: TopicInterestsScreen(advisorKind: 'LECTURER'),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Chưa khai báo chuyên môn'), findsOneWidget);
    expect(find.text('Cập nhật chuyên môn'), findsOneWidget);
  });

  testWidgets('TopicDetailScreen displays Rule 26 badge when wasMyEXE101Team is true', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          advisorTeamDetailProvider('team-1').overrideWith((ref) async => {
            'id': 'team-1',
            'name': 'Nhóm Test',
            'projectName': 'Đề tài Kế thừa',
            'courseCode': 'EXE201',
            'isProjectContinued': true,
            'members': [],
            'domains': [],
          }),
        ],
        child: const MaterialApp(
          home: TopicDetailScreen(
            teamId: 'team-1',
            advisorKind: 'LECTURER',
            wasMyEXE101Team: true,
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Đã hướng dẫn ở EXE101'), findsOneWidget);
  });
}
