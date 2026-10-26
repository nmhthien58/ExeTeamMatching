import { useState } from 'react'
import { toApiError } from '@/shared/api/apiClient'
import { ErrorState } from '@/shared/components/States'
import { Banner, Button, Dialog, Icon, Skeleton, StatusChip } from '@/shared/components/ui'
import { FilePickerDialog } from '@/features/files/FilePickerDialog'
import { downloadFile } from '@/features/files/api'
import { Avatar, Drawer, RoleChip } from '@/features/workspace/components'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { formatDateTime, formatRelative } from '@/features/workspace/format'
import { useWorkspaceContext } from '@/features/workspace/context'
import {
  statusLabels,
  taskStatuses,
  useAddTaskComment,
  useDeleteTask,
  useMoveTask,
  useReviewSubmission,
  useSubmitDeliverable,
  useTask,
  type Submission,
  type TaskDetail,
} from './api'
import { DueChip, SubmissionChip } from './TaskCardView'
import { TaskFormDrawer } from './TaskFormDrawer'

/**
 * C05 – Chi tiết việc / bài nộp (FE-39, rule 23): trạng thái, người làm, hạn, mô tả, bình luận.
 * Bài nộp: các bản đã nộp, Nộp bài (nhóm), Đạt / Cần sửa (giảng viên hướng dẫn, qua dialog xác nhận).
 */
export function TaskDetailDrawer({ taskId, onClose }: { taskId: string; onClose: () => void }) {
  const task = useTask(taskId)
  return (
    <Drawer open onClose={onClose} title={task.data?.kind === 'DELIVERABLE' ? 'Bài nộp' : 'Việc'}>
      {task.error ? (
        <ErrorState error={task.error} onRetry={() => void task.refetch()} />
      ) : !task.data ? (
        <div role="status" aria-label="Đang tải" className="flex flex-col gap-3">
          <Skeleton className="h-8 w-3/4" />
          <Skeleton className="h-6 w-1/2" />
          <Skeleton className="h-24 w-full" />
        </div>
      ) : (
        <TaskDetailBody task={task.data} onDeleted={onClose} />
      )}
    </Drawer>
  )
}

function TaskDetailBody({ task, onDeleted }: { task: TaskDetail; onDeleted: () => void }) {
  const { workspace, canEdit } = useWorkspaceContext()
  const isInstructor = workspace.myRole === 'INSTRUCTOR'
  const isDeliverable = task.kind === 'DELIVERABLE'
  const canManage = !task.isArchive && (isDeliverable ? isInstructor : canEdit)
  const move = useMoveTask(workspace.id)
  const remove = useDeleteTask(task)
  const [editing, setEditing] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState(false)

  return (
    <div className="flex flex-col gap-5">
      {task.isArchive && <Banner title="Việc của EXE101 – chỉ xem" />}

      <div className="flex flex-col gap-2">
        <h3 className="text-title font-semibold">{task.title}</h3>
        <div className="flex flex-wrap items-center gap-2">
          <StatusChip tone={task.status === 'DONE' ? 'success' : task.status === 'DOING' ? 'info' : 'neutral'}>
            {statusLabels[task.status]}
          </StatusChip>
          <DueChip task={task} />
          {task.submissionStatus && <SubmissionChip status={task.submissionStatus} />}
        </div>
      </div>

      {canEdit && !task.isArchive && (
        <label className="flex flex-col gap-2">
          <span className="font-medium">Trạng thái</span>
          <select
            value={task.status}
            onChange={(e) => move.mutate({ taskId: task.id, status: e.target.value as TaskDetail['status'] })}
            className="min-h-10 rounded-sm border border-border-strong bg-surface px-3"
          >
            {taskStatuses.map((s) => (
              <option key={s} value={s}>
                {statusLabels[s]}
              </option>
            ))}
          </select>
        </label>
      )}

      <dl className="grid grid-cols-[120px_1fr] gap-y-3 text-body">
        <dt className="text-text-secondary">Người làm</dt>
        <dd>
          {task.assignees.length === 0 ? (
            <span className="text-text-secondary">Chưa giao</span>
          ) : (
            <ul className="flex flex-col gap-1">
              {task.assignees.map((a) => (
                <li key={a.personId} className="flex items-center gap-2">
                  <Avatar name={a.displayName} size={24} />
                  {a.displayName}
                </li>
              ))}
            </ul>
          )}
        </dd>
        <dt className="text-text-secondary">Hạn</dt>
        <dd>{task.dueAt ? formatDateTime(task.dueAt) : 'Không có hạn'}</dd>
        <dt className="text-text-secondary">Người tạo</dt>
        <dd className="flex items-center gap-2">
          {task.createdBy.displayName}
          <RoleChip role={task.createdBy.role} />
        </dd>
        {task.sourceMeetingId && (
          <>
            <dt className="text-text-secondary">Nguồn</dt>
            <dd>Từ biên bản họp</dd>
          </>
        )}
      </dl>

      {task.description && <p className="max-w-[70ch] whitespace-pre-line text-body-lg">{task.description}</p>}

      {isDeliverable && <Submissions task={task} />}

      <Comments task={task} />

      {canManage && (
        <div className="flex gap-2 border-t border-border pt-4">
          <Button variant="outline" icon="edit" onClick={() => setEditing(true)}>
            Sửa
          </Button>
          <Button variant="text" icon="delete" className="text-error hover:bg-error-container" onClick={() => setConfirmDelete(true)}>
            Xóa
          </Button>
        </div>
      )}

      {editing && <TaskFormDrawer task={task} onClose={() => setEditing(false)} />}
      <Dialog
        open={confirmDelete}
        onClose={() => setConfirmDelete(false)}
        title={isDeliverable ? 'Xóa bài nộp?' : 'Xóa việc?'}
        actions={
          <>
            <Button variant="text" onClick={() => setConfirmDelete(false)}>
              Hủy
            </Button>
            <Button variant="danger" loading={remove.isPending} onClick={() => remove.mutate(undefined, { onSuccess: onDeleted })}>
              Xóa
            </Button>
          </>
        }
      >
        {remove.error ? toApiError(remove.error).detail ?? toApiError(remove.error).title : 'Cả nhóm sẽ không thấy việc này nữa.'}
      </Dialog>
    </div>
  )
}

