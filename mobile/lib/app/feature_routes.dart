import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../features/change_requests/change_request_screens.dart';
import '../features/change_requests/routes.dart';
import '../features/lecturers/routes.dart';
import '../features/lecturer/lecturer_home_screen.dart';
import '../features/lecturer/topic_interests_screen.dart';

import '../features/discover/discover_screen.dart';
import '../features/discover/seeking_posts_screen.dart';
import '../features/discover/team_detail_screen.dart';
import '../features/files/files_screen.dart';
import '../features/home/home_screen.dart';
import '../features/lecturer_profile/lecturer_profile_screen.dart';
import '../features/lecturers/team_lecturer_screen.dart';
import '../features/meeting_schedule/meeting_schedule_screen.dart';
import '../features/meetings/meeting_detail_screen.dart';
import '../features/meetings/meetings_screen.dart';
import '../features/messages/channel_screen.dart';
import '../features/messages/conversations_screen.dart';
import '../features/notifications/notifications_screen.dart';
import '../features/invitations/invitations_screen.dart';
import '../features/my_team/members_screen.dart';
import '../features/my_team/my_team_screen.dart';
import '../features/my_team/recruitment_post_screen.dart';
import '../features/my_team/requests_screen.dart';
import '../features/my_team/team_history_screen.dart';
import '../features/my_team/team_form_screen.dart';
import '../features/profile/my_profile_screens.dart';
import '../features/profile/student_profile_screen.dart';
import '../features/round_closed/auto_assigned_screen.dart';
import '../features/settings/settings_screen.dart';
import '../features/round_closed/round_closed_screen.dart';
import '../features/suggestions/invite_students_screen.dart';
import '../features/suggestions/suggested_members_screen.dart';
import '../features/suggestions/suggested_teams_screen.dart';
import '../features/tasks/task_board_screen.dart';
import '../features/tasks/task_detail_screen.dart';
import '../features/workspace/project_overview_screen.dart';
import '../shared/widgets/states.dart';
import 'navigation.dart';
import 'role_shell.dart';

/// Màn hình thật của các tab (thay ScreenPlaceholder). Mỗi mảng thêm tab của mình vào đây.
final Map<String, Widget Function(GoRouterState state)> tabScreens = {
  '/sv/du-an': (_) => const ProjectOverviewScreen(basePath: '/sv/du-an'),
  '/sv/nhom': (_) => const MyTeamScreen(),
  // A05 chặn trước S01, S02 khi sinh viên chưa đánh giá đủ 5 kỹ năng – MB-08, MB-12 (Nam).
  '/sv': (_) => const ProfileGate(child: HomeScreen()),
  '/sv/ho-so': (_) => const ProfileGate(child: MyProfileScreen()),
  '/sv/kham-pha': (_) => const DiscoverScreen(),
  '/sv/thong-bao': (_) => const NotificationsScreen(roleRoot: '/sv'),
  '/gv': (_) => const LecturerHomeScreen(),
  '/gv/de-tai': (_) => const TopicInterestsScreen(advisorKind: 'LECTURER'),
  '/mentor/de-tai': (_) => const TopicInterestsScreen(advisorKind: 'MENTOR'),
  '/gv/thong-bao': (_) => const NotificationsScreen(roleRoot: '/gv'),
  '/mentor/thong-bao': (_) => const NotificationsScreen(roleRoot: '/mentor'),
  // C02 của cố vấn – MB-39 (Cường).
  '/gv/tin-nhan': (_) => const AdvisorConversationsScreen(basePath: '/gv/tin-nhan'),
  '/mentor/tin-nhan': (_) => const AdvisorConversationsScreen(basePath: '/mentor/tin-nhan'),
};

