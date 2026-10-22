import { useQuery } from '@tanstack/react-query'
import { useParams } from 'react-router-dom'
import { api } from '@/shared/api/apiClient'

/** Vai trò trong workspace (collab.yaml PersonRef.role). */
export type WorkspaceRole = 'LEADER' | 'MEMBER' | 'INSTRUCTOR' | 'MENTOR'

export interface PersonRef {
  personType: 'STUDENT' | 'LECTURER' | 'MENTOR'
  personId: string
  displayName: string
  role?: WorkspaceRole | null
}

export interface TaskProgress {
  todo: number
  doing: number
  done: number
  overdue: number
}

export interface MeetingCard {
  id: string
  title: string
  startsAt?: string | null
  isMentorSession?: boolean
  isWeekly?: boolean
}

export interface RecentActivity {
  kind: 'TASK' | 'FILE' | 'MEETING' | 'QUESTION'
  title: string
  at: string
}

/** Schema WorkspaceSummary / WorkspaceOverview – trường số liệu của từng module có thể chưa có khi module chưa xong. */
export interface WorkspaceOverview {
  id: string
  teamId: string
  teamName: string
  projectName?: string | null
  courseCode: 'EXE101' | 'EXE201'
  myRole: WorkspaceRole | 'ADMIN'
  previousWorkspaceId?: string | null
  unreadCount?: number
  openQuestionCount?: number
  nextMeetingAt?: string | null
  mentorSessionsDone?: number
  expectedMentorSessions?: number
  taskProgress?: TaskProgress
  people?: PersonRef[]
  upcomingMeetings?: MeetingCard[]
  recentActivity?: RecentActivity[]
}

export const workspaceKeys = {
  mine: ['workspaces', 'me'] as const,
  detail: (id: string) => ['workspaces', id] as const,
}

export function useMyWorkspaces() {
  return useQuery({
    queryKey: workspaceKeys.mine,
    queryFn: async () => (await api.get<WorkspaceOverview[]>('/api/workspaces/me')).data,
  })
}

/**
 * Workspace đang xem: giảng viên / mentor mở theo `:workspaceId` (từ L04, M01); sinh viên chỉ có một workspace
 * nên lấy từ <c>/api/workspaces/me</c>.
 */
export function useCurrentWorkspaceId(): { workspaceId?: string; isLoading: boolean; error: unknown; noWorkspace: boolean } {
  const { workspaceId } = useParams()
  const mine = useMyWorkspaces()
  if (workspaceId) return { workspaceId, isLoading: false, error: null, noWorkspace: false }
  return {
    workspaceId: mine.data?.[0]?.id,
    isLoading: mine.isLoading,
    error: mine.error,
    noWorkspace: mine.isSuccess && mine.data.length === 0,
  }
}

export function useWorkspace(workspaceId?: string) {
  return useQuery({
    queryKey: workspaceKeys.detail(workspaceId ?? ''),
    queryFn: async () => (await api.get<WorkspaceOverview>(`/api/workspaces/${workspaceId}`)).data,
    enabled: !!workspaceId,
  })
}

/** Sinh viên của nhóm được tạo / sửa việc, tải file; cố vấn chỉ xem và bình luận; Bộ môn chỉ xem (rule 21, 23, 24). */
export const isStudentRole = (role?: string) => role === 'LEADER' || role === 'MEMBER'
