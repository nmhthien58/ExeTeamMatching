import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/lecturers/lecturer_detail_screen.dart';
import 'package:exe_team_matching/features/lecturers/lecturer_preferences_screen.dart';
import 'package:exe_team_matching/features/lecturers/lecturers_api.dart';
import 'package:exe_team_matching/features/lecturers/lecturers_models.dart';
import 'package:exe_team_matching/features/my_team/my_team_api.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart' show CatalogRef;
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

/// MB-36 – checklist flutter-handoff §5 cho nguyện vọng giảng viên S23 (đang chọn và đã khóa) và hồ sơ giảng viên S24 (MB-27):
/// 360 và 412 dp, chữ hệ thống 130% không tràn, vùng chạm ≥ 48, nút có nhãn, chữ đạt tương phản.
void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  const ai = CatalogRef(id: 1, code: 'D01', name: 'AI & Data Innovation');
  const saas = CatalogRef(id: 4, code: 'D04', name: 'Software / SaaS / Platform');
  const edu = CatalogRef(id: 5, code: 'D05', name: 'Giáo dục và đào tạo trực tuyến');
  // Tên và lĩnh vực dài để thử xuống dòng khi chữ 130%.
  const lecturers = [
    LecturerSummary(id: 'l1', fullName: 'PGS. TS. Nguyễn Hoàng Bảo Anh', email: 'anhnhb.giangvien@fe.edu.vn', domains: [ai, saas, edu], maxTeams: 6, currentAssignedTeamCount: 4),
    LecturerSummary(id: 'l2', fullName: 'ThS. Trần Thị Minh Châu', email: 'chauttm@fe.edu.vn', domains: [edu], maxTeams: 6, currentAssignedTeamCount: 6, isFull: true),
    LecturerSummary(id: 'l3', fullName: 'TS. Lê Văn Đức', email: 'duclv@fe.edu.vn', domains: [saas]),
  ];
  const interested = LecturerRef(lecturerId: 'l1', fullName: 'PGS. TS. Nguyễn Hoàng Bảo Anh', domains: [ai, saas]);
  LecturerPreferenceListResponse preferences({bool locked = false}) => LecturerPreferenceListResponse(
        maxPreferences: 3,
        locked: locked,
        lockAt: DateTime.now().add(const Duration(days: 2)),
        preferences: const [
          LecturerPreferenceItem(lecturerId: 'l1', fullName: 'PGS. TS. Nguyễn Hoàng Bảo Anh', domains: [ai, saas, edu], rank: 1, isInterested: true),
          LecturerPreferenceItem(lecturerId: 'l3', fullName: 'TS. Lê Văn Đức', domains: [saas], rank: 2),
        ],
        interestedLecturers: const [interested],
        previousInstructor: const LecturerRef(lecturerId: 'l9', fullName: 'TS. Vũ Đình Khánh Linh', domains: [ai]),
      );

  final screens = <String, (Widget, LecturerPreferenceListResponse)>{
    'S23': (const LecturerPreferencesScreen(), preferences()),
    'S23 đã khóa': (const LecturerPreferencesScreen(), preferences(locked: true)),
    'S24': (const LecturerDetailScreen(lecturerId: 'l1'), preferences()),
  };

  for (final size in const [Size(360, 800), Size(412, 915)]) {
    for (final entry in screens.entries) {
      testWidgets('${entry.key} ở ${size.width.toInt()} dp, chữ 130%', (tester) async {
        tester.view.physicalSize = size * 3;
        tester.view.devicePixelRatio = 3;
        addTearDown(tester.view.reset);

        final (screen, prefs) = entry.value;
        // Màn mở như màn cấp 2 để nút Back / pop có chỗ quay về như trong app.
        final router = GoRouter(initialLocation: '/sv/man', routes: [
          GoRoute(path: '/sv', builder: (_, _) => const Text('S01'), routes: [GoRoute(path: 'man', builder: (_, _) => screen)]),
        ]);
        // Không dùng addTearDown: flutter_test kiểm SemanticsHandle trước khi chạy tear-down.
        final semantics = tester.ensureSemantics();
        try {
          await tester.pumpWidget(ProviderScope(
            overrides: [
              lecturersApiProvider.overrideWithValue(_FakeLecturers(lecturers, prefs)),
              myTeamDetailProvider.overrideWith((ref) async => const MyTeam(
                    id: 't1',
                    name: 'Nhóm Bình Minh Sáng Tạo',
                    status: 'FINALIZED',
                    courseCode: 'EXE201',
                    members: [],
                    domains: [],
                    isLeader: true,
                    memberCount: 5,
                    specializationCount: 3,
                    relation: 'LEADER',
                  )),
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
          // Màn phải dựng nội dung thật (tên giảng viên), không dừng ở trạng thái trống / lỗi.
          expect(find.textContaining('Nguyễn Hoàng Bảo Anh'), findsWidgets);

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

class _FakeLecturers extends LecturersApi {
  _FakeLecturers(this.lecturers, this.preferences) : super(Dio());

  final List<LecturerSummary> lecturers;
  final LecturerPreferenceListResponse preferences;

  @override
  Future<LecturerPreferenceListResponse> getPreferences(String teamId) async => preferences;

  @override
  Future<List<TopicInterestItem>> getTopicInterests(String teamId) async => const [
        TopicInterestItem(
            id: 'i1', teamId: 't1', advisorId: 'l1', advisorName: 'PGS. TS. Nguyễn Hoàng Bảo Anh', advisorEmail: 'anhnhb.giangvien@fe.edu.vn',
            advisorRole: 'LECTURER', domains: [CatalogRef(id: 1, code: 'D01', name: 'AI & Data Innovation')], status: 'OPEN'),
      ];

  @override
  Future<List<LecturerSummary>> listLecturers({String? search, int page = 1, int pageSize = 50}) async => lecturers;

  @override
  Future<LecturerSummary> getLecturer(String id) async => lecturers.firstWhere((l) => l.id == id);
}
