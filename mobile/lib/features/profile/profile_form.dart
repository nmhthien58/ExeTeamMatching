import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../suggestions/suggestions_api.dart' show specializationsProvider;
import 'profile_api.dart';
import 'profile_form_view_model.dart';

/// Thanh kéo 1–5 (flutter-handoff.md mục 2): [value] null = "Chưa đánh giá"; chạm vào thanh mới tính là đã đánh giá.
/// Khi kéo, nút đi liền theo ngón tay (không nhảy theo nấc), thả tay mới làm tròn về 1–5. Lúc không kéo vẫn có
/// `divisions` để TalkBack tăng / giảm từng nấc.
class SkillSlider extends StatefulWidget {
  const SkillSlider({super.key, required this.label, required this.value, required this.onChanged});

  final String label;
  final int? value;
  final ValueChanged<int> onChanged;

  @override
  State<SkillSlider> createState() => _SkillSliderState();
}

class _SkillSliderState extends State<SkillSlider> {
  double? _drag;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final value = _drag?.round() ?? widget.value;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(child: Text(widget.label, style: theme.textTheme.bodyMedium)),
            Text(
              value == null ? 'Chưa đánh giá' : '$value/5',
              style: theme.textTheme.bodySmall?.copyWith(color: value == null ? theme.colorScheme.onSurfaceVariant : theme.colorScheme.primary),
            ),
          ],
        ),
        Slider(
          min: 1,
          max: 5,
          divisions: _drag == null ? 4 : null,
          value: _drag ?? (widget.value ?? 1).toDouble(),
          // Chưa đánh giá: thanh nhạt để người dùng thấy cần chạm.
          activeColor: value == null ? context.appColors.neutralContainer : null,
          semanticFormatterCallback: (v) => value == null ? '${widget.label}, chưa đánh giá' : '${widget.label}, ${v.round()} trên 5',
          onChanged: (v) {
            setState(() => _drag = v);
            if (v.round() != widget.value) widget.onChanged(v.round());
          },
          onChangeEnd: (v) {
            setState(() => _drag = null);
            widget.onChanged(v.round());
          },
        ),
        // Nhãn hai đầu thang theo Figma `Skill/Slider`; TalkBack đọc được để biết 1 và 5 nghĩa là gì.
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s6),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Chữ phóng to thì mỗi nhãn tự xuống dòng trong nửa của mình, không tràn.
              Expanded(child: Text('Mới bắt đầu', style: theme.textTheme.bodySmall)),
              const SizedBox(width: AppSpacing.s2),
              Expanded(child: Text('Thành thạo', textAlign: TextAlign.end, style: theme.textTheme.bodySmall)),
            ],
          ),
        ),
      ],
    );
  }
}

/// Phần chung của A05 và S03: 5 thanh kỹ năng, giới thiệu (≤ 500), công tắc "Tôi đang tìm nhóm" khi [showSeekingSwitch]
/// (đã có nhóm thì công tắc bị khóa kèm giải thích).
/// View của [ProfileFormViewModel]; lưu xong gọi [onSaved] để màn cha điều hướng.
class ProfileForm extends ConsumerStatefulWidget {
  const ProfileForm({
    super.key,
    required this.profile,
    required this.submitLabel,
    this.onSaved,
    this.showSeekingSwitch = false,
    this.header,
  });

  final StudentProfile profile;
  final String submitLabel;
  final bool showSeekingSwitch;
  final Widget? header;

  /// Gọi sau khi lưu thành công (lỗi thì form hiện banner và giữ dữ liệu đã nhập).
  final VoidCallback? onSaved;

  @override
  ConsumerState<ProfileForm> createState() => _ProfileFormState();
}

class _ProfileFormState extends ConsumerState<ProfileForm> {
  late final _description = TextEditingController(text: widget.profile.description ?? '');
  late final _phone = TextEditingController(text: widget.profile.phone ?? '');

  ProfileFormViewModel get _vm => ref.read(profileFormViewModelProvider(widget.profile).notifier);

