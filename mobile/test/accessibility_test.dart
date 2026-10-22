import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/files/files_screen.dart';
import 'package:exe_team_matching/features/suggestions/suggested_members_screen.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart';
import 'package:exe_team_matching/features/tasks/task_board_screen.dart';
import 'package:exe_team_matching/features/workspace/project_overview_screen.dart';
import 'package:exe_team_matching/features/workspace/workspace_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/suggestions_fakes.dart' as s;
import 'support/workspace_fakes.dart' as w;

/// MB-36 – checklist flutter-handoff §5 cho màn hình của Nguyên: 360 và 412 dp, chữ hệ thống 130% không tràn,
/// vùng chạm ≥ 48, nút có nhãn, chữ đạt tương phản.
void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  final fakeCollab = w.FakeCollabApi(
    workspaces: [w.workspace()],
    board: [
      w.task('a', assignees: [w.me], dueAt: DateTime.now().add(const Duration(days: 1))),
      w.task('b', status: TaskStatus.doing, deliverable: true, dueAt: DateTime.now().subtract(const Duration(days: 1))),
    ],
    folderContent: FolderContent(
      canUpload: true,
      folders: const [Folder(id: 'w0', name: 'EXE101 (kỳ trước)', isArchive: true, itemCount: 3)],
      files: [ProjectFile(id: 'f1', name: 'bao-cao-tuan-6-ban-cuoi-cung.pdf', currentVersion: 2, sizeBytes: 1024, updatedByName: 'Trần Minh Khoa', updatedAt: DateTime.now())],
    ),
  );
  final fakeSuggestions = s.FakeSuggestionsApi(
    team: s.team(),
    members: MemberSuggestions(missingSpecializations: const [s.spec], items: [
      MemberSuggestion(
        student: s.student('01', 'Nguyễn Hoàng Bảo Anh'),
        skills: const ['UI/UX 5/5', 'Marketing 4/5'],
        score: 0.7,
        reasons: const ['Bù chuyên ngành nhóm đang thiếu', 'Mạnh Thiết kế UI/UX (5/5) – kỹ năng nhóm đang yếu'],
        alreadyInvited: false,
      ),
    ]),
  );

  final screens = <String, Widget>{
    'C01': const ProjectOverviewScreen(basePath: '/sv/du-an'),
    'C04': const TaskBoardScreen(basePath: '/sv/du-an'),
    'C06': const FilesScreen(),
    'S18': const SuggestedMembersScreen(),
  };

  for (final size in const [Size(360, 800), Size(412, 915)]) {
    for (final entry in screens.entries) {
      testWidgets('${entry.key} ở ${size.width.toInt()} dp, chữ 130%', (tester) async {
        tester.view.physicalSize = size * 3;
        tester.view.devicePixelRatio = 3;
        addTearDown(tester.view.reset);
        final semantics = tester.ensureSemantics();

        await tester.pumpWidget(ProviderScope(
          overrides: [
            w.collabApiOverride(fakeCollab),
            w.authOverride(),
            suggestionsApiProvider.overrideWithValue(fakeSuggestions),
          ],
          child: MaterialApp(
            theme: AppTheme.light(),
            builder: (context, child) => MediaQuery(
              data: MediaQuery.of(context).copyWith(textScaler: const TextScaler.linear(1.3)),
              child: child!,
            ),
            home: entry.value,
          ),
        ));
        await tester.pumpAndSettle();

        // Không tràn bố cục (RenderFlex overflow sẽ là exception).
        expect(tester.takeException(), isNull);
        await expectLater(tester, meetsGuideline(androidTapTargetGuideline));
        await expectLater(tester, meetsGuideline(labeledTapTargetGuideline));
        await expectLater(tester, meetsGuideline(textContrastGuideline));
        semantics.dispose();
      });
    }
  }
}
