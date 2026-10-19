import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:exe_team_matching/app/app.dart';

void main() {
  testWidgets('Ứng dụng khởi động và hiện tên ứng dụng', (tester) async {
    await tester.pumpWidget(const ProviderScope(child: ExeTeamMatchingApp()));
    await tester.pumpAndSettle();

    expect(find.text('EXE Team Matching'), findsOneWidget);
  });
}
