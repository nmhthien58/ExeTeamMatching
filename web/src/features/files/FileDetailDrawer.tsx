import { useState } from 'react'
import { toApiError } from '@/shared/api/apiClient'
import { ErrorState } from '@/shared/components/States'
import { Banner, Button, Icon, Skeleton } from '@/shared/components/ui'
import { Avatar, Drawer, RoleChip } from '@/features/workspace/components'
import { formatDateTime, formatRelative, formatSize } from '@/features/workspace/format'
import { useWorkspaceContext } from '@/features/workspace/context'
import { downloadFile, fileIcon, useAddFileComment, useFileDetail, type ProjectFileDetail } from './api'

/** Chi tiết file trong C06: lịch sử phiên bản (tải từng bản) và bình luận theo phiên bản. */
export function FileDetailDrawer({ fileId, onClose }: { fileId: string; onClose: () => void }) {
  const detail = useFileDetail(fileId)
  return (
    <Drawer open onClose={onClose} title={detail.data?.name ?? 'File'}>
      {detail.error ? (
        <ErrorState error={detail.error} onRetry={() => void detail.refetch()} />
      ) : !detail.data ? (
        <div role="status" aria-label="Đang tải" className="flex flex-col gap-3">
          <Skeleton className="h-12" />
          <Skeleton className="h-24" />
        </div>
      ) : (
        <FileDetailBody file={detail.data} />
      )}
    </Drawer>
  )
}

function FileDetailBody({ file }: { file: ProjectFileDetail }) {
  const { workspace } = useWorkspaceContext()
  const comment = useAddFileComment(workspace.id, file.id)
  const [body, setBody] = useState('')
  const [version, setVersion] = useState<number>(file.currentVersion)
  const [downloadError, setDownloadError] = useState<string | null>(null)
  const canComment = workspace.myRole !== 'ADMIN' && !file.isArchive

  const download = async (v: number) => {
    setDownloadError(null)
    try {
      await downloadFile(file.id, v)
    } catch (e) {
      setDownloadError(toApiError(e).title)
    }
  }

  return (
    <div className="flex flex-col gap-5">
      {file.isArchive && <Banner title="File của EXE101 – chỉ xem" />}
      <div className="flex items-center gap-3">
        <Icon name={fileIcon(file.name)} size={48} className="text-text-secondary" />
        <div className="min-w-0 flex-1">
          <p className="break-words text-subtitle font-semibold">{file.name}</p>
          <p className="text-body text-text-secondary">
            v{file.currentVersion} · {formatSize(file.sizeBytes)}
          </p>
        </div>
        <Button icon="download" onClick={() => void download(file.currentVersion)}>
          Tải xuống
        </Button>
      </div>
      {downloadError && <p className="text-body text-error">{downloadError}</p>}

      <section>
        <h3 className="mb-2 text-subtitle font-semibold">Phiên bản</h3>
        <ul className="flex flex-col">
          {file.versions.map((v) => (
            <li key={v.version} className="flex min-h-12 items-center gap-3 border-b border-border last:border-none">
              <span className="w-10 font-semibold tabular-nums">v{v.version}</span>
              <span className="min-w-0 flex-1 text-body">
                {v.uploadedByName} ·{' '}
                <time dateTime={v.createdAt} title={formatDateTime(v.createdAt)}>
                  {formatRelative(v.createdAt)}
                </time>{' '}
                · {formatSize(v.sizeBytes)}
              </span>
              <button type="button" onClick={() => void download(v.version)} className="grid size-10 place-items-center rounded-md hover:bg-background" aria-label={`Tải phiên bản ${v.version}`}>
                <Icon name="download" />
              </button>
            </li>
          ))}
        </ul>
      </section>

      <section className="flex flex-col gap-3">
        <h3 className="text-subtitle font-semibold">Bình luận ({file.comments.length})</h3>
        <ul className="flex flex-col gap-3">
          {file.comments.map((c) => (
            <li key={c.id} className="flex gap-3">
              <Avatar name={c.author.displayName} isMentor={c.author.role === 'MENTOR'} />
              <div className="min-w-0 flex-1">
                <p className="flex flex-wrap items-center gap-2 font-medium">
                  {c.author.displayName}
                  <RoleChip role={c.author.role} />
                  {c.version && <span className="text-caption text-text-secondary">v{c.version}</span>}
                  <time className="text-caption font-normal text-text-secondary" dateTime={c.createdAt}>
                    {formatRelative(c.createdAt)}
                  </time>
                </p>
                <p className="whitespace-pre-line">{c.body}</p>
              </div>
            </li>
          ))}
        </ul>
        {canComment && (
          <form
            className="flex flex-col gap-2"
            onSubmit={(e) => {
              e.preventDefault()
              if (body.trim()) comment.mutate({ body: body.trim(), version }, { onSuccess: () => setBody('') })
            }}
          >
            <div className="flex items-center gap-2">
              <label htmlFor="file-comment-version" className="text-body text-text-secondary">
                Phiên bản
              </label>
              <select id="file-comment-version" value={version} onChange={(e) => setVersion(Number(e.target.value))}
                className="min-h-10 rounded-sm border border-border-strong bg-surface px-2">
                {file.versions.map((v) => (
                  <option key={v.version} value={v.version}>
                    v{v.version}
                  </option>
                ))}
              </select>
            </div>
            <label htmlFor="file-comment" className="sr-only">
              Bình luận
            </label>
            <textarea id="file-comment" rows={2} maxLength={2000} value={body} placeholder="Viết bình luận…"
              onChange={(e) => setBody(e.target.value)} className="rounded-sm border border-border-strong px-3 py-2" />
            {comment.error && <p className="text-body text-error">{toApiError(comment.error).title}. Nội dung vẫn được giữ, hãy thử lại.</p>}
            <div className="flex justify-end">
              <Button type="submit" variant="outline" icon="send" loading={comment.isPending} disabled={!body.trim()}>
                Gửi
              </Button>
            </div>
          </form>
        )}
      </section>
    </div>
  )
}
