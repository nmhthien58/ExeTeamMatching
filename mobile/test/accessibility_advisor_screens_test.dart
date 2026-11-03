import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/auth/session.dart';
import 'package:exe_team_matching/core/phase.dart';
import 'package:exe_team_matching/core/providers.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_api.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_models.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_screen.dart';
import 'package:exe_team_matching/features/lecturer/topic_interests_screen.dart';
import 'package:exe_team_matching/features/lecturer_profile/lecturer_profile_screen.dart' show LecturerProfile, lecturerProfileProvider;
import 'package:exe_team_matching/features/lecturers/team_lecturer_screen.dart';
import 'package:exe_team_matching/features/meeting_schedule/meeting_schedule_api.dart';
import 'package:exe_team_matching/features/meeting_schedule/meeting_schedule_screen.dart';
import 'package:exe_team_matching/features/my_team/my_team_api.dart';
import 'package:exe_team_matching/features/realtime/collab_hub.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

/// MB-36 – checklist flutter-handoff §5 cho giảng viên của nhóm S25 (MB-28), lịch họp hằng tuần C09 phía sinh viên và giảng viên (MB-46),
/// trang chủ giảng viên L01 (MB-32) và đề tài phù hợp L07 (MB-43): 360 và 412 dp, chữ hệ thống 130% không tràn, vùng chạm ≥ 48,
/// nút có nhãn, chữ đạt tương phản. Dữ liệu dùng tên dài để thử xuống dòng.
void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  const longTeam = 'Nhóm Bình Minh Sáng Tạo Khởi Nghiệp';
  const longLecturer = 'PGS. TS. Nguyễn Hoàng Bảo Anh';
  const ai = CatalogRef(id: 1, code: 'D01', name: 'AI & Data Innovation');
  const edu = CatalogRef(id: 5, code: 'D05', name: 'Giáo dục và đào tạo trực tuyến');

  // ---------- S25 ----------
  const teamCard = TeamDetail(card: TeamCard(id: 'team-1', name: longTeam, status: 'FINALIZED', memberCount: 5), members: [], isLeader: true);
  List<Override> s25(TeamLecturerData data) => [
        myTeamProvider.overrideWith((ref) async => teamCard),
        teamLecturerDetailsProvider('team-1').overrideWith((ref) async => data),
      ];
  final s25Before = s25(const TeamLecturerData(preferences: {
    'locked': false,
    'preferences': [
      {'fullName': longLecturer, 'rank': 1, 'isInterested': true},
      {'fullName': 'ThS. Trần Thị Minh Châu', 'rank': 2},
    ],
  }));
  final s25After = s25(const TeamLecturerData(assignment: {'lecturerName': longLecturer, 'reason': 'PREFERENCE', 'preferenceRank': 1}));

  // ---------- C09 ----------
  ScheduleSlot slot(String id, String start, String end, String state, {String? workspaceId, String? teamName}) =>
      ScheduleSlot(id: id, dayOfWeek: 2, startTime: start, endTime: end, state: state, workspaceId: workspaceId, teamName: teamName);
  final c09Api = _FakeSchedule(
    teamSchedule: TeamMeetingSchedule(
      lecturerName: longLecturer,
      location: 'https://meet.google.com/abc-defg-hij',
      bookingDeadline: DateTime.utc(2099, 11, 9, 16, 59),
      canBook: true,
      mySlot: null,
      slots: [
        slot('a', '09:00', '09:15', 'TAKEN', teamName: 'Nhóm Hoàng Hôn'),
        slot('b', '09:15', '09:30', 'FREE'),
        slot('c', '09:30', '09:45', 'FREE'),
      ],
    ),
    mineSchedule: MeetingSchedule(
      id: 'sc1',
      status: 'PUBLISHED',
      slotMinutes: 15,
      location: 'https://meet.google.com/abc-defg-hij',
      bookingDeadline: DateTime.utc(2099, 11, 9),
      windows: const [ScheduleWindow(dayOfWeek: 2, startTime: '09:00', endTime: '09:45')],
      slots: [
        slot('a', '09:00', '09:15', 'TAKEN', workspaceId: 'w2', teamName: longTeam),
        slot('b', '09:15', '09:30', 'FREE'),
      ],
      teamCount: 3,
      teamsWithoutSlot: const ['Nhóm Hoàng Hôn Rực Rỡ', 'Nhóm Sao Mai'],
    ),
  );
  final c09 = <Override>[
    meetingScheduleApiProvider.overrideWithValue(c09Api),
    collabHubProvider.overrideWithValue(CollabHub(_NoopConnection.new)),
    myTeamDetailProvider.overrideWith((ref) async => const MyTeam(
          id: 't2', name: longTeam, status: 'FINALIZED', courseCode: 'EXE201',
          members: [], domains: [], isLeader: true, memberCount: 5, specializationCount: 3,
        )),
    teamWorkspaceIdProvider.overrideWith((ref, teamId) async => 'w1'),
  ];

  // ---------- L01, L07 ----------
  final advisorApi = _FakeAdvisor();
  List<Override> lecturer({required bool teamFormation}) => [
        lecturerHomeApiProvider.overrideWithValue(advisorApi),
        phasesProvider.overrideWith((ref) async => [
              CoursePhase(courseCode: 'EXE201', isTeamFormation: teamFormation),
              CoursePhase(courseCode: 'EXE101', isTeamFormation: teamFormation),
            ]),
        shellPhaseProvider.overrideWithValue(ShellPhase(isTeamFormation: teamFormation, isMentoringWindow: false)),
        authControllerProvider.overrideWith(_FakeAuth.new),
        lecturerProfileProvider.overrideWith((ref) async => const LecturerProfile(
              fullName: longLecturer, email: 'anhnhb.giangvien@fe.edu.vn', domains: [ai, edu], maxTeamsExe101: 6, maxTeamsExe201: 6,
            )),
      ];

  final screens = <String, (Widget, List<Override>)>{
    'S25 trước ghép': (const TeamLecturerScreen(), s25Before),
    'S25 sau ghép': (const TeamLecturerScreen(), s25After),
    'C09 sinh viên': (const TeamScheduleScreen(), c09),
    'C09 giảng viên': (const LecturerScheduleScreen(), c09),
    'L01 đợt lập nhóm': (const LecturerHomeScreen(), lecturer(teamFormation: true)),
    'L01 trong học kỳ': (const LecturerHomeScreen(), lecturer(teamFormation: false)),
    'L07 giảng viên': (const TopicInterestsScreen(advisorKind: 'LECTURER'), lecturer(teamFormation: true)),
    'L07 mentor': (const TopicInterestsScreen(advisorKind: 'MENTOR'), lecturer(teamFormation: true)),
  };

  for (final size in const [Size(360, 800), Size(412, 915)]) {
    for (final entry in screens.entries) {
      testWidgets('${entry.key} ở ${size.width.toInt()} dp, chữ 130%', (tester) async {
        tester.view.physicalSize = size * 3;
        tester.view.devicePixelRatio = 3;
        addTearDown(tester.view.reset);

        final (screen, overrides) = entry.value;
        // Màn mở như màn cấp 2 để nút Back / pop có chỗ quay về như trong app.
        final router = GoRouter(initialLocation: '/gv/man', routes: [
          GoRoute(path: '/gv', builder: (_, _) => const Text('Trang chủ'), routes: [GoRoute(path: 'man', builder: (_, _) => screen)]),
        ]);
        // Không dùng addTearDown: flutter_test kiểm SemanticsHandle trước khi chạy tear-down.
        final semantics = tester.ensureSemantics();
        try {
          await tester.pumpWidget(ProviderScope(
            overrides: overrides,
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

class _FakeSchedule extends MeetingScheduleApi {
  _FakeSchedule({required this.teamSchedule, required this.mineSchedule}) : super(Dio());

  final TeamMeetingSchedule teamSchedule;
  final MeetingSchedule mineSchedule;

  @override
  Future<TeamMeetingSchedule?> team(String workspaceId) async => teamSchedule;

  @override
  Future<MeetingSchedule?> mine() async => mineSchedule;
}

class _FakeAdvisor extends LecturerHomeApi {
  _FakeAdvisor() : super(Dio());

  static const _domains = [CatalogRef(id: 1, code: 'D01', name: 'AI & Data Innovation'), CatalogRef(id: 4, code: 'D04', name: 'Software / SaaS / Platform')];

  @override
  Future<List<LecturerPreferenceMe>> getMyPreferences() async => const [
        LecturerPreferenceMe(
          teamId: 'team-1', teamName: 'Nhóm Bình Minh Sáng Tạo Khởi Nghiệp', courseCode: 'EXE201', memberCount: 5,
          projectName: 'EduSpark – nền tảng học nhóm theo lịch rảnh', domains: _domains, rank: 1, iAmInterested: true, wasMyEXE101Team: true,
        ),
      ];

  @override
  Future<List<TopicInterestItem>> getMyTopicInterests({String advisorKind = 'LECTURER'}) async => const [
        TopicInterestItem(id: 'i1', teamId: 'team-2', advisorId: 'adv-1', advisorName: 'PGS. TS. Nguyễn Hoàng Bảo Anh', status: 'OPEN', advisorRole: 'LECTURER'),
      ];

  @override
  Future<TopicRecommendationsResponse> getRecommendations({String advisorKind = 'LECTURER', String? courseCode, int page = 1, int pageSize = 20}) async =>
      const TopicRecommendationsResponse(
        openInterestCount: 1,
        interestLimit: 3,
        interestOpen: true,
        items: [
          TopicRecommendationItem(
            teamId: 'team-2', teamName: 'Nhóm Hoàng Hôn Rực Rỡ', courseCode: 'EXE201', projectName: 'GreenMove – đặt xe điện chung cho sinh viên',
            projectDescription: 'Ứng dụng đặt xe điện chung theo tuyến cố định giữa ký túc xá và trường', domains: _domains, matchingDomains: _domains,
            memberCount: 4, maxMemberCount: 6, otherInterestedCount: 2, wasMyEXE101Team: true,
          ),
        ],
      );

  @override
  Future<LecturerTeamsMeResponse> getLecturerTeamsMe({String? courseCode}) async => const LecturerTeamsMeResponse(
        maxTeams: 6,
        instructing: [
          AdvisedTeamItem(
            teamId: 'team-1', teamName: 'Nhóm Bình Minh Sáng Tạo Khởi Nghiệp', courseCode: 'EXE201',
            projectName: 'EduSpark – nền tảng học nhóm theo lịch rảnh', memberCount: 5, reason: 'MUTUAL', domains: _domains,
          ),
        ],
      );

  @override
  Future<LecturerSemesterStats> getSemesterStats() async => const LecturerSemesterStats(
        needsReplyCount: 3, overdueSubmissionsCount: 2, meetingsTodayCount: 1, missingCheckpointsCount: 4, hasPostedWeeklySchedule: false,
      );
}

class _FakeAuth extends AuthController {
  @override
  Future<UserInfo?> build() async =>
      UserInfo(id: 'lec-1', email: 'anhnhb.giangvien@fe.edu.vn', fullName: 'PGS. TS. Nguyễn Hoàng Bảo Anh', roles: const {Role.lecturer});
}

class _NoopConnection implements HubConnectionLike {
  @override
  Future<void> start() async {}
  @override
  Future<void> stop() async {}
  @override
  Future<void> invoke(String method, String id) async {}
  @override
  void on(String event, void Function(Object? payload) handler) {}
  @override
  void off(String event, void Function(Object? payload) handler) {}
  @override
  void onStatus({required VoidCallback reconnecting, required VoidCallback reconnected, required VoidCallback closed}) {}
}
