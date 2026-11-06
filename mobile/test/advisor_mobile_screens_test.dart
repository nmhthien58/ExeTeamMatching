import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/core/phase.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/lecturer/advised_teams_screen.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_api.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_models.dart';
import 'package:exe_team_matching/features/mentor/mentor_home_screen.dart';
import 'package:exe_team_matching/features/meetings/meetings_api.dart';
import 'package:exe_team_matching/features/my_team/team_advisors.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

class FakeAdvisorApi extends LecturerHomeApi {
  FakeAdvisorApi() : super(Dio());

  var progress = const CheckpointProgress(
    workspaceId: 'workspace-1',
    teamName: 'Nhóm Alpha',
    doneCount: 1,
    items: [
      CheckpointItem(checkpointNo: 1, presented: true),
      CheckpointItem(checkpointNo: 2, presented: false, overdue: true),
      CheckpointItem(checkpointNo: 3, presented: false),
      CheckpointItem(checkpointNo: 4, presented: false),
    ],
  );
  var profile = const MentorProfile(
    id: 'mentor-1',
    fullName: 'Mentor Mai',
    maxTeams: 3,
    currentTeamCount: 1,
    preferredTeams: 2,
  );
  int markCalls = 0;
  int undoCalls = 0;
  int savePreferredCalls = 0;
  int teamActivityCalls = 0;
  ApiException? markError;

  static const team = AdvisedTeamItem(
    teamId: 'team-1',
    teamName: 'Nhóm Alpha',
    courseCode: 'EXE101',
    classCode: 'SE1801',
    projectName: 'Trợ lý học tập',
    memberCount: 5,
    reason: 'PREFERENCE',
    preferenceRank: 1,
  );

  static const workspace = AdvisorWorkspaceRef(
    id: 'workspace-1',
    teamId: 'team-1',
    teamName: 'Nhóm Alpha',
    courseCode: 'EXE101',
    myRole: 'INSTRUCTOR',
  );

  @override
  Future<LecturerTeamsMeResponse> getLecturerTeamsMe({
    String? courseCode,
  }) async => const LecturerTeamsMeResponse(instructing: [team]);

  @override
  Future<List<AdvisedTeamItem>> getMentorTeamsMe() async => [
    const AdvisedTeamItem(
      teamId: 'team-1',
      teamName: 'Nhóm Alpha',
      courseCode: 'EXE101',
      projectName: 'Trợ lý học tập',
      memberCount: 5,
      reason: 'MENTOR_INTEREST',
    ),
  ];

  @override
  Future<List<AdvisorWorkspaceRef>> getAdvisorWorkspaces() async => [workspace];

  @override
  Future<List<CheckpointProgress>> getCheckpoints({int? missing}) async => [
    progress,
  ];

  @override
  Future<CheckpointProgress> getCheckpoint(String workspaceId) async =>
      progress;

  @override
  Future<AdvisorTeamDetail> getAdvisorTeamDetail(String teamId) async =>
      AdvisorTeamDetail(
        ideaDescription: 'Ứng dụng hỗ trợ sinh viên ôn tập.',
        team: TeamDetail(
          card: const TeamCard(
            id: 'team-1',
            name: 'Nhóm Alpha',
            status: 'ACTIVE',
            memberCount: 1,
            projectName: 'Trợ lý học tập',
          ),
          members: const [
            StudentRef(
              id: 'student-1',
              studentCode: 'SE123456',
              fullName: 'Nguyễn An',
              email: 'an@fpt.edu.vn',
              isLeader: true,
            ),
          ],
          isLeader: false,
        ),
      );

  @override
  Future<AdvisorTeamActivity> getTeamActivity(
    AdvisorWorkspaceRef workspace, {
    required bool includeTasks,
    required bool includeSchedule,
  }) async {
    teamActivityCalls++;
    return AdvisorTeamActivity(
      workspace: workspace,
      meetings: const MeetingList(
        items: [],
        mentorSessionsDone: 2,
        expectedMentorSessions: 3,
      ),
    );
  }

