import 'package:exe_team_matching/features/round_closed/auto_assigned_screen.dart';
import 'package:exe_team_matching/features/suggestions/invite_students_screen.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart';
import 'package:exe_team_matching/features/tasks/task_detail_screen.dart';
import 'package:exe_team_matching/features/workspace/workspace_models.dart';

import 'support/a11y.dart';
import 'support/suggestions_fakes.dart' as s;
import 'support/workspace_fakes.dart' as w;

/// MB-36 – checklist flutter-handoff §5 cho chi tiết việc / bài nộp C05 (sinh viên và giảng viên), mời sinh viên S19,
/// được ghép tự động S31.
void main() {
  const lecturer = PersonRef(personType: 'LECTURER', personId: 'l1', displayName: 'PGS. TS. Nguyễn Hoàng Bảo Anh', role: WorkspaceRole.instructor);
  final deliverable = TaskDetail(
    card: TaskCard(
      id: 'd', workspaceId: 'w1', isDeliverable: true, title: 'Báo cáo tuần 3 – khảo sát nhu cầu học nhóm của sinh viên năm hai',
      status: TaskStatus.doing, position: 0, assignees: const [w.me], dueAt: DateTime.now().add(const Duration(days: 1)),
    ),
    createdBy: lecturer,
    description: 'Nộp bản PDF gồm kết quả khảo sát 30 sinh viên, phân tích đối thủ và đề xuất tính năng cho checkpoint 2.',
    submissions: [
      Submission(id: 'sb2', fileId: 'f1', fileName: 'bao-cao-khao-sat-nhu-cau-hoc-nhom.pdf', version: 2, submittedBy: w.me,
          submittedAt: DateTime.now(), isLate: false, reviewStatus: SubmissionStatus.pending),
      Submission(id: 'sb1', fileId: 'f1', fileName: 'bao-cao-khao-sat-nhu-cau-hoc-nhom.pdf', version: 1, submittedBy: w.me,
          submittedAt: DateTime.now().subtract(const Duration(days: 2)), isLate: true, reviewStatus: SubmissionStatus.needsRevision,
          reviewComment: 'Thêm số liệu khảo sát và nguồn trích dẫn.'),
    ],
    comments: [
      Comment(id: 'c1', author: lecturer, body: 'Nhóm nhớ ghi rõ cách chọn mẫu khảo sát nhé.', createdAt: DateTime.now()),
    ],
  );
  A11yCase c05(WorkspaceRole role) => (
        screen: const TaskDetailScreen(taskId: 'd', workspaceId: 'w1'),
        overrides: [w.collabApiOverride(w.FakeCollabApi(workspaces: [w.workspace(role: role)], detail: deliverable)), w.authOverride()],
        mustShow: 'Báo cáo tuần 3',
      );

  final leaderTeam = TeamDetail(
    card: const TeamCard(id: 't1', name: 'Nhóm Bình Minh Sáng Tạo Khởi Nghiệp', status: 'ACTIVE', memberCount: 4, projectName: 'EduSpark – nền tảng học nhóm'),
    members: [s.student('01', 'Nguyễn Hoàng Bảo Anh Minh'), s.student('02', 'Trần Thị Minh Châu Ngọc')],
    isLeader: true,
  );
  final students = [s.student('03', 'Lê Văn Đức Thịnh Phát'), s.student('04', 'Phạm Thu Hà Linh')];

  accessibilityTests({
    'C05 sinh viên': c05(WorkspaceRole.leader),
    'C05 giảng viên': c05(WorkspaceRole.instructor),
    'S19': (
      screen: const InviteStudentsScreen(),
      overrides: [suggestionsApiProvider.overrideWithValue(s.FakeSuggestionsApi(team: leaderTeam, students: students))],
      mustShow: 'Mời',
    ),
    'S31': (
      screen: const AutoAssignedScreen(),
      overrides: [suggestionsApiProvider.overrideWithValue(s.FakeSuggestionsApi(team: leaderTeam))],
      mustShow: 'Nhóm Bình Minh Sáng Tạo',
    ),
  });
}