  @override
  void dispose() {
    _description.dispose();
    _phone.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final saved = await _vm.save(description: _description.text, phone: _phone.text, withSeeking: widget.showSeekingSwitch && !widget.profile.hasTeam);
    if (saved && mounted) widget.onSaved?.call();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final state = ref.watch(profileFormViewModelProvider(widget.profile));
    final missing = state.missing;
    final allSpecializations = ref.watch(specializationsProvider).value ?? const [];
    final specializations = allSpecializations
        .where((s) => widget.profile.majorId == null || s.majorId == null || s.majorId == widget.profile.majorId)
        .toList();
    final hasMatchingSpec = specializations.any((s) => s.id == state.specializationId);
    final selectedValue = hasMatchingSpec ? state.specializationId : null;
    return ListView(
      padding: const EdgeInsets.all(AppSpacing.s4),
      children: [
        ?widget.header,
        if (state.error != null) ...[ErrorBanner(message: 'Chưa lưu được hồ sơ. ${state.error}', onRetry: _submit), const SizedBox(height: AppSpacing.s4)],
        Text('Chuyên ngành', style: theme.textTheme.titleMedium),
        const SizedBox(height: AppSpacing.s2),
        DropdownButtonFormField<int>(
          isExpanded: true,
          initialValue: selectedValue,
          decoration: InputDecoration(
            labelText: 'Chuyên ngành *',
            hintText: 'Chọn chuyên ngành của bạn',
            errorText: state.missingSpec ? 'Vui lòng chọn chuyên ngành' : null,
          ),
          items: [
            for (final spec in specializations)
              DropdownMenuItem(value: spec.id, child: Text('${spec.name} (${spec.code})', overflow: TextOverflow.ellipsis)),
          ],
          onChanged: (id) => _vm.setSpecialization(id),
        ),
        const SizedBox(height: AppSpacing.s4),
        Text('Kỹ năng', style: theme.textTheme.titleMedium),
        const SizedBox(height: AppSpacing.s2),
        for (final MapEntry(key: skill, value: label) in skillLabels.entries)
          SkillSlider(label: label, value: state.levels[skill], onChanged: (v) => _vm.setLevel(skill, v)),
        const SizedBox(height: AppSpacing.s4),
        TextField(
          controller: _phone,
          keyboardType: TextInputType.phone,
          autofillHints: const [AutofillHints.telephoneNumber],
          decoration: const InputDecoration(
            labelText: 'Số điện thoại (không bắt buộc)',
            hintText: 'VD: 0901234567',
            helperText: 'Chỉ bạn, thành viên cùng nhóm và Bộ môn thấy.',
          ),
        ),
        const SizedBox(height: AppSpacing.s4),
        TextField(
          controller: _description,
          maxLines: 4,
          maxLength: 500,
          decoration: const InputDecoration(labelText: 'Giới thiệu (không bắt buộc)', hintText: 'Bạn muốn làm gì trong nhóm?', alignLabelWithHint: true),
        ),
        if (widget.showSeekingSwitch)
          SwitchListTile(
            contentPadding: EdgeInsets.zero,
            title: const Text('Tôi đang tìm nhóm'),
            subtitle: Text(widget.profile.hasTeam
                ? 'Bạn đã có nhóm trong học kỳ này nên không thể bật.'
                : 'Trưởng nhóm thấy bạn ở mục "Sinh viên tìm nhóm" và có thể mời bạn.'),
            value: state.seeking && !widget.profile.hasTeam,
            onChanged: widget.profile.hasTeam ? null : _vm.setSeeking,
          ),
        const SizedBox(height: AppSpacing.s4),
        if (missing > 0) ...[
          Text('Còn $missing kỹ năng chưa đánh giá', style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
          const SizedBox(height: AppSpacing.s2),
        ],
        if (state.missingSpec) ...[
          Text('Chưa chọn chuyên ngành', style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.error)),
          const SizedBox(height: AppSpacing.s2),
        ],
        LoadingButton(label: widget.submitLabel, loading: state.saving, onPressed: !state.canSave ? null : _submit),
      ],
    );
  }
}
