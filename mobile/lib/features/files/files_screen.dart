import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../../core/theme/app_tokens.dart';
import '../../shared/widgets/states.dart';
import '../workspace/format.dart';
import '../workspace/workspace_api.dart';
import '../workspace/workspace_models.dart';
import '../workspace/workspace_widgets.dart';
import 'file_detail_sheet.dart';
import 'file_services.dart';

/// C06 – File dự án (MB-41, quy tắc 24): đường dẫn thư mục, thư mục rồi file (bản mới nhất), tải lên có tiến độ,
/// trùng tên → hỏi "Tạo phiên bản mới?", lỗi quá 25 MB / sai định dạng tại chỗ, xóa qua dialog.
/// Giảng viên, mentor chỉ xem và bình luận. Thư mục "EXE101 (kỳ trước)" ghim ở đầu, chỉ đọc.
class FilesScreen extends ConsumerStatefulWidget {
  const FilesScreen({super.key, this.workspaceId});

  final String? workspaceId;

  @override
  ConsumerState<FilesScreen> createState() => _FilesScreenState();
}

class _UploadState {
  const _UploadState(this.file, {this.progress = 0, this.error});

  final PickedFile file;
  final double progress;
  final String? error;
}

class _FilesScreenState extends ConsumerState<FilesScreen> {
  /// Thư mục đang mở (null = gốc).
  final List<Folder> _trail = [];
  _UploadState? _upload;

  String? get _folderId => _trail.lastOrNull?.id;