  @override
  Future<CheckpointProgress> markCheckpoint(
    String workspaceId, {
    required int checkpointNo,
    required DateTime presentedOn,
    String? comment,
  }) async {
    if (markError != null) throw markError!;
    markCalls++;
    progress = CheckpointProgress(
      workspaceId: workspaceId,
      teamName: 'Nhóm Alpha',
      doneCount: 2,
      items: [
        const CheckpointItem(checkpointNo: 1, presented: true),
        CheckpointItem(
          checkpointNo: checkpointNo,
          presented: true,
          presentedOn: presentedOn,
          comment: comment,
          canUndo: true,
        ),
        const CheckpointItem(checkpointNo: 3, presented: false),
        const CheckpointItem(checkpointNo: 4, presented: false),
      ],
    );
    return progress;
  }

  @override
  Future<void> undoCheckpoint(String workspaceId, int checkpointNo) async {
    undoCalls++;
    progress = const CheckpointProgress(
      workspaceId: 'workspace-1',
      teamName: 'Nhóm Alpha',
      doneCount: 1,
      items: [
        CheckpointItem(checkpointNo: 1, presented: true),
        CheckpointItem(checkpointNo: 2, presented: false),
        CheckpointItem(checkpointNo: 3, presented: false),
        CheckpointItem(checkpointNo: 4, presented: false),
      ],
    );
  }

  @override
  Future<MentorProfile> getMentorProfile() async => profile;

  @override
  Future<MentorProfile> savePreferredTeams(int preferredTeams) async {
    savePreferredCalls++;
    profile = MentorProfile(
      id: profile.id,
      fullName: profile.fullName,
      maxTeams: profile.maxTeams,
      currentTeamCount: profile.currentTeamCount,
      preferredTeams: preferredTeams,
    );
    return profile;
  }

  @override
  Future<int> getOpenQuestionCount() async => 2;

  @override
  Future<TopicRecommendationsResponse> getRecommendations({
    String advisorKind = 'LECTURER',
    String? courseCode,
    int page = 1,
    int pageSize = 20,
  }) async => const TopicRecommendationsResponse(openInterestCount: 2);
}

