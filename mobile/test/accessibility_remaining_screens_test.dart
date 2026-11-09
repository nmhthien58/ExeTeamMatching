import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/auth/session.dart';
import 'package:exe_team_matching/core/phase.dart';
import 'package:exe_team_matching/core/providers.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/auth/login_screen.dart';
import 'package:exe_team_matching/features/change_requests/change_request_screens.dart';
import 'package:exe_team_matching/features/change_requests/change_requests_api.dart';
import 'package:exe_team_matching/features/lecturer/lecturer_home_api.dart';
import 'package:exe_team_matching/features/lecturer/topic_interests_screen.dart';
import 'package:exe_team_matching/features/lecturer_profile/lecturer_profile_screen.dart';
import 'package:exe_team_matching/features/meetings/meeting_detail_screen.dart';
import 'package:exe_team_matching/features/meetings/meetings_api.dart';
import 'package:exe_team_matching/features/meetings/meetings_screen.dart';
import 'package:exe_team_matching/features/my_team/my_team_api.dart';
import 'package:exe_team_matching/features/my_team/requests_screen.dart';
import 'package:exe_team_matching/features/my_team/team_form_screen.dart';
import 'package:exe_team_matching/features/onboarding/onboarding_screen.dart';
import 'package:exe_team_matching/features/onboarding/onboarding_store.dart';
import 'package:exe_team_matching/features/onboarding/splash_screen.dart';
import 'package:exe_team_matching/features/profile/my_profile_screens.dart';
import 'package:exe_team_matching/features/profile/profile_api.dart';
import 'package:exe_team_matching/features/profile/student_profile_screen.dart';
import 'package:exe_team_matching/features/settings/settings_screen.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart';
import 'package:exe_team_matching/features/workspace/workspace_api.dart';
import 'package:exe_team_matching/features/workspace/workspace_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/a11y.dart';
import 'support/suggestions_fakes.dart' as s;
import 'support/workspace_fakes.dart' as w;

