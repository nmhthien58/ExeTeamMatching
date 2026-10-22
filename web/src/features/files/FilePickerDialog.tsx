import { useState } from 'react'
import { api, toApiError } from '@/shared/api/apiClient'
import { ErrorState } from '@/shared/components/States'
import { Button, Icon, Skeleton } from '@/shared/components/ui'
import { Drawer } from '@/features/workspace/components'
import { formatRelative } from '@/features/workspace/format'
import { fileIcon, useFolder, type ProjectFileDetail } from './api'

/**
 * Chọn một file của dự án để nộp bài (C05): duyệt thư mục như C06, chọn file → nộp **phiên bản mới nhất**.
 * Kho EXE101 không hiện vì không nộp được file kỳ trước.
 */
export function FilePickerDialog({ workspaceId, onClose, onPick }: {
  workspaceId: string
  onClose: () => void
  onPick: (fileVersionId: string) => void
}) {
  const [folderId, setFolderId] = useState<string | undefined>()
  const [resolving, setResolving] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const folder = useFolder(workspaceId, folderId)

  const pick = async (fileId: string) => {
    setResolving(fileId)
    setError(null)
    try {
      const { data } = await api.get<ProjectFileDetail>(`/api/files/${fileId}`)
      const latest = data.versions.find((v) => v.version === data.currentVersion) ?? data.versions[0]
      onPick(latest.id)
    } catch (e) {
      setError(toApiError(e).title)
    } finally {
      setResolving(null)
    }
  }

  const content = folder.data
  return (
    <Drawer open onClose={onClose} title="Chọn file để nộp">
      {folder.error ? (
        <ErrorState error={folder.error} onRetry={() => void folder.refetch()} />
      ) : !content ? (
        <div role="status" aria-label="Đang tải" className="flex flex-col gap-2">
          {Array.from({ length: 4 }, (_, i) => (
            <Skeleton key={i} className="h-12" />
          ))}
        </div>
      ) : (
        <div className="flex flex-col gap-2">
          {folderId && (
            <Button variant="text" icon="arrow_back" className="self-start" onClick={() => setFolderId(content.folder?.parentId ?? undefined)}>
              {content.folder?.name ?? 'Quay lại'}
            </Button>
          )}
          {error && <p className="text-body text-error">{error}</p>}
          <ul className="flex flex-col">
            {content.folders
              .filter((f) => !f.isArchive)
              .map((f) => (
                <li key={f.id}>
                  <button type="button" onClick={() => setFolderId(f.id)} className="flex min-h-12 w-full items-center gap-3 rounded-md px-2 text-left hover:bg-background">
                    <Icon name="folder" className="text-primary" />
                    <span className="flex-1 truncate">{f.name}</span>
                    <Icon name="chevron_right" className="text-text-secondary" />
                  </button>
                </li>
              ))}
            {content.files.map((f) => (
              <li key={f.id}>
                <button
                  type="button"
                  disabled={!!resolving}
                  onClick={() => void pick(f.id)}
                  className="flex min-h-12 w-full items-center gap-3 rounded-md px-2 text-left hover:bg-background disabled:opacity-60"
                >
                  <Icon name={fileIcon(f.name)} className="text-text-secondary" />
                  <span className="min-w-0 flex-1">
                    <span className="block truncate font-medium">{f.name}</span>
                    <span className="block text-caption text-text-secondary">
                      v{f.currentVersion} · {f.updatedByName} · {formatRelative(f.updatedAt)}
                    </span>
                  </span>
                  {resolving === f.id && <span className="text-caption text-text-secondary">Đang chọn…</span>}
                </button>
              </li>
            ))}
          </ul>
          {content.folders.length === 0 && content.files.length === 0 && (
            <p className="py-6 text-center text-text-secondary">Thư mục trống. Tải file lên ở mục File trước.</p>
          )}
        </div>
      )}
    </Drawer>
  )
}
