import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/network/api_client.dart';
import '../../core/providers.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../suggestions/suggestions_api.dart';
import 'paths.dart';
import 'match_reason.dart';

export 'paths.dart' show teamLecturerPath;
class TeamLecturerData {
  const TeamLecturerData({this.assignment, this.preferences});
  final Map<String, dynamic>? assignment;
  final Map<String, dynamic>? preferences;
}

class TeamLecturerApi {
  const TeamLecturerApi(this.dio);
  final Dio dio;
  Future<TeamLecturerData> load(String teamId) async {
    try {
      final assignment = (await dio.get<Map<String, dynamic>>('/api/teams/$teamId/lecturer')).data;
      if (assignment != null) return TeamLecturerData(assignment: assignment);
      final preferences = (await dio.get<Map<String, dynamic>>('/api/teams/$teamId/lecturer-preferences')).data;
      if (preferences == null) throw const FormatException('Thiếu dữ liệu nguyện vọng');
      return TeamLecturerData(preferences: preferences);
    } on DioException catch (error) {
      throw error.apiError;
    }
  }
}

final teamLecturerApiProvider = Provider<TeamLecturerApi>((ref) => TeamLecturerApi(ref.watch(dioProvider)));
final teamLecturerDetailsProvider = FutureProvider.autoDispose.family<TeamLecturerData, String>(
  (ref, teamId) => ref.watch(teamLecturerApiProvider).load(teamId),
);



class TeamLecturerScreen extends ConsumerWidget {
  const TeamLecturerScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final team = ref.watch(myTeamProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Giảng viên của nhóm')),
      body: team.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ErrorState(error: error, onRetry: () => ref.invalidate(myTeamProvider)),
        data: (value) {
          if (value == null) return const EmptyState(title: 'Bạn chưa có nhóm');
          final teamId = value.card.id;
          return ref.watch(teamLecturerDetailsProvider(teamId)).when(
                loading: () => const Center(child: CircularProgressIndicator()),
                error: (error, _) => ErrorState(error: error, onRetry: () => ref.invalidate(teamLecturerDetailsProvider(teamId))),
                data: (data) => _content(context, ref, teamId, data, value.isLeader),
              );
        },
      ),
    );
  }

  Widget _content(BuildContext context, WidgetRef ref, String teamId, TeamLecturerData data, bool isLeader) {
    final assignment = data.assignment;
    final theme = Theme.of(context);
    if (assignment != null) {
      final reason = lecturerMatchReason(assignment['reason'] as String?, assignment['preferenceRank'] as int?) ?? 'Đã phân công';
      return ListView(padding: const EdgeInsets.all(AppSpacing.s4), children: [
        Text('Giảng viên hướng dẫn', style: theme.textTheme.titleLarge),
        const SizedBox(height: AppSpacing.s3),
        Card(child: ListTile(
          leading: const Icon(Icons.school_outlined),
          title: Text(assignment['lecturerName'] as String? ?? 'Giảng viên'),
          subtitle: Text(reason),
        )),
        const SizedBox(height: AppSpacing.s3),
        const Text('Nhóm đã được ghép, nguyện vọng không thể chỉnh sửa.'),
      ]);
    }
    final preferences = data.preferences!;
    final items = (preferences['preferences'] as List? ?? const []).cast<Map<String, dynamic>>();
    final deadline = DateTime.tryParse(preferences['lockAt'] as String? ?? '')?.toLocal();
    final locked = preferences['locked'] == true;
    return ListView(padding: const EdgeInsets.all(AppSpacing.s4), children: [
      Text('Chưa ghép giảng viên', style: theme.textTheme.titleLarge),
      const SizedBox(height: AppSpacing.s2),
      if (deadline != null) Text('Hạn chốt: ${deadline.day.toString().padLeft(2, '0')}/${deadline.month.toString().padLeft(2, '0')}/${deadline.year}'),
      if (locked) const Text('Đợt chọn nguyện vọng đã đóng.'),
      const SizedBox(height: AppSpacing.s4),
      Text('Nguyện vọng của nhóm', style: theme.textTheme.titleMedium),
      if (items.isEmpty) const Padding(
        padding: EdgeInsets.symmetric(vertical: AppSpacing.s4),
        child: Text('Nhóm chưa chọn nguyện vọng giảng viên.'),
      ),
      for (final item in items)
        Card(child: ListTile(
          leading: CircleAvatar(child: Text('${item['rank'] ?? '-'}')),
          title: Text(item['fullName'] as String? ?? 'Giảng viên'),
          subtitle: item['isInterested'] == true ? const Text('Quan tâm nhóm bạn') : null,
        )),
      if (isLeader && !locked) ...[
        const SizedBox(height: AppSpacing.s4),
        FilledButton(onPressed: () async {
          await context.push(lecturerPreferencesPath);
          if (context.mounted) ref.invalidate(teamLecturerDetailsProvider(teamId));
        }, child: const Text('Chỉnh sửa nguyện vọng')),
      ],
    ]);
  }
}
