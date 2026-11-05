import 'package:exe_team_matching/core/firebase/analytics_service.dart';
import 'package:exe_team_matching/core/firebase/remote_config_service.dart';
import 'package:exe_team_matching/features/discover/idea_ai_contract.dart';
import 'package:exe_team_matching/features/discover/idea_suggest_sheet.dart';
import 'package:exe_team_matching/features/discover/idea_suggest_view_model.dart';
import 'package:exe_team_matching/features/my_team/my_team_api.dart' show domainsProvider;
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart' show CatalogRef;
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/firebase_fakes.dart';

const _description = 'Ứng dụng giúp sinh viên đặt lịch dùng chung phòng họp';
const _draft = IdeaDraft(
  title: 'Đặt phòng họp nhóm',
  problem: 'Sinh viên khó tìm phòng',
  solution: 'Lịch dùng chung',
  targetCustomer: 'Sinh viên đại học',
  tags: ['Giáo dục', 'NGOAI'],
);
const _domains = [CatalogRef(id: 1, code: 'D01', name: 'Giáo dục'), CatalogRef(id: 2, code: 'D02', name: 'Y tế')];

class _FakeAi implements AiService {
  Object? error;
  String? input;
  List<String>? tags;

  @override
  Future<IdeaDraft> suggestIdea({required String description, required List<String> allowedTags}) async {
    input = description;
    tags = allowedTags;
    if (error case final e?) throw e;
    return _draft;
  }
}

