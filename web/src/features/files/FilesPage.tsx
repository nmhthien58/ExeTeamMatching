import { useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import clsx from 'clsx'
import { toApiError } from '@/shared/api/apiClient'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Banner, Button, Dialog, Icon, Skeleton, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { formatRelative, formatSize } from '@/features/workspace/format'
import { useWorkspaceContext } from '@/features/workspace/context'
import {
  ACCEPT_ATTRIBUTE,
  downloadFile,
  fileIcon,
  useCreateFolder,
  useDeleteFile,
  useFolder,
  useUploadFile,
  validateUpload,
  type Folder,
  type ProjectFile,
} from './api'
import { FileDetailDrawer } from './FileDetailDrawer'

interface UploadState {
  file: File
  progress: number
  error?: string
}

/**
 * C06 – File dự án (FE-40, rule 24): thư mục, file (chỉ bản mới nhất), tải lên có tiến độ, trùng tên → hỏi tạo phiên bản mới,
 * xóa mềm qua dialog. Giảng viên / mentor chỉ xem và bình luận. Thư mục "EXE101 (kỳ trước)" ghim ở đầu, chỉ đọc.
 * Thư mục đang mở và file đang xem nằm trên URL (`?thu-muc=`, `?file=`).
 */
export function FilesPage() {
  const { workspace } = useWorkspaceContext()
  const [params, setParams] = useSearchParams()
  const folderId = params.get('thu-muc') ?? undefined
  const openFileId = params.get('file') ?? undefined
  const folder = useFolder(workspace.id, folderId)
  const upload = useUploadFile(workspace.id)
  const createFolder = useCreateFolder(workspace.id)
  const remove = useDeleteFile(workspace.id)
  const snackbar = useSnackbar()
  const input = useRef<HTMLInputElement>(null)
  const [uploading, setUploading] = useState<UploadState | null>(null)
  const [versionConfirm, setVersionConfirm] = useState<{ file: File; existing: ProjectFile } | null>(null)
  const [deleting, setDeleting] = useState<ProjectFile | null>(null)
  const [folderDialog, setFolderDialog] = useState(false)
  const [folderName, setFolderName] = useState('')
  const content = folder.data
  const canUpload = !!content?.canUpload

  const setParam = (key: string, value?: string) => {
    const next = new URLSearchParams(params)
    if (value) next.set(key, value)
    else next.delete(key)
    setParams(next)
  }

  const startUpload = (file: File) => {
    const invalid = validateUpload(file)
    if (invalid) {
      setUploading({ file, progress: 0, error: invalid })
      return
    }
    setUploading({ file, progress: 0 })
    upload.mutate(
      { file, folderId, onProgress: (progress) => setUploading((u) => (u ? { ...u, progress } : u)) },
      {
        onSuccess: (result) => {
          setUploading(null)
          snackbar.show(result.createdNewVersion ? `Đã tải lên phiên bản ${result.file.currentVersion}` : 'Đã tải lên')
        },
        onError: (error) => setUploading({ file, progress: 0, error: toApiError(error).detail ?? toApiError(error).title }),
      },
    )
  }

  /** Trùng tên (không phân biệt hoa thường) trong thư mục → hỏi trước khi tạo phiên bản mới. */
  const onChosen = (file: File | undefined) => {
    if (!file) return
    const existing = content?.files.find((f) => f.name.toLowerCase() === file.name.toLowerCase())
    if (existing) setVersionConfirm({ file, existing })
    else startUpload(file)
  }

  return (
    <>
      <PageHeader
        title="File"
        action={
          canUpload && (
            <Button icon="upload" loading={upload.isPending} onClick={() => input.current?.click()}>
              Tải lên
            </Button>
          )
        }
      />
      <input
        ref={input}
        type="file"
        accept={ACCEPT_ATTRIBUTE}
        className="sr-only"
        tabIndex={-1}
        aria-hidden
        onChange={(e) => {
          onChosen(e.target.files?.[0])
          e.target.value = ''
        }}
      />

      <div className="mb-3 flex flex-wrap items-center gap-2">
        <Breadcrumb trail={content?.breadcrumb ?? []} onOpen={(id) => setParam('thu-muc', id)} />
        {canUpload && (
          <Button variant="outline" icon="create_new_folder" className="ml-auto" onClick={() => setFolderDialog(true)}>
            Thư mục mới
          </Button>
        )}
      </div>

      {content && !content.canUpload && !content.folder?.isArchive && workspace.myRole !== 'LEADER' && workspace.myRole !== 'MEMBER' && (
        <p className="mb-3 text-body text-text-secondary">Giảng viên và mentor xem và bình luận file; chỉ thành viên nhóm tải lên.</p>
      )}
      {content?.folder?.isArchive && <div className="mb-3"><Banner title="Kho EXE101 – chỉ xem" /></div>}

      {uploading && (
        <UploadRow state={uploading} onRetry={() => startUpload(uploading.file)} onDismiss={() => setUploading(null)} />
      )}

      <div className="rounded-lg bg-surface shadow-elevation-1">
        {folder.error ? (
          <div className="p-4">
            <ErrorState error={folder.error} onRetry={() => void folder.refetch()} />
          </div>
        ) : !content ? (
          <ListSkeleton />
        ) : content.folders.length === 0 && content.files.length === 0 ? (
          <EmptyState
            icon="folder_open"
            title="Thư mục trống."
            action={canUpload && <Button onClick={() => input.current?.click()}>Tải lên</Button>}
          />
        ) : (
          <ul>
            {content.folders.map((f) => (
              <FolderRow key={f.id} folder={f} onOpen={() => setParam('thu-muc', f.id)} />
            ))}
            {content.files.map((f) => (
              <FileRow
                key={f.id}
                file={f}
                onOpen={() => setParam('file', f.id)}
                onDelete={canUpload && !f.isArchive ? () => setDeleting(f) : undefined}
              />
            ))}
          </ul>
        )}
      </div>

      {openFileId && <FileDetailDrawer fileId={openFileId} onClose={() => setParam('file')} />}

      <Dialog
        open={!!versionConfirm}
        onClose={() => setVersionConfirm(null)}
        title="Tạo phiên bản mới?"
        actions={
          <>
            <Button variant="text" onClick={() => setVersionConfirm(null)}>
              Hủy
            </Button>
            <Button
              onClick={() => {
                startUpload(versionConfirm!.file)
                setVersionConfirm(null)
              }}
            >
              Tạo phiên bản mới
            </Button>
          </>
        }
      >
        {versionConfirm &&
          `"${versionConfirm.existing.name}" đã có (v${versionConfirm.existing.currentVersion}). Tải lên sẽ tạo v${versionConfirm.existing.currentVersion + 1}; các bản cũ vẫn tải về được.`}
      </Dialog>

      <Dialog
        open={!!deleting}
        onClose={() => setDeleting(null)}
        title="Xóa file?"
        actions={
          <>
            <Button variant="text" onClick={() => setDeleting(null)}>
              Hủy
            </Button>
            <Button
              variant="danger"
              loading={remove.isPending}
              onClick={() =>
                remove.mutate(deleting!.id, {
                  onSuccess: () => {
                    setDeleting(null)
                    snackbar.show('Đã xóa file')
                  },
                })
              }
            >
              Xóa
            </Button>
          </>
        }
      >
        {remove.error
          ? toApiError(remove.error).title
          : `Cả nhóm sẽ không thấy "${deleting?.name}" nữa, kể cả các phiên bản cũ.`}
      </Dialog>

      <Dialog
        open={folderDialog}
        onClose={() => setFolderDialog(false)}
        title="Thư mục mới"
        actions={
          <>
            <Button variant="text" onClick={() => setFolderDialog(false)}>
              Hủy
            </Button>
            <Button
              loading={createFolder.isPending}
              disabled={!folderName.trim()}
              onClick={() =>
                createFolder.mutate(
                  { name: folderName.trim(), parentId: folderId },
                  {
                    onSuccess: () => {
                      setFolderDialog(false)
                      setFolderName('')
                      createFolder.reset()
                    },
                  },
                )
              }
            >
              Tạo thư mục
            </Button>
          </>
        }
      >
        <label className="flex flex-col gap-2 text-text-primary">
          <span className="font-medium">Tên thư mục</span>
          <input value={folderName} maxLength={100} onChange={(e) => setFolderName(e.target.value)}
            className="min-h-10 rounded-sm border border-border-strong px-3" />
        </label>
        {createFolder.error && <p className="mt-2 text-error">{toApiError(createFolder.error).detail ?? toApiError(createFolder.error).title}</p>}
      </Dialog>
      {snackbar.element}
    </>
  )
}

function Breadcrumb({ trail, onOpen }: { trail: Folder[]; onOpen: (id?: string) => void }) {
  return (
    <nav aria-label="Đường dẫn thư mục" className="flex min-w-0 flex-wrap items-center gap-1">
      <button type="button" onClick={() => onOpen(undefined)} className="min-h-10 rounded-md px-2 font-medium text-secondary hover:bg-secondary-container">
        Tất cả file
      </button>
      {trail.map((f, i) => (
        <span key={f.id} className="flex items-center gap-1">
          <Icon name="chevron_right" size={20} className="text-text-secondary" />
          {i === trail.length - 1 ? (
            <span aria-current="page" className="px-2 font-medium">
              {f.name}
            </span>
          ) : (
            <button type="button" onClick={() => onOpen(f.id)} className="min-h-10 rounded-md px-2 font-medium text-secondary hover:bg-secondary-container">
              {f.name}
            </button>
          )}
        </span>
      ))}
    </nav>
  )
}

function FolderRow({ folder, onOpen }: { folder: Folder; onOpen: () => void }) {
  return (
    <li className="border-b border-border last:border-none">
      <button type="button" onClick={onOpen} className="flex min-h-16 w-full items-center gap-3 px-4 text-left hover:bg-background">
        <Icon name={folder.isArchive ? 'history' : 'folder'} className={folder.isArchive ? 'text-secondary' : 'text-brand-orange'} />
        <span className="min-w-0 flex-1">
          <span className="block truncate text-body-lg">{folder.name}</span>
          <span className="block text-body text-text-secondary">{folder.itemCount} mục</span>
        </span>
        {folder.isArchive && <StatusChip tone="neutral" icon="lock">Chỉ xem</StatusChip>}
        <Icon name="chevron_right" className="text-text-secondary" />
      </button>
    </li>
  )
}

/** Dòng file (DESIGN.md 6.3): icon theo loại, tên, "v3 · Minh · 2 giờ trước", nút tải xuống. */
function FileRow({ file, onOpen, onDelete }: { file: ProjectFile; onOpen: () => void; onDelete?: () => void }) {
  return (
    <li className="flex min-h-16 items-center gap-3 border-b border-border px-4 last:border-none">
      <Icon name={fileIcon(file.name)} className="text-text-secondary" />
      <button type="button" onClick={onOpen} className="min-w-0 flex-1 py-2 text-left">
        <span className="line-clamp-2 text-body-lg hover:underline">{file.name}</span>
        <span className="block text-body text-text-secondary">
          v{file.currentVersion} · {file.updatedByName} · {formatRelative(file.updatedAt)} · {formatSize(file.sizeBytes)}
        </span>
      </button>
      <button type="button" onClick={() => void downloadFile(file.id)} className="grid size-10 place-items-center rounded-md hover:bg-background" aria-label={`Tải xuống ${file.name}`}>
        <Icon name="download" />
      </button>
      {onDelete && (
        <button type="button" onClick={onDelete} className="grid size-10 place-items-center rounded-md text-error hover:bg-error-container" aria-label={`Xóa ${file.name}`}>
          <Icon name="delete" />
        </button>
      )}
    </li>
  )
}

function UploadRow({ state, onRetry, onDismiss }: { state: UploadState; onRetry: () => void; onDismiss: () => void }) {
  return (
    <div className={clsx('mb-3 flex items-center gap-3 rounded-md px-4 py-3', state.error ? 'bg-error-container' : 'bg-surface shadow-elevation-1')}>
      <Icon name={state.error ? 'error' : 'upload'} className={state.error ? 'text-error' : 'text-secondary'} />
      <div className="min-w-0 flex-1">
        <p className="truncate font-medium">{state.file.name}</p>
        {state.error ? (
          <p className="text-body text-error">{state.error}</p>
        ) : (
          <div className="mt-1 flex items-center gap-2">
            <div className="h-1.5 flex-1 rounded-full bg-neutral-container" role="progressbar" aria-valuenow={state.progress} aria-valuemin={0} aria-valuemax={100} aria-label="Tiến độ tải lên">
              <div className="h-full rounded-full bg-primary transition-[width]" style={{ width: `${state.progress}%` }} />
            </div>
            <span className="text-caption tabular-nums text-text-secondary">{state.progress}%</span>
          </div>
        )}
      </div>
      {state.error && (
        <>
          <Button variant="text" onClick={onRetry}>
            Thử lại
          </Button>
          <button type="button" onClick={onDismiss} className="grid size-10 place-items-center rounded-md" aria-label="Đóng">
            <Icon name="close" />
          </button>
        </>
      )}
    </div>
  )
}

function ListSkeleton() {
  return (
    <div role="status" aria-label="Đang tải file">
      {Array.from({ length: 5 }, (_, i) => (
        <div key={i} className="flex h-16 items-center gap-3 border-b border-border px-4">
          <Skeleton className="size-6" />
          <Skeleton className="h-4 w-56" />
        </div>
      ))}
    </div>
  )
}

