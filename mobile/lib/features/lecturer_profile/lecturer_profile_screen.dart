import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/network/api_client.dart';
import '../../core/providers.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../suggestions/suggestions_api.dart';
import 'lecturer_profile_view_model.dart';

const lecturerProfilePath = '/gv/ho-so';
const lecturerSettingsPath = '/gv/cai-dat';

/// Tối đa 18 domain (academic.yaml, LecturerInput).
const maxDomains = 18;

class LecturerProfile {
  const LecturerProfile({required this.fullName, required this.email, required this.domains, this.maxTeamsExe101, this.maxTeamsExe201});

  final String fullName;
  final String email;
  final List<CatalogRef> domains;

  /// Do Bộ môn đặt (W11); null = không giới hạn.
  final int? maxTeamsExe101;
  final int? maxTeamsExe201;

  factory LecturerProfile.fromJson(Map<String, dynamic> j) => LecturerProfile(
        fullName: j['fullName'] as String? ?? '',
        email: j['email'] as String? ?? '',
        domains: [for (final d in (j['domains'] as List? ?? const [])) CatalogRef.fromJson(Map<String, dynamic>.from(d as Map))],
        maxTeamsExe101: j['maxTeamsExe101'] as int?,
        maxTeamsExe201: j['maxTeamsExe201'] as int?,
      );
}

/// academic.yaml – Lecturers `me` (L06). Giảng viên chỉ sửa domain chuyên môn; số nhóm tối đa do Bộ môn đặt.
class LecturerProfileApi {
  LecturerProfileApi(this._dio);

  final Dio _dio;

  Future<LecturerProfile> me() async {
    try {
      return LecturerProfile.fromJson((await _dio.get<Map<String, dynamic>>('/api/lecturers/me')).data!);
    } on DioException catch (e) {
      throw e.apiError;
    }
  }

  Future<LecturerProfile> saveDomains(List<int> domainIds) async {
    try {
      return LecturerProfile.fromJson((await _dio.put<Map<String, dynamic>>('/api/lecturers/me', data: {'domainIds': domainIds})).data!);
    } on DioException catch (e) {
      throw e.apiError;
    }
  }

  /// Chỉ domain đang dùng.
  Future<List<CatalogRef>> domains() async {
    try {
      final response = await _dio.get<List<dynamic>>('/api/domains');
      return [
        for (final d in response.data!)
          if ((d as Map)['isActive'] != false) CatalogRef.fromJson(Map<String, dynamic>.from(d)),
      ];
    } on DioException catch (e) {
      throw e.apiError;
    }
  }
}

final lecturerProfileApiProvider = Provider<LecturerProfileApi>((ref) => LecturerProfileApi(ref.watch(dioProvider)));

final lecturerProfileProvider = FutureProvider.autoDispose<LecturerProfile>((ref) => ref.watch(lecturerProfileApiProvider).me(), retry: (_, _) => null);

final activeDomainsProvider = FutureProvider.autoDispose<List<CatalogRef>>((ref) => ref.watch(lecturerProfileApiProvider).domains(), retry: (_, _) => null);

/// L06 – Hồ sơ giảng viên (MB-35): chọn domain chuyên môn (đầu vào gợi ý đề tài L07, quy tắc 19);
/// số nhóm tối đa theo môn chỉ xem (Bộ môn đặt ở W11). Trạng thái: xem · chỉnh sửa · đang lưu · lỗi lưu.
class LecturerProfileScreen extends ConsumerWidget {
  const LecturerProfileScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Hồ sơ giảng viên'),
        actions: [IconButton(tooltip: 'Cài đặt', icon: const Icon(Icons.settings_outlined), onPressed: () => context.push(lecturerSettingsPath))],
      ),
      body: ref.watch(lecturerProfileProvider).when(
            loading: () => const SkeletonList(count: 2),
            error: (e, _) => ErrorState(error: e, onRetry: () => ref.read(lecturerProfileViewModelProvider.notifier).refresh()),
            data: (profile) => _LecturerProfileView(profile: profile),
          ),
    );
  }
}

class _LecturerProfileView extends ConsumerWidget {
  const _LecturerProfileView({required this.profile});

