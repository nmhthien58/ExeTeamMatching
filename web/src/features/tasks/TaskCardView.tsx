import { Link } from 'react-router-dom'
import clsx from 'clsx'
import { Icon, StatusChip, type StatusTone } from '@/shared/components/ui'
import { AvatarStack } from '@/features/workspace/components'
import { dueChip } from '@/features/workspace/format'
import { statusLabels, taskStatuses, type SubmissionStatus, type TaskCard, type TaskStatus } from './api'

const submissionChips: Record<SubmissionStatus, { text: string; tone: StatusTone; icon?: string }> = {
  NOT_SUBMITTED: { text: 'Chưa nộp', tone: 'neutral' },
  PENDING: { text: 'Chờ duyệt', tone: 'warning' },
  ACCEPTED: { text: 'Đạt', tone: 'success' },
  NEEDS_REVISION: { text: 'Cần sửa', tone: 'warning', icon: 'edit_note' },
}

export function SubmissionChip({ status }: { status: SubmissionStatus }) {
  const chip = submissionChips[status]
  return (
    <StatusChip tone={chip.tone} icon={chip.icon}>
      {chip.text}
    </StatusChip>
  )
}

export function DueChip({ task }: { task: Pick<TaskCard, 'dueAt' | 'isOverdue' | 'status'> }) {
  if (!task.dueAt || task.status === 'DONE') return null
  const chip = dueChip(task.dueAt, new Date(), task.isOverdue)
  return (
    <StatusChip tone={chip.tone} icon={chip.icon}>
      {chip.text}
    </StatusChip>
  )
}

/**
 * Thẻ việc (DESIGN.md 6.3): tiêu đề, người làm xếp chồng, chip hạn; bài nộp có icon `assignment` và chip trạng thái nộp.
 * Trên web kéo thả được; menu "Chuyển sang…" là cách thay thế không cần kéo (WCAG 2.2).
 */
export function TaskCardView({
  task,
  to,
  canMove,
  onMove,
  draggable,
}: {
  task: TaskCard
  to: string
  canMove: boolean
  onMove: (status: TaskStatus) => void
  draggable?: boolean
}) {
  return (
    <article
      draggable={draggable && canMove && !task.isArchive}
      onDragStart={(e) => {
        e.dataTransfer.setData('text/task-id', task.id)
        e.dataTransfer.effectAllowed = 'move'
      }}
      className={clsx(
        'group relative rounded-lg bg-surface p-3 shadow-elevation-1',
        draggable && canMove && !task.isArchive && 'cursor-grab active:cursor-grabbing',
      )}
    >
      <div className="flex items-start gap-2">
        {task.kind === 'DELIVERABLE' && <Icon name="assignment" className="text-info" label="Bài nộp" />}
        <Link to={to} className="line-clamp-2 min-w-0 flex-1 text-subtitle font-semibold after:absolute after:inset-0 hover:underline">
          {task.title}
        </Link>
        {canMove && !task.isArchive && <MoveMenu current={task.status} onMove={onMove} />}
      </div>
      <div className="mt-2 flex flex-wrap items-center gap-2">
        <AvatarStack people={task.assignees} label={task.assignees.length ? `Người làm: ${task.assignees.map((a) => a.displayName).join(', ')}` : undefined} />
        <DueChip task={task} />
        {task.submissionStatus && <SubmissionChip status={task.submissionStatus} />}
        {task.isArchive && <StatusChip tone="neutral" icon="history">EXE101</StatusChip>}
        {task.commentCount > 0 && (
          <span className="ml-auto inline-flex items-center gap-1 text-caption text-text-secondary" aria-label={`${task.commentCount} bình luận`}>
            <Icon name="chat_bubble" size={20} />
            {task.commentCount}
          </span>
        )}
      </div>
    </article>
  )
}

/** Menu "Chuyển sang…" – nằm trên lớp link của thẻ để bấm được. */
function MoveMenu({ current, onMove }: { current: TaskStatus; onMove: (status: TaskStatus) => void }) {
  return (
    <label className="relative z-10">
      <span className="sr-only">Chuyển sang</span>
      <select
        value=""
        onChange={(e) => e.target.value && onMove(e.target.value as TaskStatus)}
        className="h-8 w-8 cursor-pointer appearance-none rounded-md bg-transparent text-transparent hover:bg-background focus:text-text-primary"
        title="Chuyển sang…"
      >
        <option value="">Chuyển sang…</option>
        {taskStatuses
          .filter((s) => s !== current)
          .map((s) => (
            <option key={s} value={s}>
              {statusLabels[s]}
            </option>
          ))}
      </select>
      <Icon name="more_vert" className="pointer-events-none absolute inset-1 text-text-secondary" />
    </label>
  )
}
