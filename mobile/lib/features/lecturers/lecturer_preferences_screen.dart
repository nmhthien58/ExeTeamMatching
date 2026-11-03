import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../my_team/my_team_api.dart' show myTeamDetailProvider;
import 'lecturers_api.dart';
import 'lecturers_models.dart';
import 'paths.dart';

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

  String _formatDateTime(DateTime dt) {
    return '${dt.day.toString().padLeft(2, '0')}/${dt.month.toString().padLeft(2, '0')}/${dt.year} ${dt.hour.toString().padLeft(2, '0')}:${dt.minute.toString().padLeft(2, '0')}';
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
          final lecturersAsync = ref.watch(lecturersListProvider(_searchQuery));

          return preferencesAsync.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (err, _) => ErrorState(
              error: err,
              onRetry: () => ref.invalidate(lecturerPreferencesProvider(team.id)),
            ),
            data: (response) {
              final isLocked = response.locked;
              final maxPreferences = response.maxPreferences;
              final preferences = response.preferences;
              final currentIds = preferences.map((p) => p.lecturerId).toList();
              final previousInstructor = response.previousInstructor;
              final interestedLecturers = response.interestedLecturers;

              return RefreshIndicator(
                onRefresh: () async {
                  ref.invalidate(lecturerPreferencesProvider(team.id));
                  ref.invalidate(lecturersListProvider(_searchQuery));
                },
                child: ListView(
                  padding: const EdgeInsets.all(AppSpacing.s4),
                  children: [
                    // Status banner
                    if (isLocked)
                      Container(
                        padding: const EdgeInsets.symmetric(
                          horizontal: AppSpacing.s4,
                          vertical: AppSpacing.s3,
                        ),
                        decoration: BoxDecoration(
                          color: Palette.errorContainer,
                          borderRadius: BorderRadius.circular(AppRadius.md),
                        ),
                        child: Row(
                          children: [
                            const Icon(
                              Icons.lock_outline,
                              size: 20,
                              color: Palette.error,
                            ),
                            const SizedBox(width: AppSpacing.s3),
                            Expanded(
                              child: Text(
                                'Nguyện vọng đã khóa · Hệ thống đã chốt hoặc hoàn tất ghép giảng viên.',
                                style: theme.textTheme.bodyMedium?.copyWith(
                                  color: Palette.error,
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                            ),
                          ],
                        ),
                      )
                    else
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
                                    ? response.lockAt != null
                                        ? 'Đã tự lưu · Hạn chốt: ${_formatDateTime(response.lockAt!)}'
                                        : 'Đã tự lưu · Trưởng nhóm sửa được tới hạn chốt đợt lập nhóm'
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
                      'Danh sách nguyện vọng (tối đa $maxPreferences)',
                      style: theme.textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    const SizedBox(height: AppSpacing.s2),
                    for (int i = 0; i < maxPreferences; i++) ...[
                      if (i < preferences.length)
                        _buildPreferenceCard(
                          context,
                          teamId: team.id,
                          index: i,
                          pref: preferences[i],
                          allPreferences: preferences,
                          canEdit: isLeader && !isLocked,
                        )
                      else
                        _buildEmptySlot(context, priority: i + 1),
                      const SizedBox(height: AppSpacing.s2),
                    ],

                    const SizedBox(height: AppSpacing.s4),

                    // Section: Đã hướng dẫn ở EXE101 (nếu có)
                    if (previousInstructor != null) ...[
                      _buildPreviousInstructorSection(
                        context,
                        teamId: team.id,
                        previousInstructor: previousInstructor,
                        currentIds: currentIds,
                        canEdit: isLeader && !isLocked && currentIds.length < maxPreferences,
                      ),
                      const SizedBox(height: AppSpacing.s4),
                    ],

                    // Section: Giảng viên quan tâm nhóm bạn
                    if (interestedLecturers.isNotEmpty) ...[
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
                          teamId: team.id,
                          lecturer: lecturer,
                          currentIds: currentIds,
                          canEdit: isLeader && !isLocked && currentIds.length < maxPreferences,
                        ),
                      const SizedBox(height: AppSpacing.s4),
                    ],

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
                          horizontal: AppSpacing.s3,
                          vertical: AppSpacing.s2,
                        ),
                      ),
                      onSubmitted: (val) => setState(() => _searchQuery = val.trim()),
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
                          return Padding(
                            padding: const EdgeInsets.symmetric(vertical: AppSpacing.s4),
                            child: Center(
                              child: Text(
                                _searchQuery.isEmpty
                                    ? 'Chưa có giảng viên nào'
                                    : 'Không tìm thấy giảng viên phù hợp',
                                style: theme.textTheme.bodyMedium?.copyWith(
                                  color: theme.colorScheme.onSurfaceVariant,
                                ),
                              ),
                            ),
                          );
                        }

                        return Column(
                          children: [
                            for (final l in lecturers)
                              _buildLecturerCatalogCard(
                                context,
                                teamId: team.id,
                                lecturer: l,
                                preferences: preferences,
                                currentIds: currentIds,
                                canEdit: isLeader && !isLocked,
                                maxPreferences: maxPreferences,
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
      padding: const EdgeInsets.all(AppSpacing.s3),
      decoration: BoxDecoration(
        color: theme.colorScheme.surface,
        borderRadius: BorderRadius.circular(AppRadius.md),
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
              style: theme.textTheme.labelMedium?.copyWith(
                color: theme.colorScheme.onSurfaceVariant,
                fontWeight: FontWeight.bold,
              ),
            ),
          ),
          const SizedBox(width: AppSpacing.s3),
          Expanded(
            child: Text(
              'Chưa chọn nguyện vọng $priority',
              style: theme.textTheme.bodyMedium?.copyWith(
                color: theme.colorScheme.onSurfaceVariant,
                fontStyle: FontStyle.italic,
              ),
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
    required bool canEdit,
  }) {
    final theme = Theme.of(context);
    final priority = index + 1;
    final canMoveUp = canEdit && index > 0;
    final canMoveDown = canEdit && index < allPreferences.length - 1;

    return Card(
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.md),
        side: BorderSide(color: theme.colorScheme.outlineVariant),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.md),
        onTap: () => context.push('$lecturerPreferencesPath/${pref.lecturerId}'),
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.s3),
          child: Row(
            children: [
              CircleAvatar(
                radius: 14,
                backgroundColor: theme.colorScheme.primary,
                child: Text(
                  '$priority',
                  style: theme.textTheme.labelMedium?.copyWith(
                    color: theme.colorScheme.onPrimary,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
              const SizedBox(width: AppSpacing.s3),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    // Wrap: tên dài hoặc chữ 130% thì nhãn "Đã quan tâm" xuống dòng thay vì tràn (flutter-handoff §5).
                    Wrap(
                      spacing: AppSpacing.s1,
                      runSpacing: AppSpacing.s1,
                      crossAxisAlignment: WrapCrossAlignment.center,
                      children: [
                        Text(
                          pref.fullName,
                          style: theme.textTheme.titleSmall?.copyWith(
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                        if (pref.isInterested)
                          Container(
                            padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                            decoration: BoxDecoration(
                              color: Palette.successContainer,
                              borderRadius: BorderRadius.circular(AppRadius.sm),
                            ),
                            child: Text(
                              'Đã quan tâm',
                              style: theme.textTheme.labelSmall?.copyWith(
                                color: Palette.success,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ),
                      ],
                    ),
                    const SizedBox(height: AppSpacing.s1),
                    if (pref.domains.isNotEmpty)
                      Wrap(
                        spacing: 4,
                        runSpacing: 4,
                        children: [
                          for (final d in pref.domains.take(2))
                            Container(
                              padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                              decoration: BoxDecoration(
                                color: theme.colorScheme.surfaceContainerHighest,
                                borderRadius: BorderRadius.circular(AppRadius.sm),
                              ),
                              child: Text(
                                d.name.isNotEmpty ? d.name : d.code,
                                style: theme.textTheme.labelSmall,
                              ),
                            ),
                          if (pref.domains.length > 2)
                            Text(
                              '+${pref.domains.length - 2}',
                              style: theme.textTheme.labelSmall?.copyWith(
                                color: theme.colorScheme.onSurfaceVariant,
                              ),
                            ),
                        ],
                      ),
                  ],
                ),
              ),
              if (canEdit) ...[
                IconButton(
                  icon: const Icon(Icons.arrow_upward, size: 20),
                  tooltip: 'Tăng ưu tiên',
                  onPressed: canMoveUp
                      ? () {
                          final newIds = allPreferences.map((p) => p.lecturerId).toList();
                          final temp = newIds[index];
                          newIds[index] = newIds[index - 1];
                          newIds[index - 1] = temp;
                          _updatePreferencesWithUndo(
                            context,
                            teamId,
                            newIds,
                            allPreferences.map((p) => p.lecturerId).toList(),
                            'Đã tăng ưu tiên của ${pref.fullName}',
                          );
                        }
                      : null,
                ),
                IconButton(
                  icon: const Icon(Icons.arrow_downward, size: 20),
                  tooltip: 'Giảm ưu tiên',
                  onPressed: canMoveDown
                      ? () {
                          final newIds = allPreferences.map((p) => p.lecturerId).toList();
                          final temp = newIds[index];
                          newIds[index] = newIds[index + 1];
                          newIds[index + 1] = temp;
                          _updatePreferencesWithUndo(
                            context,
                            teamId,
                            newIds,
                            allPreferences.map((p) => p.lecturerId).toList(),
                            'Đã giảm ưu tiên của ${pref.fullName}',
                          );
                        }
                      : null,
                ),
                IconButton(
                  icon: const Icon(Icons.close, size: 20),
                  tooltip: 'Xóa nguyện vọng',
                  onPressed: () {
                    final previousIds = allPreferences.map((p) => p.lecturerId).toList();
                    final newIds = allPreferences
                        .where((p) => p.lecturerId != pref.lecturerId)
                        .map((p) => p.lecturerId)
                        .toList();
                    _updatePreferencesWithUndo(
                      context,
                      teamId,
                      newIds,
                      previousIds,
                      'Đã bỏ ${pref.fullName} khỏi nguyện vọng',
                    );
                  },
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildPreviousInstructorSection(
    BuildContext context, {
    required String teamId,
    required LecturerRef previousInstructor,
    required List<String> currentIds,
    required bool canEdit,
  }) {
    final theme = Theme.of(context);
    final isAlreadySelected = currentIds.contains(previousInstructor.lecturerId);

    return Container(
      padding: const EdgeInsets.all(AppSpacing.s3),
      decoration: BoxDecoration(
        color: Palette.infoContainer.withValues(alpha: 0.5),
        borderRadius: BorderRadius.circular(AppRadius.md),
        border: Border.all(color: Palette.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Icon(Icons.history_edu, size: 18, color: Palette.infoDark),
              const SizedBox(width: AppSpacing.s2),
              Expanded(
                child: Text(
                  'Đã hướng dẫn ở EXE101',
                  style: theme.textTheme.titleSmall?.copyWith(
                    fontWeight: FontWeight.bold,
                    color: Palette.infoDark,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.s2),
          Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      previousInstructor.fullName,
                      style: theme.textTheme.titleSmall?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    if (previousInstructor.domains.isNotEmpty) ...[
                      const SizedBox(height: AppSpacing.s1),
                      Wrap(
                        spacing: 4,
                        children: [
                          for (final d in previousInstructor.domains.take(2))
                            Container(
                              padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                              decoration: BoxDecoration(
                                color: theme.colorScheme.surface,
                                borderRadius: BorderRadius.circular(AppRadius.sm),
                              ),
                              child: Text(
                                d.name.isNotEmpty ? d.name : d.code,
                                style: theme.textTheme.labelSmall,
                              ),
                            ),
                        ],
                      ),
                    ],
                  ],
                ),
              ),
              if (isAlreadySelected)
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                  decoration: BoxDecoration(
                    color: Palette.successContainer,
                    borderRadius: BorderRadius.circular(AppRadius.sm),
                  ),
                  child: Text(
                    'Đã chọn',
                    style: theme.textTheme.labelSmall?.copyWith(
                      color: Palette.success,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                )
              else if (canEdit)
                FilledButton.tonal(
                  onPressed: () {
                    final newIds = [...currentIds, previousInstructor.lecturerId];
                    _updatePreferencesWithUndo(
                      context,
                      teamId,
                      newIds,
                      currentIds,
                      'Đã thêm ${previousInstructor.fullName} vào nguyện vọng',
                    );
                  },
                  child: const Text('Thêm'),
                ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildInterestedLecturerCard(
    BuildContext context, {
    required String teamId,
    required LecturerRef lecturer,
    required List<String> currentIds,
    required bool canEdit,
  }) {
    final theme = Theme.of(context);
    final isAlreadySelected = currentIds.contains(lecturer.lecturerId);

    return Card(
      elevation: 0,
      margin: const EdgeInsets.only(bottom: AppSpacing.s2),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.md),
        side: BorderSide(color: theme.colorScheme.outlineVariant),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.md),
        onTap: () => context.push('$lecturerPreferencesPath/${lecturer.lecturerId}'),
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.s3),
          child: Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      lecturer.fullName,
                      style: theme.textTheme.titleSmall?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    const SizedBox(height: AppSpacing.s1),
                    if (lecturer.domains.isNotEmpty)
                      Wrap(
                        spacing: 4,
                        children: [
                          for (final d in lecturer.domains.take(2))
                            Container(
                              padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                              decoration: BoxDecoration(
                                color: theme.colorScheme.surfaceContainerHighest,
                                borderRadius: BorderRadius.circular(AppRadius.sm),
                              ),
                              child: Text(
                                d.name.isNotEmpty ? d.name : d.code,
                                style: theme.textTheme.labelSmall,
                              ),
                            ),
                        ],
                      ),
                  ],
                ),
              ),
              if (isAlreadySelected)
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                  decoration: BoxDecoration(
                    color: Palette.successContainer,
                    borderRadius: BorderRadius.circular(AppRadius.sm),
                  ),
                  child: Text(
                    'Đã chọn',
                    style: theme.textTheme.labelSmall?.copyWith(
                      color: Palette.success,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                )
              else if (canEdit)
                FilledButton.tonal(
                  onPressed: () {
                    final newIds = [...currentIds, lecturer.lecturerId];
                    _updatePreferencesWithUndo(
                      context,
                      teamId,
                      newIds,
                      currentIds,
                      'Đã thêm ${lecturer.fullName} vào nguyện vọng',
                    );
                  },
                  child: const Text('Thêm vào nguyện vọng'),
                ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildLecturerCatalogCard(
    BuildContext context, {
    required String teamId,
    required LecturerSummary lecturer,
    required List<LecturerPreferenceItem> preferences,
    required List<String> currentIds,
    required bool canEdit,
    required int maxPreferences,
  }) {
    final theme = Theme.of(context);
    final prefIndex = currentIds.indexOf(lecturer.id);
    final isSelected = prefIndex >= 0;
    final isFull = lecturer.isFull;

    return Card(
      elevation: 0,
      margin: const EdgeInsets.only(bottom: AppSpacing.s2),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.md),
        side: BorderSide(
          color: isSelected
              ? theme.colorScheme.primary
              : theme.colorScheme.outlineVariant,
        ),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.md),
        onTap: () => context.push('$lecturerPreferencesPath/${lecturer.id}'),
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.s3),
          child: Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Expanded(
                          child: Text(
                            lecturer.fullName,
                            style: theme.textTheme.titleSmall?.copyWith(
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ),
                        if (isFull)
                          Container(
                            padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                            decoration: BoxDecoration(
                              color: Palette.errorContainer,
                              borderRadius: BorderRadius.circular(AppRadius.sm),
                            ),
                            child: Text(
                              'Đã đủ nhóm',
                              style: theme.textTheme.labelSmall?.copyWith(
                                color: Palette.error,
                              ),
                            ),
                          ),
                      ],
                    ),
                    const SizedBox(height: AppSpacing.s1),
                    Text(
                      lecturer.email,
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: theme.colorScheme.onSurfaceVariant,
                      ),
                    ),
                    const SizedBox(height: AppSpacing.s1),
                    if (lecturer.domains.isNotEmpty)
                      Wrap(
                        spacing: 4,
                        runSpacing: 4,
                        children: [
                          for (final d in lecturer.domains.take(3))
                            Container(
                              padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                              decoration: BoxDecoration(
                                color: theme.colorScheme.surfaceContainerHighest,
                                borderRadius: BorderRadius.circular(AppRadius.sm),
                              ),
                              child: Text(
                                d.name.isNotEmpty ? d.name : d.code,
                                style: theme.textTheme.labelSmall,
                              ),
                            ),
                        ],
                      ),
                  ],
                ),
              ),
              const SizedBox(width: AppSpacing.s2),
              if (isSelected)
                Column(
                  children: [
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                      decoration: BoxDecoration(
                        color: theme.colorScheme.primaryContainer,
                        borderRadius: BorderRadius.circular(AppRadius.sm),
                      ),
                      child: Text(
                        'NV #${prefIndex + 1}',
                        style: theme.textTheme.labelSmall?.copyWith(
                          color: theme.colorScheme.onPrimaryContainer,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ),
                    if (canEdit) ...[
                      const SizedBox(height: 4),
                      TextButton(
                        onPressed: () {
                          final newIds = currentIds.where((id) => id != lecturer.id).toList();
                          _updatePreferencesWithUndo(
                            context,
                            teamId,
                            newIds,
                            currentIds,
                            'Đã bỏ ${lecturer.fullName} khỏi nguyện vọng',
                          );
                        },
                        child: const Text('Bỏ'),
                      ),
                    ],
                  ],
                )
              else if (canEdit)
                FilledButton.tonal(
                  onPressed: (!isFull && currentIds.length < maxPreferences)
                      ? () {
                          final newIds = [...currentIds, lecturer.id];
                          _updatePreferencesWithUndo(
                            context,
                            teamId,
                            newIds,
                            currentIds,
                            'Đã thêm ${lecturer.fullName} vào nguyện vọng',
                          );
                        }
                      : null,
                  child: const Text('Chọn'),
                ),
            ],
          ),
        ),
      ),
    );
  }
}