  final LecturerProfile profile;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final vm = ref.read(lecturerProfileViewModelProvider.notifier);
    final state = ref.watch(lecturerProfileViewModelProvider);
    Future<void> save() async {
      if (!await vm.save() || !context.mounted) return;
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(const SnackBar(content: Text('Đã lưu chuyên môn')));
    }

    final theme = Theme.of(context);
    final p = profile;
    final editing = state.editing;
    String limit(int? n) => n == null ? 'Không giới hạn' : '$n nhóm';

    return ListView(
      padding: const EdgeInsets.all(AppSpacing.s4),
      children: [
        Text(p.fullName, style: theme.textTheme.titleLarge),
        const SizedBox(height: AppSpacing.s1),
        Text(p.email, style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
        const SizedBox(height: AppSpacing.s4),
        Text('Số nhóm tối đa nhận', style: theme.textTheme.titleMedium),
        const SizedBox(height: AppSpacing.s2),
        Text('EXE101: ${limit(p.maxTeamsExe101)} · EXE201: ${limit(p.maxTeamsExe201)}', style: theme.textTheme.bodyMedium),
        Text('Do Bộ môn đặt. Liên hệ Bộ môn nếu cần thay đổi.', style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
        const SizedBox(height: AppSpacing.s4),
        Row(
          children: [
            Expanded(child: Text('Chuyên môn (domain)', style: theme.textTheme.titleMedium)),
            if (editing == null) TextButton(onPressed: () => vm.startEditing(p), child: const Text('Sửa')),
          ],
        ),
        Text('Dùng để gợi ý đề tài phù hợp với bạn.', style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
        const SizedBox(height: AppSpacing.s2),
        if (editing == null)
          p.domains.isEmpty
              ? Text('Chưa khai báo chuyên môn.', style: theme.textTheme.bodyMedium)
              : Wrap(
                  spacing: AppSpacing.s2,
                  runSpacing: AppSpacing.s2,
                  children: [for (final d in p.domains) Chip(label: Text('${d.code} · ${d.name}'))],
                )
        else
          _DomainEditor(
            selected: editing,
            current: p.domains,
            saving: state.saving,
            error: state.error,
            onToggle: vm.toggle,
            onCancel: vm.cancel,
            onSave: save,
          ),
      ],
    );
  }
}

class _DomainEditor extends ConsumerWidget {
  const _DomainEditor({
    required this.selected,
    required this.current,
    required this.saving,
    required this.error,
    required this.onToggle,
    required this.onCancel,
    required this.onSave,
  });

  final Set<int> selected;
  final List<CatalogRef> current;
  final bool saving;
  final String? error;
  final ValueChanged<int> onToggle;
  final VoidCallback onCancel;
  final VoidCallback onSave;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    return ref.watch(activeDomainsProvider).when(
          loading: () => const Skeleton(height: 120),
          error: (e, _) => ErrorBanner(message: 'Không tải được danh mục domain.', onRetry: () => ref.invalidate(activeDomainsProvider)),
          data: (active) {
            // Domain đã ngừng mà giảng viên đang có vẫn hiện để bỏ chọn được.
            final options = [...active, ...current.where((c) => active.every((a) => a.id != c.id))];
            final full = selected.length >= maxDomains;
            return Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (error != null) ...[ErrorBanner(message: error!), const SizedBox(height: AppSpacing.s2)],
                Wrap(
                  spacing: AppSpacing.s2,
                  runSpacing: AppSpacing.s2,
                  children: [
                    for (final d in options)
                      FilterChip(
                        label: Text('${d.code} · ${d.name}'),
                        selected: selected.contains(d.id),
                        onSelected: !selected.contains(d.id) && full ? null : (_) => onToggle(d.id),
                      ),
                  ],
                ),
                const SizedBox(height: AppSpacing.s2),
                Text('Đã chọn ${selected.length}/$maxDomains', style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
                const SizedBox(height: AppSpacing.s3),
                Row(
                  mainAxisAlignment: MainAxisAlignment.end,
                  children: [
                    TextButton(onPressed: saving ? null : onCancel, child: const Text('Hủy')),
                    const SizedBox(width: AppSpacing.s2),
                    LoadingButton(label: 'Lưu', loading: saving, expand: false, onPressed: onSave),
                  ],
                ),
              ],
            );
          },
        );
  }
}
