import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:exe_team_matching/core/phase.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/change_requests/change_request_screens.dart';
import 'package:exe_team_matching/features/change_requests/change_requests_api.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart';
import 'support/suggestions_fakes.dart' as fixtures;

class FakeChangeRequestsApi extends ChangeRequestsApi {
  FakeChangeRequestsApi(this.changes, {this.requests = const []})
    : super(Dio());

  final PendingChanges changes;
  final List<ChangeRequest> requests;
  bool failCreate = false;
  int createCalls = 0;
  List<String>? submittedIds;
  String? submittedReason;
  String? projectName;
  String? projectDescription;

  @override
  Future<PendingChanges> pending(String teamId) async => changes;

  @override
  Future<List<ChangeRequest>> list(String teamId) async => requests;

  @override
  Future<ChangeRequestDetail> detail(String id) async => ChangeRequestDetail(
    request: ChangeRequest(
      id: id,
      roundName: 'Đợt đổi nhóm',
      reason: 'Thành viên chuyển lịch học',
      changedMemberCount: 1,
      changedPercent: 25,
      status: 'REJECTED',
      createdAt: DateTime.utc(2026, 10, 20),
      reviewedByName: 'Bộ môn EXE',
      reviewedAt: DateTime.utc(2026, 10, 21),
      reviewNote: 'Cần bổ sung thông tin',
    ),
    items: [
      ChangeItem(
        id: 'add-1',
        action: 'ADD',
        student: fixtures.student('5', 'Sinh viên 5'),
      ),
    ],
  );

  @override
  Future<ChangeItem> setProjectChange(
    String teamId, {
    required String name,
    String? description,
  }) async {
    projectName = name;
    projectDescription = description;
    return const ChangeItem(
      id: 'project-1',
      action: 'PROJECT_CHANGE',
      newProjectName: 'Đề tài mới',
    );
  }

  @override
  Future<ChangeRequest> create(
    String teamId, {
    required List<String> itemIds,
    required String reason,
  }) async {
    createCalls++;
    submittedIds = itemIds;
    submittedReason = reason;
    if (failCreate) throw Exception('API unavailable');
    return ChangeRequest(
      id: 'request-1',
      roundName: 'Đợt đổi nhóm',
      reason: reason,
      changedMemberCount: itemIds.length,
      changedPercent: 25,
      status: 'PENDING',
      createdAt: DateTime.utc(2026, 10, 23),
    );
  }
}

PendingChanges pendingChanges() => PendingChanges(
  baselineMembers: [
    for (var i = 1; i <= 4; i++) fixtures.student('$i', 'Sinh viên $i'),
  ],
  pendingItems: [
    ChangeItem(
      id: 'add-1',
      action: 'ADD',
      student: fixtures.student('5', 'Sinh viên 5'),
    ),
  ],
  wouldHaveMemberCount: 5,
  wouldHaveSpecializationCount: 2,
);

