import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/network/api_client.dart';
import '../../core/network/api_exception.dart';
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
  const LecturerProfile({
    required this.fullName,
    required this.email,
    required this.domains,
    this.maxTeamsExe101,
    this.maxTeamsExe201,
    this.desiredTeamsExe101,
    this.desiredTeamsExe201,
    this.isProfileCompleted = true,
    this.profileCompletedAt,
    this.phone,
  });

  final String fullName;
  final String email;
  final List<CatalogRef> domains;

  /// Do Bộ môn đặt (W11); null = không giới hạn.
  final int? maxTeamsExe101;
  final int? maxTeamsExe201;

  /// Giảng viên tự khai báo (BE-93).
  final int? desiredTeamsExe101;
  final int? desiredTeamsExe201;
  final bool isProfileCompleted;
  final String? profileCompletedAt;
  final String? phone;

  factory LecturerProfile.fromJson(Map<String, dynamic> j) => LecturerProfile(
        fullName: j['fullName'] as String? ?? '',
        email: j['email'] as String? ?? '',
        domains: [for (final d in (j['domains'] as List? ?? const [])) CatalogRef.fromJson(Map<String, dynamic>.from(d as Map))],
        maxTeamsExe101: j['maxTeamsExe101'] as int?,
        maxTeamsExe201: j['maxTeamsExe201'] as int?,
        desiredTeamsExe101: j['desiredTeamsExe101'] as int?,
        desiredTeamsExe201: j['desiredTeamsExe201'] as int?,
        isProfileCompleted: j['isProfileCompleted'] as bool? ?? false,
        profileCompletedAt: j['profileCompletedAt'] as String?,
        phone: j['phone'] as String?,
      );
}

