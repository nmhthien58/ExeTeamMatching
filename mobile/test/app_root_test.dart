import 'package:exe_team_matching/app/app.dart';
import 'package:exe_team_matching/core/providers.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/main.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';

/// MB-47 – đăng xuất thì dựng lại ProviderScope: provider cache dữ liệu người dùng cũ bị tạo mới.
void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  testWidgets('gọi signedOutProvider → ProviderScope mới, provider được tạo lại', (tester) async {
    FlutterSecureStorage.setMockInitialValues({'exe.onboardingSeen': 'true'});
    await tester.pumpWidget(const AppRoot());
    await tester.pumpAndSettle();

    ProviderContainer container() => ProviderScope.containerOf(tester.element(find.byType(ExeTeamMatchingApp)));
    final before = container();
    final storeBefore = before.read(sessionStoreProvider);

    before.read(signedOutProvider)();
    await tester.pumpAndSettle();

    final after = container();
    expect(identical(after, before), isFalse);
    expect(identical(after.read(sessionStoreProvider), storeBefore), isFalse);
  });
}
