import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/phase.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/lecturer/advised_teams_screen.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_api.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_models.dart';
import 'package:exe_team_matching/features/meetings/meetings_api.dart';
import 'package:exe_team_matching/features/mentor/mentor_home_screen.dart';
import 'package:exe_team_matching/features/my_team/team_advisors.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

/// MB-36 – checklist flutter-handoff §5 cho nhóm cố vấn L04, chi tiết nhóm L05 (giảng viên và mentor) và trang chủ mentor M01
/// trước / trong giai đoạn mentor (MB-33, MB-34, MB-44 – #133): 360 và 412 dp, chữ hệ thống 130% không tràn, vùng chạm ≥ 48,
/// nút có nhãn, chữ đạt tương phản. Dữ liệu dùng tên nhóm, đề tài, sinh viên dài để thử xuống dòng.
void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  final screens = <String, (Widget, bool)>{
    'L04 giảng viên': (const AdvisedTeamsScreen(advisorKind: 'LECTURER'), true),
    'L04 mentor': (const AdvisedTeamsScreen(advisorKind: 'MENTOR'), true),
    'L05 giảng viên': (const AdvisedTeamDetailScreen(teamId: 'team-1', advisorKind: 'LECTURER'), true),
    'L05 mentor': (const AdvisedTeamDetailScreen(teamId: 'team-1', advisorKind: 'MENTOR'), true),
    'M01 trong giai đoạn mentor': (const MentorHomeScreen(), true),
    'M01 trước tuần mentor': (const MentorHomeScreen(), false),
  };

  for (final size in const [Size(360, 800), Size(412, 915)]) {
    for (final entry in screens.entries) {
      testWidgets('${entry.key} ở ${size.width.toInt()} dp, chữ 130%', (tester) async {
        tester.view.physicalSize = size * 3;
        tester.view.devicePixelRatio = 3;
        addTearDown(tester.view.reset);

        final (screen, mentoringWindow) = entry.value;
        // Màn mở như màn cấp 2 để nút Back / pop có chỗ quay về như trong app.
        final router = GoRouter(initialLocation: '/gv/man', routes: [
          GoRoute(path: '/gv', builder: (_, _) => const Text('Trang chủ'), routes: [GoRoute(path: 'man', builder: (_, _) => screen)]),
        ]);
        // Không dùng addTearDown: flutter_test kiểm SemanticsHandle trước khi chạy tear-down.
        final semantics = tester.ensureSemantics();
        try {
          await tester.pumpWidget(ProviderScope(
            overrides: [
              lecturerHomeApiProvider.overrideWithValue(_FakeAdvisor()),
              shellPhaseProvider.overrideWithValue(ShellPhase(isTeamFormation: false, isMentoringWindow: mentoringWindow)),
              teamLecturerProvider.overrideWith((ref, teamId) async => const TeamAdvisor(name: 'PGS. TS. Nguyễn Hoàng Bảo Anh')),
            ],
            child: MaterialApp.router(
              theme: AppTheme.light(),
              routerConfig: router,
              builder: (context, child) => MediaQuery(
                data: MediaQuery.of(context).copyWith(textScaler: const TextScaler.linear(1.3)),
                child: child!,
              ),
            ),
          ));
          await tester.pumpAndSettle();
          // Màn phải dựng nội dung thật (nhóm hoặc hồ sơ mentor), không dừng ở trạng thái trống / lỗi.
          expect(find.textContaining('Bình Minh').evaluate().isNotEmpty || find.textContaining('Đỗ Quang Vinh').evaluate().isNotEmpty, isTrue);

          // Không tràn bố cục (RenderFlex overflow sẽ là exception).
          expect(tester.takeException(), isNull);
          await expectLater(tester, meetsGuideline(androidTapTargetGuideline));
          await expectLater(tester, meetsGuideline(labeledTapTargetGuideline));
          await expectLater(tester, meetsGuideline(textContrastGuideline));
        } finally {
          semantics.dispose();
        }
      });
    }
  }
}

