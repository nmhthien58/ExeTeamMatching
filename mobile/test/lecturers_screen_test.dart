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
    LecturerPreferenceListResponse? preferencesResponse,
    this.interests = const [],
    this.lecturers = const [],
  })  : preferencesResponse = preferencesResponse ??
            const LecturerPreferenceListResponse(
              maxPreferences: 3,
              locked: false,
              preferences: [],
              interestedLecturers: [],
            ),
        super(Dio());

  LecturerPreferenceListResponse preferencesResponse;
  List<TopicInterestItem> interests;
  List<LecturerSummary> lecturers;

  List<String>? lastUpdatedIds;
  int updateCallCount = 0;

  @override
  Future<LecturerPreferenceListResponse> getPreferences(String teamId) async =>
      preferencesResponse;

  @override
  Future<LecturerPreferenceListResponse> updatePreferences(
    String teamId,
    List<String> lecturerIds,
  ) async {
    updateCallCount++;
    lastUpdatedIds = lecturerIds;

    final newPrefs = <LecturerPreferenceItem>[];
    for (int i = 0; i < lecturerIds.length; i++) {
      final id = lecturerIds[i];
      final l = lecturers.firstWhere((x) => x.id == id, orElse: () {
        final existing = preferencesResponse.preferences
            .where((p) => p.lecturerId == id)
            .firstOrNull;
        return LecturerSummary(
          id: id,
          fullName: existing?.fullName ?? 'Giảng viên $id',
          email: 'email@fpt.edu.vn',
          domains: existing?.domains ?? [],
        );
      });
      newPrefs.add(
        LecturerPreferenceItem(
          lecturerId: l.id,
          fullName: l.fullName,
          domains: l.domains,
          rank: i + 1,
          isInterested: preferencesResponse.interestedLecturers
              .any((item) => item.lecturerId == l.id),
        ),
      );
    }

    preferencesResponse = LecturerPreferenceListResponse(
      maxPreferences: preferencesResponse.maxPreferences,
      locked: preferencesResponse.locked,
      lockAt: preferencesResponse.lockAt,
      preferences: newPrefs,
      interestedLecturers: preferencesResponse.interestedLecturers,
      previousInstructor: preferencesResponse.previousInstructor,
    );
    return preferencesResponse;
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

final sampleLecturer3 = LecturerSummary(
  id: 'lec-3',
  fullName: 'TS. Lê Hoàng C',
  email: 'clh@fpt.edu.vn',
  domains: const [CatalogRef(id: 3, code: 'D03', name: 'Fintech')],
  isActive: true,
  isFull: false,
);

final samplePreviousInstructor = LecturerRef(
  lecturerId: 'lec-prev',
  fullName: 'TS. Vũ Đình D',
  domains: const [CatalogRef(id: 1, code: 'D01', name: 'Trí tuệ nhân tạo')],
);

Widget createPreferencesTestWidget({
  required FakeLecturersApi api,
  bool isLeader = true,
  String teamId = 'team-1',
}) {
  return ProviderScope(
    overrides: [
      lecturersApiProvider.overrideWithValue(api),
      myTeamDetailProvider.overrideWith((ref) async {
        return MyTeam(
          id: teamId,
          name: 'Exe Pioneers',
          status: 'FORMING',
          courseCode: 'EXE201',
          members: const [],
          domains: const [],
          isLeader: isLeader,
          memberCount: 4,
          specializationCount: 2,
        );
      }),
    ],
    child: MaterialApp(
      theme: AppTheme.light(),
      home: const LecturerPreferencesScreen(),
    ),
  );
}

Widget createDetailTestWidget({
  required FakeLecturersApi api,
  required String lecturerId,
  bool isLeader = true,
  String teamId = 'team-1',
}) {
  return ProviderScope(
    overrides: [
      lecturersApiProvider.overrideWithValue(api),
      myTeamDetailProvider.overrideWith((ref) async {
        return MyTeam(
          id: teamId,
          name: 'Exe Pioneers',
          status: 'FORMING',
          courseCode: 'EXE201',
          members: const [],
          domains: const [],
          isLeader: isLeader,
          memberCount: 4,
          specializationCount: 2,
        );
      }),
    ],
    child: MaterialApp(
      theme: AppTheme.light(),
      home: LecturerDetailScreen(lecturerId: lecturerId),
    ),
  );
}

void main() {
  group('S23 - LecturerPreferencesScreen', () {
    testWidgets('deserializes real backend JSON structure correctly', (tester) async {
      final realBackendJson = {
        'maxPreferences': 3,
        'locked': false,
        'lockAt': '2026-11-05T12:00:00Z',
        'preferences': [
          {
            'lecturerId': 'lec-1',
            'fullName': 'TS. Nguyễn Văn A',
            'domains': [
              {'id': 1, 'code': 'D01', 'name': 'Trí tuệ nhân tạo'}
            ],
            'rank': 1,
            'isInterested': true,
          }
        ],
        'interestedLecturers': [
          {
            'lecturerId': 'lec-2',
            'fullName': 'ThS. Trần Thị B',
            'domains': [
              {'id': 2, 'code': 'D02', 'name': 'IoT'}
            ],
          }
        ],
        'previousInstructor': {
          'lecturerId': 'lec-prev',
          'fullName': 'TS. Vũ Đình D',
          'domains': [
            {'id': 1, 'code': 'D01', 'name': 'Trí tuệ nhân tạo'}
          ],
        }
      };

      final parsed = LecturerPreferenceListResponse.fromJson(realBackendJson);
      expect(parsed.maxPreferences, 3);
      expect(parsed.locked, false);
      expect(parsed.lockAt, isNotNull);
      expect(parsed.preferences.length, 1);
      expect(parsed.preferences.first.fullName, 'TS. Nguyễn Văn A');
      expect(parsed.preferences.first.isInterested, true);
      expect(parsed.interestedLecturers.length, 1);
      expect(parsed.interestedLecturers.first.fullName, 'ThS. Trần Thị B');
      expect(parsed.previousInstructor?.fullName, 'TS. Vũ Đình D');
    });

    testWidgets('shows preferences, deadline, and previous instructor for leader', (tester) async {
      final api = FakeLecturersApi(
        preferencesResponse: LecturerPreferenceListResponse(
          maxPreferences: 3,
          locked: false,
          lockAt: DateTime(2026, 11, 5, 12, 0),
          preferences: [
            LecturerPreferenceItem(
              lecturerId: sampleLecturer1.id,
              fullName: sampleLecturer1.fullName,
              domains: sampleLecturer1.domains,
              rank: 1,
            ),
          ],
          interestedLecturers: [
            LecturerRef(
              lecturerId: sampleLecturer2.id,
              fullName: sampleLecturer2.fullName,
              domains: sampleLecturer2.domains,
            ),
          ],
          previousInstructor: samplePreviousInstructor,
        ),
        lecturers: [sampleLecturer1, sampleLecturer2, sampleLecturer3],
      );

      await tester.pumpWidget(createPreferencesTestWidget(api: api, isLeader: true));
      await tester.pumpAndSettle();

      // Title & Preference 1
      expect(find.text('Nguyện vọng giảng viên'), findsOneWidget);
      expect(find.text('TS. Nguyễn Văn A'), findsWidgets);

      // Slots 2 and 3 are empty
      expect(find.text('Chưa chọn nguyện vọng 2'), findsOneWidget);
      expect(find.text('Chưa chọn nguyện vọng 3'), findsOneWidget);

      // Previous instructor
      expect(find.text('Đã hướng dẫn ở EXE101'), findsOneWidget);
      expect(find.text('TS. Vũ Đình D'), findsOneWidget);

      // Interested lecturers
      expect(find.text('Quan tâm nhóm bạn'), findsOneWidget);
      expect(find.text('ThS. Trần Thị B'), findsWidgets);

      // Deadline in banner
      expect(find.textContaining('Hạn chốt: 05/11/2026'), findsOneWidget);
    });

    testWidgets('reorders preferences when moving down', (tester) async {
      final api = FakeLecturersApi(
        preferencesResponse: LecturerPreferenceListResponse(
          maxPreferences: 3,
          locked: false,
          preferences: [
            LecturerPreferenceItem(
              lecturerId: sampleLecturer1.id,
              fullName: sampleLecturer1.fullName,
              domains: sampleLecturer1.domains,
              rank: 1,
            ),
            LecturerPreferenceItem(
              lecturerId: sampleLecturer2.id,
              fullName: sampleLecturer2.fullName,
              domains: sampleLecturer2.domains,
              rank: 2,
            ),
          ],
          interestedLecturers: [],
        ),
        lecturers: [sampleLecturer1, sampleLecturer2],
      );

      await tester.pumpWidget(createPreferencesTestWidget(api: api, isLeader: true));
      await tester.pumpAndSettle();

      final downButtons = find.byTooltip('Giảm ưu tiên');
      expect(downButtons, findsWidgets);

      // Tap down on the first item
      await tester.tap(downButtons.first);
      await tester.pumpAndSettle();

      expect(api.lastUpdatedIds, ['lec-2', 'lec-1']);
      expect(find.text('Đã giảm ưu tiên của TS. Nguyễn Văn A'), findsOneWidget);
    });

    testWidgets('removes preference with undo option', (tester) async {
      final api = FakeLecturersApi(
        preferencesResponse: LecturerPreferenceListResponse(
          maxPreferences: 3,
          locked: false,
          preferences: [
            LecturerPreferenceItem(
              lecturerId: sampleLecturer1.id,
              fullName: sampleLecturer1.fullName,
              domains: sampleLecturer1.domains,
              rank: 1,
            ),
          ],
          interestedLecturers: [],
        ),
        lecturers: [sampleLecturer1],
      );

      await tester.pumpWidget(createPreferencesTestWidget(api: api, isLeader: true));
      await tester.pumpAndSettle();

      final removeBtn = find.byTooltip('Xóa nguyện vọng');
      expect(removeBtn, findsOneWidget);

      await tester.tap(removeBtn);
      await tester.pumpAndSettle();

      expect(api.lastUpdatedIds, <String>[]);
      expect(find.text('Đã bỏ TS. Nguyễn Văn A khỏi nguyện vọng'), findsOneWidget);

      // Tap undo
      final undoBtn = find.text('Hoàn tác');
      expect(undoBtn, findsOneWidget);
      await tester.tap(undoBtn);
      await tester.pumpAndSettle();

      expect(api.lastUpdatedIds, ['lec-1']);
    });

    testWidgets('displays locked banner and disables edits when locked', (tester) async {
      final api = FakeLecturersApi(
        preferencesResponse: LecturerPreferenceListResponse(
          maxPreferences: 3,
          locked: true,
          preferences: [
            LecturerPreferenceItem(
              lecturerId: sampleLecturer1.id,
              fullName: sampleLecturer1.fullName,
              domains: sampleLecturer1.domains,
              rank: 1,
            ),
          ],
          interestedLecturers: [],
        ),
        lecturers: [sampleLecturer1],
      );

      await tester.pumpWidget(createPreferencesTestWidget(api: api, isLeader: true));
      await tester.pumpAndSettle();

      // Lock banner
      expect(
        find.text('Nguyện vọng đã khóa · Hệ thống đã chốt hoặc hoàn tất ghép giảng viên.'),
        findsOneWidget,
      );

      // No reorder or remove buttons
      expect(find.byTooltip('Xóa nguyện vọng'), findsNothing);
      expect(find.byTooltip('Tăng ưu tiên'), findsNothing);
      expect(find.byTooltip('Giảm ưu tiên'), findsNothing);
    });

    testWidgets('non-leader cannot edit preferences', (tester) async {
      final api = FakeLecturersApi(
        preferencesResponse: LecturerPreferenceListResponse(
          maxPreferences: 3,
          locked: false,
          preferences: [
            LecturerPreferenceItem(
              lecturerId: sampleLecturer1.id,
              fullName: sampleLecturer1.fullName,
              domains: sampleLecturer1.domains,
              rank: 1,
            ),
          ],
          interestedLecturers: [],
        ),
        lecturers: [sampleLecturer1],
      );

      await tester.pumpWidget(createPreferencesTestWidget(api: api, isLeader: false));
      await tester.pumpAndSettle();

      expect(
        find.text('Chỉ trưởng nhóm mới có quyền chọn và đổi thứ tự nguyện vọng'),
        findsOneWidget,
      );
      expect(find.byTooltip('Xóa nguyện vọng'), findsNothing);
    });
  });

  group('S24 - LecturerDetailScreen', () {
    testWidgets('leader can add lecturer to preferences', (tester) async {
      final api = FakeLecturersApi(
        preferencesResponse: const LecturerPreferenceListResponse(
          maxPreferences: 3,
          locked: false,
          preferences: [],
          interestedLecturers: [],
        ),
        lecturers: [sampleLecturer1],
      );

      await tester.pumpWidget(
        createDetailTestWidget(api: api, lecturerId: sampleLecturer1.id, isLeader: true),
      );
      await tester.pumpAndSettle();

      expect(find.text('TS. Nguyễn Văn A'), findsOneWidget);
      expect(find.text('anv@fpt.edu.vn'), findsOneWidget);
      expect(find.text('Đang nhận nhóm'), findsOneWidget);

      final addBtn = find.widgetWithText(FilledButton, 'Thêm vào NV1');
      expect(addBtn, findsOneWidget);

      await tester.tap(addBtn);
      await tester.pumpAndSettle();

      expect(api.lastUpdatedIds, ['lec-1']);
      expect(find.text('Đã thêm TS. Nguyễn Văn A vào NV1'), findsOneWidget);
    });

    testWidgets('shows locked message and disables button when preferences are locked', (tester) async {
      final api = FakeLecturersApi(
        preferencesResponse: const LecturerPreferenceListResponse(
          maxPreferences: 3,
          locked: true,
          preferences: [],
          interestedLecturers: [],
        ),
        lecturers: [sampleLecturer1],
      );

      await tester.pumpWidget(
        createDetailTestWidget(api: api, lecturerId: sampleLecturer1.id, isLeader: true),
      );
      await tester.pumpAndSettle();

      expect(
        find.text('Nguyện vọng đã khóa sau khi ghép cố vấn / hết hạn chốt'),
        findsOneWidget,
      );

      final addBtn = tester.widget<FilledButton>(find.widgetWithText(FilledButton, 'Thêm vào NV1'));
      expect(addBtn.onPressed, isNull);
    });
  });
}
