import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import type { PersonRef } from '@/features/workspace/api'
import type { ProjectFile } from '@/features/files/api'

export type TaskStatus = 'TODO' | 'DOING' | 'DONE'
export type TaskKind = 'TASK' | 'DELIVERABLE'
export type SubmissionStatus = 'NOT_SUBMITTED' | 'PENDING' | 'ACCEPTED' | 'NEEDS_REVISION'

/** Schema TaskCard (collab.yaml). */
export interface TaskCard {
  id: string
  workspaceId: string
  teamName: string
  kind: TaskKind
  title: string
  status: TaskStatus
  position: number
  assignees: PersonRef[]
  dueAt?: string | null
  isOverdue: boolean
  submissionStatus?: SubmissionStatus | null
  commentCount: number
  isArchive: boolean
}

export interface Comment {
  id: string
  author: PersonRef
  body: string
  version?: number | null
  createdAt: string
}

export interface Submission {
  id: string
  taskId: string
  file: ProjectFile
  version: number
  submittedBy: PersonRef
  submittedAt: string
  isLate: boolean
  reviewStatus: Exclude<SubmissionStatus, 'NOT_SUBMITTED'>
  reviewComment?: string | null
  reviewedAt?: string | null
}

export interface TaskDetail extends TaskCard {
  description?: string | null
  createdBy: PersonRef
  sourceMeetingId?: string | null
  submissions: Submission[]
  comments: Comment[]
  completedAt?: string | null
}

export interface TaskInput {
  title: string
  description?: string | null
  kind?: TaskKind
  assigneeStudentIds?: string[]
  dueAt?: string | null
}

export interface TaskFilters {
  mine?: boolean
  kind?: TaskKind
  overdue?: boolean
  includeArchive?: boolean
}

export const taskKeys = {
  board: (workspaceId: string) => ['tasks', 'board', workspaceId] as const,
  boardFiltered: (workspaceId: string, filters: TaskFilters) => ['tasks', 'board', workspaceId, filters] as const,
  detail: (taskId: string) => ['tasks', 'detail', taskId] as const,
}

export const statusLabels: Record<TaskStatus, string> = { TODO: 'Cần làm', DOING: 'Đang làm', DONE: 'Xong' }
export const taskStatuses: TaskStatus[] = ['TODO', 'DOING', 'DONE']

export function useTaskBoard(workspaceId: string | undefined, filters: TaskFilters) {
  return useQuery({
    queryKey: taskKeys.boardFiltered(workspaceId ?? '', filters),
    queryFn: async () =>
      (
        await api.get<TaskCard[]>(`/api/workspaces/${workspaceId}/tasks`, {
          params: { mine: filters.mine || undefined, kind: filters.kind, overdue: filters.overdue || undefined, includeArchive: filters.includeArchive || undefined },
        })
      ).data,
    enabled: !!workspaceId,
  })
}

export function useTask(taskId: string | undefined) {
  return useQuery({
    queryKey: taskKeys.detail(taskId ?? ''),
    queryFn: async () => (await api.get<TaskDetail>(`/api/tasks/${taskId}`)).data,
    enabled: !!taskId,
  })
}

/** Làm mới bảng việc và chi tiết sau khi ghi. */
function useInvalidateTasks() {
  const queryClient = useQueryClient()
  return (workspaceId: string, taskId?: string) => {
    void queryClient.invalidateQueries({ queryKey: taskKeys.board(workspaceId) })
    if (taskId) void queryClient.invalidateQueries({ queryKey: taskKeys.detail(taskId) })
  }
}

export function useCreateTask(workspaceId: string) {
  const invalidate = useInvalidateTasks()
  return useMutation({
    mutationFn: async (input: TaskInput) => (await api.post<TaskDetail>(`/api/workspaces/${workspaceId}/tasks`, input)).data,
    onSuccess: () => invalidate(workspaceId),
  })
}

