import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/phase.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/discover/posts_api.dart';
import 'package:exe_team_matching/features/discover/seeking_posts_screen.dart';
import 'package:exe_team_matching/features/my_team/my_team_api.dart';
import 'package:exe_team_matching/features/my_team/my_team_screen.dart';
import 'package:exe_team_matching/features/my_team/recruitment_post_screen.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

const _marketing = CatalogRef(id: 2, code: 'MKT', name: 'Marketing');
const _d05 = CatalogRef(id: 5, code: 'D05', name: 'Giáo dục');

final _post = SeekingPost(
  id: 'p1',
  title: 'Frontend',
  description: 'Làm giao diện React',
  student: const StudentRef(id: 's9', studentCode: 'SE180009', fullName: 'Nguyễn An', specialization: _marketing),
  domainIds: const [5],
);

MyTeam _team({bool leader = true, RecruitmentPost? recruitment}) => MyTeam(
      id: 't1',
      name: 'Nhóm Bình Minh',
      status: 'DRAFT',
      courseCode: 'EXE101',
      members: const [],
      domains: const [],
      isLeader: leader,
      memberCount: 3,
      specializationCount: 1,
      relation: leader ? 'LEADER' : 'MEMBER',
      recruitmentPost: recruitment,
    );

class _FakePosts extends PostsApi {
  _FakePosts({this.items = const [], this.mine}) : super(Dio());

  final List<SeekingPost> items;
  final SeekingPost? mine;
  final calls = <String>[];

  @override
  Future<SeekingPage> seeking({int? specializationId, required int page}) async =>
      SeekingPage(items: items.where((p) => specializationId == null || p.student.specialization?.id == specializationId).toList(), page: 1, totalPages: 1);

  @override
  Future<SeekingPost?> mySeeking() async => mine;

  @override
  Future<SeekingPost> saveMySeeking({required String title, String? description, required List<int> domainIds}) async {
    calls.add('seeking $title $domainIds');
    return _post;
  }

  @override
  Future<void> removeMySeeking() async => calls.add('remove seeking');

  @override
  Future<RecruitmentPost> saveRecruitment(String teamId, {required String title, String? description, required List<int> specializationIds}) async {
    calls.add('recruit $teamId $title $specializationIds');
    return RecruitmentPost(id: 'r1', title: title, specializationIds: specializationIds);
  }

  @override
  Future<void> closeRecruitment(String teamId) async => calls.add('close $teamId');
}

Future<void> _pump(WidgetTester tester, Widget screen, {required _FakePosts api, MyTeam? myTeam}) async {
  final router = GoRouter(initialLocation: '/sv/man', routes: [
    GoRoute(path: '/sv', builder: (_, _) => const Text('S01'), routes: [
      GoRoute(path: 'man', builder: (_, _) => screen),
      GoRoute(path: 'sinh-vien/:id', builder: (_, s) => Text('S04 ${s.pathParameters['id']}')),
    ]),
  ]);
  final overrides = <Override>[
    shellPhaseProvider.overrideWithValue(const ShellPhase(isTeamFormation: true, isMentoringWindow: false)),
    postsApiProvider.overrideWithValue(api),
    myTeamDetailProvider.overrideWith((ref) async => myTeam),
    specializationsProvider.overrideWith((ref) async => const [_marketing]),
    domainsProvider.overrideWith((ref) async => const [_d05]),
  ];
  await tester.pumpWidget(ProviderScope(overrides: overrides, child: MaterialApp.router(theme: AppTheme.light(), routerConfig: router)));
  await tester.pumpAndSettle();
}

