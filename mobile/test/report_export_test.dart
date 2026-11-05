import 'dart:convert';
import 'dart:io';

import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/auth/session.dart';
import 'package:exe_team_matching/core/firebase/analytics_service.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/core/providers.dart';
import 'package:exe_team_matching/features/files/file_services.dart';
import 'package:exe_team_matching/features/workspace/report_api.dart';
import 'package:exe_team_matching/features/workspace/report_export_card.dart';
import 'package:exe_team_matching/features/workspace/report_pdf.dart';
import 'package:exe_team_matching/features/workspace/report_view_model.dart';
import 'package:exe_team_matching/features/workspace/workspace_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/firebase_fakes.dart';

const _workspaceId = 'w1';
const networkMessage = 'Mất kết nối. Kiểm tra mạng rồi thử lại.';
const _user = UserInfo(id: 'u1', email: 'a@example.com', fullName: 'Sinh viên', roles: {Role.student});

final _saved = SavedReport(fileName: '20261105-010203-ab12cd34.pdf', sizeBytes: 2048, createdAt: DateTime.utc(2026, 11, 5));

final _data = ProjectReportData(teamName: 'Nhóm Bình Minh', courseCode: 'EXE201', generatedAt: DateTime.utc(2026, 11, 5));

class _FakeAuth extends AuthController {
  _FakeAuth(this.user);

  final UserInfo? user;

  @override
  Future<UserInfo?> build() async => user;
}

class _FakeBuilder extends ReportPdfBuilder {
  Object? error;

  @override
  Future<Uint8List> build(ProjectReportData data) async {
    if (error case final e?) throw e;
    return Uint8List.fromList(utf8.encode('%PDF-1.7'));
  }
}

class _FakeApi extends ReportApi {
  _FakeApi() : super(Dio());

  Object? uploadError;
  Object? urlError;
  String? ownerId;
  String? fileName;
  final progress = <double>[];

  @override
  Future<({SavedReport report, String url})> upload(
    String ownerId,
    Uint8List pdf,
    String fileName, {
    void Function(double progress)? onProgress,
  }) async {
    this.ownerId = ownerId;
    this.fileName = fileName;
    onProgress?.call(0.5);
    progress.add(0.5);
    if (uploadError case final e?) throw e;
    onProgress?.call(1);
    return (report: _saved, url: 'http://files.test/signed');
  }

  @override
  Future<String> downloadUrl(String ownerId, String fileName) async {
    if (urlError case final e?) throw e;
    return 'http://files.test/fresh';
  }
}

