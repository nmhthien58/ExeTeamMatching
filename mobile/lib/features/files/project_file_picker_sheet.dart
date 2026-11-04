import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../workspace/format.dart';
import '../workspace/workspace_api.dart';
import 'file_services.dart';
import 'files_view_model.dart';

/// Chọn file dự án để nộp bài (C05 → C06): duyệt thư mục, chọn file → trả id **phiên bản mới nhất**.
/// Kho EXE101 không hiện vì không nộp được file kỳ trước.
Future<String?> pickProjectFileVersion(BuildContext context, String workspaceId) => showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      showDragHandle: true,
      builder: (_) => _ProjectFilePicker(workspaceId: workspaceId),
    );

class _ProjectFilePicker extends ConsumerStatefulWidget {
  const _ProjectFilePicker({required this.workspaceId});

  final String workspaceId;

  @override
  ConsumerState<_ProjectFilePicker> createState() => _ProjectFilePickerState();
}

class _ProjectFilePickerState extends ConsumerState<_ProjectFilePicker> {
  ProjectFilePickerViewModel get _vm => ref.read(projectFilePickerViewModelProvider(widget.workspaceId).notifier);

  Future<void> _pick(String fileId) async {
    final versionId = await _vm.pick(fileId);
    if (versionId != null && mounted) Navigator.pop(context, versionId);
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(projectFilePickerViewModelProvider(widget.workspaceId));
    final folder = ref.watch(folderProvider(_vm.query));
    final theme = Theme.of(context);
    return SizedBox(
      height: MediaQuery.sizeOf(context).height * 0.7,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s6),
            child: Text('Chọn file để nộp', style: theme.textTheme.titleLarge),
          ),
          if (state.path.isNotEmpty)
            TextButton.icon(onPressed: _vm.back, icon: const Icon(Icons.arrow_back), label: const Text('Quay lại')),
          if (state.error != null) Padding(padding: const EdgeInsets.all(AppSpacing.s4), child: ErrorBanner(message: state.error!)),
          Expanded(
            child: folder.when(
              loading: () => const SkeletonList(count: 3),
              error: (e, _) => ErrorState(error: e, onRetry: _vm.retry),
              data: (content) {
                final folders = ProjectFilePickerViewModel.selectableFolders(content);
                if (folders.isEmpty && content.files.isEmpty) {
                  return const EmptyState(icon: Icons.folder_open, title: 'Thư mục trống.', description: 'Tải file lên ở mục File trước.');
                }
                return ListView(
                  children: [
                    for (final f in folders)
                      ListTile(
                        leading: Icon(Icons.folder_outlined, color: theme.colorScheme.primary),
                        title: Text(f.name),
                        trailing: const Icon(Icons.chevron_right),
                        onTap: () => _vm.openFolder(f.id),
                      ),
                    for (final f in content.files)
                      ListTile(
                        enabled: state.resolving == null,
                        leading: Icon(fileIcon(f.name)),
                        title: Text(f.name, maxLines: 2, overflow: TextOverflow.ellipsis),
                        subtitle: Text('v${f.currentVersion} · ${f.updatedByName} · ${formatRelative(f.updatedAt)}'),
                        trailing: state.resolving == f.id ? const SizedBox.square(dimension: 20, child: CircularProgressIndicator(strokeWidth: 2)) : null,
                        onTap: () => _pick(f.id),
                      ),
                  ],
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}
