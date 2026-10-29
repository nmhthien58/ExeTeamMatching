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
    this.recommendations = const [],
    this.semesterStats = const LecturerSemesterStats(),
  }) : super(Dio());

  List<LecturerPreferenceMe> preferencesMe;
  List<TopicInterestItem> topicInterests;
  List<TopicRecommendationItem> recommendations;
  LecturerSemesterStats semesterStats;

  int openInterestCallCount = 0;
  String? lastOpenedTeamId;
  int withdrawInterestCallCount = 0;
  String? lastWithdrawnInterestId;

  @override
  Future<List<LecturerPreferenceMe>> getMyPreferences() async => preferencesMe;

  @override
  Future<List<TopicInterestItem>> getMyTopicInterests() async => topicInterests;

  @override
  Future<List<TopicRecommendationItem>> getRecommendations() async =>
      recommendations;

  @override
  Future<void> openInterest(String teamId) async {
    openInterestCallCount++;
    lastOpenedTeamId = teamId;
  }

  @override
  Future<void> withdrawInterest(String interestId) async {
    withdrawInterestCallCount++;
    lastWithdrawnInterestId = interestId;
  }

  @override
  Future<LecturerSemesterStats> getSemesterStats() async => semesterStats;
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
  matchingDomainCount: 2,
  isInheritedFromExe101: true,
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
    testWidgets('renders greeting, 2 metric cards, and preferences me list',
        (tester) async {
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
        recommendations: [sampleRecommendation],
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

      // Metrics
      expect(find.text('Nhóm chọn tôi'), findsWidgets);
      expect(find.text('2'), findsWidgets); // 2 teams chose this lecturer
      expect(find.text('Đang quan tâm'), findsWidgets);
      expect(find.text('1'), findsWidgets); // 1 open interest

      // Team Alpha in list
      expect(find.text('Team Alpha'), findsOneWidget);
      expect(find.text('Nguyện vọng 1'), findsOneWidget);
      expect(find.text('Quan tâm lại'), findsOneWidget);

      // Team Beta in list
      expect(find.text('Team Beta'), findsOneWidget);
      expect(find.text('Nguyện vọng 2'), findsOneWidget);
      expect(find.text('Hai bên cùng chọn'), findsOneWidget);

      // Recommendation card
      expect(find.text('Fintech Mobile App'), findsOneWidget);
      expect(find.text('Kế thừa EXE101'), findsOneWidget);
    });

    testWidgets('opens interest on recommendation card with undo',
        (tester) async {
      final fakeApi = FakeLecturerHomeApi(
        preferencesMe: [],
        topicInterests: [],
        recommendations: [sampleRecommendation],
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

      await tester.drag(find.byType(Scrollable).first, const Offset(0, -300));
      await tester.pumpAndSettle();
      await tester.tap(interestBtn);
      await tester.pumpAndSettle();

      expect(fakeApi.openInterestCallCount, 1);
      expect(fakeApi.lastOpenedTeamId, sampleRecommendation.teamId);
      expect(find.text('Đã mở quan tâm nhóm "Team Gamma"'), findsOneWidget);
      expect(find.text('Hoàn tác'), findsOneWidget);
    });
  });

  group('L01 - LecturerHomeScreen (In-Semester Phase)', () {
    testWidgets('renders 3 metrics cards, schedule prompt, and assigned teams',
        (tester) async {
      final fakeApi = FakeLecturerHomeApi(
        semesterStats: LecturerSemesterStats(
          needsReplyCount: 3,
          overdueSubmissionsCount: 1,
          meetingsTodayCount: 2,
          hasPostedWeeklySchedule: false,
          assignedTeams: const [
            LecturerAssignedTeam(
              id: 'team-alpha',
              name: 'Team Alpha',
              courseCode: 'EXE201',
              projectName: 'Hệ thống quản lý điểm',
              memberCount: 5,
              latestCheckpoint: 'Checkpoint 2/4',
              workspaceId: 'ws-alpha',
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

      // Greeting and phase
      expect(find.text('Xin chào, Thầy Khoa'), findsOneWidget);
      expect(find.text('Học kỳ đang diễn ra · Theo dõi & hướng dẫn'), findsOneWidget);

      // 3 metric cards
      expect(find.text('Cần trả lời'), findsOneWidget);
      expect(find.text('3'), findsOneWidget);
      expect(find.text('Trễ hạn nộp'), findsOneWidget);
      expect(find.text('1'), findsOneWidget);
      expect(find.text('Họp hôm nay'), findsOneWidget);
      expect(find.text('2'), findsOneWidget);

      // Weekly schedule banner
      expect(find.text('Đăng lịch họp hằng tuần (C09)'), findsOneWidget);

      // Assigned team card
      expect(find.text('Team Alpha'), findsOneWidget);
      expect(find.text('5 thành viên · Checkpoint 2/4'), findsOneWidget);
    });
  });
}

class FakeAuthController extends AuthController {
  FakeAuthController(this._user);
  final UserInfo _user;

  @override
  Future<UserInfo?> build() async => _user;
}
