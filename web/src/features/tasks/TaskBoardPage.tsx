import { useState, type DragEvent } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router-dom'
import clsx from 'clsx'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Button, Icon, Skeleton } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useLayoutMode } from '@/shared/layout/RoleShell'
import { toApiError } from '@/shared/api/apiClient'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { useWorkspaceContext } from '@/features/workspace/context'
import { statusLabels, taskStatuses, useMoveTask, useTaskBoard, type TaskCard, type TaskFilters, type TaskStatus } from './api'
import { TaskCardView } from './TaskCardView'
import { TaskDetailDrawer } from './TaskDetailDrawer'
import { TaskFormDrawer } from './TaskFormDrawer'

/**
 * C04 – Bảng việc (FE-39, rule 23). ≥ 1024 px: ba cột cạnh nhau, kéo thả; hẹp hơn: tab theo trạng thái.
 * Lọc nhanh Của tôi / Bài nộp / Trễ hạn đồng bộ URL. Bấm thẻ mở C05 trong drawer (`viec/:taskId`).
 */
export function TaskBoardPage() {
  const { workspace, basePath, canEdit } = useWorkspaceContext()
  const { taskId } = useParams()
  const navigate = useNavigate()
  const [params, setParams] = useSearchParams()
  const filters: TaskFilters = {
    mine: params.get('mine') === '1',
    kind: params.get('kind') === 'DELIVERABLE' ? 'DELIVERABLE' : undefined,
    overdue: params.get('overdue') === '1',
    includeArchive: params.get('exe101') === '1',
  }
  const board = useTaskBoard(workspace.id, filters)
  const move = useMoveTask(workspace.id)
  const snackbar = useSnackbar()
  const isWide = useLayoutMode() === 'sidebar'
  const [tab, setTab] = useState<TaskStatus>('TODO')
  const isInstructor = workspace.myRole === 'INSTRUCTOR'
  const canCreate = canEdit || isInstructor
  const creating = params.get('tao') === '1'

  const toggle = (key: string, on: boolean, value = '1') => {
    const next = new URLSearchParams(params)
    if (on) next.set(key, value)
    else next.delete(key)
    setParams(next, { replace: true })
  }

  const moveTask = (task: TaskCard, status: TaskStatus, position?: number) => {
    if (task.status === status && position === undefined) return
    const previous = { status: task.status, position: task.position }
    move.mutate(
      { taskId: task.id, status, position },
      {
        onSuccess: () =>
          snackbar.show(`Đã chuyển sang ${statusLabels[status]}`, () =>
            move.mutate({ taskId: task.id, status: previous.status, position: previous.position })),
        onError: (error) => snackbar.show(toApiError(error).title),
      },
    )
  }

  const columns = taskStatuses.map((status) => ({
    status,
    tasks: (board.data ?? []).filter((t) => t.status === status).sort((a, b) => a.position - b.position),
  }))

  return (
    <>
      <PageHeader
        title="Việc"
        action={
          canCreate && (
            <Button icon="add" onClick={() => toggle('tao', true)}>
              {isInstructor ? 'Tạo bài nộp' : 'Tạo việc'}
            </Button>
          )
        }
      />

      <div className="mb-4 flex flex-wrap gap-2" role="group" aria-label="Lọc nhanh">
        {canEdit && <FilterChip label="Của tôi" active={filters.mine!} onToggle={(on) => toggle('mine', on)} />}
        <FilterChip label="Bài nộp" active={filters.kind === 'DELIVERABLE'} onToggle={(on) => toggle('kind', on, 'DELIVERABLE')} />
        <FilterChip label="Trễ hạn" active={filters.overdue!} onToggle={(on) => toggle('overdue', on)} />
        {workspace.previousWorkspaceId && (
          <FilterChip label="Việc EXE101" active={filters.includeArchive!} onToggle={(on) => toggle('exe101', on)} />
        )}
      </div>

      {board.error ? (
        <ErrorState error={board.error} onRetry={() => void board.refetch()} />
      ) : board.isLoading ? (
        <BoardSkeleton wide={isWide} />
      ) : board.data!.length === 0 ? (
        <div className="rounded-lg bg-surface shadow-elevation-1">
          <EmptyState
            icon="task_alt"
            title={Object.values(filters).some(Boolean) ? 'Không có việc nào khớp bộ lọc.' : 'Chưa có việc nào.'}
            action={canCreate && <Button onClick={() => toggle('tao', true)}>{isInstructor ? 'Tạo bài nộp' : 'Tạo việc'}</Button>}
          />
        </div>
      ) : isWide ? (
        <div className="grid grid-cols-3 items-start gap-4">
          {columns.map((column) => (
            <BoardColumn
              key={column.status}
              status={column.status}
              tasks={column.tasks}
              canMove={canEdit}
              basePath={basePath}
              onDrop={(task, position) => moveTask(task, column.status, position)}
              findTask={(id) => board.data!.find((t) => t.id === id)}
              onMove={moveTask}
            />
          ))}
        </div>
      ) : (
        <>
          <div role="tablist" aria-label="Trạng thái" className="mb-3 flex border-b border-border">
            {columns.map((column) => (
              <button
                key={column.status}
                role="tab"
                type="button"
                aria-selected={tab === column.status}
                onClick={() => setTab(column.status)}
                className={clsx(
                  'min-h-12 flex-1 border-b-2 font-medium',
                  tab === column.status ? 'border-primary text-primary-dark' : 'border-transparent text-text-secondary',
                )}
              >
                {statusLabels[column.status]} ({column.tasks.length})
              </button>
            ))}
          </div>
          <TaskList
            tasks={columns.find((c) => c.status === tab)!.tasks}
            status={tab}
            canMove={canEdit}
            basePath={basePath}
            onMove={moveTask}
          />
        </>
      )}

      {taskId && <TaskDetailDrawer taskId={taskId} onClose={() => navigate(`${basePath}/viec?${params.toString()}`)} />}
      {creating && <TaskFormDrawer onClose={() => toggle('tao', false)} />}
      {snackbar.element}
    </>
  )
}

