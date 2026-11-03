import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

/// Một màn cần kiểm: widget, provider giả, và chữ phải thấy để chắc màn dựng nội dung thật (không dừng ở trống / lỗi).
typedef A11yCase = ({Widget screen, List<Override> overrides, String mustShow});

/// Checklist flutter-handoff §5 (MB-36) cho [cases]: mỗi màn ở 360 và 412 dp, chữ hệ thống 130%; không tràn bố cục,
/// vùng chạm ≥ 48, nút có nhãn, chữ đạt tương phản. Màn mở như màn cấp 2 dưới [root] để nút Back có chỗ quay về.
void accessibilityTests(Map<String, A11yCase> cases, {String root = '/sv'}) {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  for (final size in const [Size(360, 800), Size(412, 915)]) {
    for (final entry in cases.entries) {
      testWidgets('${entry.key} ở ${size.width.toInt()} dp, chữ 130%', (tester) async {
        tester.view.physicalSize = size * 3;
        tester.view.devicePixelRatio = 3;
        addTearDown(tester.view.reset);

        final c = entry.value;
        final router = GoRouter(initialLocation: '$root/man', routes: [
          GoRoute(path: root, builder: (_, _) => const Text('Trang chủ'), routes: [GoRoute(path: 'man', builder: (_, _) => c.screen)]),
        ]);
        // Không dùng addTearDown: flutter_test kiểm SemanticsHandle trước khi chạy tear-down.
        final semantics = tester.ensureSemantics();
        try {
          await tester.pumpWidget(ProviderScope(
            overrides: c.overrides,
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
          expect(find.textContaining(c.mustShow), findsWidgets, reason: 'màn phải dựng nội dung thật');

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