void main() {
  setUpAll(() => AppTheme.useGoogleFonts = false);

  testWidgets('S10: chưa có nhóm thấy lối đăng bài, thẻ có chuyên ngành, vai trò, domain; không có nút mời', (tester) async {
    await _pump(tester, const Scaffold(body: SeekingPostsTab()), api: _FakePosts(items: [_post]));
    expect(find.text('Đăng bài "Tôi đang tìm nhóm"'), findsOneWidget);
    expect(find.text('Nguyễn An'), findsOneWidget);
    expect(find.text('SE180009 · Marketing'), findsOneWidget);
    expect(find.text('Vai trò: Frontend'), findsOneWidget);
    expect(find.text('D05'), findsOneWidget);
    expect(find.text('Mời vào nhóm'), findsNothing);

    await tester.tap(find.text('Nguyễn An'));
    await tester.pumpAndSettle();
    expect(find.text('S04 ${_post.student.id}'), findsOneWidget);
  });

  testWidgets('S10: trưởng nhóm còn chỗ thấy nút Mời vào nhóm; lọc chuyên ngành không khớp → trống', (tester) async {
    await _pump(tester, const Scaffold(body: SeekingPostsTab()), api: _FakePosts(items: [_post.copyWithSpecialization(null)]), myTeam: _team());
    expect(find.text('Mời vào nhóm'), findsOneWidget);
    expect(find.text('Đăng bài "Tôi đang tìm nhóm"'), findsNothing);
    await tester.tap(find.widgetWithText(ChoiceChip, 'Marketing'));
    await tester.pumpAndSettle();
    expect(find.text('Chưa có sinh viên đang tìm nhóm'), findsOneWidget);
  });

  testWidgets('S11: thiếu vai trò báo lỗi tại ô; nhập đủ thì đăng kèm domain và quay lại', (tester) async {
    final api = _FakePosts();
    await _pump(tester, const SeekingPostFormScreen(), api: api);
    expect(find.text('Gỡ bài'), findsNothing);
    await tester.tap(find.text('Đăng bài'));
    await tester.pumpAndSettle();
    expect(find.text('Nhập vai trò mong muốn.'), findsOneWidget);

    await tester.enterText(find.widgetWithText(TextField, 'Vai trò mong muốn'), 'Backend');
    await tester.tap(find.widgetWithText(FilterChip, 'D05'));
    await tester.tap(find.text('Đăng bài'));
    await tester.pumpAndSettle();
    expect(api.calls, ['seeking Backend [5]']);
    expect(find.text('S01'), findsOneWidget);
  });

  testWidgets('S11: đã có bài → Gỡ bài cần xác nhận', (tester) async {
    final api = _FakePosts(mine: _post);
    await _pump(tester, const SeekingPostFormScreen(), api: api);
    expect(find.text('Sửa bài tìm nhóm'), findsOneWidget);
    await tester.tap(find.widgetWithText(TextButton, 'Gỡ bài'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Gỡ bài'));
    await tester.pumpAndSettle();
    expect(api.calls, ['remove seeking']);
  });

  testWidgets('S21: số chỗ trống theo sĩ số; sửa bài giữ dữ liệu cũ, lưu kèm chuyên ngành; đóng bài cần xác nhận', (tester) async {
    final api = _FakePosts();
    await _pump(tester, const RecruitmentPostScreen(), api: api,
        myTeam: _team(recruitment: const RecruitmentPost(id: 'r1', title: 'Cần BA', specializationIds: [])));
    expect(find.text('Còn 3 chỗ trống'), findsOneWidget);
    expect(find.text('Cần BA'), findsOneWidget);
    await tester.tap(find.widgetWithText(FilterChip, 'Marketing'));
    await tester.tap(find.text('Lưu'));
    await tester.pumpAndSettle();
    expect(api.calls, ['recruit t1 Cần BA [2]']);
  });

  testWidgets('S21: thành viên không phải trưởng nhóm thì không đăng bài', (tester) async {
    await _pump(tester, const RecruitmentPostScreen(), api: _FakePosts(), myTeam: _team(leader: false));
    expect(find.text('Chỉ trưởng nhóm đăng bài tuyển'), findsOneWidget);
  });

  testWidgets('S15: trưởng nhóm có lối tắt Bài tuyển thành viên, ghi Đang mở khi có bài', (tester) async {
    await _pump(tester, const Scaffold(body: MyTeamScreen()), api: _FakePosts(),
        myTeam: _team(recruitment: const RecruitmentPost(id: 'r1', title: 'Cần BA')));
    await tester.scrollUntilVisible(find.text('Bài tuyển thành viên'), 200);
    expect(find.text('Đang mở'), findsOneWidget);
  });
}

extension on SeekingPost {
  SeekingPost copyWithSpecialization(CatalogRef? specialization) => SeekingPost(
        id: id,
        title: title,
        description: description,
        student: StudentRef(id: student.id, studentCode: student.studentCode, fullName: student.fullName, specialization: specialization),
        domainIds: domainIds,
      );
}
