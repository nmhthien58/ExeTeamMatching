import 'package:flutter/material.dart';

import '../../core/network/api_exception.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import 'profile_api.dart';

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
      ],
    );
  }
}

/// Phần chung của A05 và S03: 5 thanh kỹ năng, giới thiệu (≤ 500), công tắc "Tôi đang tìm nhóm" khi [showSeekingSwitch].
class ProfileForm extends StatefulWidget {
  const ProfileForm({
    super.key,
    required this.profile,
    required this.submitLabel,
    required this.onSubmit,
    this.showSeekingSwitch = false,
    this.header,
  });

  final StudentProfile profile;
  final String submitLabel;
  final bool showSeekingSwitch;
  final Widget? header;

  /// Lưu; lỗi ném [ApiException] để form hiện banner và giữ dữ liệu đã nhập.
  final Future<void> Function({required Map<String, int> skills, required String description, bool? isSeekingTeam}) onSubmit;

  @override
  State<ProfileForm> createState() => _ProfileFormState();
}

class _ProfileFormState extends State<ProfileForm> {
  late final Map<String, int> _levels = {...widget.profile.skills};
  late final _description = TextEditingController(text: widget.profile.description ?? '');
  late bool _seeking = widget.profile.isSeekingTeam;
  bool _saving = false;
  String? _error;

  @override
  void dispose() {
    _description.dispose();
    super.dispose();
  }

  int get _missing => skillLabels.keys.where((k) => !_levels.containsKey(k)).length;

  Future<void> _submit() async {
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await widget.onSubmit(
        skills: Map.of(_levels),
        description: _description.text.trim(),
        isSeekingTeam: widget.showSeekingSwitch ? _seeking : null,
      );
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.detail ?? e.title);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return ListView(
      padding: const EdgeInsets.all(AppSpacing.s4),
      children: [
        ?widget.header,
        if (_error != null) ...[ErrorBanner(message: 'Chưa lưu được hồ sơ. $_error', onRetry: _submit), const SizedBox(height: AppSpacing.s4)],
        Text('Kỹ năng', style: theme.textTheme.titleMedium),
        const SizedBox(height: AppSpacing.s2),
        for (final MapEntry(key: skill, value: label) in skillLabels.entries)
          SkillSlider(label: label, value: _levels[skill], onChanged: (v) => setState(() => _levels[skill] = v)),
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
            subtitle: const Text('Nhóm khác thấy bạn trong danh sách tìm thành viên.'),
            value: _seeking,
            onChanged: (v) => setState(() => _seeking = v),
          ),
        const SizedBox(height: AppSpacing.s4),
        if (_missing > 0) ...[
          Text('Còn $_missing kỹ năng chưa đánh giá', style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
          const SizedBox(height: AppSpacing.s2),
        ],
        LoadingButton(label: widget.submitLabel, loading: _saving, onPressed: _missing > 0 ? null : _submit),
      ],
    );
  }
}
