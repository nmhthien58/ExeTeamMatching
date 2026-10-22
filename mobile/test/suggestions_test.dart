import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/round_closed/round_closed_screen.dart';
import 'package:exe_team_matching/features/suggestions/invite_students_screen.dart';
import 'package:exe_team_matching/features/suggestions/suggested_members_screen.dart';
import 'package:exe_team_matching/features/suggestions/suggested_teams_screen.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/suggestions_fakes.dart';

void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  test('mức phù hợp là nhãn, không phải %', () {
    expect(fitLabel(0.72), 'Rất phù hợp');
    expect(fitLabel(0.4), 'Phù hợp');
    expect(fitLabel(0.1), 'Có thể phù hợp');
  });

  testWidgets('S07: hồ sơ chưa đủ dữ liệu → mời hoàn thiện hồ sơ', (tester) async {
    await pump(tester, const SuggestedTeamsScreen(), FakeSuggestionsApi());
    expect(find.text('Chưa đủ dữ liệu để gợi ý'), findsOneWidget);
    expect(find.text('Hoàn thiện hồ sơ'), findsOneWidget);
  });

  testWidgets('S07: thẻ nhóm có mức phù hợp, số chỗ còn lại và lý do', (tester) async {
    await pump(
      tester,
      const SuggestedTeamsScreen(),
      FakeSuggestionsApi(teams: const [
        TeamSuggestion(team: TeamCard(id: 't2', name: 'GreenLoop', status: 'ACTIVE', memberCount: 4), score: 0.7, reasons: ['Nhóm đang cần chuyên ngành của bạn']),
      ]),
    );
    expect(find.text('GreenLoop'), findsOneWidget);
    expect(find.text('Rất phù hợp'), findsOneWidget);
    expect(find.text('Còn 2 chỗ'), findsOneWidget);
    expect(find.text('Nhóm đang cần chuyên ngành của bạn'), findsOneWidget);
  });

  testWidgets('S18: trưởng nhóm gửi lời mời kèm lời nhắn, thẻ chuyển "Đã mời"', (tester) async {
    final api = FakeSuggestionsApi(
      team: team(),
      members: MemberSuggestions(missingSpecializations: const [spec], items: [
        MemberSuggestion(student: student('01', 'Ngô Bảo Anh'), skills: const ['UI/UX 5/5'], score: 0.7, reasons: const ['Bù chuyên ngành nhóm đang thiếu'], alreadyInvited: false),
      ]),
    );
    await pump(tester, const SuggestedMembersScreen(), api);

    expect(find.text('Nhóm bạn đang thiếu: Thiết kế đồ họa'), findsOneWidget);
    await tester.tap(find.text('Gửi lời mời'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField), 'Nhóm cần bạn làm UI');
    await tester.tap(find.widgetWithText(FilledButton, 'Gửi lời mời'));
    await tester.pumpAndSettle();

    expect(api.invites, ['t1 01 Nhóm cần bạn làm UI']);
    expect(find.text('Đã mời'), findsOneWidget);
    expect(find.text('Đã gửi lời mời'), findsOneWidget);
  });

  testWidgets('S18: thành viên thường không thấy nút mời', (tester) async {
    final api = FakeSuggestionsApi(
      team: team(leader: false),
      members: MemberSuggestions(missingSpecializations: const [], items: [
        MemberSuggestion(student: student('01', 'Ngô Bảo Anh'), skills: const [], score: 0.5, reasons: const [], alreadyInvited: false),
      ]),
    );
    await pump(tester, const SuggestedMembersScreen(), api);

    expect(find.text('Chỉ trưởng nhóm gửi được lời mời.'), findsOneWidget);
    expect(find.text('Gửi lời mời'), findsNothing);
  });

  testWidgets('S19: nhóm đủ 6 khóa nút mời; tìm không thấy có câu hướng dẫn', (tester) async {
    final api = FakeSuggestionsApi(team: team(members: 6), students: [student('01', 'Ngô Bảo Anh')]);
    await pump(tester, const InviteStudentsScreen(), api);

    expect(find.text('Nhóm đã đủ 6 người'), findsOneWidget);
    final button = tester.widget<OutlinedButton>(find.ancestor(of: find.text('Gửi lời mời'), matching: find.byWidgetPredicate((w) => w is OutlinedButton)));
    expect(button.onPressed, isNull);

    await tester.enterText(find.byType(TextField), 'không có ai');
    await tester.pump(const Duration(milliseconds: 400));
    await tester.pumpAndSettle();
    expect(find.text('Không tìm thấy sinh viên chưa có nhóm'), findsOneWidget);
  });

  testWidgets('S30: chưa có nhóm → được ghép tự động', (tester) async {
    await pump(tester, const RoundClosedScreen(), FakeSuggestionsApi());
    expect(find.text('Bạn sẽ được ghép nhóm tự động'), findsOneWidget);
    expect(find.text('Đợt đăng ký đã đóng'), findsOneWidget);
  });
}