void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  Widget app(
    Widget home,
    FakeAdvisorApi api, {
    bool mentoringWindow = true,
    double textScale = 1,
  }) => ProviderScope(
    overrides: [
      lecturerHomeApiProvider.overrideWithValue(api),
      shellPhaseProvider.overrideWithValue(
        ShellPhase(isTeamFormation: false, isMentoringWindow: mentoringWindow),
      ),
      teamLecturerProvider.overrideWith(
        (ref, teamId) async => const TeamAdvisor(name: 'Cô Lan'),
      ),
    ],
    child: MaterialApp(
      theme: AppTheme.light(),
      builder: (context, child) => MediaQuery(
        data: MediaQuery.of(
          context,
        ).copyWith(textScaler: TextScaler.linear(textScale)),
        child: child!,
      ),
      home: home,
    ),
  );

  testWidgets('L04 shows summary without loading per-team activity', (
    tester,
  ) async {
    final api = FakeAdvisorApi();
    await tester.pumpWidget(
      app(const AdvisedTeamsScreen(advisorKind: 'LECTURER'), api),
    );
    await tester.pumpAndSettle();

    expect(find.text('Nhóm Alpha'), findsOneWidget);
    expect(find.text('Nguyện vọng 1'), findsOneWidget);
    expect(find.text('Checkpoint 1/4'), findsOneWidget);
    expect(api.teamActivityCalls, 0);
  });

  testWidgets('L05 marks checkpoint and offers undo', (tester) async {
    tester.view.physicalSize = const Size(800, 1200);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final api = FakeAdvisorApi();
    await tester.pumpWidget(
      app(
        const AdvisedTeamDetailScreen(
          teamId: 'team-1',
          advisorKind: 'LECTURER',
        ),
        api,
      ),
    );
    await tester.pumpAndSettle();
    expect(api.teamActivityCalls, 1);

    await tester.scrollUntilVisible(
      find.text('Đánh dấu Checkpoint'),
      300,
      scrollable: find.byType(Scrollable).first,
    );
    await tester.tap(find.text('Đánh dấu Checkpoint'));
    await tester.pumpAndSettle();
    expect(find.textContaining('Nhận xét'), findsOneWidget);
    await tester.tap(find.text('Lưu'));
    await tester.pump(const Duration(seconds: 1));

    expect(api.markCalls, 1);
    expect(find.text('Đã ghi nhận Checkpoint 2'), findsOneWidget);
    tester.widget<SnackBarAction>(find.byType(SnackBarAction)).onPressed();
    await tester.pumpAndSettle();
    expect(api.undoCalls, 1);
  });

  testWidgets('L05 limits checkpoint date and shows API detail', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(800, 1200);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final api = FakeAdvisorApi()
      ..markError = const ApiException(
        status: 400,
        code: 'future-checkpoint',
        title: 'Ngày không hợp lệ',
        detail: 'Ngày trình bày không được ở tương lai.',
      );
    await tester.pumpWidget(
      app(
        const AdvisedTeamDetailScreen(
          teamId: 'team-1',
          advisorKind: 'LECTURER',
        ),
        api,
      ),
    );
    await tester.pumpAndSettle();
    await tester.scrollUntilVisible(
      find.text('Đánh dấu Checkpoint'),
      300,
      scrollable: find.byType(Scrollable).first,
    );
    await tester.tap(find.text('Đánh dấu Checkpoint'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Ngày trình bày'));
    await tester.pumpAndSettle();

    final picker = tester.widget<CalendarDatePicker>(
      find.byType(CalendarDatePicker),
    );
    final today = DateTime.now();
    expect(DateUtils.dateOnly(picker.lastDate), DateUtils.dateOnly(today));
    await tester.tap(find.byType(TextButton).last);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Lưu'));
    await tester.pumpAndSettle();

    expect(find.text('Ngày trình bày không được ở tương lai.'), findsOneWidget);
  });

  testWidgets('M01 before week 6 shows interests and saves preferred teams', (
    tester,
  ) async {
    final api = FakeAdvisorApi();
    await tester.pumpWidget(
      app(const MentorHomeScreen(), api, mentoringWindow: false),
    );
    await tester.pumpAndSettle();

    expect(
      find.text('Đang quan tâm 2 đề tài · Ghép vào tuần 6'),
      findsOneWidget,
    );
    await tester.tap(find.text('Sửa số nhóm mong muốn'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Lưu'));
    await tester.pumpAndSettle();
    expect(api.savePreferredCalls, 1);
  });

  testWidgets('M01 mentoring window shows metrics and mentor sessions', (
    tester,
  ) async {
    final api = FakeAdvisorApi();
    await tester.pumpWidget(app(const MentorHomeScreen(), api));
    await tester.pumpAndSettle();

    expect(find.text('Cần trả lời'), findsOneWidget);
    expect(find.text('Họp sắp tới'), findsOneWidget);
    expect(find.text('Chờ chốt lịch'), findsOneWidget);
    expect(find.text('Buổi mentor 2/3'), findsOneWidget);
    expect(find.text('Giảng viên: Cô Lan'), findsOneWidget);
  });

  for (final size in const [Size(360, 800), Size(412, 915)]) {
    testWidgets('advisor screens fit ${size.width}dp at 130% text scale', (
      tester,
    ) async {
      tester.view.physicalSize = size;
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final api = FakeAdvisorApi();

      await tester.pumpWidget(
        app(
          const AdvisedTeamsScreen(advisorKind: 'LECTURER'),
          api,
          textScale: 1.3,
        ),
      );
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      await _expectGuidelines(tester);

      await tester.pumpWidget(
        app(
          const AdvisedTeamsScreen(advisorKind: 'MENTOR'),
          api,
          textScale: 1.3,
        ),
      );
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      await _expectGuidelines(tester);

      await tester.pumpWidget(
        app(
          const AdvisedTeamDetailScreen(
            teamId: 'team-1',
            advisorKind: 'LECTURER',
          ),
          api,
          textScale: 1.3,
        ),
      );
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      await _expectGuidelines(tester);

      await tester.pumpWidget(
        app(
          const MentorHomeScreen(),
          api,
          mentoringWindow: false,
          textScale: 1.3,
        ),
      );
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      await _expectGuidelines(tester);

      await tester.pumpWidget(
        app(const MentorHomeScreen(), api, textScale: 1.3),
      );
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      await _expectGuidelines(tester);
    });
  }
}

Future<void> _expectGuidelines(WidgetTester tester) async {
  await expectLater(tester, meetsGuideline(androidTapTargetGuideline));
  await expectLater(tester, meetsGuideline(labeledTapTargetGuideline));
  await expectLater(tester, meetsGuideline(textContrastGuideline));
}