/** Bản nộp (mới nhất ở trên); nhóm nộp phiên bản file từ C06, giảng viên hướng dẫn duyệt bản đang chờ. */
function Submissions({ task }: { task: TaskDetail }) {
  const { workspace, canEdit } = useWorkspaceContext()
  const submit = useSubmitDeliverable(task)
  const review = useReviewSubmission(task)
  const snackbar = useSnackbar()
  const [picking, setPicking] = useState(false)
  const [reviewing, setReviewing] = useState<{ submission: Submission; status: 'ACCEPTED' | 'NEEDS_REVISION' } | null>(null)
  const [comment, setComment] = useState('')
  const isInstructor = workspace.myRole === 'INSTRUCTOR'
  const latest = task.submissions[0]

  return (
    <section className="flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <h3 className="text-subtitle font-semibold">Bản đã nộp</h3>
        {canEdit && !task.isArchive && task.submissionStatus !== 'ACCEPTED' && (
          <Button icon="upload_file" loading={submit.isPending} onClick={() => setPicking(true)}>
            Nộp bài
          </Button>
        )}
      </div>
      {submit.error && <Banner tone="error" title={toApiError(submit.error).title}>{toApiError(submit.error).detail}</Banner>}

      {task.submissions.length === 0 ? (
        <p className="text-body text-text-secondary">Nhóm chưa nộp bản nào.</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {task.submissions.map((s) => (
            <li key={s.id} className="rounded-md border border-border p-3">
              <div className="flex flex-wrap items-center gap-2">
                <Icon name="description" className="text-text-secondary" />
                <button type="button" className="min-w-0 flex-1 truncate text-left font-medium text-primary-dark hover:underline"
                  onClick={() => void downloadFile(s.file.id, s.version)}>
                  {s.file.name} · v{s.version}
                </button>
                <SubmissionChip status={s.reviewStatus} />
                {s.isLate && <StatusChip tone="error" icon="alarm">Nộp muộn</StatusChip>}
              </div>
              <p className="mt-1 text-caption text-text-secondary">
                {s.submittedBy.displayName} · {formatRelative(s.submittedAt)}
              </p>
              {s.reviewComment && <p className="mt-2 rounded-sm bg-warning-container px-3 py-2 text-body">{s.reviewComment}</p>}
              {isInstructor && s.id === latest?.id && s.reviewStatus === 'PENDING' && (
                <div className="mt-3 flex gap-2">
                  <Button onClick={() => setReviewing({ submission: s, status: 'ACCEPTED' })}>Đạt</Button>
                  <Button variant="outline" onClick={() => setReviewing({ submission: s, status: 'NEEDS_REVISION' })}>
                    Cần sửa
                  </Button>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}

      {picking && (
        <FilePickerDialog
          workspaceId={workspace.id}
          onClose={() => setPicking(false)}
          onPick={(versionId) => {
            setPicking(false)
            submit.mutate(versionId, { onSuccess: () => snackbar.show('Đã nộp bài') })
          }}
        />
      )}

      <Dialog
        open={!!reviewing}
        onClose={() => setReviewing(null)}
        title={reviewing?.status === 'ACCEPTED' ? 'Đánh dấu bài nộp đạt?' : 'Yêu cầu nhóm sửa?'}
        actions={
          <>
            <Button variant="text" onClick={() => setReviewing(null)}>
              Hủy
            </Button>
            <Button
              loading={review.isPending}
              disabled={reviewing?.status === 'NEEDS_REVISION' && !comment.trim()}
              onClick={() =>
                reviewing &&
                review.mutate(
                  { submissionId: reviewing.submission.id, status: reviewing.status, comment: comment.trim() || undefined },
                  {
                    onSuccess: () => {
                      snackbar.show(reviewing.status === 'ACCEPTED' ? 'Đã đánh dấu đạt' : 'Đã yêu cầu sửa')
                      setReviewing(null)
                      setComment('')
                    },
                  },
                )
              }
            >
              {reviewing?.status === 'ACCEPTED' ? 'Đạt' : 'Cần sửa'}
            </Button>
          </>
        }
      >
        <p>Nhóm sẽ nhận thông báo ngay.</p>
        <label className="mt-3 flex flex-col gap-2 text-text-primary">
          <span className="font-medium">{reviewing?.status === 'NEEDS_REVISION' ? 'Nhận xét' : 'Nhận xét (không bắt buộc)'}</span>
          <textarea rows={3} maxLength={2000} value={comment} onChange={(e) => setComment(e.target.value)}
            className="rounded-sm border border-border-strong px-3 py-2" />
        </label>
        {review.error && <p className="mt-2 text-error">{toApiError(review.error).title}</p>}
      </Dialog>
      {snackbar.element}
    </section>
  )
}

function Comments({ task }: { task: TaskDetail }) {
  const { workspace } = useWorkspaceContext()
  const add = useAddTaskComment(task)
  const [body, setBody] = useState('')
  const canComment = workspace.myRole !== 'ADMIN' && !task.isArchive
  return (
    <section className="flex flex-col gap-3">
      <h3 className="text-subtitle font-semibold">Bình luận ({task.comments.length})</h3>
      <ul className="flex flex-col gap-3">
        {task.comments.map((c) => (
          <li key={c.id} className="flex gap-3">
            <Avatar name={c.author.displayName} size={32} isMentor={c.author.role === 'MENTOR'} />
            <div className="min-w-0 flex-1">
              <p className="flex flex-wrap items-center gap-2 font-medium">
                {c.author.displayName}
                <RoleChip role={c.author.role} />
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
            if (body.trim()) add.mutate(body.trim(), { onSuccess: () => setBody('') })
          }}
        >
          <label htmlFor={`comment-${task.id}`} className="sr-only">
            Bình luận
          </label>
          <textarea id={`comment-${task.id}`} rows={2} maxLength={2000} value={body} placeholder="Viết bình luận…"
            onChange={(e) => setBody(e.target.value)} className="rounded-sm border border-border-strong px-3 py-2" />
          {add.error && <p className="text-body text-error">{toApiError(add.error).title}. Nội dung vẫn được giữ, hãy thử lại.</p>}
          <div className="flex justify-end">
            <Button type="submit" variant="outline" icon="send" loading={add.isPending} disabled={!body.trim()}>
              Gửi
            </Button>
          </div>
        </form>
      )}
    </section>
  )
}