/// academic.yaml – Lecturers `me` (L06). Giảng viên sửa domain chuyên môn và số nhóm mong muốn; số nhóm tối đa do Bộ môn đặt.
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

  Future<LecturerProfile> saveProfile({
    required List<int> domainIds,
    int? desiredTeamsExe101,
    int? desiredTeamsExe201,
  }) async {
    try {
      final data = <String, dynamic>{
        'domainIds': domainIds,
      };
      if (desiredTeamsExe101 != null) data['desiredTeamsExe101'] = desiredTeamsExe101;
      if (desiredTeamsExe201 != null) data['desiredTeamsExe201'] = desiredTeamsExe201;
      return LecturerProfile.fromJson((await _dio.put<Map<String, dynamic>>('/api/lecturers/me', data: data)).data!);
    } on DioException catch (e) {
      throw e.apiError;
    }
  }

  Future<LecturerProfile> saveDomains(List<int> domainIds) => saveProfile(domainIds: domainIds);

  /// Chỉ đổi SĐT; gửi kèm domain hiện có vì API bắt buộc `domainIds`. Chuỗi rỗng thì xóa SĐT.
  Future<LecturerProfile> savePhone(LecturerProfile profile, String phone) async {
    try {
      final data = {'domainIds': [for (final d in profile.domains) d.id], 'phone': phone.trim()};
      return LecturerProfile.fromJson((await _dio.put<Map<String, dynamic>>('/api/lecturers/me', data: data)).data!);
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

/// Khảo sát lần đầu (MB-66): giảng viên chưa khai báo chuyên môn và số nhóm mong muốn thì thấy L06 ở chế độ sửa
/// thay cho [child]. Lưu xong hồ sơ được làm mới, [child] hiện ngay tại chỗ. Không đọc được hồ sơ thì không chặn.
class LecturerProfileGate extends ConsumerWidget {
  const LecturerProfileGate({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final profile = ref.watch(lecturerProfileProvider);
    if (profile.isLoading && !profile.hasValue) {
      return const Scaffold(body: SafeArea(child: SkeletonList(count: 3)));
    }
    final p = profile.value;
    if (p != null && !p.isProfileCompleted) return _LecturerSurveyScreen(profile: p);
    return child;
  }
}

class _LecturerSurveyScreen extends ConsumerStatefulWidget {
  const _LecturerSurveyScreen({required this.profile});

  final LecturerProfile profile;

  @override
  ConsumerState<_LecturerSurveyScreen> createState() => _LecturerSurveyScreenState();
}

class _LecturerSurveyScreenState extends ConsumerState<_LecturerSurveyScreen> {
  @override
  void initState() {
    super.initState();
    // Mở sẵn chế độ sửa để giảng viên khai báo ngay.
    Future.microtask(() {
      if (!mounted) return;
      if (ref.read(lecturerProfileViewModelProvider).editing == null) {
        ref.read(lecturerProfileViewModelProvider.notifier).startEditing(widget.profile);
      }
    });
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Khảo sát giảng viên'), automaticallyImplyLeading: false),
        body: _LecturerProfileView(profile: widget.profile, survey: true),
      );
}

class _LecturerProfileView extends ConsumerWidget {
  const _LecturerProfileView({required this.profile, this.survey = false});

  final LecturerProfile profile;

  /// Khảo sát lần đầu: mở sẵn chế độ sửa, không có nút Hủy.
  final bool survey;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final vm = ref.read(lecturerProfileViewModelProvider.notifier);
    final state = ref.watch(lecturerProfileViewModelProvider);
    Future<void> save() async {
      if (!await vm.save(profile) || !context.mounted) return;
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
        if (!p.isProfileCompleted) ...[
          Container(
            padding: const EdgeInsets.all(AppSpacing.s3),
            decoration: BoxDecoration(
              color: theme.colorScheme.errorContainer.withValues(alpha: 0.4),
              borderRadius: BorderRadius.circular(AppRadius.md),
              border: Border.all(color: theme.colorScheme.error),
            ),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Icon(Icons.warning_amber_rounded, color: theme.colorScheme.error),
                const SizedBox(width: AppSpacing.s2),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Hồ sơ chưa hoàn thiện',
                        style: theme.textTheme.titleSmall?.copyWith(color: theme.colorScheme.error, fontWeight: FontWeight.bold),
                      ),
                      const SizedBox(height: AppSpacing.s1),
                      Text(
                        'Bạn cần chọn ít nhất 1 hướng chuyên môn và khai báo số nhóm mong muốn hướng dẫn (tối đa ${p.maxTeamsExe101 ?? 4} nhóm cho EXE101 và ${p.maxTeamsExe201 ?? 4} nhóm cho EXE201) trước khi được tham gia ghép nhóm tự động.',
                        style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onErrorContainer),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: AppSpacing.s4),
        ],
        Text(p.fullName, style: theme.textTheme.titleLarge),
        const SizedBox(height: AppSpacing.s1),
        Text(p.email, style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
        const SizedBox(height: AppSpacing.s4),
        Row(
          children: [
            Expanded(child: Text('Số điện thoại', style: theme.textTheme.titleMedium)),
            TextButton(onPressed: () => _editPhone(context, ref, p), child: const Text('Sửa')),
          ],
        ),
        Text(p.phone?.isNotEmpty == true ? p.phone! : 'Chưa khai báo', style: theme.textTheme.bodyMedium),
        Text('Chỉ nhóm bạn hướng dẫn và Bộ môn thấy.', style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
        const SizedBox(height: AppSpacing.s4),
        Row(
          children: [
            Expanded(child: Text('Số nhóm mong muốn hướng dẫn', style: theme.textTheme.titleMedium)),
            if (editing == null) TextButton(onPressed: () => vm.startEditing(p), child: const Text('Sửa')),
          ],
        ),
        const SizedBox(height: AppSpacing.s2),
        Text(
          'EXE101: ${p.desiredTeamsExe101 != null ? '${p.desiredTeamsExe101} nhóm' : 'Chưa khai báo'} · EXE201: ${p.desiredTeamsExe201 != null ? '${p.desiredTeamsExe201} nhóm' : 'Chưa khai báo'}',
          style: theme.textTheme.bodyMedium,
        ),
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
            profile: p,
            desiredTeamsExe101: state.desiredTeamsExe101,
            desiredTeamsExe201: state.desiredTeamsExe201,
            saving: state.saving,
            error: state.error,
            onToggle: vm.toggle,
            onChangedDesired101: vm.setDesiredTeamsExe101,
            onChangedDesired201: vm.setDesiredTeamsExe201,
            onCancel: survey ? null : vm.cancel,
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
    required this.profile,
    required this.desiredTeamsExe101,
    required this.desiredTeamsExe201,
    required this.saving,
    required this.error,
    required this.onToggle,
    required this.onChangedDesired101,
    required this.onChangedDesired201,
    required this.onCancel,
    required this.onSave,
  });

  final Set<int> selected;
  final List<CatalogRef> current;
  final LecturerProfile profile;
  final int? desiredTeamsExe101;
  final int? desiredTeamsExe201;
  final bool saving;
  final String? error;
  final ValueChanged<int> onToggle;
  final ValueChanged<int?> onChangedDesired101;
  final ValueChanged<int?> onChangedDesired201;
  final VoidCallback? onCancel;
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
                const SizedBox(height: AppSpacing.s2),
                Row(
                  children: [
                    Expanded(
                      child: TextFormField(
                        initialValue: desiredTeamsExe101?.toString() ?? '',
                        keyboardType: TextInputType.number,
                        decoration: InputDecoration(
                          labelText: 'Mong muốn EXE101 (tối đa ${profile.maxTeamsExe101 ?? 4})',
                          hintText: 'Số nhóm',
                          isDense: true,
                        ),
                        onChanged: (v) => onChangedDesired101(int.tryParse(v)),
                      ),
                    ),
                    const SizedBox(width: AppSpacing.s2),
                    Expanded(
                      child: TextFormField(
                        initialValue: desiredTeamsExe201?.toString() ?? '',
                        keyboardType: TextInputType.number,
                        decoration: InputDecoration(
                          labelText: 'Mong muốn EXE201 (tối đa ${profile.maxTeamsExe201 ?? 4})',
                          hintText: 'Số nhóm',
                          isDense: true,
                        ),
                        onChanged: (v) => onChangedDesired201(int.tryParse(v)),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: AppSpacing.s3),
                Row(
                  mainAxisAlignment: MainAxisAlignment.end,
                  children: [
                    if (onCancel != null) ...[
                      TextButton(onPressed: saving ? null : onCancel, child: const Text('Hủy')),
                      const SizedBox(width: AppSpacing.s2),
                    ],
                    LoadingButton(label: 'Lưu', loading: saving, expand: false, onPressed: onSave),
                  ],
                ),
              ],
            );
          },
        );
  }
}

/// Hộp sửa SĐT giảng viên; lỗi định dạng từ API hiện ngay dưới ô nhập.
Future<void> _editPhone(BuildContext context, WidgetRef ref, LecturerProfile profile) async {
  final saved = await showDialog<bool>(context: context, builder: (_) => _PhoneDialog(profile: profile));
  if (saved == true && context.mounted) {
    ref.invalidate(lecturerProfileProvider);
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(const SnackBar(content: Text('Đã lưu số điện thoại')));
  }
}

class _PhoneDialog extends ConsumerStatefulWidget {
  const _PhoneDialog({required this.profile});

  final LecturerProfile profile;

  @override
  ConsumerState<_PhoneDialog> createState() => _PhoneDialogState();
}

class _PhoneDialogState extends ConsumerState<_PhoneDialog> {
  late final _phone = TextEditingController(text: widget.profile.phone ?? '');
  String? _error;
  bool _saving = false;

  @override
  void dispose() {
    _phone.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await ref.read(lecturerProfileApiProvider).savePhone(widget.profile, _phone.text);
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (e) {
      if (mounted) {
        setState(() {
          _saving = false;
          _error = e.fieldErrors['phone']?.first ?? e.detail ?? e.title;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
        title: const Text('Số điện thoại'),
        content: TextField(
          controller: _phone,
          autofocus: true,
          keyboardType: TextInputType.phone,
          autofillHints: const [AutofillHints.telephoneNumber],
          decoration: InputDecoration(hintText: 'VD: 0901234567', helperText: 'Để trống để xóa số.', errorText: _error),
        ),
        actions: [
          TextButton(onPressed: _saving ? null : () => Navigator.pop(context, false), child: const Text('Hủy')),
          TextButton(onPressed: _saving ? null : _save, child: const Text('Lưu')),
        ],
      );
}
