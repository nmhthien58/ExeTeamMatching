import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/phase.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../suggestions/suggestions_api.dart';
import '../workspace/workspace_widgets.dart';
import 'profile_api.dart';
import 'profile_form.dart';
import 'skill_bars.dart';

const editProfilePath = '/sv/ho-so/sua';

/// Sinh viên chưa đánh giá đủ 5 kỹ năng thì thấy A05 thay cho [child] (quy tắc 16). Đọc hồ sơ thật thay vì
/// `profileCompleted` trong phiên vì cờ đó không đổi sau khi lưu. Không đọc được hồ sơ thì không chặn.
class ProfileGate extends ConsumerWidget {
  const ProfileGate({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final profile = ref.watch(myProfileProvider);
    if (profile.isLoading && !profile.hasValue) return const Scaffold(body: SafeArea(child: SkeletonList(count: 3)));
    if (profile.value?.profileCompleted == false) return CompleteProfileScreen(profile: profile.value!);
    return child;
  }
}

/// A05 – Hoàn thiện hồ sơ lần đầu (MB-08): thông tin import chỉ đọc, thu gọn; 5 SkillSlider; giới thiệu. Không có SĐT/Zalo.
/// Lưu xong hồ sơ được làm mới, [ProfileGate] hiện S01 ngay tại chỗ nên Back không quay lại A05.
class CompleteProfileScreen extends ConsumerWidget {
  const CompleteProfileScreen({super.key, required this.profile});

  final StudentProfile profile;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final subtitle = [profile.fullName, profile.studentCode, profile.specialization].where((s) => s.isNotEmpty).join(' · ');
    return Scaffold(
      appBar: AppBar(title: const Text('Hoàn thiện hồ sơ'), automaticallyImplyLeading: false),
      body: ProfileForm(
        profile: profile,
        submitLabel: 'Hoàn tất',
        header: Card(
          margin: const EdgeInsets.only(bottom: AppSpacing.s4),
          child: ExpansionTile(
            title: const Text('Thông tin từ bộ môn'),
            subtitle: Text(subtitle, maxLines: 1, overflow: TextOverflow.ellipsis),
            childrenPadding: const EdgeInsets.fromLTRB(AppSpacing.s4, 0, AppSpacing.s4, AppSpacing.s4),
            expandedCrossAxisAlignment: CrossAxisAlignment.start,
            children: [
              _InfoRow('Họ tên', profile.fullName),
              _InfoRow('MSSV', profile.studentCode),
              _InfoRow('Chuyên ngành', profile.specialization),
              _InfoRow('Mã lớp', profile.classCode ?? 'Chưa có'),
            ],
          ),
        ),
        onSubmit: ({required skills, required description, isSeekingTeam}) async {
          await ref.read(profileApiProvider).saveMe(skills: skills, description: description);
          ref.invalidate(myProfileProvider);
        },
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow(this.label, this.value);

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: AppSpacing.s1),
      child: Row(
        children: [
          SizedBox(width: 112, child: Text(label, style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant))),
          Expanded(child: Text(value, style: theme.textTheme.bodyMedium)),
        ],
      ),
    );
  }
}

/// S02 – Hồ sơ của tôi (MB-12): SkillBars, giới thiệu, nhóm hiện tại; không có mục liên lạc (quy tắc 16).
class MyProfileScreen extends ConsumerWidget {
  const MyProfileScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Hồ sơ của tôi'),
        actions: [IconButton(tooltip: 'Sửa hồ sơ', icon: const Icon(Icons.edit_outlined), onPressed: () => context.push(editProfilePath))],
      ),
      body: ref.watch(myProfileProvider).when(
            loading: () => const SkeletonList(count: 3),
            error: (e, _) => ErrorState(error: e, onRetry: () => ref.invalidate(myProfileProvider)),
            data: (profile) => RefreshIndicator(
              onRefresh: () async {
                ref.invalidate(myTeamProvider);
                ref.invalidate(myProfileProvider);
                await ref.read(myProfileProvider.future);
              },
              child: _ProfileView(profile: profile),
            ),
          ),
    );
  }
}