function FilterChip({ label, active, onToggle }: { label: string; active: boolean; onToggle: (on: boolean) => void }) {
  return (
    <button
      type="button"
      aria-pressed={active}
      onClick={() => onToggle(!active)}
      className={clsx(
        'inline-flex min-h-10 items-center gap-1 rounded-full px-4 font-medium',
        active ? 'bg-primary text-white' : 'border border-border-strong bg-surface text-text-primary hover:bg-background',
      )}
    >
      {active && <Icon name="check" size={20} />}
      {label}
    </button>
  )
}

/** Một cột trên web: thả thẻ vào cuối cột hoặc trước một thẻ khác. */
function BoardColumn({ status, tasks, canMove, basePath, onDrop, findTask, onMove }: {
  status: TaskStatus
  tasks: TaskCard[]
  canMove: boolean
  basePath: string
  onDrop: (task: TaskCard, position?: number) => void
  findTask: (id: string) => TaskCard | undefined
  onMove: (task: TaskCard, status: TaskStatus) => void
}) {
  const [over, setOver] = useState(false)
  const dropAt = (e: DragEvent, position?: number) => {
    e.preventDefault()
    e.stopPropagation()
    setOver(false)
    const task = findTask(e.dataTransfer.getData('text/task-id'))
    if (task) onDrop(task, position)
  }
  return (
    <section
      aria-label={`${statusLabels[status]} – ${tasks.length} việc`}
      onDragOver={(e) => {
        if (!canMove) return
        e.preventDefault()
        setOver(true)
      }}
      onDragLeave={() => setOver(false)}
      onDrop={(e) => dropAt(e)}
      className={clsx('flex min-h-40 flex-col gap-3 rounded-lg p-3', over ? 'bg-primary-container' : 'bg-neutral-container/60')}
    >
      <h2 className="flex items-center justify-between text-subtitle font-semibold">
        {statusLabels[status]}
        <span className="text-body text-text-secondary tabular-nums">{tasks.length}</span>
      </h2>
      {tasks.length === 0 ? (
        <p className="py-6 text-center text-body text-text-secondary">Chưa có việc</p>
      ) : (
        tasks.map((task, index) => (
          <div key={task.id} onDrop={(e) => dropAt(e, index)} onDragOver={(e) => canMove && e.preventDefault()}>
            <TaskCardView task={task} to={`${basePath}/viec/${task.id}`} canMove={canMove} draggable onMove={(s) => onMove(task, s)} />
          </div>
        ))
      )}
    </section>
  )
}

function TaskList({ tasks, status, canMove, basePath, onMove }: {
  tasks: TaskCard[]
  status: TaskStatus
  canMove: boolean
  basePath: string
  onMove: (task: TaskCard, status: TaskStatus) => void
}) {
  if (tasks.length === 0) {
    return <EmptyState icon="inbox" title={`Chưa có việc ở cột ${statusLabels[status]}.`} />
  }
  return (
    <div role="tabpanel" className="flex flex-col gap-3">
      {tasks.map((task) => (
        <TaskCardView key={task.id} task={task} to={`${basePath}/viec/${task.id}`} canMove={canMove} onMove={(s) => onMove(task, s)} />
      ))}
    </div>
  )
}

function BoardSkeleton({ wide }: { wide: boolean }) {
  return (
    <div role="status" aria-label="Đang tải bảng việc" className={clsx('grid gap-4', wide ? 'grid-cols-3' : 'grid-cols-1')}>
      {Array.from({ length: wide ? 3 : 1 }, (_, c) => (
        <div key={c} className="flex flex-col gap-3 rounded-lg bg-neutral-container/60 p-3">
          <Skeleton className="h-6 w-24" />
          {Array.from({ length: 3 }, (_, i) => (
            <Skeleton key={i} className="h-20 rounded-lg" />
          ))}
        </div>
      ))}
    </div>
  )
}