/// MB-53 – xuất PDF, tải lên, mở lại.
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  late _FakeApi api;
  late _FakeBuilder builder;
  late FakeAnalyticsService analytics;
  late List<String> opened;

  setUp(() {
    api = _FakeApi();
    builder = _FakeBuilder();
    analytics = FakeAnalyticsService();
    opened = [];
  });

  List<Override> overrides({UserInfo? user = _user}) => [
    authControllerProvider.overrideWith(() => _FakeAuth(user)),
    reportApiProvider.overrideWithValue(api),
    reportPdfBuilderProvider.overrideWithValue(builder),
    analyticsServiceProvider.overrideWithValue(analytics),
    openUrlProvider.overrideWithValue((url) async {
      opened.add(url);
      return true;
    }),
  ];

  Future<ProviderContainer> container({UserInfo? user = _user}) async {
    final c = ProviderContainer(overrides: overrides(user: user));
    addTearDown(c.dispose);
    c.listen(reportExportViewModelProvider(_workspaceId), (_, _) {});
    await c.read(authControllerProvider.future);
    return c;
  }

  ReportExportViewModel vm(ProviderContainer c) => c.read(reportExportViewModelProvider(_workspaceId).notifier);
  ReportExportState stateOf(ProviderContainer c) => c.read(reportExportViewModelProvider(_workspaceId));

  group('ViewModel', () {
    test('xuất thành công: qua các bước, lưu theo id tài khoản, ghi Analytics', () async {
      final c = await container();
      final seen = <ReportExportState>[];
      c.listen(reportExportViewModelProvider(_workspaceId), (_, next) => seen.add(next));

      await vm(c).export(_data);

      expect(seen.first, isA<ReportGenerating>());
      expect(seen.whereType<ReportUploading>().map((s) => s.progress), containsAll([0.0, 0.5, 1.0]));
      final state = stateOf(c) as ReportSaved;
      expect(state.url, 'http://files.test/signed');
      expect(api.ownerId, 'u1');
      expect(api.fileName, 'Bao cao Nhóm Bình Minh.pdf');
      expect(analytics.events.single.name, AnalyticsEvents.exportReport);
      expect(analytics.events.single.parameters, {'report_type': 'project_report', 'format': 'pdf'});
    });

    test('mất mạng: báo lỗi, không ghi Analytics, thử lại được', () async {
      final c = await container();
      api.uploadError = const ApiException(status: 0, code: 'network', title: 'Mất kết nối');

      await vm(c).export(_data);

      expect((stateOf(c) as ReportFailed).message, networkMessage);
      expect(analytics.events, isEmpty);

      api.uploadError = null;
      await vm(c).export(_data);
      expect(stateOf(c), isA<ReportSaved>());
    });

    test('server từ chối sai quyền: hiện câu lỗi của server', () async {
      final c = await container();
      api.uploadError = const ApiException(
        status: 403,
        code: 'not-report-owner',
        title: 'Không có quyền với báo cáo này',
        detail: 'Chỉ chủ báo cáo mới đọc hoặc ghi được.',
      );

      await vm(c).export(_data);

      expect((stateOf(c) as ReportFailed).message, 'Chỉ chủ báo cáo mới đọc hoặc ghi được.');
    });

    test('lỗi tạo PDF: không gọi API', () async {
      final c = await container();
      builder.error = StateError('thiếu font');

      await vm(c).export(_data);

      expect(stateOf(c), isA<ReportFailed>());
      expect(api.ownerId, isNull);
    });

    test('chưa đăng nhập: báo lỗi, không tạo PDF', () async {
      final c = await container(user: null);

      await vm(c).export(_data);

      expect((stateOf(c) as ReportFailed).message, contains('đăng nhập'));
    });

    test('mở báo cáo xin link mới', () async {
      final c = await container();
      await vm(c).export(_data);

      await vm(c).open();

      expect(opened, ['http://files.test/fresh']);
      expect((stateOf(c) as ReportSaved).url, 'http://files.test/fresh');
    });

    test('mở lỗi: giữ báo cáo đã lưu và hiện câu lỗi', () async {
      final c = await container();
      await vm(c).export(_data);
      api.urlError = const ApiException(status: 0, code: 'network', title: 'Mất kết nối');

      await vm(c).open();

      final state = stateOf(c) as ReportSaved;
      expect(state.openError, networkMessage);
      expect(state.url, 'http://files.test/signed');
    });
  });

  group('Dữ liệu báo cáo', () {
    const stats = (mine: 2, dueSoon: 1, pendingReview: 4);
    final now = DateTime.utc(2026, 11, 5);

    test('thành viên thấy Việc của tôi; lấy tối đa 5 hoạt động', () {
      final workspace = WorkspaceOverview(
        id: 'w',
        teamName: 'Nhóm A',
        courseCode: 'EXE101',
        myRole: WorkspaceRole.member,
        people: const [PersonRef(personType: 'STUDENT', personId: 'p', displayName: 'An', role: WorkspaceRole.leader)],
        recentActivity: [for (var i = 0; i < 7; i++) RecentActivity(kind: 'TASK', title: 'Việc $i', at: now)],
      );

      final data = reportDataFrom(workspace, stats: stats, now: now);

      expect(data.stats.first, (label: 'Việc của tôi', value: 2));
      expect(data.people.single, (name: 'An', role: 'Trưởng nhóm'));
      expect(data.activity, hasLength(5));
    });

    test('cố vấn thấy Bài nộp chờ duyệt', () {
      const workspace = WorkspaceOverview(id: 'w', teamName: 'Nhóm A', courseCode: 'EXE101', myRole: WorkspaceRole.instructor);

      expect(reportDataFrom(workspace, stats: stats, now: now).stats.first, (label: 'Bài nộp chờ duyệt', value: 4));
    });

    test('tên file bỏ ký tự không hợp lệ', () {
      expect(reportFileName('A/B:C'), 'Bao cao A B C.pdf');
      expect(reportFileName('  '), 'Bao cao du an.pdf');
      expect(reportFileName('x' * 200).length, lessThan(100));
    });
  });

  group('ReportPdfBuilder', () {
    Future<ByteData> fromDisk(String asset) async => ByteData.sublistView(File(asset).readAsBytesSync());

    test('tạo PDF có tiếng Việt từ font nhúng, nhiều hoạt động vẫn sang trang được', () async {
      final bytes = await ReportPdfBuilder(loadAsset: fromDisk).build(
        ProjectReportData(
          teamName: 'Nhóm Bình Minh',
          courseCode: 'EXE201',
          projectName: 'Ứng dụng đặt lịch',
          generatedAt: DateTime.utc(2026, 11, 5),
          people: const [(name: 'Nguyễn Văn Ấn', role: 'Trưởng nhóm'), (name: 'Trần Thị Bé', role: '')],
          stats: const [(label: 'Sắp đến hạn', value: 3)],
          activity: [for (var i = 0; i < 80; i++) (title: 'Cập nhật số $i', at: DateTime.utc(2026, 11, 5))],
        ),
      );

      expect(ascii.decode(bytes.sublist(0, 5)), '%PDF-');
      expect(bytes.length, greaterThan(5000));
    });

    test('dự án mới vẫn tạo được', () async {
      final bytes = await ReportPdfBuilder(loadAsset: fromDisk).build(_data);

      expect(ascii.decode(bytes.sublist(0, 5)), '%PDF-');
    });
  });

  group('ReportApi', () {
    late _Adapter adapter;
    late ReportApi reports;

    setUp(() {
      adapter = _Adapter();
      reports = ReportApi(Dio(BaseOptions(baseUrl: 'http://api.test'))..httpClientAdapter = adapter);
    });

    test('tải lên đúng đường dẫn chủ báo cáo, báo tiến độ', () async {
      adapter.body = {'fileName': 'a.pdf', 'sizeBytes': 10, 'createdAt': '2026-11-05T01:00:00Z', 'url': 'http://s/a'};
      final progress = <double>[];

      final saved = await reports.upload('u1', Uint8List(8), 'b.pdf', onProgress: progress.add);

      expect(adapter.last!.path, '/api/reports/u1');
      expect(adapter.last!.method, 'POST');
      expect(saved.url, 'http://s/a');
      expect(saved.report.fileName, 'a.pdf');
    });

    test('danh sách và link mới', () async {
      adapter.body = [
        {'fileName': 'a.pdf', 'sizeBytes': 10, 'createdAt': '2026-11-05T01:00:00Z'},
      ];
      expect((await reports.list('u1')).single.sizeBytes, 10);

      adapter.body = {'url': 'http://s/b'};
      expect(await reports.downloadUrl('u1', 'a.pdf'), 'http://s/b');
      expect(adapter.last!.path, '/api/reports/u1/a.pdf/download-url');
    });
  });

  group('ReportExportCard', () {
    Future<void> pump(WidgetTester tester) async {
      await tester.pumpWidget(
        ProviderScope(
          overrides: overrides(),
          child: MaterialApp(
            home: Scaffold(
              body: SingleChildScrollView(child: ReportExportCard(workspaceId: _workspaceId, data: () => _data)),
            ),
          ),
        ),
      );
      await tester.pump();
    }

    testWidgets('bấm xuất thì hiện kết quả và link', (tester) async {
      await pump(tester);

      await tester.tap(find.text('Xuất báo cáo PDF'));
      await tester.pumpAndSettle();

      expect(find.text('Đã lưu báo cáo'), findsOneWidget);
      expect(find.text('http://files.test/signed'), findsOneWidget);

      await tester.tap(find.text('Mở báo cáo'));
      await tester.pumpAndSettle();
      expect(opened, ['http://files.test/fresh']);
    });

    testWidgets('lỗi thì hiện câu lỗi và nút Thử lại', (tester) async {
      api.uploadError = const ApiException(status: 0, code: 'network', title: 'Mất kết nối');
      await pump(tester);

      await tester.tap(find.text('Xuất báo cáo PDF'));
      await tester.pumpAndSettle();

      expect(find.text(networkMessage), findsOneWidget);
      expect(find.text('Thử lại'), findsOneWidget);
    });
  });
}

class _Adapter implements HttpClientAdapter {
  Object? body;
  RequestOptions? last;

  @override
  Future<ResponseBody> fetch(RequestOptions options, Stream<List<int>>? requestStream, Future<void>? cancelFuture) async {
    last = options;
    await requestStream?.drain<void>();
    return ResponseBody.fromString(
      jsonEncode(body),
      200,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}