class _ProfileView extends ConsumerWidget {
  const _ProfileView({required this.profile});

  final StudentProfile profile;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final team = ref.watch(myTeamProvider);
    final subtitle = [profile.studentCode, profile.specialization, profile.classCode].whereType<String>().where((s) => s.isNotEmpty).join(' · ');
    return ListView(
      padding: const EdgeInsets.all(AppSpacing.s4),
      children: [
        Text(profile.fullName, style: theme.textTheme.titleLarge),
        const SizedBox(height: AppSpacing.s1),
        Text(subtitle, style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
        if (profile.isSeekingTeam) ...[
          const SizedBox(height: AppSpacing.s2),
          const Align(alignment: Alignment.centerLeft, child: StatusChip(label: 'Đang tìm nhóm', tone: StatusTone.info, icon: Icons.person_search_outlined)),
        ],
        const SizedBox(height: AppSpacing.s4),
        Text('Kỹ năng', style: theme.textTheme.titleMedium),
        const SizedBox(height: AppSpacing.s2),
        SkillBars(levels: profile.skills),
        const SizedBox(height: AppSpacing.s4),
        Text('Giới thiệu', style: theme.textTheme.titleMedium),
        const SizedBox(height: AppSpacing.s2),
        Text(profile.description?.isNotEmpty == true ? profile.description! : 'Chưa có giới thiệu.', style: theme.textTheme.bodyMedium),
        const SizedBox(height: AppSpacing.s4),
        Text('Nhóm hiện tại', style: theme.textTheme.titleMedium),
        const SizedBox(height: AppSpacing.s2),
        team.when(
          loading: () => const Skeleton(width: 180, height: 20),
          error: (e, _) => ErrorBanner(message: 'Không tải được nhóm.', onRetry: () => ref.invalidate(myTeamProvider)),
          data: (team) => team == null
              ? Text('Bạn chưa có nhóm.', style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant))
              : Card(
                  margin: EdgeInsets.zero,
                  child: ListTile(
                    leading: const Icon(Icons.groups_outlined),
                    title: Text(team.card.name),
                    subtitle: Text('${team.card.memberCount} thành viên'),
                    trailing: const Icon(Icons.chevron_right),
                    onTap: () => context.go('/sv/nhom'),
                  ),
                ),
        ),
      ],
    );
  }
}

/// S03 – Chỉnh sửa hồ sơ (MB-12): 5 SkillSlider, giới thiệu, công tắc "Tôi đang tìm nhóm" chỉ trong giai đoạn lập nhóm.
class EditProfileScreen extends ConsumerWidget {
  const EditProfileScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final isTeamFormation = ref.watch(shellPhaseProvider).isTeamFormation;
    return Scaffold(
      appBar: AppBar(title: const Text('Chỉnh sửa hồ sơ')),
      body: ref.watch(myProfileProvider).when(
            loading: () => const SkeletonList(count: 3),
            error: (e, _) => ErrorState(error: e, onRetry: () => ref.invalidate(myProfileProvider)),
            data: (profile) => ProfileForm(
              profile: profile,
              submitLabel: 'Lưu',
              showSeekingSwitch: isTeamFormation && !profile.hasTeam,
              onSubmit: ({required skills, required description, isSeekingTeam}) async {
                await ref.read(profileApiProvider).saveMe(skills: skills, description: description, isSeekingTeam: isSeekingTeam);
                ref.invalidate(myProfileProvider);
                if (!context.mounted) return;
                ScaffoldMessenger.of(context)
                  ..hideCurrentSnackBar()
                  ..showSnackBar(const SnackBar(content: Text('Đã lưu hồ sơ')));
                context.pop();
              },
            ),
          ),
    );
  }
}
