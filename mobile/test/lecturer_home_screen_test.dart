import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:exe_team_matching/core/auth/session.dart';
import 'package:exe_team_matching/core/phase.dart';
import 'package:exe_team_matching/core/providers.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_api.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_models.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_screen.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart' show CatalogRef;

class FakeLecturerHomeApi extends LecturerHomeApi {
  FakeLecturerHomeApi({
    this.preferencesMe = const [],
    this.topicInterests = const [],
    this.recommendationsResponse = const TopicRecommendationsResponse(),
    this.teamsMeResponse = const LecturerTeamsMeResponse(),
    this.semesterStats = const LecturerSemesterStats(),
    this.shouldStatsThrow = false,
  }) : super(Dio());

  List<LecturerPreferenceMe> preferencesMe;
  List<TopicInterestItem> topicInterests;
  TopicRecommendationsResponse recommendationsResponse;
  LecturerTeamsMeResponse teamsMeResponse;
  LecturerSemesterStats semesterStats;
  bool shouldStatsThrow;

  int openInterestCallCount = 0;
  String? lastOpenedTeamId;
  String? lastOpenedAdvisorKind;
  int withdrawInterestCallCount = 0;
  String? lastWithdrawnInterestId;

  @override
  Future<List<LecturerPreferenceMe>> getMyPreferences() async => preferencesMe;

  @override
  Future<List<TopicInterestItem>> getMyTopicInterests({String advisorKind = 'LECTURER'}) async => topicInterests;

  @override
  Future<TopicRecommendationsResponse> getRecommendations({
    String advisorKind = 'LECTURER',
    String? courseCode,
    int page = 1,
    int pageSize = 20,
  }) async =>
      recommendationsResponse;

  @override
  Future<void> openInterest(String teamId, {String advisorKind = 'LECTURER'}) async {
    openInterestCallCount++;
    lastOpenedTeamId = teamId;
    lastOpenedAdvisorKind = advisorKind;
  }

  @override
  Future<void> withdrawInterest(String interestId) async {
    withdrawInterestCallCount++;
    lastWithdrawnInterestId = interestId;
  }

  @override
  Future<LecturerTeamsMeResponse> getLecturerTeamsMe({String? courseCode}) async => teamsMeResponse;

  @override
  Future<LecturerSemesterStats> getSemesterStats() async {
    if (shouldStatsThrow) {
      throw Exception('Collaboration Service unavailable');
    }
    return semesterStats;
  }
}

final sampleLecturerUser = UserInfo(
  id: 'lec-uid',
  email: 'khoanv@fpt.edu.vn',
  fullName: 'Thầy Khoa',
  roles: const {Role.lecturer},
);

final samplePref1 = LecturerPreferenceMe(
  teamId: 'team-alpha',
  teamName: 'Team Alpha',
  courseCode: 'EXE101',
  memberCount: 5,
  projectName: 'Hệ thống quản lý điểm',
  domains: const [CatalogRef(id: 1, code: 'D01', name: 'AI')],
  rank: 1,
  iAmInterested: false,
);

final samplePref2 = LecturerPreferenceMe(
  teamId: 'team-beta',
  teamName: 'Team Beta',
  courseCode: 'EXE201',
  memberCount: 4,
  projectName: 'Smart Logistics',
  domains: const [CatalogRef(id: 2, code: 'D02', name: 'IoT')],
  rank: 2,
  iAmInterested: true,
);

final sampleRecommendation = TopicRecommendationItem(
  teamId: 'team-gamma',
  teamName: 'Team Gamma',
  courseCode: 'EXE201',
  projectName: 'Fintech Mobile App',
  projectDescription: 'Ứng dụng thanh toán thông minh kế thừa từ EXE101',
  domains: const [CatalogRef(id: 3, code: 'D03', name: 'Fintech')],
  memberCount: 5,
  matchingDomains: const [CatalogRef(id: 3, code: 'D03', name: 'Fintech')],
  wasMyEXE101Team: true,
);

