import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../my_team/my_team_api.dart' show myTeamDetailProvider;
import 'lecturers_api.dart';
import 'lecturers_models.dart';

class LecturerPreferencesScreen extends ConsumerStatefulWidget {
  const LecturerPreferencesScreen({super.key});

  @override
  ConsumerState<LecturerPreferencesScreen> createState() => _LecturerPreferencesScreenState();
}

class _LecturerPreferencesScreenState extends ConsumerState<LecturerPreferencesScreen> {
  String _searchQuery = '';
  final TextEditingController _searchController = TextEditingController();

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _updatePreferencesWithUndo(
    BuildContext context,
    String teamId,
    List<String> newIds,
    List<String> previousIds,
    String message,
  ) async {
    try {
      await ref.read(lecturersApiProvider).updatePreferences(teamId, newIds);
      ref.invalidate(lecturerPreferencesProvider(teamId));

      if (!context.mounted) return;
      ScaffoldMessenger.of(context).hideCurrentSnackBar();
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(message),
          duration: const Duration(seconds: 5),
          action: SnackBarAction(
            label: 'Hoàn tác',
            onPressed: () async {
              try {
                await ref.read(lecturersApiProvider).updatePreferences(teamId, previousIds);
                ref.invalidate(lecturerPreferencesProvider(teamId));
              } catch (_) {}
            },
          ),
        ),
      );
    } catch (e) {
      if (!context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(e is Exception ? e.toString().replaceAll('Exception: ', '') : 'Không thể cập nhật nguyện vọng'),
          backgroundColor: Theme.of(context).colorScheme.error,
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final myTeamAsync = ref.watch(myTeamDetailProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Nguyện vọng giảng viên'),
      ),
      body: myTeamAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (err, _) => ErrorState(
          error: err,
          onRetry: () => ref.invalidate(myTeamDetailProvider),
        ),
        data: (team) {
          if (team == null) {
            return const EmptyState(
              icon: Icons.groups_outlined,
              title: 'Bạn chưa có nhóm',
              description: 'Tạo hoặc tham gia một nhóm để đăng ký nguyện vọng giảng viên.',
            );
          }

          final isLeader = team.isLeader;
          final preferencesAsync = ref.watch(lecturerPreferencesProvider(team.id));
          final topicInterestsAsync = ref.watch(teamTopicInterestsProvider(team.id));
          final lecturersAsync = ref.watch(lecturersListProvider(_searchQuery));

          return preferencesAsync.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (err, _) => ErrorState(
              error: err,
              onRetry: () => ref.invalidate(lecturerPreferencesProvider(team.id)),
            ),
            data: (preferences) {
              final currentIds = preferences.map((p) => p.lecturer.id).toList();

              return RefreshIndicator(
                onRefresh: () async {
                  ref.invalidate(lecturerPreferencesProvider(team.id));
                  ref.invalidate(teamTopicInterestsProvider(team.id));
                  ref.invalidate(lecturersListProvider(_searchQuery));
                },
                child: ListView(
                  padding: const EdgeInsets.all(AppSpacing.s4),
                  children: [
                    // Banner tự lưu & quyền trưởng nhóm
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: AppSpacing.s4,
                        vertical: AppSpacing.s3,
                      ),
                      decoration: BoxDecoration(
                        color: theme.colorScheme.primaryContainer,
                        borderRadius: BorderRadius.circular(AppRadius.md),
                      ),
                      child: Row(
                        children: [
                          Icon(
                            Icons.check_circle_outline,
                            size: 20,
                            color: theme.colorScheme.primary,
                          ),
                          const SizedBox(width: AppSpacing.s3),
                          Expanded(
                            child: Text(
                              isLeader
                                  ? 'Đã tự lưu · Trưởng nhóm sửa được tới hạn chốt đợt lập nhóm'
                                  : 'Chỉ trưởng nhóm mới có quyền chọn và đổi thứ tự nguyện vọng',
                              style: theme.textTheme.bodyMedium?.copyWith(
                                color: theme.colorScheme.onPrimaryContainer,
                                fontWeight: FontWeight.w500,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: AppSpacing.s4),

                    // Section: 3 ô nguyện vọng có thứ tự
                    Text(
                      'Danh sách nguyện vọng (tối đa 3)',
                      style: theme.textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    const SizedBox(height: AppSpacing.s2),
                    for (int i = 0; i < 3; i++) ...[
                      if (i < preferences.length)
                        _buildPreferenceCard(
                          context,
                          teamId: team.id,
                          index: i,
                          pref: preferences[i],
                          allPreferences: preferences,
                          isLeader: isLeader,
                        )
                      else
                        _buildEmptySlot(context, priority: i + 1),
                      const SizedBox(height: AppSpacing.s2),
                    ],

                    const SizedBox(height: AppSpacing.s4),

                    // Section: Giảng viên quan tâm nhóm bạn
                    topicInterestsAsync.when(
                      loading: () => const SizedBox.shrink(),
                      error: (_, _) => const SizedBox.shrink(),
                      data: (interests) {
                        final openInterests = interests.where((item) => item.status == 'OPEN').toList();
                        if (openInterests.isEmpty) return const SizedBox.shrink();

                        return Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
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
                                    '${openInterests.length}',
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
                            for (final interest in openInterests)
                              _buildInterestedLecturerCard(
                                context,
                                teamId: team.id,
                                interest: interest,
                                currentIds: currentIds,
                                isLeader: isLeader,
                              ),
                            const SizedBox(height: AppSpacing.s4),
                          ],
                        );
                      },
                    ),

                    // Section: Tất cả giảng viên
                    Text(
                      'Tất cả giảng viên',
                      style: theme.textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    const SizedBox(height: AppSpacing.s2),
                    TextField(
                      controller: _searchController,
                      decoration: InputDecoration(
                        hintText: 'Tìm theo tên hoặc lĩnh vực chuyên môn...',
                        prefixIcon: const Icon(Icons.search),
                        suffixIcon: _searchQuery.isNotEmpty
                            ? IconButton(
                                icon: const Icon(Icons.clear),
                                onPressed: () {
                                  _searchController.clear();
                                  setState(() => _searchQuery = '');
                                },
                              )
                            : null,
                        border: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(AppRadius.md),
                        ),
                        contentPadding: const EdgeInsets.symmetric(
                          horizontal: AppSpacing.s4,
                          vertical: AppSpacing.s2,
                        ),
                      ),
                      onChanged: (val) {
                        setState(() => _searchQuery = val);
                      },
                    ),
                    const SizedBox(height: AppSpacing.s3),

                    lecturersAsync.when(
                      loading: () => const Center(
                        child: Padding(
                          padding: EdgeInsets.all(AppSpacing.s4),
                          child: CircularProgressIndicator(),
                        ),
                      ),
                      error: (err, _) => ErrorState(
                        error: err,
                        onRetry: () => ref.invalidate(lecturersListProvider(_searchQuery)),
                      ),
                      data: (lecturers) {
                        if (lecturers.isEmpty) {
                          return const Padding(
                            padding: EdgeInsets.symmetric(vertical: AppSpacing.s6),
                            child: EmptyState(
                              icon: Icons.search_off,
                              title: 'Không tìm thấy giảng viên',
                              description: 'Thử tìm với từ khóa khác.',
                            ),
                          );
                        }

                        return Column(
                          children: [
                            for (final lecturer in lecturers)
                              _buildLecturerListTile(
                                context,
                                teamId: team.id,
                                lecturer: lecturer,
                                currentIds: currentIds,
                                isLeader: isLeader,
                              ),
                          ],
                        );
                      },
                    ),
                  ],
                ),
              );
            },
          );
        },
      ),
    );
  }

  Widget _buildEmptySlot(BuildContext context, {required int priority}) {
    final theme = Theme.of(context);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s4, vertical: AppSpacing.s3),
      decoration: BoxDecoration(
        color: theme.colorScheme.surface,
        borderRadius: BorderRadius.circular(AppRadius.lg),
        border: Border.all(
          color: theme.colorScheme.outlineVariant,
          style: BorderStyle.solid,
        ),
      ),
      child: Row(
        children: [
          CircleAvatar(
            radius: 14,
            backgroundColor: theme.colorScheme.surfaceContainerHighest,
            child: Text(
              '$priority',
              style: theme.textTheme.bodyMedium?.copyWith(
                fontWeight: FontWeight.bold,
                color: theme.colorScheme.onSurfaceVariant,
              ),
            ),
          ),
          const SizedBox(width: AppSpacing.s3),
          Text(
            'Chưa chọn nguyện vọng $priority',
            style: theme.textTheme.bodyMedium?.copyWith(
              color: theme.colorScheme.onSurfaceVariant,
              fontStyle: FontStyle.italic,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildPreferenceCard(
    BuildContext context, {
    required String teamId,
    required int index,
    required LecturerPreferenceItem pref,
    required List<LecturerPreferenceItem> allPreferences,
    required bool isLeader,
  }) {
    final theme = Theme.of(context);
    final priority = index + 1;
    final currentIds = allPreferences.map((p) => p.lecturer.id).toList();

    return Card(
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.lg),
        side: BorderSide(color: theme.colorScheme.outlineVariant),
      ),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.s3),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                CircleAvatar(
                  radius: 14,
                  backgroundColor: theme.colorScheme.primary,
                  child: Text(
                    '$priority',
                    style: theme.textTheme.bodyMedium?.copyWith(
                      fontWeight: FontWeight.bold,
                      color: theme.colorScheme.onPrimary,
                    ),
                  ),
                ),
                const SizedBox(width: AppSpacing.s3),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        pref.lecturer.fullName,
                        style: theme.textTheme.titleSmall?.copyWith(
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      Text(
                        pref.lecturer.email,
                        style: theme.textTheme.bodySmall?.copyWith(
                          color: theme.colorScheme.onSurfaceVariant,
                        ),
                      ),
                    ],
                  ),
                ),
                if (pref.isFull)
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                    margin: const EdgeInsets.only(right: 4),
                    decoration: BoxDecoration(
                      color: Palette.neutralContainer,
                      borderRadius: BorderRadius.circular(AppRadius.full),
                    ),
                    child: Text(
                      'Đã đủ',
                      style: theme.textTheme.labelSmall?.copyWith(
                        color: Palette.textSecondary,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
                if (isLeader) ...[
                  // WCAG 2.5.7: Nút lên / xuống thay cho kéo thả
                  if (index > 0)
                    IconButton(
                      icon: const Icon(Icons.arrow_upward, size: 20),
                      tooltip: 'Đổi lên ưu tiên cao hơn',
                      visualDensity: VisualDensity.compact,
                      onPressed: () {
                        final nextIds = List<String>.from(currentIds);
                        final temp = nextIds[index];
                        nextIds[index] = nextIds[index - 1];
                        nextIds[index - 1] = temp;
                        _updatePreferencesWithUndo(
                          context,
                          teamId,
                          nextIds,
                          currentIds,
                          'Đã chuyển ${pref.lecturer.fullName} lên NV$index',
                        );
                      },
                    ),
                  if (index < allPreferences.length - 1)
                    IconButton(
                      icon: const Icon(Icons.arrow_downward, size: 20),
                      tooltip: 'Đổi xuống ưu tiên thấp hơn',
                      visualDensity: VisualDensity.compact,
                      onPressed: () {
                        final nextIds = List<String>.from(currentIds);
                        final temp = nextIds[index];
                        nextIds[index] = nextIds[index + 1];
                        nextIds[index + 1] = temp;
                        _updatePreferencesWithUndo(
                          context,
                          teamId,
                          nextIds,
                          currentIds,
                          'Đã chuyển ${pref.lecturer.fullName} xuống NV${index + 2}',
                        );
                      },
                    ),
                  IconButton(
                    icon: Icon(Icons.close, size: 20, color: theme.colorScheme.error),
                    tooltip: 'Bỏ khỏi nguyện vọng',
                    visualDensity: VisualDensity.compact,
                    onPressed: () {
                      final nextIds = List<String>.from(currentIds)..removeAt(index);
                      _updatePreferencesWithUndo(
                        context,
                        teamId,
                        nextIds,
                        currentIds,
                        'Đã bỏ ${pref.lecturer.fullName} khỏi nguyện vọng',
                      );
                    },
                  ),
                ],
              ],
            ),
            if (pref.lecturer.domains.isNotEmpty) ...[
              const SizedBox(height: AppSpacing.s2),
              Wrap(
                spacing: 4,
                runSpacing: 4,
                children: [
                  for (final domain in pref.lecturer.domains)
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                      decoration: BoxDecoration(
                        color: theme.colorScheme.surfaceContainerHighest,
                        borderRadius: BorderRadius.circular(AppRadius.sm),
                      ),
                      child: Text(
                        domain.name.isNotEmpty ? domain.name : domain.code,
                        style: theme.textTheme.labelSmall?.copyWith(
                          color: theme.colorScheme.onSurfaceVariant,
                        ),
                      ),
                    ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildInterestedLecturerCard(
    BuildContext context, {
    required String teamId,
    required TopicInterestItem interest,
    required List<String> currentIds,
    required bool isLeader,
  }) {
    final theme = Theme.of(context);
    final inPrefIndex = currentIds.indexOf(interest.advisorId);
    final isAlreadySelected = inPrefIndex >= 0;
    final canAdd = isLeader && !isAlreadySelected && currentIds.length < 3;

    return Card(
      elevation: 0,
      margin: const EdgeInsets.only(bottom: AppSpacing.s2),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.lg),
        side: BorderSide(color: theme.colorScheme.outlineVariant),
      ),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.s3),
        child: Row(
          children: [
            CircleAvatar(
              backgroundColor: Palette.infoContainer,
              child: Text(
                interest.advisorName.isNotEmpty ? interest.advisorName[0] : 'G',
                style: const TextStyle(color: Palette.infoDark, fontWeight: FontWeight.bold),
              ),
            ),
            const SizedBox(width: AppSpacing.s3),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Flexible(
                        child: Text(
                          interest.advisorName,
                          style: theme.textTheme.titleSmall?.copyWith(
                            fontWeight: FontWeight.bold,
                          ),
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                      const SizedBox(width: 6),
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 1),
                        decoration: BoxDecoration(
                          color: Palette.infoContainer,
                          borderRadius: BorderRadius.circular(AppRadius.full),
                        ),
                        child: Text(
                          'Quan tâm',
                          style: theme.textTheme.labelSmall?.copyWith(
                            color: Palette.infoDark,
                            fontSize: 10,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ),
                    ],
                  ),
                  Text(
                    interest.advisorEmail,
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: theme.colorScheme.onSurfaceVariant,
                    ),
                  ),
                ],
              ),
            ),
            if (isAlreadySelected)
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                decoration: BoxDecoration(
                  color: theme.colorScheme.primaryContainer,
                  borderRadius: BorderRadius.circular(AppRadius.full),
                ),
                child: Text(
                  'NV${inPrefIndex + 1}',
                  style: theme.textTheme.labelSmall?.copyWith(
                    color: theme.colorScheme.onPrimaryContainer,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              )
            else if (canAdd)
              FilledButton.tonal(
                style: FilledButton.styleFrom(
                  padding: const EdgeInsets.symmetric(horizontal: 12),
                  visualDensity: VisualDensity.compact,
                ),
                onPressed: () {
                  final nextSlot = currentIds.length + 1;
                  final nextIds = List<String>.from(currentIds)..add(interest.advisorId);
                  _updatePreferencesWithUndo(
                    context,
                    teamId,
                    nextIds,
                    currentIds,
                    'Đã thêm ${interest.advisorName} vào NV$nextSlot',
                  );
                },
                child: Text('Thêm vào NV${currentIds.length + 1}'),
              ),
          ],
        ),
      ),
    );
  }

  Widget _buildLecturerListTile(
    BuildContext context, {
    required String teamId,
    required LecturerSummary lecturer,
    required List<String> currentIds,
    required bool isLeader,
  }) {
    final theme = Theme.of(context);
    final inPrefIndex = currentIds.indexOf(lecturer.id);
    final isAlreadySelected = inPrefIndex >= 0;
    final canAdd = isLeader && !isAlreadySelected && !lecturer.isFull && currentIds.length < 3;

    return Card(
      elevation: 0,
      margin: const EdgeInsets.only(bottom: AppSpacing.s2),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.lg),
        side: BorderSide(color: theme.colorScheme.outlineVariant),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.lg),
        onTap: () {
          context.push('/sv/nhom/giang-vien/${lecturer.id}');
        },
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.s3),
          child: Row(
            children: [
              CircleAvatar(
                backgroundColor: theme.colorScheme.primaryContainer,
                child: Text(
                  lecturer.fullName.isNotEmpty ? lecturer.fullName[0] : 'G',
                  style: TextStyle(
                    color: theme.colorScheme.primary,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
              const SizedBox(width: AppSpacing.s3),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Flexible(
                          child: Text(
                            lecturer.fullName,
                            style: theme.textTheme.titleSmall?.copyWith(
                              fontWeight: FontWeight.bold,
                            ),
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                        if (lecturer.isFull) ...[
                          const SizedBox(width: 6),
                          Container(
                            padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 1),
                            decoration: BoxDecoration(
                              color: Palette.neutralContainer,
                              borderRadius: BorderRadius.circular(AppRadius.full),
                            ),
                            child: Text(
                              'Đã đủ',
                              style: theme.textTheme.labelSmall?.copyWith(
                                color: Palette.textSecondary,
                                fontSize: 10,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ),
                        ],
                      ],
                    ),
                    Text(
                      lecturer.email,
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: theme.colorScheme.onSurfaceVariant,
                      ),
                    ),
                    if (lecturer.domains.isNotEmpty) ...[
                      const SizedBox(height: 4),
                      Text(
                        lecturer.domains.map((d) => d.name.isNotEmpty ? d.name : d.code).join(', '),
                        style: theme.textTheme.labelSmall?.copyWith(
                          color: theme.colorScheme.onSurfaceVariant,
                        ),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ],
                  ],
                ),
              ),
              const SizedBox(width: 8),
              if (isAlreadySelected)
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                  decoration: BoxDecoration(
                    color: theme.colorScheme.primaryContainer,
                    borderRadius: BorderRadius.circular(AppRadius.full),
                  ),
                  child: Text(
                    'NV${inPrefIndex + 1}',
                    style: theme.textTheme.labelSmall?.copyWith(
                      color: theme.colorScheme.onPrimaryContainer,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                )
              else if (canAdd)
                FilledButton.tonal(
                  style: FilledButton.styleFrom(
                    padding: const EdgeInsets.symmetric(horizontal: 10),
                    visualDensity: VisualDensity.compact,
                  ),
                  onPressed: () {
                    final nextSlot = currentIds.length + 1;
                    final nextIds = List<String>.from(currentIds)..add(lecturer.id);
                    _updatePreferencesWithUndo(
                      context,
                      teamId,
                      nextIds,
                      currentIds,
                      'Đã thêm ${lecturer.fullName} vào NV$nextSlot',
                    );
                  },
                  child: Text('Thêm NV${currentIds.length + 1}'),
                )
              else
                const Icon(Icons.chevron_right, color: Colors.grey),
            ],
          ),
        ),
      ),
    );
  }
}