/// Màn cấp 2 của từng khung (mở bằng `context.push`, có nút Back). Mỗi mảng thêm route của mình vào đây.
List<RouteBase> featureRoutes(Shell shell) => switch (shell) {
      Shell.student => [
          GoRoute(path: teamLecturerPath, builder: (_, _) => const TeamLecturerScreen()),
          // C09 – MB-46 (Cường).
          GoRoute(path: '/sv/nhom/lich-hop', builder: (_, _) => const TeamScheduleScreen()),
          ..._workspaceRoutes('/sv/du-an/', workspaceIdOf: (_) => null, basePathOf: (_) => '/sv/du-an'),
          ..._studentTeamRoutes,
          ...changeRequestRoutes,
          ...lecturerRoutes,
          GoRoute(path: '/sv/doi-nhom', redirect: (_, _) => changeRequestListPath),
          GoRoute(path: '/sv/nhom/doi-nhom', redirect: (_, _) => changeRequestListPath),
          GoRoute(path: '/sv/change-requests', redirect: (_, _) => changeRequestListPath),
          GoRoute(path: '/sv/change-requests/:id', redirect: (_, s) => '$changeRequestListPath/${s.pathParameters['id']}'),
          GoRoute(path: '/app://change-requests', redirect: (_, _) => changeRequestListPath),
        ],
      Shell.lecturer => [
          GoRoute(
            path: '/gv/de-tai/:teamId',
            builder: (_, state) => TopicDetailScreen(
              teamId: state.pathParameters['teamId']!,
              advisorKind: 'LECTURER',
            ),
          ),
          _advisorWorkspace('/gv/du-an'),
          _channelRoute('/gv/tin-nhan'),
          // L06 – MB-35, A07 – MB-10 (Nam). L01 (Thành) chưa có nút mở L06.
          GoRoute(path: lecturerProfilePath, builder: (_, _) => const LecturerProfileScreen()),
          GoRoute(path: lecturerSettingsPath, builder: (_, _) => const SettingsScreen()),
          // C09 – MB-46 (Cường); lối vào từ L01, C07 do màn đó thêm.
          GoRoute(path: '/gv/lich-hop', builder: (_, _) => const LecturerScheduleScreen()),
        ],
      Shell.mentor => [
          GoRoute(
            path: '/mentor/de-tai/:teamId',
            builder: (_, state) => TopicDetailScreen(
              teamId: state.pathParameters['teamId']!,
              advisorKind: 'MENTOR',
            ),
          ),
          _advisorWorkspace('/mentor/du-an'),
          _channelRoute('/mentor/tin-nhan'),
          GoRoute(path: '/mentor/cai-dat', builder: (_, _) => const SettingsScreen()),
        ],
    };

/// S07 (trong Khám phá, chỉ mở trong đợt lập nhóm – quy tắc 18), S18, S19 (Nhóm của tôi), S30, S31 (sau hạn chót) – MB-15, MB-23, MB-31.
final _studentTeamRoutes = [
  GoRoute(path: '/sv/kham-pha/goi-y', builder: (_, _) => const TeamFormationOnly(child: SuggestedTeamsScreen())),
  GoRoute(path: '/sv/nhom/goi-y', builder: (_, _) => const SuggestedMembersScreen()),
  GoRoute(path: '/sv/nhom/moi', builder: (_, _) => const InviteStudentsScreen()),
  GoRoute(path: '/sv/nhom/da-khoa', builder: (_, _) => const RoundClosedScreen()),
  GoRoute(path: '/sv/nhom/ghep-tu-dong', builder: (_, _) => const AutoAssignedScreen()),
  // S13, S16, S17, S20 – MB-19, MB-21, MB-22, MB-24 (Cường).
  GoRoute(path: '/sv/nhom/tao', builder: (_, _) => const TeamFormationOnly(child: TeamFormScreen())),
  GoRoute(path: '/sv/nhom/sua', builder: (_, _) => const EditTeamScreen()),
  GoRoute(path: '/sv/nhom/thanh-vien', builder: (_, _) => const MembersScreen()),
  GoRoute(path: '/sv/nhom/yeu-cau', builder: (_, _) => const RequestsScreen()),
  // S08 (chỉ trong đợt lập nhóm – quy tắc 18), S12 – MB-16, MB-18 (Cường).
  GoRoute(
    path: '/sv/kham-pha/nhom/:teamId',
    builder: (_, s) => TeamFormationOnly(child: TeamDetailScreen(teamId: s.pathParameters['teamId']!)),
  ),
  GoRoute(path: '/sv/loi-moi', builder: (_, _) => const InvitationsScreen()),
  // S11, S21 – MB-17, MB-25 (Cường); chỉ trong đợt lập nhóm (quy tắc 18).
  GoRoute(path: '/sv/kham-pha/tim-nhom', builder: (_, _) => const TeamFormationOnly(child: SeekingPostFormScreen())),
  GoRoute(path: '/sv/nhom/bai-tuyen', builder: (_, _) => const TeamFormationOnly(child: RecruitmentPostScreen())),
  // S22 – MB-26 (Cường).
  GoRoute(path: '/sv/nhom/lich-su', builder: (_, _) => const TeamHistoryScreen()),
  // S03 – MB-12, S04 – MB-13 (Nam).
  GoRoute(path: editProfilePath, builder: (_, _) => const EditProfileScreen()),
  // A07 – MB-10 (Nam).
  GoRoute(path: studentSettingsPath, builder: (_, _) => const SettingsScreen()),
  GoRoute(path: '/sv/sinh-vien/:id', builder: (_, s) => StudentProfileScreen(studentId: s.pathParameters['id']!)),
];

