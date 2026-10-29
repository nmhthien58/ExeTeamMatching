import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/lecturers/lecturer_detail_screen.dart';
import 'package:exe_team_matching/features/lecturers/lecturer_preferences_screen.dart';
import 'package:exe_team_matching/features/lecturers/lecturers_api.dart';
import 'package:exe_team_matching/features/lecturers/lecturers_models.dart';
import 'package:exe_team_matching/features/my_team/my_team_api.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart'
    show CatalogRef;

class FakeLecturersApi extends LecturersApi {
  FakeLecturersApi({
    this.preferences = const [],
    this.interests = const [],
    this.lecturers = const [],
  }) : super(Dio());

  List<LecturerPreferenceItem> preferences;
  List<TopicInterestItem> interests;
  List<LecturerSummary> lecturers;

  List<String>? lastUpdatedIds;
  int updateCallCount = 0;

  @override
  Future<List<LecturerPreferenceItem>> getPreferences(String teamId) async =>
      preferences;

  @override
  Future<List<LecturerPreferenceItem>> updatePreferences(
    String teamId,
    List<String> lecturerIds,
  ) async {
    updateCallCount++;
    lastUpdatedIds = lecturerIds;
    // Update local preferences list
    final newPrefs = <LecturerPreferenceItem>[];
    for (int i = 0; i < lecturerIds.length; i++) {
      final id = lecturerIds[i];
      final l = lecturers.firstWhere((x) => x.id == id, orElse: () {
        return preferences.firstWhere((p) => p.lecturer.id == id).lecturer;
      });
      newPrefs.add(
        LecturerPreferenceItem(
          priority: i + 1,
          lecturer: l,
          isActive: true,
          currentAssignedTeamCount: 1,
          maxTeams: 5,
          isFull: l.isFull,
        ),
      );
    }
    preferences = newPrefs;
    return preferences;
  }

  @override
  Future<List<TopicInterestItem>> getTopicInterests(String teamId) async =>
      interests;

  @override
  Future<List<LecturerSummary>> listLecturers({
    String? search,
    int page = 1,
    int pageSize = 50,
  }) async {
    if (search != null && search.isNotEmpty) {
      return lecturers
          .where((l) =>
              l.fullName.toLowerCase().contains(search.toLowerCase()) ||
              l.email.toLowerCase().contains(search.toLowerCase()))
          .toList();
    }
    return lecturers;
  }

  @override
  Future<LecturerSummary> getLecturer(String id) async {
    return lecturers.firstWhere((l) => l.id == id);
  }
}

final sampleLecturer1 = LecturerSummary(
  id: 'lec-1',
  fullName: 'TS. Nguyễn Văn A',
  email: 'anv@fpt.edu.vn',
  domains: const [CatalogRef(id: 1, code: 'D01', name: 'Trí tuệ nhân tạo')],
  isActive: true,
  isFull: false,
);

final sampleLecturer2 = LecturerSummary(
  id: 'lec-2',
  fullName: 'ThS. Trần Thị B',
  email: 'btt@fpt.edu.vn',
  domains: const [CatalogRef(id: 2, code: 'D02', name: 'IoT')],
  isActive: true,
  isFull: false,
);

final sampleLecturerFull = LecturerSummary(
  id: 'lec-3',
  fullName: 'PGS. TS. Lê Văn C',
  email: 'clv@fpt.edu.vn',
  domains: const [CatalogRef(id: 3, code: 'D03', name: 'Fintech')],
  isActive: true,
  isFull: true,
);

final sampleTeamLeader = MyTeam(
  id: 'team-1',
  name: 'Alpha Team',
  status: 'ACTIVE',
  courseCode: 'EXE101',
  members: const [],
  domains: const [],
  isLeader: true,
  memberCount: 5,
  specializationCount: 3,
);

final sampleTeamMember = MyTeam(
  id: 'team-1',
  name: 'Alpha Team',
  status: 'ACTIVE',
  courseCode: 'EXE101',
  members: const [],
  domains: const [],
  isLeader: false,
  memberCount: 5,
  specializationCount: 3,
);