/// MB-56 – gợi ý ý tưởng bằng AI ở S09, với AiService giả.
void main() {
  late _FakeAi ai;
  late FakeAnalyticsService analytics;

  setUp(() {
    ai = _FakeAi();
    analytics = FakeAnalyticsService();
  });

  List<Override> overrides({bool enabled = true}) => [
    aiServiceProvider.overrideWithValue(ai),
    analyticsServiceProvider.overrideWithValue(analytics),
    domainsProvider.overrideWith((ref) async => _domains),
    remoteConfigServiceProvider.overrideWithValue(FakeRemoteConfigService(AppRemoteConfig(aiIdeaEnabled: enabled))),
  ];

  ProviderContainer container({bool enabled = true}) {
    final c = ProviderContainer(overrides: overrides(enabled: enabled));
    addTearDown(c.dispose);
    c.listen(ideaSuggestViewModelProvider, (_, _) {});
    return c;
  }

  IdeaSuggestViewModel vm(ProviderContainer c) => c.read(ideaSuggestViewModelProvider.notifier);

  group('ViewModel', () {
    test('thành công: có bản nháp, gửi danh mục lĩnh vực cho AI', () async {
      final c = container();

      await vm(c).suggest(_description);

      expect(c.read(ideaSuggestViewModelProvider), isA<IdeaSuggestReady>());
      expect(ai.tags, ['Giáo dục', 'Y tế']);
    });

    test('không gửi email và MSSV lên AI', () async {
      final c = container();

      await vm(c).suggest('Mình là SE184118, email sv.a@fpt.edu.vn, muốn làm app đặt phòng họp');

      expect(ai.input, isNot(contains('SE184118')));
      expect(ai.input, isNot(contains('@')));
      expect(ai.input, contains('app đặt phòng họp'));
    });

    test('mô tả quá ngắn: báo tại ô, không gọi AI', () async {
      final c = container();

      await vm(c).suggest('app');

      expect((c.read(ideaSuggestViewModelProvider) as IdeaSuggestIdle).inputError, isNotNull);
      expect(ai.input, isNull);
    });

    test('cờ tắt: không gọi AI', () async {
      final c = container(enabled: false);

      await vm(c).suggest(_description);

      expect(vm(c).enabled, isFalse);
      expect(ai.input, isNull);
      expect((c.read(ideaSuggestViewModelProvider) as IdeaSuggestFailed).message, contains('tự nhập'));
    });

    for (final (code, text) in const [
      ('disabled', 'đang tắt'),
      ('quota', 'quá tải'),
      ('network', 'Mất kết nối'),
      ('bad_json', 'không đọc được'),
      ('blocked', 'không xử lý được'),
      ('unknown', 'Không tạo được'),
    ]) {
      test('lỗi $code: báo câu tiếng Việt', () async {
        final c = container();
        ai.error = AiException(code);

        await vm(c).suggest(_description);

        expect((c.read(ideaSuggestViewModelProvider) as IdeaSuggestFailed).message, contains(text));
      });
    }

    test('lỗi ngoài dự kiến: không làm hỏng form', () async {
      final c = container();
      ai.error = StateError('hỏng');

      await vm(c).suggest(_description);

      expect(c.read(ideaSuggestViewModelProvider), isA<IdeaSuggestFailed>());
    });

    test('dùng gợi ý: trả nội dung điền form và ghi Analytics', () async {
      final c = container();
      await vm(c).suggest(_description);

      final fields = await vm(c).accept();

      expect(fields!.$1, 'Đặt phòng họp nhóm');
      expect(fields.$2, contains('Vấn đề: Sinh viên khó tìm phòng'));
      expect(analytics.events.single.name, AnalyticsEvents.aiResultAccepted);
      expect(analytics.events.single.parameters, {'feature': 'idea_suggest', 'index': 0});
    });

    test('chưa có gợi ý thì không có gì để dùng', () async {
      final c = container();

      expect(await vm(c).accept(), isNull);
      expect(analytics.events, isEmpty);
    });
  });

  group('ideaFieldsFrom', () {
    test('chỉ giữ tag có trong danh mục', () {
      final (_, description) = ideaFieldsFrom(_draft, _domains);

      expect(description, contains('Lĩnh vực: Giáo dục'));
      expect(description, isNot(contains('NGOAI')));
    });

    test('không có tag hợp lệ thì bỏ dòng lĩnh vực', () {
      const draft = IdeaDraft(title: 'A', problem: 'b', solution: 'c', targetCustomer: 'd', tags: ['NGOAI']);

      expect(ideaFieldsFrom(draft, _domains).$2, isNot(contains('Lĩnh vực')));
    });

    test('cắt theo quy tắc 17: tiêu đề 80, mô tả 300', () {
      final draft = IdeaDraft(title: 'x' * 100, problem: 'y' * 400, solution: 'c', targetCustomer: 'd');

      final (title, description) = ideaFieldsFrom(draft, _domains);

      expect(title.length, 80);
      expect(description.length, lessThanOrEqualTo(300));
    });
  });

  group('IdeaSuggestSheet', () {
    Future<(String, String)?> open(WidgetTester tester, Future<void> Function() steps) async {
      (String, String)? result;
      await tester.pumpWidget(
        ProviderScope(
          overrides: overrides(),
          child: MaterialApp(
            home: Builder(
              builder: (context) => Scaffold(
                body: TextButton(
                  onPressed: () async => result = await showModalBottomSheet<(String, String)>(
                    context: context,
                    isScrollControlled: true,
                    builder: (_) => const IdeaSuggestSheet(),
                  ),
                  child: const Text('mở'),
                ),
              ),
            ),
          ),
        ),
      );
      await tester.tap(find.text('mở'));
      await tester.pumpAndSettle();
      await steps();
      return result;
    }

    testWidgets('xem gợi ý có nhãn AI, dùng gợi ý thì đóng và trả nội dung', (tester) async {
      final result = await open(tester, () async {
        await tester.enterText(find.byType(TextField), _description);
        await tester.tap(find.text('Tạo gợi ý'));
        await tester.pumpAndSettle();

        expect(find.text('Gợi ý của AI'), findsOneWidget);
        expect(find.text('Đặt phòng họp nhóm'), findsOneWidget);

        await tester.tap(find.text('Dùng gợi ý'));
        await tester.pumpAndSettle();
      });

      expect(result?.$1, 'Đặt phòng họp nhóm');
    });

    testWidgets('AI lỗi: báo lỗi, vẫn có nút Tự nhập', (tester) async {
      ai.error = const AiException('quota');

      final result = await open(tester, () async {
        await tester.enterText(find.byType(TextField), _description);
        await tester.tap(find.text('Tạo gợi ý'));
        await tester.pumpAndSettle();

        expect(find.textContaining('quá tải'), findsOneWidget);

        await tester.tap(find.text('Tự nhập'));
        await tester.pumpAndSettle();
      });

      expect(result, isNull);
    });
  });
}