Future<void> pumpCreate(WidgetTester tester, FakeChangeRequestsApi api) async {
  AppTheme.useGoogleFonts = false;
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        myCourseProvider.overrideWith((ref) async => 'EXE201'),
        myTeamProvider.overrideWith((ref) async => fixtures.team()),
        openChangeRoundProvider.overrideWith((ref) async => true),
        changeRequestsApiProvider.overrideWithValue(api),
      ],
      child: MaterialApp(
        theme: AppTheme.light(),
        home: const CreateChangeRequestScreen(),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  for (final width in [360.0, 412.0]) {
    testWidgets('S27 khóa tạo yêu cầu khi đợt đóng ở $width dp', (
      tester,
    ) async {
      tester.view.physicalSize = Size(width, 800);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            myCourseProvider.overrideWith((ref) async => 'EXE201'),
            myTeamProvider.overrideWith((ref) async => fixtures.team()),
            openChangeRoundProvider.overrideWith((ref) async => false),
          ],
          child: MaterialApp(
            builder: (context, child) => MediaQuery(
              data: MediaQuery.of(
                context,
              ).copyWith(textScaler: const TextScaler.linear(1.3)),
              child: child!,
            ),
            home: const CreateChangeRequestScreen(),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('Đợt đổi nhóm đã đóng'), findsOneWidget);
      expect(find.text('Tiếp tục'), findsNothing);
      expect(tester.takeException(), isNull);
    });
  }

  testWidgets('S27 yêu cầu chọn thay đổi trước khi nhập lý do', (tester) async {
    final api = FakeChangeRequestsApi(pendingChanges());
    await pumpCreate(tester, api);
    final next = find.widgetWithText(FilledButton, 'Tiếp tục');
    expect(tester.widget<FilledButton>(next).onPressed, isNull);
    await tester.tap(find.text('Thêm Sinh viên 5'));
    await tester.pump();
    expect(tester.widget<FilledButton>(next).onPressed, isNotNull);
    await tester.ensureVisible(next);
    await tester.tap(next);
    await tester.pumpAndSettle();
    expect(find.text('Lý do · Bước 2/2'), findsOneWidget);
    expect(find.text('25%'), findsOneWidget);
  });

  testWidgets('S27 chặn thay đổi khiến nhóm dưới 4 người', (tester) async {
    final api = FakeChangeRequestsApi(
      PendingChanges(
        baselineMembers: [
          for (var i = 1; i <= 4; i++) fixtures.student('$i', 'Sinh viên $i'),
        ],
        pendingItems: [
          ChangeItem(
            id: 'remove-1',
            action: 'REMOVE',
            student: fixtures.student('4', 'Sinh viên 4'),
          ),
        ],
        wouldHaveMemberCount: 3,
        wouldHaveSpecializationCount: 2,
      ),
    );
    await pumpCreate(tester, api);
    await tester.tap(find.text('Xóa Sinh viên 4'));
    await tester.pump();

    expect(
      find.text('Nhóm còn dưới 4 người. Không thể gửi yêu cầu.'),
      findsOneWidget,
    );
    expect(
      tester
          .widget<FilledButton>(find.widgetWithText(FilledButton, 'Tiếp tục'))
          .onPressed,
      isNull,
    );
    expect(api.createCalls, 0);
  });

  testWidgets('S27 cảnh báo thiếu chuyên ngành nhưng vẫn cho tiếp tục', (
    tester,
  ) async {
    final data = pendingChanges();
    final api = FakeChangeRequestsApi(
      PendingChanges(
        baselineMembers: data.baselineMembers,
        pendingItems: data.pendingItems,
        wouldHaveMemberCount: data.wouldHaveMemberCount,
        wouldHaveSpecializationCount: 1,
      ),
    );
    await pumpCreate(tester, api);
    await tester.tap(find.text('Thêm Sinh viên 5'));
    await tester.pump();

    expect(
      find.text(
        'Nhóm chưa có đủ 2 chuyên ngành. Bộ môn sẽ kiểm tra khi chốt nhóm.',
      ),
      findsOneWidget,
    );
    expect(
      tester
          .widget<FilledButton>(find.widgetWithText(FilledButton, 'Tiếp tục'))
          .onPressed,
      isNotNull,
    );
  });

  testWidgets('S28 không gửi khi lý do dưới 10 ký tự', (tester) async {
    final api = FakeChangeRequestsApi(pendingChanges());
    await pumpCreate(tester, api);
    await tester.tap(find.text('Thêm Sinh viên 5'));
    await tester.pump();
    final next = find.widgetWithText(FilledButton, 'Tiếp tục');
    await tester.ensureVisible(next);
    await tester.tap(next);
    await tester.pumpAndSettle();
    await tester.enterText(
      find.widgetWithText(TextField, 'Lý do thay đổi'),
      'Quá ngắn',
    );
    await tester.ensureVisible(find.text('Gửi yêu cầu đổi nhóm'));
    await tester.tap(find.text('Gửi yêu cầu đổi nhóm'));
    await tester.pumpAndSettle();

    expect(find.text('Lý do cần ít nhất 10 ký tự.'), findsWidgets);
    expect(find.text('Gửi yêu cầu'), findsNothing);
    expect(api.createCalls, 0);
  });

  testWidgets('S28 gửi yêu cầu chỉ đổi đề tài qua mục thay đổi mới', (
    tester,
  ) async {
    final api = FakeChangeRequestsApi(pendingChanges())..failCreate = true;
    await pumpCreate(tester, api);
    await tester.enterText(
      find.widgetWithText(TextField, 'Tên đề tài mới'),
      'Đề tài mới',
    );
    await tester.enterText(
      find.widgetWithText(TextField, 'Mô tả đề tài mới'),
      'Mô tả mới',
    );
    final next = find.widgetWithText(FilledButton, 'Tiếp tục');
    await tester.ensureVisible(next);
    await tester.tap(next);
    await tester.pumpAndSettle();
    await tester.enterText(
      find.widgetWithText(TextField, 'Lý do thay đổi'),
      'Đề tài phù hợp hơn với mục tiêu nhóm',
    );
    await tester.ensureVisible(find.text('Gửi yêu cầu đổi nhóm'));
    await tester.tap(find.text('Gửi yêu cầu đổi nhóm'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Gửi yêu cầu'));
    await tester.pumpAndSettle();

    expect(api.projectName, 'Đề tài mới');
    expect(api.projectDescription, 'Mô tả mới');
    expect(api.submittedIds, ['project-1']);
    expect(api.submittedReason, 'Đề tài phù hợp hơn với mục tiêu nhóm');
  });

  testWidgets('S28 lỗi API giữ lý do và lựa chọn để gửi lại', (tester) async {
    final api = FakeChangeRequestsApi(pendingChanges())..failCreate = true;
    await pumpCreate(tester, api);
    await tester.tap(find.text('Thêm Sinh viên 5'));
    await tester.pump();
    final next = find.widgetWithText(FilledButton, 'Tiếp tục');
    await tester.ensureVisible(next);
    await tester.tap(next);
    await tester.pumpAndSettle();
    await tester.enterText(
      find.widgetWithText(TextField, 'Lý do thay đổi'),
      'Đổi thành viên do lịch học',
    );
    await tester.ensureVisible(find.text('Gửi yêu cầu đổi nhóm'));
    await tester.tap(find.text('Gửi yêu cầu đổi nhóm'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Gửi yêu cầu'));
    await tester.pumpAndSettle();

    expect(api.createCalls, 1);
    expect(api.submittedIds, ['add-1']);
    expect(api.submittedReason, 'Đổi thành viên do lịch học');
    expect(
      find.text('Không gửi được yêu cầu. Nội dung đã nhập được giữ lại.'),
      findsWidgets,
    );
    expect(find.text('Đổi thành viên do lịch học'), findsOneWidget);
  });

  testWidgets('S28 gửi thành công và mở chi tiết yêu cầu mới', (tester) async {
    AppTheme.useGoogleFonts = false;
    final api = FakeChangeRequestsApi(pendingChanges());
    final router = GoRouter(
      initialLocation: changeRequestCreatePath,
      routes: [
        GoRoute(
          path: changeRequestCreatePath,
          builder: (_, _) => const CreateChangeRequestScreen(),
        ),
        GoRoute(
          path: '$changeRequestListPath/:id',
          builder: (_, state) =>
              Scaffold(body: Text('Chi tiết ${state.pathParameters['id']}')),
        ),
      ],
    );
    addTearDown(router.dispose);
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          myCourseProvider.overrideWith((ref) async => 'EXE201'),
          myTeamProvider.overrideWith((ref) async => fixtures.team()),
          openChangeRoundProvider.overrideWith((ref) async => true),
          changeRequestsApiProvider.overrideWithValue(api),
        ],
        child: MaterialApp.router(
          theme: AppTheme.light(),
          routerConfig: router,
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Thêm Sinh viên 5'));
    await tester.pump();
    final next = find.widgetWithText(FilledButton, 'Tiếp tục');
    await tester.ensureVisible(next);
    await tester.tap(next);
    await tester.pumpAndSettle();
    await tester.enterText(
      find.widgetWithText(TextField, 'Lý do thay đổi'),
      'Đổi thành viên do lịch học',
    );
    await tester.ensureVisible(find.text('Gửi yêu cầu đổi nhóm'));
    await tester.tap(find.text('Gửi yêu cầu đổi nhóm'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Gửi yêu cầu'));
    await tester.pumpAndSettle();

    expect(api.submittedIds, ['add-1']);
    expect(api.submittedReason, 'Đổi thành viên do lịch học');
    expect(find.text('Chi tiết request-1'), findsOneWidget);
  });

  testWidgets('S26 lọc danh sách theo trạng thái và đợt', (tester) async {
    AppTheme.useGoogleFonts = false;
    final api = FakeChangeRequestsApi(
      pendingChanges(),
      requests: [
        ChangeRequest(
          id: 'pending',
          roundName: 'Đợt A',
          reason: 'Đổi thành viên',
          changedMemberCount: 1,
          changedPercent: 25,
          status: 'PENDING',
          createdAt: DateTime.utc(2026, 10, 23),
        ),
        ChangeRequest(
          id: 'approved',
          roundName: 'Đợt B',
          reason: 'Đổi đề tài',
          changedMemberCount: 0,
          changedPercent: 0,
          status: 'APPROVED',
          createdAt: DateTime.utc(2026, 10, 22),
        ),
      ],
    );
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          myCourseProvider.overrideWith((ref) async => 'EXE201'),
          myTeamProvider.overrideWith((ref) async => fixtures.team()),
          openChangeRoundProvider.overrideWith((ref) async => false),
          changeRequestsApiProvider.overrideWithValue(api),
        ],
        child: MaterialApp(
          theme: AppTheme.light(),
          home: const ChangeRequestListScreen(),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Đợt A'), findsOneWidget);
    expect(find.text('Đợt B'), findsOneWidget);

    await tester.tap(find.byType(DropdownButtonFormField<String>).first);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Đã duyệt').last);
    await tester.pumpAndSettle();
    expect(find.text('Đợt A'), findsNothing);
    expect(find.text('Đợt B'), findsOneWidget);

    await tester.tap(find.byType(DropdownButtonFormField<String>).last);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Đợt A').last);
    await tester.pumpAndSettle();
    expect(find.text('Không có yêu cầu phù hợp'), findsOneWidget);
  });

  testWidgets('S29 hiển thị lịch sử xét duyệt, ghi chú từ chối và tạo lại', (
    tester,
  ) async {
    AppTheme.useGoogleFonts = false;
    final api = FakeChangeRequestsApi(pendingChanges());
    await tester.pumpWidget(
      ProviderScope(
        overrides: [changeRequestsApiProvider.overrideWithValue(api)],
        child: MaterialApp(
          theme: AppTheme.light(),
          home: const ChangeRequestDetailScreen(requestId: 'request-1'),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Đã gửi'), findsOneWidget);
    expect(find.text('Bộ môn xét duyệt'), findsOneWidget);
    expect(find.textContaining('Bộ môn EXE'), findsOneWidget);
    expect(find.text('Thêm Sinh viên 5'), findsOneWidget);
    expect(find.text('Ghi chú Bộ môn: Cần bổ sung thông tin'), findsOneWidget);
    await tester.scrollUntilVisible(find.text('Tạo yêu cầu mới'), 200);
    expect(find.text('Tạo yêu cầu mới'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
}
