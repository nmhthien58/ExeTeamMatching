import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/shared/widgets/deadline_banner.dart';
import 'package:exe_team_matching/shared/widgets/states.dart';
import 'package:exe_team_matching/shared/widgets/team_requirement_bar.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

Widget wrap(Widget child) => MaterialApp(theme: AppTheme.light(), home: Scaffold(body: Center(child: child)));

void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  group('DeadlineBanner – 4 mức', () {
    final now = DateTime(2026, 9, 10, 8);

    testWidgets('còn hơn 72 giờ: ghi giờ hạn chót', (tester) async {
      await tester.pumpWidget(wrap(DeadlineBanner(deadline: DateTime(2026, 9, 17, 18), now: now)));
      expect(find.text('Hạn chót 18:00 · 17/09/2026'), findsOneWidget);
      expect(find.byIcon(Icons.event), findsOneWidget);
    });

    testWidgets('còn 72 giờ trở xuống: số ngày giờ', (tester) async {
      await tester.pumpWidget(wrap(DeadlineBanner(deadline: now.add(const Duration(days: 2, hours: 5)), now: now)));
      expect(find.text('Còn 2 ngày 5 giờ'), findsOneWidget);
      expect(find.byIcon(Icons.schedule), findsOneWidget);
    });

    testWidgets('còn 24 giờ trở xuống: số giờ, icon cảnh báo', (tester) async {
      await tester.pumpWidget(wrap(DeadlineBanner(deadline: now.add(const Duration(hours: 5)), now: now)));
      expect(find.text('Còn 5 giờ'), findsOneWidget);
      expect(find.byIcon(Icons.warning_amber_rounded), findsOneWidget);
    });

    testWidgets('đã khóa', (tester) async {
      await tester.pumpWidget(wrap(DeadlineBanner(deadline: now.subtract(const Duration(minutes: 1)), now: now)));
      expect(find.text('Đợt đăng ký đã đóng'), findsOneWidget);
      expect(find.byIcon(Icons.lock_outline), findsOneWidget);
    });
  });

  testWidgets('TeamRequirementBar đầy đủ: báo thiếu chuyên ngành thứ hai, đọc được bằng trình đọc màn hình', (tester) async {
    final semantics = tester.ensureSemantics();
    await tester.pumpWidget(wrap(const TeamRequirementBar(memberCount: 4, specializationCount: 1, full: true)));

    expect(find.text('4/6 thành viên'), findsOneWidget);
    expect(find.bySemanticsLabel('4–6 thành viên: đạt'), findsOneWidget);
    expect(find.bySemanticsLabel('Ít nhất 2 chuyên ngành: chưa đạt'), findsOneWidget);
    semantics.dispose();
  });

  testWidgets('LoadingButton đang gửi: khóa bấm và giữ nhãn cho bố cục', (tester) async {
    var taps = 0;
    await tester.pumpWidget(wrap(LoadingButton(label: 'Gửi', loading: true, onPressed: () => taps++)));
    await tester.tap(find.byType(FilledButton));
    expect(taps, 0);
    expect(find.byType(CircularProgressIndicator), findsOneWidget);
  });

  testWidgets('ErrorBanner có nút Thử lại', (tester) async {
    var retried = false;
    await tester.pumpWidget(wrap(ErrorBanner(message: 'Không gửi được', onRetry: () => retried = true)));
    await tester.tap(find.text('Thử lại'));
    expect(retried, isTrue);
  });
}