/// MB-36 – checklist flutter-handoff §5 cho các màn còn lại: đăng nhập / chưa có trong danh sách / giới thiệu / màn chờ
/// (A01–A04), hồ sơ (A05, S02, S03, hoàn thiện hồ sơ), hồ sơ sinh viên S04, tạo / sửa nhóm S13, S14, yêu cầu tham gia S20,
/// đổi nhóm S26–S28, lịch họp C07, C08, cài đặt A07, hồ sơ giảng viên L06, chi tiết đề tài L07.
void main() {
  const longName = 'Nguyễn Hoàng Bảo Anh Minh';
  const longTeam = 'Nhóm Bình Minh Sáng Tạo Khởi Nghiệp';
  const domain = CatalogRef(id: 4, code: 'D04', name: 'Software / SaaS / Platform');

  // ---------- Hồ sơ (Nam) ----------
  final leaderTeam = TeamDetail(
    card: const TeamCard(id: 't1', name: longTeam, status: 'ACTIVE', memberCount: 3),
    members: [s.student('01', longName)],
    isLeader: true,
  );
  final profile = [
    profileApiProvider.overrideWithValue(_FakeProfile()),
    suggestionsApiProvider.overrideWithValue(s.FakeSuggestionsApi(team: leaderTeam)),
    shellPhaseProvider.overrideWithValue(const ShellPhase(isTeamFormation: true, isMentoringWindow: false)),
  ];

  // ---------- Nhóm của tôi (Cường) ----------
  final myTeam = MyTeam(
    id: 't1', name: longTeam, status: 'ACTIVE', courseCode: 'EXE101',
    members: [Member(id: 's1', studentCode: 'SE184118', fullName: longName, isLeader: true, specialization: s.spec)],
    domains: const [domain], isLeader: true, memberCount: 3, specializationCount: 2, projectName: 'EduSpark – nền tảng học nhóm theo lịch rảnh',
    ideaDescription: 'Ghép bạn học theo môn và lịch rảnh, gợi ý phòng học trống.', relation: 'LEADER',
  );
  final team = [
    myTeamApiProvider.overrideWithValue(_FakeMyTeam(myTeam)),
    shellPhaseProvider.overrideWithValue(const ShellPhase(isTeamFormation: true, isMentoringWindow: false)),
  ];

  // ---------- Đổi nhóm (Thành) ----------
  final changes = [
    myCourseProvider.overrideWith((ref) async => 'EXE201'),
    myTeamProvider.overrideWith((ref) async => TeamDetail(
          card: const TeamCard(id: 't1', name: longTeam, status: 'ACTIVE', memberCount: 4),
          members: [for (var i = 1; i <= 4; i++) s.student('$i', '$longName $i')],
          isLeader: true,
        )),
    openChangeRoundProvider.overrideWith((ref) async => true),
    changeRequestsApiProvider.overrideWithValue(_FakeChangeRequests()),
  ];

  // ---------- Lịch họp (Nam) ----------
  final meetings = [
    w.authOverride(),
    meetingsApiProvider.overrideWithValue(_FakeMeetings()),
    currentWorkspaceProvider.overrideWith((ref, id) async => const WorkspaceOverview(id: 'w1', teamName: longTeam, courseCode: 'EXE201', myRole: WorkspaceRole.leader)),
  ];

  // ---------- Cài đặt, hồ sơ giảng viên (Nam) ----------
  final settings = [
    authControllerProvider.overrideWith(_FakeLecturerAuth.new),
    settingsApiProvider.overrideWithValue(_FakeSettings()),
    lecturerProfileApiProvider.overrideWithValue(_FakeLecturerProfile()),
  ];

  accessibilityTests({
    'A03 đăng nhập': (screen: const LoginScreen(), overrides: [], mustShow: 'EXE Team Matching'),
    'A04 chưa có trong danh sách': (screen: const NotInListScreen(email: 'nguyenhoangbaoanhminh.se184118@example.com'), overrides: [], mustShow: 'example.com'),
    'A02 giới thiệu': (screen: OnboardingScreen(onFinished: () {}, onboardingStore: _FakeOnboarding()), overrides: [], mustShow: 'Bỏ qua'),
    'A05 hồ sơ của tôi': (screen: const MyProfileScreen(), overrides: profile, mustShow: longName),
    'S03 sửa hồ sơ': (screen: const EditProfileScreen(), overrides: profile, mustShow: 'Chỉnh sửa hồ sơ'),
    'Hoàn thiện hồ sơ': (screen: const CompleteProfileScreen(profile: StudentProfile(id: 's1', studentCode: 'SE184118', fullName: longName, specialization: 'Kỹ thuật phần mềm', profileCompleted: false)), overrides: profile, mustShow: 'hồ sơ'),
    'S04 hồ sơ sinh viên': (screen: const StudentProfileScreen(studentId: 's2'), overrides: profile, mustShow: 'Trần Thị Minh Châu Ngọc'),
    'S13 tạo nhóm': (screen: const TeamFormScreen(), overrides: team, mustShow: 'Tạo nhóm'),
    'S14 sửa nhóm': (screen: const EditTeamScreen(), overrides: team, mustShow: 'Sửa thông tin'),
    'S20 yêu cầu tham gia': (screen: const RequestsScreen(), overrides: team, mustShow: 'Phạm Thu Hà Linh'),
    'S26 danh sách yêu cầu thay đổi': (screen: const ChangeRequestListScreen(), overrides: changes, mustShow: 'Yêu cầu thay đổi'),
    'S27 tạo yêu cầu thay đổi': (screen: const CreateChangeRequestScreen(), overrides: changes, mustShow: 'Phạm Thu Hà Linh'),
    'S28 chi tiết yêu cầu': (screen: const ChangeRequestDetailScreen(requestId: 'request-1'), overrides: changes, mustShow: 'Chi tiết yêu cầu'),
    'C07 lịch họp': (screen: const MeetingsScreen(basePath: '/sv/du-an'), overrides: meetings, mustShow: 'Chốt phạm vi MVP'),
    'C08 chi tiết họp': (screen: MeetingDetailScreen(meetingId: 'm1', pickTime: (_) async => DateTime(2030, 12, 1, 9)), overrides: meetings, mustShow: 'Chốt phạm vi MVP'),
    'A07 cài đặt': (screen: const SettingsScreen(), overrides: settings, mustShow: 'Cài đặt'),
    'L06 hồ sơ giảng viên': (screen: const LecturerProfileScreen(), overrides: settings, mustShow: 'PGS. TS. Nguyễn Hoàng Bảo Anh'),
    'L07 chi tiết đề tài': (
      screen: const TopicDetailScreen(teamId: 'team-1', advisorKind: 'LECTURER', wasMyEXE101Team: true),
      overrides: [
        advisorTeamDetailProvider('team-1').overrideWith((ref) async => {
              'id': 'team-1', 'name': longTeam, 'projectName': 'EduSpark – nền tảng học nhóm theo lịch rảnh của sinh viên',
              'courseCode': 'EXE201', 'isProjectContinued': true, 'memberCount': 5,
              'members': [
                {'id': 's1', 'studentCode': 'SE184118', 'fullName': longName, 'isLeader': true, 'specialization': {'id': 1, 'code': 'SE', 'name': 'Kỹ thuật phần mềm'}},
              ],
              'domains': [{'id': 4, 'code': 'D04', 'name': 'Software / SaaS / Platform'}],
            }),
      ],
      mustShow: longTeam,
    ),
  });

  // A01 có vòng chờ quay mãi nên không đợi pumpAndSettle như helper: dựng vài khung hình rồi kiểm.
  for (final size in const [Size(360, 800), Size(412, 915)]) {
    testWidgets('A01 màn chờ ở ${size.width.toInt()} dp, chữ 130%', (tester) async {
      tester.view.physicalSize = size * 3;
      tester.view.devicePixelRatio = 3;
      addTearDown(tester.view.reset);
      FlutterSecureStorage.setMockInitialValues({});
      final semantics = tester.ensureSemantics();
      try {
        await tester.pumpWidget(ProviderScope(
          child: MaterialApp(
            theme: AppTheme.light(),
            builder: (context, child) => MediaQuery(data: MediaQuery.of(context).copyWith(textScaler: const TextScaler.linear(1.3)), child: child!),
            home: SplashScreen(onboardingStore: _FakeOnboarding(), onResolved: (_) {}),
          ),
        ));
        await tester.pump(const Duration(milliseconds: 300));
        expect(find.text('EXE Team Matching'), findsOneWidget);
        expect(tester.takeException(), isNull);
        await expectLater(tester, meetsGuideline(textContrastGuideline));
      } finally {
        semantics.dispose();
      }
    });
  }
}

