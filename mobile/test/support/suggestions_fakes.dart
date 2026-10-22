import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/theme/app_theme.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

/// Dữ liệu và API giả cho test màn hình gợi ý (S07, S18, S19, S30, S31).
const spec = CatalogRef(id: 5, code: 'GD', name: 'Thiết kế đồ họa');
StudentRef student(String id, String name) => StudentRef(id: id, studentCode: 'SE18$id', fullName: name, specialization: spec);

TeamDetail team({bool leader = true, int members = 3}) => TeamDetail(
      card: TeamCard(id: 't1', name: 'EduSpark', status: 'ACTIVE', memberCount: members),
      members: const [],
      isLeader: leader,
    );

class FakeSuggestionsApi extends SuggestionsApi {
  FakeSuggestionsApi({this.team, this.teams = const [], this.members, this.students = const []}) : super(Dio());

  final TeamDetail? team;
  final List<TeamSuggestion> teams;
  final MemberSuggestions? members;
  final List<StudentRef> students;
  final invites = <String>[];

  @override
  Future<TeamDetail?> myTeam() async => team;

  @override
  Future<List<TeamSuggestion>> teamsForMe() async => teams;

  @override
  Future<MemberSuggestions> membersFor(String teamId) async => members!;

  @override
  Future<List<StudentRef>> searchStudents(String search, int? specializationId) async =>
      students.where((s) => s.fullName.toLowerCase().contains(search.toLowerCase())).toList();

  @override
  Future<List<CatalogRef>> specializations() async => const [spec];

  @override
  Future<void> invite(String teamId, String studentId, {String? message}) async => invites.add('$teamId $studentId ${message ?? ''}');
}

Future<void> pump(WidgetTester tester, Widget screen, FakeSuggestionsApi api) async {
  await tester.pumpWidget(ProviderScope(
    overrides: [suggestionsApiProvider.overrideWithValue(api)],
    child: MaterialApp(theme: AppTheme.light(), home: screen),
  ));
  await tester.pumpAndSettle();
}