  void _snack(String message) => ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(message)));

  Future<void> _choose(WorkspaceOverview ws, FolderContent content) async {
    final picked = await ref.read(pickLocalFileProvider)();
    if (picked == null || !mounted) return;
    final invalid = validateUpload(picked.name, picked.size);
    if (invalid != null) {
      setState(() => _upload = _UploadState(picked, error: invalid));
      return;
    }
    final existing = content.files.where((f) => f.name.toLowerCase() == picked.name.toLowerCase()).firstOrNull;
    if (existing != null) {
      final ok = await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: const Text('Tạo phiên bản mới?'),
          content: Text(
              '"${existing.name}" đã có (v${existing.currentVersion}). Tải lên sẽ tạo v${existing.currentVersion + 1}; các bản cũ vẫn tải về được.'),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy')),
            TextButton(onPressed: () => Navigator.pop(context, true), child: const Text('Tạo phiên bản mới')),
          ],
        ),
      );
      if (ok != true) return;
    }
    await _send(ws, picked);
  }

  Future<void> _send(WorkspaceOverview ws, PickedFile picked) async {
    setState(() => _upload = _UploadState(picked));
    try {
      final (file, newVersion) = await ref.read(collabApiProvider).upload(
            ws.id,
            await picked.toMultipart(),
            folderId: _folderId,
            onProgress: (p) => mounted ? setState(() => _upload = _UploadState(picked, progress: p)) : null,
          );
      if (!mounted) return;
      setState(() => _upload = null);
      ref.invalidate(folderProvider);
      _snack(newVersion ? 'Đã tải lên phiên bản ${file.currentVersion}' : 'Đã tải lên');
    } catch (e) {
      final error = e.apiError;
      if (mounted) setState(() => _upload = _UploadState(picked, error: error.detail ?? error.title));
    }
  }

  Future<void> _delete(ProjectFile file) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Xóa file?'),
        content: Text('Cả nhóm sẽ không thấy "${file.name}" nữa, kể cả các phiên bản cũ.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy')),
          TextButton(
            style: TextButton.styleFrom(foregroundColor: Theme.of(context).colorScheme.error),
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Xóa'),
          ),
        ],
      ),
    );
    if (ok != true) return;
    try {
      await ref.read(collabApiProvider).deleteFile(file.id);
      ref.invalidate(folderProvider);
      _snack('Đã xóa file');
    } catch (e) {
      if (mounted) {
        await showDialog<void>(
          context: context,
          builder: (context) => AlertDialog(
            title: const Text('Không xóa được'),
            content: Text(e.apiError.title),
            actions: [TextButton(onPressed: () => Navigator.pop(context), child: const Text('Đóng'))],
          ),
        );
      }
    }
  }

  Future<void> _createFolder(WorkspaceOverview ws) async {
    final name = await showDialog<String>(context: context, builder: (_) => const _FolderNameDialog());
    if (name == null || name.isEmpty) return;
    try {
      await ref.read(collabApiProvider).createFolder(ws.id, name, parentId: _folderId);
      ref.invalidate(folderProvider);
    } catch (e) {
      if (mounted) _showError(e.apiError.detail ?? e.apiError.title);
    }
  }

  void _showError(String message) => showDialog<void>(
        context: context,
        builder: (context) => AlertDialog(
          content: Text(message),
          actions: [TextButton(onPressed: () => Navigator.pop(context), child: const Text('Đóng'))],
        ),
      );

  Future<void> _open(ProjectFile file) async {
    try {
      await ref.read(openUrlProvider)(await ref.read(collabApiProvider).downloadUrl(file.id));
    } catch (e) {
      if (mounted) _showError(e.apiError.title);
    }
  }

  @override
  Widget build(BuildContext context) {
    final workspace = ref.watch(currentWorkspaceProvider(widget.workspaceId));
    return workspace.when(
      loading: () => Scaffold(appBar: AppBar(title: const Text('File')), body: const SkeletonList()),
      error: (e, _) => Scaffold(
        appBar: AppBar(title: const Text('File')),
        body: ErrorState(error: e, onRetry: () => ref.invalidate(currentWorkspaceProvider(widget.workspaceId))),
      ),
      data: (ws) => ws == null ? Scaffold(appBar: AppBar(title: const Text('File')), body: const EmptyState(title: 'Bạn chưa có nhóm')) : _files(ws),
    );
  }

  Widget _files(WorkspaceOverview ws) {
    final query = (workspaceId: ws.id, folderId: _folderId);
    final folder = ref.watch(folderProvider(query));
    final content = folder.value;
    final canUpload = content?.canUpload ?? false;

    return PopScope(
      canPop: _trail.isEmpty,
      onPopInvokedWithResult: (didPop, _) => didPop ? null : setState(() => _trail.removeLast()),
      child: Scaffold(
        appBar: AppBar(
          title: Text(_trail.lastOrNull?.name ?? 'File'),
          actions: [
            if (canUpload) IconButton(tooltip: 'Thư mục mới', onPressed: () => _createFolder(ws), icon: const Icon(Icons.create_new_folder_outlined)),
          ],
        ),
        floatingActionButton: canUpload && (_upload == null || _upload!.error != null)
            ? FloatingActionButton.extended(onPressed: () => _choose(ws, content!), icon: const Icon(Icons.upload), label: const Text('Tải lên'))
            : null,
        body: RefreshIndicator(
          onRefresh: () async => ref.invalidate(folderProvider(query)),
          child: ListView(
            padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s2, AppSpacing.s4, 96),
            children: [
              if (_trail.isNotEmpty) _Breadcrumb(trail: _trail, onOpen: (i) => setState(() => _trail.removeRange(i, _trail.length))),
              if (content?.folder?.isArchive ?? false)
                const Padding(
                  padding: EdgeInsets.only(bottom: AppSpacing.s3),
                  child: StatusChip(label: 'Kho EXE101 – chỉ xem', tone: StatusTone.neutral, icon: Icons.lock_outline),
                ),
              if (content != null && !content.canUpload && !ws.canEdit)
                Padding(
                  padding: const EdgeInsets.only(bottom: AppSpacing.s3),
                  child: Text('Giảng viên và mentor xem và bình luận file; chỉ thành viên nhóm tải lên.', style: Theme.of(context).textTheme.bodyMedium),
                ),
              if (_upload != null) _UploadRow(state: _upload!, onRetry: () => _send(ws, _upload!.file), onDismiss: () => setState(() => _upload = null)),
              ...folder.when(
                skipLoadingOnReload: true,
                loading: () => [const SkeletonList(count: 4)],
                error: (e, _) => [ErrorState(error: e, onRetry: () => ref.invalidate(folderProvider(query)))],
                data: (content) => content.folders.isEmpty && content.files.isEmpty
                    ? [
                        EmptyState(
                          icon: Icons.folder_open,
                          title: 'Thư mục trống.',
                          action: canUpload ? FilledButton(onPressed: () => _choose(ws, content), child: const Text('Tải lên')) : null,
                        ),
                      ]
                    : [
                        for (final f in content.folders)
                          Card.outlined(
                            child: ListTile(
                              leading: Icon(f.isArchive ? Icons.history : Icons.folder_outlined,
                                  color: f.isArchive ? context.appColors.info : context.appColors.brandOrange),
                              title: Text(f.name),
                              subtitle: Text('${f.itemCount} mục'),
                              trailing: f.isArchive ? const Icon(Icons.lock_outline, semanticLabel: 'Chỉ xem') : const Icon(Icons.chevron_right),
                              onTap: () => setState(() => _trail.add(f)),
                            ),
                          ),
                        for (final f in content.files)
                          Card.outlined(
                            child: ListTile(
                              leading: Icon(fileIcon(f.name), size: 32),
                              title: Text(f.name, maxLines: 2, overflow: TextOverflow.ellipsis),
                              subtitle: Text('v${f.currentVersion} · ${f.updatedByName} · ${formatRelative(f.updatedAt)}'),
                              onTap: () => showFileDetailSheet(context, workspace: ws, fileId: f.id),
                              trailing: PopupMenuButton<String>(
                                tooltip: 'Tùy chọn của ${f.name}',
                                onSelected: (v) => v == 'open' ? _open(f) : _delete(f),
                                itemBuilder: (_) => [
                                  const PopupMenuItem(value: 'open', child: Text('Tải xuống')),
                                  if (canUpload && !f.isArchive) const PopupMenuItem(value: 'delete', child: Text('Xóa')),
                                ],
                              ),
                            ),
                          ),
                      ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Breadcrumb extends StatelessWidget {
  const _Breadcrumb({required this.trail, required this.onOpen});

  final List<Folder> trail;

  /// Mở lại thư mục ở vị trí i (0 = gốc).
  final ValueChanged<int> onOpen;

  @override
  Widget build(BuildContext context) => Wrap(
        crossAxisAlignment: WrapCrossAlignment.center,
        children: [
          TextButton(onPressed: () => onOpen(0), child: const Text('Tất cả file')),
          for (var i = 0; i < trail.length; i++) ...[
            const Icon(Icons.chevron_right, size: 20),
            i == trail.length - 1
                ? Padding(padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s2), child: Text(trail[i].name, style: Theme.of(context).textTheme.labelLarge))
                : TextButton(onPressed: () => onOpen(i + 1), child: Text(trail[i].name)),
          ],
        ],
      );
}

class _UploadRow extends StatelessWidget {
  const _UploadRow({required this.state, required this.onRetry, required this.onDismiss});

  final _UploadState state;
  final VoidCallback onRetry;
  final VoidCallback onDismiss;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    if (state.error != null) {
      return Padding(
        padding: const EdgeInsets.only(bottom: AppSpacing.s3),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            // Lỗi kích thước / định dạng không thử lại được – chọn file khác.
            ErrorBanner(message: '${state.file.name}: ${state.error}', onRetry: validateUpload(state.file.name, state.file.size) == null ? onRetry : null),
            TextButton(onPressed: onDismiss, child: const Text('Đóng')),
          ],
        ),
      );
    }
    return Card.outlined(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.s4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(state.file.name, style: theme.textTheme.labelLarge, maxLines: 1, overflow: TextOverflow.ellipsis),
            const SizedBox(height: AppSpacing.s2),
            LinearProgressIndicator(value: state.progress, semanticsLabel: 'Tiến độ tải lên', semanticsValue: '${(state.progress * 100).round()}%'),
          ],
        ),
      ),
    );
  }
}

class _FolderNameDialog extends StatefulWidget {
  const _FolderNameDialog();

  @override
  State<_FolderNameDialog> createState() => _FolderNameDialogState();
}

class _FolderNameDialogState extends State<_FolderNameDialog> {
  final _controller = TextEditingController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
        title: const Text('Thư mục mới'),
        content: TextField(controller: _controller, autofocus: true, maxLength: 100, decoration: const InputDecoration(labelText: 'Tên thư mục')),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Hủy')),
          TextButton(onPressed: () => Navigator.pop(context, _controller.text.trim()), child: const Text('Tạo thư mục')),
        ],
      );
}