/// Dự án của giảng viên / mentor theo id workspace (mở từ L04, L05, M01).
GoRoute _advisorWorkspace(String base) => GoRoute(
      path: '$base/:workspaceId',
      builder: (_, state) => ProjectOverviewScreen(basePath: '$base/${state.pathParameters['workspaceId']}', workspaceId: state.pathParameters['workspaceId']),
      routes: _workspaceRoutes(
        '',
        workspaceIdOf: (s) => s.pathParameters['workspaceId'],
        basePathOf: (s) => '$base/${s.pathParameters['workspaceId']}',
      ),
    );

/// C04, C05, C06 (MB-40, MB-41), Tin nhắn C02, C03 (MB-39), Lịch họp C07, C08 (MB-42).
/// [prefix] là phần đầu đường dẫn (rỗng khi lồng trong route dự án của cố vấn).
List<GoRoute> _workspaceRoutes(
  String prefix, {
  required String? Function(GoRouterState) workspaceIdOf,
  required String Function(GoRouterState) basePathOf,
}) {
  return [
    GoRoute(
      path: '${prefix}viec',
      builder: (_, s) => TaskBoardScreen(
        basePath: basePathOf(s),
        workspaceId: workspaceIdOf(s),
        mine: s.uri.queryParameters['mine'] == '1',
        deliverablesOnly: s.uri.queryParameters['kind'] == 'DELIVERABLE',
        openCreate: s.uri.queryParameters['tao'] == '1',
      ),
    ),
    GoRoute(path: '${prefix}viec/:taskId', builder: (_, s) => TaskDetailScreen(taskId: s.pathParameters['taskId']!, workspaceId: workspaceIdOf(s))),
    GoRoute(path: '${prefix}file', builder: (_, s) => FilesScreen(workspaceId: workspaceIdOf(s))),
    GoRoute(path: '${prefix}tin-nhan', builder: (_, s) => ConversationsScreen(basePath: basePathOf(s), workspaceId: workspaceIdOf(s))),
    GoRoute(
      path: '${prefix}tin-nhan/:channelId',
      builder: (_, s) => switch (s.uri.queryParameters['ws'] ?? workspaceIdOf(s)) {
        final String ws => ChannelScreen(workspaceId: ws, channelId: s.pathParameters['channelId']!),
        null => ConversationsScreen(basePath: basePathOf(s)),
      },
    ),
    GoRoute(path: '${prefix}lich-hop', builder: (_, s) => MeetingsScreen(basePath: basePathOf(s), workspaceId: workspaceIdOf(s))),
    GoRoute(path: '${prefix}lich-hop/:meetingId', builder: (_, s) => MeetingDetailScreen(meetingId: s.pathParameters['meetingId']!)),
  ];
}

/// C03 mở từ tab Tin nhắn của cố vấn: `{base}/{channelId}?ws={workspaceId}` – MB-39 (Cường).
GoRoute _channelRoute(String base) => GoRoute(
      path: '$base/:channelId',
      builder: (_, s) => switch (s.uri.queryParameters['ws']) {
        final String ws => ChannelScreen(workspaceId: ws, channelId: s.pathParameters['channelId']!),
        null => Scaffold(appBar: AppBar(), body: const EmptyState(icon: Icons.forum_outlined, title: 'Không tìm thấy kênh')),
      },
    );
