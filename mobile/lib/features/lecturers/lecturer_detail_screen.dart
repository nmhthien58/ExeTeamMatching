import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../my_team/my_team_api.dart' show myTeamDetailProvider;
import 'lecturers_api.dart';

class LecturerDetailScreen extends ConsumerWidget {
  const LecturerDetailScreen({super.key, required this.lecturerId});

  final String lecturerId;

  Future<void> _updatePreferencesWithUndo(
    BuildContext context,
    WidgetRef ref,
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
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final lecturerAsync = ref.watch(lecturerDetailProvider(lecturerId));
    final myTeamAsync = ref.watch(myTeamDetailProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Chi tiết giảng viên'),
      ),
      body: lecturerAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (err, _) => ErrorState(
          error: err,
          onRetry: () => ref.invalidate(lecturerDetailProvider(lecturerId)),
        ),
        data: (lecturer) {
          final team = myTeamAsync.value;
          final teamId = team?.id;
          final isLeader = team?.isLeader ?? false;

          final prefAsync = teamId != null ? ref.watch(lecturerPreferencesProvider(teamId)) : null;
          final prefResponse = prefAsync?.value;

          final maxPreferences = prefResponse?.maxPreferences ?? 3;
          final preferences = prefResponse?.preferences ?? const [];

          final isInterestedInMyTeam = prefResponse?.interestedLecturers.any(
                (item) => item.lecturerId == lecturer.id,
              ) ??
              false;

          final isPreviousInstructor = prefResponse?.previousInstructor?.lecturerId == lecturer.id;

          final currentIds = preferences.map((p) => p.lecturerId).toList();
          final prefIndex = currentIds.indexOf(lecturer.id);
          final isSelected = prefIndex >= 0;

          return Column(
            children: [
              Expanded(
                child: ListView(
                  padding: const EdgeInsets.all(AppSpacing.s4),
                  children: [
                    // Header card với avatar và thông tin chính
                    Center(
                      child: Column(
                        children: [
                          CircleAvatar(
                            radius: 40,
                            backgroundColor: theme.colorScheme.primaryContainer,
                            child: Text(
                              lecturer.fullName.isNotEmpty ? lecturer.fullName[0] : 'G',
                              style: theme.textTheme.headlineMedium?.copyWith(
                                color: theme.colorScheme.primary,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ),
                          const SizedBox(height: AppSpacing.s3),
                          Text(
                            lecturer.fullName,
                            style: theme.textTheme.titleLarge?.copyWith(
                              fontWeight: FontWeight.bold,
                            ),
                            textAlign: TextAlign.center,
                          ),
                          const SizedBox(height: 4),
                          Text(
                            lecturer.email,
                            style: theme.textTheme.bodyMedium?.copyWith(
                              color: theme.colorScheme.onSurfaceVariant,
                            ),
                            textAlign: TextAlign.center,
                          ),
                          const SizedBox(height: AppSpacing.s3),
                          Wrap(
                            alignment: WrapAlignment.center,
                            spacing: AppSpacing.s2,
                            runSpacing: AppSpacing.s2,
                            children: [
                              if (isPreviousInstructor)
                                Container(
                                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                                  decoration: BoxDecoration(
                                    color: Palette.infoContainer,
                                    borderRadius: BorderRadius.circular(AppRadius.full),
                                  ),
                                  child: Row(
                                    mainAxisSize: MainAxisSize.min,
                                    children: [
                                      const Icon(Icons.history_edu, size: 16, color: Palette.infoDark),
                                      const SizedBox(width: 4),
                                      Text(
                                        'GVHD EXE101',
                                        style: theme.textTheme.labelMedium?.copyWith(
                                          color: Palette.infoDark,
                                          fontWeight: FontWeight.bold,
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                              if (isInterestedInMyTeam)
                                Container(
                                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                                  decoration: BoxDecoration(
                                    color: Palette.successContainer,
                                    borderRadius: BorderRadius.circular(AppRadius.full),
                                  ),
                                  child: Row(
                                    mainAxisSize: MainAxisSize.min,
                                    children: [
                                      const Icon(Icons.favorite, size: 16, color: Palette.success),
                                      const SizedBox(width: 4),
                                      Text(
                                        'Quan tâm nhóm bạn',
                                        style: theme.textTheme.labelMedium?.copyWith(
                                          color: Palette.success,
                                          fontWeight: FontWeight.bold,
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                              Container(
                                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                                decoration: BoxDecoration(
                                  color: lecturer.isFull ? Palette.neutralContainer : Palette.successContainer,
                                  borderRadius: BorderRadius.circular(AppRadius.full),
                                ),
                                child: Text(
                                  lecturer.isFull ? 'Đã đủ nhóm' : 'Đang nhận nhóm',
                                  style: theme.textTheme.labelMedium?.copyWith(
                                    color: lecturer.isFull ? Palette.textSecondary : Palette.success,
                                    fontWeight: FontWeight.bold,
                                  ),
                                ),
                              ),
                              if (isSelected)
                                Container(
                                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                                  decoration: BoxDecoration(
                                    color: theme.colorScheme.primaryContainer,
                                    borderRadius: BorderRadius.circular(AppRadius.full),
                                  ),
                                  child: Text(
                                    'Nguyện vọng ${prefIndex + 1}',
                                    style: theme.textTheme.labelMedium?.copyWith(
                                      color: theme.colorScheme.onPrimaryContainer,
                                      fontWeight: FontWeight.bold,
                                    ),
                                  ),
                                ),
                            ],
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: AppSpacing.s6),

                    // Lĩnh vực chuyên môn
                    Text(
                      'Lĩnh vực chuyên môn',
                      style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
                    ),
                    const SizedBox(height: AppSpacing.s2),
                    if (lecturer.domains.isNotEmpty)
                      Wrap(
                        spacing: AppSpacing.s2,
                        runSpacing: AppSpacing.s2,
                        children: [
                          for (final d in lecturer.domains)
                            Chip(
                              label: Text(d.name.isNotEmpty ? d.name : d.code),
                              backgroundColor: theme.colorScheme.surfaceContainerHighest,
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(AppRadius.sm),
                                side: BorderSide(color: theme.colorScheme.outlineVariant),
                              ),
                            ),
                        ],
                      )
                    else
                      Text(
                        'Chưa cập nhật chuyên môn cụ thể.',
                        style: theme.textTheme.bodyMedium?.copyWith(
                          color: theme.colorScheme.onSurfaceVariant,
                          fontStyle: FontStyle.italic,
                        ),
                      ),

                    const SizedBox(height: AppSpacing.s6),

                    // Ghi chú về quy trình ghép
                    Card.outlined(
                      child: Padding(
                        padding: const EdgeInsets.all(AppSpacing.s4),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                Icon(Icons.info_outline, size: 20, color: theme.colorScheme.primary),
                                const SizedBox(width: AppSpacing.s2),
                                Text(
                                  'Quy tắc ghép cố vấn',
                                  style: theme.textTheme.titleSmall?.copyWith(fontWeight: FontWeight.bold),
                                ),
                              ],
                            ),
                            const SizedBox(height: AppSpacing.s2),
                            Text(
                              'Hệ thống tự động ưu tiên các trường hợp hai bên cùng quan tâm và xếp nguyện vọng. Bạn có thể chọn tối đa $maxPreferences nguyện vọng và sắp xếp thứ tự ưu tiên.',
                              style: theme.textTheme.bodySmall?.copyWith(
                                color: theme.colorScheme.onSurfaceVariant,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ],
                ),
              ),

              // Bottom persistent action bar
              if (teamId != null)
                Container(
                  padding: const EdgeInsets.all(AppSpacing.s4),
                  decoration: BoxDecoration(
                    color: theme.colorScheme.surface,
                    border: Border(
                      top: BorderSide(color: theme.colorScheme.outlineVariant),
                    ),
                    boxShadow: [
                      BoxShadow(
                        color: Colors.black.withValues(alpha: 0.04),
                        blurRadius: 8,
                        offset: const Offset(0, -2),
                      ),
                    ],
                  ),
                  child: SafeArea(
                    top: false,
                    child: prefAsync == null
                        ? const SizedBox.shrink()
                        : prefAsync.when(
                            loading: () => const Padding(
                              padding: EdgeInsets.symmetric(vertical: AppSpacing.s2),
                              child: Row(
                                mainAxisAlignment: MainAxisAlignment.center,
                                children: [
                                  SizedBox(
                                    width: 18,
                                    height: 18,
                                    child: CircularProgressIndicator(strokeWidth: 2),
                                  ),
                                  SizedBox(width: AppSpacing.s2),
                                  Text('Đang tải danh sách nguyện vọng...'),
                                ],
                              ),
                            ),
                            error: (err, _) => Padding(
                              padding: const EdgeInsets.symmetric(vertical: AppSpacing.s2),
                              child: Row(
                                children: [
                                  const Icon(Icons.error_outline, size: 20, color: Palette.error),
                                  const SizedBox(width: AppSpacing.s2),
                                  Expanded(
                                    child: Text(
                                      'Không thể tải danh sách nguyện vọng',
                                      style: theme.textTheme.bodySmall?.copyWith(color: Palette.error),
                                    ),
                                  ),
                                  TextButton(
                                    onPressed: () => ref.invalidate(lecturerPreferencesProvider(teamId)),
                                    child: const Text('Thử lại'),
                                  ),
                                ],
                              ),
                            ),
                            data: (loadedPrefResponse) {
                              final isLocked = loadedPrefResponse.locked;
                              final limit = loadedPrefResponse.maxPreferences;
                              final currentList = loadedPrefResponse.preferences;
                              final activeIds = currentList.map((p) => p.lecturerId).toList();
                              final indexInList = activeIds.indexOf(lecturer.id);
                              final itemSelected = indexInList >= 0;

                              final canAdd = isLeader &&
                                  !isLocked &&
                                  !itemSelected &&
                                  !lecturer.isFull &&
                                  activeIds.length < limit;
                              final canRemove = isLeader && !isLocked && itemSelected;

                              return Column(
                                mainAxisSize: MainAxisSize.min,
                                children: [
                                  if (isLocked)
                                    Padding(
                                      padding: const EdgeInsets.only(bottom: AppSpacing.s2),
                                      child: Text(
                                        'Nguyện vọng đã khóa sau khi ghép cố vấn / hết hạn chốt',
                                        style: theme.textTheme.bodySmall?.copyWith(
                                          color: Palette.error,
                                          fontWeight: FontWeight.w600,
                                        ),
                                      ),
                                    )
                                  else if (!isLeader)
                                    Padding(
                                      padding: const EdgeInsets.only(bottom: AppSpacing.s2),
                                      child: Text(
                                        'Chỉ trưởng nhóm mới có quyền chọn nguyện vọng',
                                        style: theme.textTheme.bodySmall?.copyWith(
                                          color: theme.colorScheme.onSurfaceVariant,
                                        ),
                                      ),
                                    )
                                  else if (!itemSelected && activeIds.length >= limit)
                                    Padding(
                                      padding: const EdgeInsets.only(bottom: AppSpacing.s2),
                                      child: Text(
                                        'Đã chọn đủ $limit nguyện vọng. Hãy bỏ bớt một nguyện vọng ở danh sách để thêm',
                                        style: theme.textTheme.bodySmall?.copyWith(
                                          color: Palette.warning,
                                        ),
                                        textAlign: TextAlign.center,
                                      ),
                                    )
                                  else if (!itemSelected && lecturer.isFull)
                                    Padding(
                                      padding: const EdgeInsets.only(bottom: AppSpacing.s2),
                                      child: Text(
                                        'Giảng viên này đã nhận đủ số nhóm tối đa',
                                        style: theme.textTheme.bodySmall?.copyWith(
                                          color: Palette.textSecondary,
                                        ),
                                      ),
                                    ),
                                  SizedBox(
                                    width: double.infinity,
                                    height: 48,
                                    child: itemSelected
                                        ? OutlinedButton.icon(
                                            style: OutlinedButton.styleFrom(
                                              foregroundColor: theme.colorScheme.error,
                                              side: BorderSide(color: theme.colorScheme.error),
                                            ),
                                            icon: const Icon(Icons.remove_circle_outline),
                                            label: const Text('Bỏ khỏi nguyện vọng'),
                                            onPressed: canRemove
                                                ? () {
                                                    final nextIds = List<String>.from(activeIds)..remove(lecturer.id);
                                                    _updatePreferencesWithUndo(
                                                      context,
                                                      ref,
                                                      teamId,
                                                      nextIds,
                                                      activeIds,
                                                      'Đã bỏ ${lecturer.fullName} khỏi nguyện vọng',
                                                    );
                                                  }
                                                : null,
                                          )
                                        : FilledButton.icon(
                                            icon: const Icon(Icons.add),
                                            label: Text(
                                              activeIds.isEmpty
                                                  ? 'Thêm vào NV1'
                                                  : 'Thêm vào NV${activeIds.length + 1}',
                                            ),
                                            onPressed: canAdd
                                                ? () {
                                                    final nextSlot = activeIds.length + 1;
                                                    final nextIds = List<String>.from(activeIds)..add(lecturer.id);
                                                    _updatePreferencesWithUndo(
                                                      context,
                                                      ref,
                                                      teamId,
                                                      nextIds,
                                                      activeIds,
                                                      'Đã thêm ${lecturer.fullName} vào NV$nextSlot',
                                                    );
                                                  }
                                                : null,
                                          ),
                                  ),
                                ],
                              );
                            },
                          ),
                  ),
                ),
            ],
          );
        },
      ),
    );
  }
}