class _FakeOnboarding extends OnboardingStore {
  @override
  Future<bool> hasSeen() async => false;

  @override
  Future<void> markSeen() async {}
}

class _FakeProfile extends ProfileApi {
  _FakeProfile() : super(Dio());

  @override
  Future<StudentProfile> me() async => const StudentProfile(
        id: 's1', studentCode: 'SE184118', fullName: 'Nguyễn Hoàng Bảo Anh Minh', specialization: 'Truyền thông đa phương tiện', classCode: 'EXE101SE01',
        skills: {'TECHNOLOGY': 4, 'UI_UX_DESIGN': 2, 'MANAGEMENT': 3, 'MARKETING': 1, 'COMMUNICATION_PLANNING': 5},
        description: 'Thích làm backend, đã làm hai dự án React và một ứng dụng Flutter cho câu lạc bộ.',
      );

  @override
  Future<StudentProfile> student(String id) async => const StudentProfile(
        id: 's2', studentCode: 'SE184119', fullName: 'Trần Thị Minh Châu Ngọc', specialization: 'Thiết kế đồ họa', classCode: 'EXE101GD01',
        skills: {'TECHNOLOGY': 2, 'UI_UX_DESIGN': 5, 'MANAGEMENT': 3, 'MARKETING': 4, 'COMMUNICATION_PLANNING': 3},
        description: 'Thích thiết kế giao diện và làm video giới thiệu sản phẩm.',
      );

  @override
  Future<Set<String>> invitedStudentIds(String teamId) async => const {};
}

class _FakeMyTeam extends MyTeamApi {
  _FakeMyTeam(this.team) : super(Dio());

  final MyTeam team;

  @override
  Future<MyTeam?> myTeam() async => team;

  @override
  Future<List<CatalogRef>> domains() async => const [
        CatalogRef(id: 4, code: 'D04', name: 'Software / SaaS / Platform'),
        CatalogRef(id: 5, code: 'D05', name: 'Giáo dục và đào tạo trực tuyến'),
      ];

  @override
  Future<List<JoinRequestItem>> joinRequests(String teamId) async => [
        JoinRequestItem(
          id: 'r1', student: const Member(id: '04', studentCode: 'SE180004', fullName: 'Phạm Thu Hà Linh', isLeader: false, specialization: s.spec), hasNoIdea: false, status: 'PENDING',
          message: 'Mình làm Digital Marketing, muốn vào nhóm để phụ trách truyền thông.',
          ideas: const [Idea('Kênh TikTok học nhóm', 'Video 60 giây giới thiệu tính năng ghép bạn học theo lịch rảnh')],
        ),
      ];

  @override
  Future<List<SentInvitation>> sentInvitations(String teamId) async => [
        SentInvitation(id: 'i1', invitee: const Member(id: '05', studentCode: 'SE180005', fullName: 'Lê Văn Đức Thịnh Phát', isLeader: false, specialization: s.spec), status: 'PENDING', expiresAt: DateTime.now().add(const Duration(hours: 30))),
      ];
}

