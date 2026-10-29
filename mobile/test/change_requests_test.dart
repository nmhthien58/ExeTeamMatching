import 'package:flutter_test/flutter_test.dart';
import 'package:exe_team_matching/features/change_requests/change_requests_api.dart';

void main() {
  group('change request contract models', () {
    test(
      'parses request decision and timestamps from the Team API contract',
      () {
        final request = ChangeRequest.fromJson({
          'id': 'request-1',
          'roundName': 'Đợt đổi nhóm',
          'reason': 'Thành viên chuyển lịch học',
          'changedMemberCount': 2,
          'changedPercent': 40.0,
          'status': 'REJECTED',
          'createdAt': '2026-10-20T10:00:00Z',
          'reviewedByName': 'Bộ môn EXE',
          'reviewedAt': '2026-10-21T10:00:00Z',
          'reviewNote': 'Vui lòng bổ sung thông tin',
        });

        expect(request.status, 'REJECTED');
        expect(request.changedMemberCount, 2);
        expect(request.changedPercent, 40);
        expect(request.reviewedByName, 'Bộ môn EXE');
        expect(request.reviewedAt, DateTime.parse('2026-10-21T10:00:00Z'));
      },
    );

    test('parses pending membership and project changes', () {
      final pending = PendingChanges.fromJson({
        'baselineMembers': [
          {
            'id': 'student-1',
            'studentCode': 'SE180001',
            'fullName': 'Nguyễn An',
          },
        ],
        'pendingItems': [
          {
            'id': 'item-1',
            'action': 'ADD',
            'student': {
              'id': 'student-2',
              'studentCode': 'SE180002',
              'fullName': 'Trần Bình',
            },
          },
          {
            'id': 'item-2',
            'action': 'PROJECT_CHANGE',
            'student': null,
            'newProjectName': 'Đề tài mới',
          },
        ],
        'currentProject': {'name': 'Đề tài hiện tại'},
        'wouldHaveMemberCount': 4,
        'wouldHaveSpecializationCount': 1,
      });

      expect(pending.baselineMembers.single.fullName, 'Nguyễn An');
      expect(pending.pendingItems.map((item) => item.title), [
        'Thêm Trần Bình',
        'Đổi đề tài: Đề tài mới',
      ]);
      expect(pending.currentProjectName, 'Đề tài hiện tại');
      expect(pending.wouldHaveMemberCount, 4);
      expect(pending.wouldHaveSpecializationCount, 1);
    });
  });
}