void main() {
  Widget buildApp({
    required FakeLecturersApi api,
    required MyTeam team,
    required Widget child,
  }) {
    return ProviderScope(
      overrides: [
        lecturersApiProvider.overrideWithValue(api),
        myTeamDetailProvider.overrideWith((ref) => Future.value(team)),
      ],
      child: MaterialApp(
        theme: AppTheme.light(),
        home: child,
      ),
    );
  }

  group('S23 - LecturerPreferencesScreen', () {
    testWidgets('renders preference slots, empty slots, and banner',
        (tester) async {
      final fakeApi = FakeLecturersApi(
        preferences: [
          LecturerPreferenceItem(
            priority: 1,
            lecturer: sampleLecturer1,
            isActive: true,
            currentAssignedTeamCount: 1,
            maxTeams: 5,
            isFull: false,
          ),
        ],
        interests: [],
        lecturers: [sampleLecturer1, sampleLecturer2, sampleLecturerFull],
      );

      await tester.pumpWidget(
        buildApp(
          api: fakeApi,
          team: sampleTeamLeader,
          child: const LecturerPreferencesScreen(),
        ),
      );
      await tester.pumpAndSettle();

      // Title & Banner
      expect(find.text('Nguyện vọng giảng viên'), findsOneWidget);
      expect(
        find.text('Đã tự lưu · Trưởng nhóm sửa được tới hạn chốt đợt lập nhóm'),
        findsOneWidget,
      );

      // Slot 1 has lecturer 1
      expect(find.text('TS. Nguyễn Văn A'), findsWidgets);
      expect(find.text('anv@fpt.edu.vn'), findsWidgets);

      // Slots 2 and 3 are empty
      expect(find.text('Chưa chọn nguyện vọng 2'), findsOneWidget);
      expect(find.text('Chưa chọn nguyện vọng 3'), findsOneWidget);
    });

    testWidgets('shows interested lecturers with add button', (tester) async {
      final fakeApi = FakeLecturersApi(
        preferences: [],
        interests: [
          TopicInterestItem(
            id: 'ti-1',
            teamId: 'team-1',
            advisorId: sampleLecturer2.id,
            advisorName: sampleLecturer2.fullName,
            advisorEmail: sampleLecturer2.email,
            advisorRole: 'LECTURER',
            status: 'OPEN',
          ),
        ],
        lecturers: [sampleLecturer1, sampleLecturer2],
      );

      await tester.pumpWidget(
        buildApp(
          api: fakeApi,
          team: sampleTeamLeader,
          child: const LecturerPreferencesScreen(),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Quan tâm nhóm bạn'), findsOneWidget);
      expect(find.text('ThS. Trần Thị B'), findsWidgets);

      // Click add to NV1
      final addBtn = find.widgetWithText(FilledButton, 'Thêm vào NV1');
      expect(addBtn, findsOneWidget);
      await tester.tap(addBtn);
      await tester.pumpAndSettle();

      expect(fakeApi.updateCallCount, 1);
      expect(fakeApi.lastUpdatedIds, [sampleLecturer2.id]);
      expect(find.text('Đã thêm ThS. Trần Thị B vào NV1'), findsOneWidget);
      expect(find.text('Hoàn tác'), findsOneWidget);
    });

    testWidgets('allows leader to remove preference and undo', (tester) async {
      final fakeApi = FakeLecturersApi(
        preferences: [
          LecturerPreferenceItem(
            priority: 1,
            lecturer: sampleLecturer1,
            isActive: true,
            currentAssignedTeamCount: 1,
            maxTeams: 5,
            isFull: false,
          ),
        ],
        interests: [],
        lecturers: [sampleLecturer1],
      );

      await tester.pumpWidget(
        buildApp(
          api: fakeApi,
          team: sampleTeamLeader,
          child: const LecturerPreferencesScreen(),
        ),
      );
      await tester.pumpAndSettle();

      // Click delete button
      final deleteBtn = find.byTooltip('Bỏ khỏi nguyện vọng');
      expect(deleteBtn, findsOneWidget);
      await tester.tap(deleteBtn);
      await tester.pumpAndSettle();

      expect(fakeApi.updateCallCount, 1);
      expect(fakeApi.lastUpdatedIds, isEmpty);

      // Tap Undo
      final undoBtn = find.text('Hoàn tác');
      expect(undoBtn, findsOneWidget);
      await tester.tap(undoBtn);
      await tester.pumpAndSettle();

      expect(fakeApi.updateCallCount, 2);
      expect(fakeApi.lastUpdatedIds, [sampleLecturer1.id]);
    });

    testWidgets('shows Da du chip and does not dim card when lecturer is full',
        (tester) async {
      final fakeApi = FakeLecturersApi(
        preferences: [
          LecturerPreferenceItem(
            priority: 1,
            lecturer: sampleLecturerFull,
            isActive: true,
            currentAssignedTeamCount: 5,
            maxTeams: 5,
            isFull: true,
          ),
        ],
        interests: [],
        lecturers: [sampleLecturerFull],
      );

      await tester.pumpWidget(
        buildApp(
          api: fakeApi,
          team: sampleTeamLeader,
          child: const LecturerPreferencesScreen(),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Đã đủ'), findsWidgets);
    });

    testWidgets('disables edit controls for non-leader', (tester) async {
      final fakeApi = FakeLecturersApi(
        preferences: [
          LecturerPreferenceItem(
            priority: 1,
            lecturer: sampleLecturer1,
            isActive: true,
            currentAssignedTeamCount: 1,
            maxTeams: 5,
            isFull: false,
          ),
        ],
        interests: [],
        lecturers: [sampleLecturer1],
      );

      await tester.pumpWidget(
        buildApp(
          api: fakeApi,
          team: sampleTeamMember,
          child: const LecturerPreferencesScreen(),
        ),
      );
      await tester.pumpAndSettle();

      expect(
        find.text('Chỉ trưởng nhóm mới có quyền chọn và đổi thứ tự nguyện vọng'),
        findsOneWidget,
      );
      expect(find.byTooltip('Bỏ khỏi nguyện vọng'), findsNothing);
    });
  });

  group('S24 - LecturerDetailScreen', () {
    testWidgets('shows details and allows leader to add to preferences',
        (tester) async {
      final fakeApi = FakeLecturersApi(
        preferences: [],
        interests: [],
        lecturers: [sampleLecturer1],
      );

      await tester.pumpWidget(
        buildApp(
          api: fakeApi,
          team: sampleTeamLeader,
          child: LecturerDetailScreen(lecturerId: sampleLecturer1.id),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Chi tiết giảng viên'), findsOneWidget);
      expect(find.text('TS. Nguyễn Văn A'), findsOneWidget);
      expect(find.text('anv@fpt.edu.vn'), findsOneWidget);
      expect(find.text('Trí tuệ nhân tạo'), findsOneWidget);

      final addBtn = find.widgetWithText(FilledButton, 'Thêm vào NV1');
      expect(addBtn, findsOneWidget);

      await tester.tap(addBtn);
      await tester.pumpAndSettle();

      expect(fakeApi.updateCallCount, 1);
      expect(fakeApi.lastUpdatedIds, [sampleLecturer1.id]);
    });

    testWidgets('shows remove button when already in preferences',
        (tester) async {
      final fakeApi = FakeLecturersApi(
        preferences: [
          LecturerPreferenceItem(
            priority: 1,
            lecturer: sampleLecturer1,
            isActive: true,
            currentAssignedTeamCount: 1,
            maxTeams: 5,
            isFull: false,
          ),
        ],
        interests: [],
        lecturers: [sampleLecturer1],
      );

      await tester.pumpWidget(
        buildApp(
          api: fakeApi,
          team: sampleTeamLeader,
          child: LecturerDetailScreen(lecturerId: sampleLecturer1.id),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Nguyện vọng 1'), findsOneWidget);
      final removeBtn =
          find.widgetWithText(OutlinedButton, 'Bỏ khỏi nguyện vọng');
      expect(removeBtn, findsOneWidget);

      await tester.tap(removeBtn);
      await tester.pumpAndSettle();

      expect(fakeApi.updateCallCount, 1);
      expect(fakeApi.lastUpdatedIds, isEmpty);
    });
  });
}
