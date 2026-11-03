import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/network/api_client.dart';
import '../../core/providers.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../suggestions/suggestions_api.dart';
import 'lecturers_api.dart' show lecturersApiProvider;
import 'match_reason.dart';
import 'paths.dart';

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
      final assignment =
          (await dio.get<Map<String, dynamic>>('/api/teams/$teamId/lecturer'))
              .data;
      if (assignment != null) return TeamLecturerData(assignment: assignment);
      final preferences = (await dio
              .get<Map<String, dynamic>>('/api/teams/$teamId/lecturer-preferences'))
          .data;
      if (preferences == null) {
        throw const FormatException('Thiếu dữ liệu nguyện vọng');
      }
      return TeamLecturerData(preferences: preferences);
    } on DioException catch (error) {
      throw error.apiError;
    }
  }
}

final teamLecturerApiProvider =
    Provider<TeamLecturerApi>((ref) => TeamLecturerApi(ref.watch(dioProvider)));

final teamLecturerDetailsProvider =
    FutureProvider.autoDispose.family<TeamLecturerData, String>(
  (ref, teamId) => ref.watch(teamLecturerApiProvider).load(teamId),
);

/// S25: Màn hình giảng viên của nhóm (Quy tắc 9, 19, 26).
/// Trước khi ghép: hiển thị nguyện vọng 1–3, giảng viên quan tâm nhóm bạn, nút thêm nhanh vào nguyện vọng.
/// Sau khi ghép: hiển thị giảng viên hướng dẫn + MatchReasonChip, nguyện vọng bị khóa.
class TeamLecturerScreen extends ConsumerWidget {
  const TeamLecturerScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final team = ref.watch(myTeamProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Giảng viên của nhóm')),
      body: team.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) =>
            ErrorState(error: error, onRetry: () => ref.invalidate(myTeamProvider)),
        data: (value) {
          if (value == null) return const EmptyState(title: 'Bạn chưa có nhóm');
          final teamId = value.card.id;
          return ref.watch(teamLecturerDetailsProvider(teamId)).when(
                loading: () => const Center(child: CircularProgressIndicator()),
                error: (error, _) => ErrorState(
                  error: error,
                  onRetry: () =>
                      ref.invalidate(teamLecturerDetailsProvider(teamId)),
                ),
                data: (data) =>
                    _content(context, ref, teamId, data, value.isLeader),
              );
        },
      ),
    );
  }

  Widget _content(
    BuildContext context,
    WidgetRef ref,
    String teamId,
    TeamLecturerData data,
    bool isLeader,
  ) {
    final assignment = data.assignment;
    final theme = Theme.of(context);

    if (assignment != null) {
      final reason = lecturerMatchReason(
            assignment['reason'] as String?,
            assignment['preferenceRank'] as int?,
          ) ??
          'Đã phân công';
      return ListView(
        padding: const EdgeInsets.all(AppSpacing.s4),
        children: [
          Text('Giảng viên hướng dẫn', style: theme.textTheme.titleLarge),
          const SizedBox(height: AppSpacing.s3),
          Card(
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(AppRadius.md),
              side: BorderSide(color: theme.colorScheme.outlineVariant),
            ),
            child: ListTile(
              leading: CircleAvatar(
                backgroundColor: theme.colorScheme.primaryContainer,
                child: Icon(
                  Icons.school_outlined,
                  color: theme.colorScheme.primary,
                ),
              ),
              title: Text(
                assignment['lecturerName'] as String? ?? 'Giảng viên',
                style: const TextStyle(fontWeight: FontWeight.bold),
              ),
              subtitle: Text(reason),
            ),
          ),
          const SizedBox(height: AppSpacing.s3),
          Container(
            padding: const EdgeInsets.all(AppSpacing.s3),
            decoration: BoxDecoration(
              color: theme.colorScheme.surfaceContainerHighest,
              borderRadius: BorderRadius.circular(AppRadius.md),
            ),
            child: Row(
              children: [
                const Icon(Icons.lock_outline, size: 18),
                const SizedBox(width: AppSpacing.s2),
                Expanded(
                  child: Text(
                    'Nhóm đã được ghép, nguyện vọng không thể chỉnh sửa.',
                    style: theme.textTheme.bodyMedium,
                  ),
                ),
              ],
            ),
          ),
          if (data.preferences != null &&
              (data.preferences!['preferences'] as List? ?? const []).isNotEmpty) ...[
            const SizedBox(height: AppSpacing.s4),
            Text('Nguyện vọng đã chọn', style: theme.textTheme.titleMedium),
            const SizedBox(height: AppSpacing.s2),
            for (final item in (data.preferences!['preferences'] as List)
                .cast<Map<String, dynamic>>())
              Card(
                margin: const EdgeInsets.only(bottom: AppSpacing.s2),
                child: ListTile(
                  leading: CircleAvatar(child: Text('${item['rank'] ?? '-'}')),
                  title: Text(item['fullName'] as String? ?? 'Giảng viên'),
                  subtitle: item['isInterested'] == true
                      ? const Text('Quan tâm nhóm bạn')
                      : null,
                ),
              ),
          ],
        ],
      );
    }

    final preferences = data.preferences!;
    final items =
        (preferences['preferences'] as List? ?? const []).cast<Map<String, dynamic>>();
    final interestedLecturers = (preferences['interestedLecturers'] as List? ??
            const [])
        .cast<Map<String, dynamic>>();
    final deadline =
        DateTime.tryParse(preferences['lockAt'] as String? ?? '')?.toLocal();
    final locked = preferences['locked'] == true;

    return ListView(
      padding: const EdgeInsets.all(AppSpacing.s4),
      children: [
        Text('Chưa ghép giảng viên', style: theme.textTheme.titleLarge),
        const SizedBox(height: AppSpacing.s2),
        if (deadline != null)
          Text(
            'Hạn chốt: ${deadline.day.toString().padLeft(2, '0')}/${deadline.month.toString().padLeft(2, '0')}/${deadline.year}',
          ),
        if (locked) const Text('Đợt chọn nguyện vọng đã đóng.'),
        const SizedBox(height: AppSpacing.s4),

        // Section: Nguyện vọng của nhóm
        Text('Nguyện vọng của nhóm', style: theme.textTheme.titleMedium),
        if (items.isEmpty)
          const Padding(
            padding: EdgeInsets.symmetric(vertical: AppSpacing.s4),
            child: Text('Nhóm chưa chọn nguyện vọng giảng viên.'),
          ),
        for (final item in items)
          Card(
            margin: const EdgeInsets.only(bottom: AppSpacing.s2),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(AppRadius.md),
              side: BorderSide(color: theme.colorScheme.outlineVariant),
            ),
            child: ListTile(
              leading: CircleAvatar(child: Text('${item['rank'] ?? '-'}')),
              title: Text(
                item['fullName'] as String? ?? 'Giảng viên',
                style: const TextStyle(fontWeight: FontWeight.bold),
              ),
              subtitle: item['isInterested'] == true
                  ? const Text('Quan tâm nhóm bạn')
                  : null,
            ),
          ),

        // Section: Giảng viên quan tâm nhóm bạn
        if (interestedLecturers.isNotEmpty) ...[
          const SizedBox(height: AppSpacing.s4),
          Row(
            children: [
              Text(
                'Quan tâm nhóm bạn',
                style: theme.textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.bold,
                ),
              ),
              const SizedBox(width: AppSpacing.s2),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                decoration: BoxDecoration(
                  color: Palette.infoContainer,
                  borderRadius: BorderRadius.circular(AppRadius.full),
                ),
                child: Text(
                  '${interestedLecturers.length}',
                  style: theme.textTheme.labelSmall?.copyWith(
                    color: Palette.infoDark,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.s1),
          Text(
            'Các giảng viên đã mở quan tâm đến đề tài hoặc nhóm bạn',
            style: theme.textTheme.bodySmall?.copyWith(
              color: theme.colorScheme.onSurfaceVariant,
            ),
          ),
          const SizedBox(height: AppSpacing.s2),
          for (final lecturer in interestedLecturers)
            _buildInterestedLecturerCard(
              context,
              ref,
              teamId: teamId,
              lecturer: lecturer,
              currentPreferences: items,
              isLeader: isLeader,
              locked: locked,
            ),
        ],

        // Edit preferences button
        if (isLeader && !locked) ...[
          const SizedBox(height: AppSpacing.s4),
          FilledButton(
            onPressed: () async {
              await context.push(lecturerPreferencesPath);
              if (context.mounted) {
                ref.invalidate(teamLecturerDetailsProvider(teamId));
              }
            },
            child: const Text('Chỉnh sửa nguyện vọng'),
          ),
        ],
      ],
    );
  }

  Widget _buildInterestedLecturerCard(
    BuildContext context,
    WidgetRef ref, {
    required String teamId,
    required Map<String, dynamic> lecturer,
    required List<Map<String, dynamic>> currentPreferences,
    required bool isLeader,
    required bool locked,
  }) {
    final theme = Theme.of(context);
    final lecturerId =
        (lecturer['lecturerId'] ?? lecturer['id'] ?? '').toString();
    final fullName = (lecturer['fullName'] ?? 'Giảng viên').toString();
    final domains =
        (lecturer['domains'] as List? ?? const []).cast<Map<String, dynamic>>();
    final currentIds = currentPreferences
        .map((p) => (p['lecturerId'] ?? p['id'] ?? '').toString())
        .toList();
    final prefIndex = currentIds.indexOf(lecturerId);
    final isSelected = prefIndex >= 0;
    final canAdd = isLeader && !locked && currentIds.length < 3 && !isSelected;

    return Card(
      elevation: 0,
      margin: const EdgeInsets.only(bottom: AppSpacing.s2),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.md),
        side: BorderSide(color: theme.colorScheme.outlineVariant),
      ),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.s3),
        child: Row(
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    fullName,
                    style: theme.textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  if (domains.isNotEmpty) ...[
                    const SizedBox(height: AppSpacing.s1),
                    Wrap(
                      spacing: 4,
                      children: [
                        for (final d in domains.take(2))
                          Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 6,
                              vertical: 2,
                            ),
                            decoration: BoxDecoration(
                              color: theme.colorScheme.surfaceContainerHighest,
                              borderRadius: BorderRadius.circular(AppRadius.sm),
                            ),
                            child: Text(
                              d['name']?.toString() ??
                                  d['code']?.toString() ??
                                  '',
                              style: theme.textTheme.labelSmall,
                            ),
                          ),
                      ],
                    ),
                  ],
                ],
              ),
            ),
            if (isSelected)
              Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                decoration: BoxDecoration(
                  color: Palette.successContainer,
                  borderRadius: BorderRadius.circular(AppRadius.sm),
                ),
                child: Text(
                  'Đã chọn (NV #${prefIndex + 1})',
                  style: theme.textTheme.labelSmall?.copyWith(
                    color: Palette.success,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              )
            else if (canAdd)
              FilledButton.tonal(
                onPressed: () async {
                  final newIds = [...currentIds, lecturerId];
                  try {
                    await ref
                        .read(lecturersApiProvider)
                        .updatePreferences(teamId, newIds);
                    ref.invalidate(teamLecturerDetailsProvider(teamId));
                    if (context.mounted) {
                      ScaffoldMessenger.of(context).hideCurrentSnackBar();
                      ScaffoldMessenger.of(context).showSnackBar(
                        SnackBar(
                          content: Text(
                            'Đã thêm $fullName vào nguyện vọng ${newIds.length}',
                          ),
                          duration: const Duration(seconds: 5),
                          action: SnackBarAction(
                            label: 'Hoàn tác',
                            onPressed: () async {
                              try {
                                await ref
                                    .read(lecturersApiProvider)
                                    .updatePreferences(teamId, currentIds);
                                ref.invalidate(
                                  teamLecturerDetailsProvider(teamId),
                                );
                              } catch (_) {}
                            },
                          ),
                        ),
                      );
                    }
                  } catch (e) {
                    if (context.mounted) {
                      ScaffoldMessenger.of(context).showSnackBar(
                        SnackBar(
                          content: Text(
                            e is Exception
                                ? e.toString().replaceAll('Exception: ', '')
                                : 'Không thể thêm nguyện vọng',
                          ),
                          backgroundColor: theme.colorScheme.error,
                        ),
                      );
                    }
                  }
                },
                child: const Text('Thêm vào nguyện vọng'),
              ),
          ],
        ),
      ),
    );
  }
}