class _FakeAdvisor extends LecturerHomeApi {
  _FakeAdvisor() : super(Dio());

  static const _teamName = 'Nhóm Bình Minh Sáng Tạo Khởi Nghiệp';
  static const _project = 'EduSpark – nền tảng học nhóm theo lịch rảnh của sinh viên';

  static const _progress = CheckpointProgress(
    workspaceId: 'workspace-1',
    teamName: _teamName,
    doneCount: 1,
    items: [
      CheckpointItem(checkpointNo: 1, presented: true, comment: 'Trình bày rõ vấn đề, cần thêm số liệu khảo sát thị trường.', canUndo: true),
      CheckpointItem(checkpointNo: 2, presented: false, overdue: true),
      CheckpointItem(checkpointNo: 3, presented: false),
      CheckpointItem(checkpointNo: 4, presented: false),
    ],
  );

  static const _team = AdvisedTeamItem(
    teamId: 'team-1', teamName: _teamName, courseCode: 'EXE101', classCode: 'EXE101SE01',
    projectName: _project, memberCount: 6, reason: 'PREFERENCE', preferenceRank: 1,
  );

  static const _workspace = AdvisorWorkspaceRef(id: 'workspace-1', teamId: 'team-1', teamName: _teamName, courseCode: 'EXE101', myRole: 'INSTRUCTOR');

  @override
  Future<LecturerTeamsMeResponse> getLecturerTeamsMe({String? courseCode}) async => const LecturerTeamsMeResponse(maxTeams: 6, instructing: [_team]);

  @override
  Future<List<AdvisedTeamItem>> getMentorTeamsMe() async => const [
        AdvisedTeamItem(teamId: 'team-1', teamName: _teamName, courseCode: 'EXE201', projectName: _project, memberCount: 6, reason: 'MENTOR_INTEREST'),
      ];

  @override
  Future<List<AdvisorWorkspaceRef>> getAdvisorWorkspaces() async => const [_workspace];

  @override
  Future<List<CheckpointProgress>> getCheckpoints({int? missing}) async => const [_progress];

  @override
  Future<CheckpointProgress> getCheckpoint(String workspaceId) async => _progress;

  @override
  Future<AdvisorTeamDetail> getAdvisorTeamDetail(String teamId) async => const AdvisorTeamDetail(
        ideaDescription: 'Ứng dụng ghép bạn học theo môn và lịch rảnh, gợi ý phòng học trống và nhắc lịch ôn tập trước kỳ thi.',
        team: TeamDetail(
          card: TeamCard(id: 'team-1', name: _teamName, status: 'FINALIZED', memberCount: 2, projectName: _project),
          members: [
            StudentRef(id: 's1', studentCode: 'SE180001', fullName: 'Nguyễn Hoàng Bảo Anh Minh', email: 'anhnhbm.se180001@example.com', isLeader: true),
            StudentRef(id: 's2', studentCode: 'SE180002', fullName: 'Trần Thị Minh Châu Ngọc', email: 'chauttmn.se180002@example.com'),
          ],
          isLeader: false,
        ),
      );

  @override
  Future<AdvisorTeamActivity> getTeamActivity(AdvisorWorkspaceRef workspace, {required bool includeTasks, required bool includeSchedule}) async =>
      AdvisorTeamActivity(
        workspace: workspace,
        meetings: const MeetingList(items: [], mentorSessionsDone: 2, expectedMentorSessions: 3),
      );

  @override
  Future<MentorProfile> getMentorProfile() async =>
      const MentorProfile(id: 'mentor-1', fullName: 'Đỗ Quang Vinh – Công ty Demo Khởi Nghiệp', maxTeams: 3, currentTeamCount: 1, preferredTeams: 2);

  @override
  Future<int> getOpenQuestionCount() async => 12;

  @override
  Future<TopicRecommendationsResponse> getRecommendations({String advisorKind = 'LECTURER', String? courseCode, int page = 1, int pageSize = 20}) async =>
      const TopicRecommendationsResponse(openInterestCount: 2);
}
