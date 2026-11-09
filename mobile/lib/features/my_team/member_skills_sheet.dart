import 'package:flutter/material.dart';

import '../../core/theme/app_tokens.dart';
import '../profile/skill_bars.dart';
import 'my_team_api.dart';

/// Bấm vào thành viên (S08, S13): bottom sheet 5 kỹ năng tự đánh giá của thành viên.
Future<void> showMemberSkills(BuildContext context, Member m) => showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      builder: (context) {
        final theme = Theme.of(context);
        return SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(AppSpacing.s4, 0, AppSpacing.s4, AppSpacing.s4),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(m.fullName, style: theme.textTheme.titleMedium),
                Text([m.studentCode, if (m.specialization?.name.isNotEmpty ?? false) m.specialization!.name].join(' · ')),
                const SizedBox(height: AppSpacing.s3),
                Text('Kỹ năng', style: theme.textTheme.titleSmall),
                const SizedBox(height: AppSpacing.s2),
                SkillBars(levels: m.skills),
              ],
            ),
          ),
        );
      },
    );
