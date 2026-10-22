import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../workspace/format.dart';
import '../workspace/workspace_api.dart';
import '../workspace/workspace_models.dart';
import '../workspace/workspace_widgets.dart';
import 'file_services.dart';

/// Chi tiết file trong C06 (bottom sheet): các phiên bản (tải từng bản) và bình luận theo phiên bản.
Future<void> showFileDetailSheet(BuildContext context, {required WorkspaceOverview workspace, required String fileId}) =>
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      showDragHandle: true,
      builder: (_) => _FileDetail(workspace: workspace, fileId: fileId),
    );

class _FileDetail extends ConsumerWidget {
  const _FileDetail({required this.workspace, required this.fileId});

  final WorkspaceOverview workspace;
  final String fileId;

  Future<void> _download(BuildContext context, WidgetRef ref, int version) async {
    try {
      await ref.read(openUrlProvider)(await ref.read(collabApiProvider).downloadUrl(fileId, version: version));
    } catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.apiError.title)));
      }
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(fileDetailProvider(fileId));
    final theme = Theme.of(context);
    return SizedBox(
      height: MediaQuery.sizeOf(context).height * 0.85,
      child: detail.when(
        loading: () => const SkeletonList(count: 2),
        error: (e, _) => ErrorState(error: e, onRetry: () => ref.invalidate(fileDetailProvider(fileId))),
        data: (d) {
          final canComment = workspace.myRole != WorkspaceRole.admin && !d.file.isArchive;
          return Column(
            children: [
              Expanded(
                child: ListView(
                  padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s6),
                  children: [
                    Row(
                      children: [
                        Icon(fileIcon(d.file.name), size: 48, color: theme.colorScheme.onSurfaceVariant),
                        const SizedBox(width: AppSpacing.s3),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(d.file.name, style: theme.textTheme.titleMedium),
                              Text('v${d.file.currentVersion} · ${formatSize(d.file.sizeBytes)}', style: theme.textTheme.bodyMedium),
                            ],
                          ),
                        ),
                      ],
                    ),
                    if (d.file.isArchive)
                      const Padding(
                        padding: EdgeInsets.only(top: AppSpacing.s3),
                        child: StatusChip(label: 'File của EXE101 – chỉ xem', tone: StatusTone.neutral, icon: Icons.lock_outline),
                      ),
                    const SizedBox(height: AppSpacing.s4),
                    Text('Phiên bản', style: theme.textTheme.titleMedium),
                    for (final v in d.versions)
                      ListTile(
                        contentPadding: EdgeInsets.zero,
                        leading: Text('v${v.version}', style: theme.textTheme.labelLarge),
                        title: Text('${v.uploadedByName} · ${formatRelative(v.createdAt)}'),
                        subtitle: Text(formatSize(v.sizeBytes)),
                        trailing: IconButton(
                          tooltip: 'Tải phiên bản ${v.version}',
                          icon: const Icon(Icons.download),
                          onPressed: () => _download(context, ref, v.version),
                        ),
                      ),
                    const SizedBox(height: AppSpacing.s4),
                    Text('Bình luận (${d.comments.length})', style: theme.textTheme.titleMedium),
                    const SizedBox(height: AppSpacing.s2),
                    CommentList(comments: d.comments),
                  ],
                ),
              ),
              if (canComment)
                DecoratedBox(
                  decoration: BoxDecoration(border: Border(top: BorderSide(color: theme.colorScheme.outlineVariant))),
                  child: Padding(
                    padding: EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s2, AppSpacing.s4, AppSpacing.s2 + MediaQuery.viewInsetsOf(context).bottom),
                    child: CommentComposer(
                      onSend: (body) async {
                        await ref.read(collabApiProvider).commentFile(fileId, body, version: d.file.currentVersion);
                        ref.invalidate(fileDetailProvider(fileId));
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