class _FakeChangeRequests extends ChangeRequestsApi {
  _FakeChangeRequests() : super(Dio());

  static final _request = ChangeRequest(
    id: 'request-1', roundName: 'Đợt đổi nhóm EXE201 học kỳ Spring', reason: 'Thành viên chuyển lịch học sang buổi tối nên không tham gia họp được',
    changedMemberCount: 1, changedPercent: 25, status: 'REJECTED', createdAt: DateTime.utc(2026, 10, 20),
    reviewedByName: 'Bộ môn EXE', reviewedAt: DateTime.utc(2026, 10, 21), reviewNote: 'Cần bổ sung minh chứng lịch học mới.',
  );

  @override
  Future<PendingChanges> pending(String teamId) async => PendingChanges(
        baselineMembers: [for (var i = 1; i <= 4; i++) s.student('$i', 'Nguyễn Hoàng Bảo Anh Minh $i')],
        pendingItems: [
          ChangeItem(id: 'add-1', action: 'ADD', student: s.student('05', 'Phạm Thu Hà Linh')),
          ChangeItem(id: 'remove-1', action: 'REMOVE', student: s.student('4', 'Nguyễn Hoàng Bảo Anh Minh 4')),
        ],
        wouldHaveMemberCount: 4,
        wouldHaveSpecializationCount: 2,
      );

  @override
  Future<List<ChangeRequest>> list(String teamId) async => [_request];

  @override
  Future<ChangeRequestDetail> detail(String id) async => ChangeRequestDetail(
        request: _request,
        items: [ChangeItem(id: 'add-1', action: 'ADD', student: s.student('05', 'Phạm Thu Hà Linh'))],
      );
}

class _FakeMeetings extends MeetingsApi {
  _FakeMeetings() : super(Dio());

  static const _vy = PersonRef(personType: 'STUDENT', personId: 's1', displayName: 'Nguyễn Hoàng Bảo Anh Minh', role: WorkspaceRole.leader);
  static final _proposed = MeetingDetail(
    card: const MeetingCard(id: 'm1', title: 'Chốt phạm vi MVP cho checkpoint 1', status: MeetingStatus.proposed, organizer: _vy, slotCount: 2, myAnswerPending: true,
        location: 'Phòng 304 hoặc Google Meet'),
    agenda: 'Chọn 3 tính năng cho checkpoint 1 và phân công người làm.',
    slots: [
      MeetingSlot(id: 'x1', startsAt: DateTime.utc(2030, 11, 10, 2), durationMinutes: 60, yesCount: 1, inviteeCount: 5),
      MeetingSlot(id: 'x2', startsAt: DateTime.utc(2030, 11, 11, 2), durationMinutes: 60, yesCount: 3, inviteeCount: 5, myAnswer: 'YES',
          yesPeople: const ['Nguyễn Hoàng Bảo Anh Minh', 'Trần Thị Minh Châu Ngọc', 'Lê Văn Đức Thịnh Phát']),
    ],
  );

  @override
  Future<MeetingList> list(String workspaceId) async => MeetingList(items: [_proposed.card], mentorSessionsDone: 1, expectedMentorSessions: 3);

  @override
  Future<MeetingDetail> detail(String id) async => _proposed;
}

class _FakeLecturerAuth extends AuthController {
  @override
  Future<UserInfo?> build() async => const UserInfo(
        id: 'u', email: 'anhnhb.giangvien@example.com', fullName: 'PGS. TS. Nguyễn Hoàng Bảo Anh', roles: {Role.lecturer}, lecturerId: 'l1');
}

class _FakeSettings extends SettingsApi {
  _FakeSettings() : super(Dio());

  @override
  Future<bool> emailEnabled() async => true;
}

class _FakeLecturerProfile extends LecturerProfileApi {
  _FakeLecturerProfile() : super(Dio());

  @override
  Future<LecturerProfile> me() async => const LecturerProfile(
        fullName: 'PGS. TS. Nguyễn Hoàng Bảo Anh', email: 'anhnhb.giangvien@example.com', maxTeamsExe101: 3, maxTeamsExe201: 3,
        domains: [CatalogRef(id: 1, code: 'D01', name: 'AI & Data Innovation'), CatalogRef(id: 5, code: 'D05', name: 'Giáo dục và đào tạo trực tuyến')],
      );

  @override
  Future<List<CatalogRef>> domains() async => const [
        CatalogRef(id: 1, code: 'D01', name: 'AI & Data Innovation'),
        CatalogRef(id: 5, code: 'D05', name: 'Giáo dục và đào tạo trực tuyến'),
      ];
}
