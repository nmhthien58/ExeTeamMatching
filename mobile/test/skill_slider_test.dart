import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/profile/profile_form.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

Future<void> _pump(WidgetTester tester, {int? value, double textScale = 1}) async {
  tester.view.physicalSize = const Size(360, 800);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(MaterialApp(
    theme: AppTheme.light(),
    home: MediaQuery(
      data: MediaQueryData(size: const Size(360, 800), textScaler: TextScaler.linear(textScale)),
      child: Scaffold(
        body: Padding(
          padding: const EdgeInsets.all(16),
          child: SkillSlider(label: 'Công nghệ', value: value, onChanged: (_) {}),
        ),
      ),
    ),
  ));
}

void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  testWidgets('có nhãn hai đầu thang theo Figma Skill/Slider', (tester) async {
    await _pump(tester, value: 4);
    expect(find.text('Mới bắt đầu'), findsOneWidget);
    expect(find.text('Thành thạo'), findsOneWidget);
    expect(find.text('4/5'), findsOneWidget);
    // "Mới bắt đầu" ở bên trái "Thành thạo".
    expect(tester.getCenter(find.text('Mới bắt đầu')).dx, lessThan(tester.getCenter(find.text('Thành thạo')).dx));
  });

  testWidgets('chưa đánh giá vẫn có nhãn', (tester) async {
    await _pump(tester);
    expect(find.text('Chưa đánh giá'), findsOneWidget);
    expect(find.text('Mới bắt đầu'), findsOneWidget);
  });

  testWidgets('chữ phóng to 200% không tràn', (tester) async {
    await _pump(tester, value: 3, textScale: 2);
    expect(tester.takeException(), isNull);
  });
}