export function useUpdateTask(task: Pick<TaskCard, 'id' | 'workspaceId'>) {
  const invalidate = useInvalidateTasks()
  return useMutation({
    mutationFn: async (input: TaskInput) => (await api.patch<TaskDetail>(`/api/tasks/${task.id}`, input)).data,
    onSuccess: () => invalidate(task.workspaceId, task.id),
  })
}

export function useDeleteTask(task: Pick<TaskCard, 'id' | 'workspaceId'>) {
  const invalidate = useInvalidateTasks()
  return useMutation({
    mutationFn: async () => {
      await api.delete(`/api/tasks/${task.id}`)
    },
    onSuccess: () => invalidate(task.workspaceId),
  })
}

/**
 * Đổi trạng thái / vị trí – cập nhật lạc quan (DESIGN.md 6.4): mọi bảng đang cache của workspace đổi ngay,
 * lỗi thì trả lại trạng thái cũ.
 */
export function useMoveTask(workspaceId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ taskId, status, position }: { taskId: string; status: TaskStatus; position?: number }) =>
      (await api.post<TaskCard>(`/api/tasks/${taskId}/move`, { status, position })).data,
    onMutate: async ({ taskId, status, position }) => {
      await queryClient.cancelQueries({ queryKey: taskKeys.board(workspaceId) })
      const snapshots = queryClient.getQueriesData<TaskCard[]>({ queryKey: taskKeys.board(workspaceId) })
      for (const [key, tasks] of snapshots) {
        if (tasks) queryClient.setQueryData(key, moveLocally(tasks, taskId, status, position))
      }
      return { snapshots }
    },
    onError: (_error, _vars, context) => {
      for (const [key, tasks] of context?.snapshots ?? []) queryClient.setQueryData(key, tasks)
    },
    onSettled: (_data, _error, { taskId }) => {
      void queryClient.invalidateQueries({ queryKey: taskKeys.board(workspaceId) })
      void queryClient.invalidateQueries({ queryKey: taskKeys.detail(taskId) })
    },
  })
}

/** Đưa việc sang cột mới ở vị trí cho trước (mặc định cuối cột) và đánh số lại các cột liên quan. */
export function moveLocally(tasks: TaskCard[], taskId: string, status: TaskStatus, position?: number): TaskCard[] {
  const moving = tasks.find((t) => t.id === taskId)
  if (!moving) return tasks
  const others = tasks.filter((t) => t.id !== taskId)
  const column = others.filter((t) => t.status === status).sort((a, b) => a.position - b.position)
  column.splice(Math.min(position ?? column.length, column.length), 0, { ...moving, status })
  const renumbered = new Map(column.map((t, i) => [t.id, { ...t, position: i }]))
  return [...others.filter((t) => t.status !== status), ...renumbered.values()]
}

export function useAddTaskComment(task: Pick<TaskCard, 'id' | 'workspaceId'>) {
  const invalidate = useInvalidateTasks()
  return useMutation({
    mutationFn: async (body: string) => (await api.post<Comment>(`/api/tasks/${task.id}/comments`, { body })).data,
    onSuccess: () => invalidate(task.workspaceId, task.id),
  })
}

export function useSubmitDeliverable(task: Pick<TaskCard, 'id' | 'workspaceId'>) {
  const invalidate = useInvalidateTasks()
  return useMutation({
    mutationFn: async (fileVersionId: string) => (await api.post<Submission>(`/api/tasks/${task.id}/submissions`, { fileVersionId })).data,
    onSuccess: () => invalidate(task.workspaceId, task.id),
  })
}

export function useReviewSubmission(task: Pick<TaskCard, 'id' | 'workspaceId'>) {
  const invalidate = useInvalidateTasks()
  return useMutation({
    mutationFn: async ({ submissionId, status, comment }: { submissionId: string; status: 'ACCEPTED' | 'NEEDS_REVISION'; comment?: string }) =>
      (await api.post<Submission>(`/api/submissions/${submissionId}/review`, { status, comment })).data,
    onSuccess: () => invalidate(task.workspaceId, task.id),
  })
}