void main() {
  Widget buildApp({
    required FakeLecturerHomeApi api,
    required bool isTeamFormation,
    UserInfo? user,
  }) {
    return ProviderScope(
      overrides: [
        lecturerHomeApiProvider.overrideWithValue(api),
        shellPhaseProvider.overrideWithValue(
          ShellPhase(
            isTeamFormation: isTeamFormation,
            isMentoringWindow: false,
          ),
        ),
        authControllerProvider.overrideWith(() => FakeAuthController(user ?? sampleLecturerUser)),
      ],
      child: MaterialApp(
        theme: AppTheme.light(),
        home: const LecturerHomeScreen(),
      ),
    );
  }

  group('L01 - LecturerHomeScreen (Team Formation Phase)', () {
    testWidgets('renders greeting, course selector, metrics, and preferences me list',
        (tester) async {
      tester.view.physicalSize = const Size(800, 1600);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);

      final fakeApi = FakeLecturerHomeApi(
        preferencesMe: [samplePref1, samplePref2],
        topicInterests: [
          TopicInterestItem(
            id: 'ti-1',
            teamId: 'team-beta',
            advisorId: 'lec-uid',
            advisorName: 'Thầy Khoa',
            advisorEmail: 'khoanv@fpt.edu.vn',
            advisorRole: 'LECTURER',
            status: 'OPEN',
          ),
        ],
        recommendationsResponse: TopicRecommendationsResponse(
          openInterestCount: 1,
          interestLimit: 7,
          interestOpen: true,
          items: [sampleRecommendation],
        ),
      );

      await tester.pumpWidget(
        buildApp(
          api: fakeApi,
          isTeamFormation: true,
        ),
      );
      await tester.pumpAndSettle();

      // Greeting
      expect(find.text('Xin chào, Thầy Khoa'), findsOneWidget);
      expect(find.text('Đợt lập nhóm · Chọn nguyện vọng & quan tâm'), findsOneWidget);

      // Course selector has EXE201 and EXE101
      expect(find.text('EXE201'), findsWidgets);
      expect(find.text('EXE101'), findsWidgets);

      // Metrics:
      expect(find.text('Nhóm chọn tôi'), findsWidgets);
      expect(find.text('2'), findsWidgets); // 2 preferences overall
      expect(find.text('Đang quan tâm'), findsWidgets);
      expect(find.text('1'), findsWidgets); // 1 open interest

      // Team Alpha and Team Beta in list
      expect(find.text('Team Alpha'), findsOneWidget);
      expect(find.text('Team Beta'), findsOneWidget);
      expect(find.text('NV1'), findsOneWidget);
      expect(find.text('NV2'), findsOneWidget);
      expect(find.text('Đôi bên quan tâm'), findsOneWidget);

      // Recommendation card
      expect(find.text('Fintech Mobile App'), findsOneWidget);
      expect(find.text('Đề tài EXE101'), findsOneWidget);
    });

    testWidgets('opens interest on recommendation card with undo',
        (tester) async {
      tester.view.physicalSize = const Size(800, 1600);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);

      final fakeApi = FakeLecturerHomeApi(
        preferencesMe: [],
        recommendationsResponse: TopicRecommendationsResponse(
          openInterestCount: 0,
          interestLimit: 7,
          interestOpen: true,
          items: [sampleRecommendation],
        ),
      );

      await tester.pumpWidget(
        buildApp(
          api: fakeApi,
          isTeamFormation: true,
        ),
      );
      await tester.pumpAndSettle();

      final interestBtn = find.widgetWithText(FilledButton, 'Quan tâm');
      expect(interestBtn, findsOneWidget);

      await tester.ensureVisible(interestBtn);
      await tester.pumpAndSettle();
      await tester.tap(interestBtn);
      await tester.pumpAndSettle();

      expect(fakeApi.openInterestCallCount, 1);
      expect(fakeApi.lastOpenedTeamId, sampleRecommendation.teamId);
      expect(fakeApi.lastOpenedAdvisorKind, 'LECTURER');
      expect(find.text('Đã mở quan tâm nhóm "Team Gamma"'), findsOneWidget);
      expect(find.text('Hoàn tác'), findsOneWidget);
    });
  });

  group('L01 - LecturerHomeScreen (In-Semester Phase)', () {
    testWidgets('renders 3 metrics cards, schedule prompt for EXE201, and assigned teams',
        (tester) async {
      final fakeApi = FakeLecturerHomeApi(
        teamsMeResponse: const LecturerTeamsMeResponse(
          maxTeams: 5,
          instructing: [
            AdvisedTeamItem(
              teamId: 'team-alpha',
              teamName: 'Team Alpha',
              courseCode: 'EXE201',
              projectName: 'Hệ thống quản lý điểm',
              memberCount: 5,
              reason: 'Nguyện vọng 1',
            ),
          ],
        ),
        semesterStats: const LecturerSemesterStats(
          needsReplyCount: 3,
          overdueSubmissionsCount: 1,
          meetingsTodayCount: 2,
          missingCheckpointsCount: 4,
          hasPostedWeeklySchedule: false,
        ),
      );

      await tester.pumpWidget(
        buildApp(
          api: fakeApi,
          isTeamFormation: false,
        ),
      );
      await tester.pumpAndSettle();

      // Greeting and phase
      expect(find.text('Xin chào, Thầy Khoa'), findsOneWidget);
      expect(find.text('Học kỳ đang diễn ra · Theo dõi & hướng dẫn'), findsOneWidget);

      // Default course is EXE201 -> 3 metric cards: Cần trả lời, Trễ hạn nộp, Họp hôm nay
      expect(find.text('Cần trả lời'), findsOneWidget);
      expect(find.text('3'), findsOneWidget);
      expect(find.text('Trễ hạn nộp'), findsOneWidget);
      expect(find.text('1'), findsOneWidget);
      expect(find.text('Họp hôm nay'), findsOneWidget);
      expect(find.text('2'), findsOneWidget);

      // Weekly schedule banner (Rule 20 for EXE201)
      expect(find.text('Đăng lịch họp hằng tuần (C09)'), findsOneWidget);

      // Advised team card from real API /api/lecturer-teams/me
      expect(find.text('Team Alpha'), findsOneWidget);
      expect(find.text('EXE201 · 5 thành viên · Nguyện vọng 1'), findsOneWidget);

      // Switch to EXE101
      await tester.tap(find.text('EXE101').first);
      await tester.pumpAndSettle();

      // Rule 21 for EXE101: 3rd metric card changes to 'Chưa xong CP', Weekly schedule prompt is hidden
      expect(find.text('Chưa xong CP'), findsOneWidget);
      expect(find.text('4'), findsOneWidget);
      expect(find.text('Họp hôm nay'), findsNothing);
      expect(find.text('Đăng lịch họp hằng tuần (C09)'), findsNothing);
    });

    testWidgets('shows warning banner when stats service is unavailable',
        (tester) async {
      final fakeApi = FakeLecturerHomeApi(
        shouldStatsThrow: true,
        teamsMeResponse: const LecturerTeamsMeResponse(
          instructing: [
            AdvisedTeamItem(
              teamId: 'team-beta',
              teamName: 'Team Beta',
              courseCode: 'EXE201',
              projectName: 'Smart Logistics',
              memberCount: 4,
              reason: 'Nguyện vọng 2',
            ),
          ],
        ),
      );

      await tester.pumpWidget(
        buildApp(
          api: fakeApi,
          isTeamFormation: false,
        ),
      );
      await tester.pumpAndSettle();

      // Error banner is displayed
      expect(find.text('Dữ liệu tương tác & checkpoint đang đồng bộ từ Collaboration Service.'), findsOneWidget);

      // Advised teams are still displayed safely
      expect(find.text('Team Beta'), findsOneWidget);
    });
  });
}

class FakeAuthController extends AuthController {
  FakeAuthController(this._user);
  final UserInfo _user;

  @override
  Future<UserInfo?> build() async => _user;
}
