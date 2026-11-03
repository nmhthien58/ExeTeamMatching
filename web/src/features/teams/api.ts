import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import type { Paged, TableQuery } from '@/shared/components/DataTable'
import type { CourseCode } from '@/shared/phase/usePhase'
import type { StudentRef, TeamCard } from '@/features/suggestions/api'
import type { FullTeam } from '@/features/my_team/api'

// API quản trị nhóm (team.yaml tag Teams, Admin) cho W15, W16.

export const adminTeamKeys = {
  list: (course: CourseCode) => ['teams', 'admin', course] as const,
  detail: (teamId: string) => ['teams', teamId] as const,
}

/** W15 – trang nhóm của học kỳ hiện hành cho DataTable: tìm kiếm, lọc trạng thái / domain / "chưa đủ điều kiện", sắp xếp. */
export const fetchAdminTeams = (course: CourseCode) => async (query: TableQuery) =>
  (
    await api.get<Paged<TeamCard>>('/api/teams', {
      params: {
        courseCode: course,
        search: query.search,
        status: query.filters.status,
        domain: query.filters.domain,
        invalidOnly: query.filters.invalidOnly === 'true' || undefined,
        sort: query.sort ?? 'name',
        page: query.page,
        pageSize: query.pageSize,
      },
    })
  ).data

export function useAdminTeam(teamId: string) {
  return useQuery({ queryKey: adminTeamKeys.detail(teamId), queryFn: async () => (await api.get<FullTeam>(`/api/teams/${teamId}`)).data })
}

/** Nhóm cùng môn để chuyển thành viên sang (chỉ nhóm còn chỗ, khác nhóm hiện tại). */
export function useMoveTargets(course: CourseCode | undefined, exceptTeamId: string) {
  return useQuery({
    queryKey: ['teams', 'admin', course, 'move-targets'],
    queryFn: async () =>
      (await api.get<Paged<TeamCard>>('/api/teams', { params: { courseCode: course, minOpenSlots: 1, sort: 'name', pageSize: 100 } })).data.items,
    enabled: !!course,
    select: (teams) => teams.filter((t) => t.id !== exceptTeamId && t.status !== 'DISBANDED'),
  })
}

/** Sinh viên chưa có nhóm của môn (BE-50) để Admin thêm vào nhóm. */
export function useUnmatchedStudents(course: CourseCode | undefined, search: string) {
  return useQuery({
    queryKey: ['unmatched-students', course, search],
    queryFn: async () =>
      (await api.get<Paged<StudentRef>>('/api/unmatched-students', { params: { courseCode: course, search: search || undefined, pageSize: 20 } })).data.items,
    enabled: !!course,
  })
}

export type AdminTeamAction =
  | { kind: 'finalize' | 'reopen' }
  | { kind: 'add'; studentId: string; reason?: string }
  | { kind: 'move'; studentId: string; targetTeamId: string }
  | { kind: 'remove'; studentId: string }
  | { kind: 'unassignMentor' }

/** W16 – Admin chốt / mở lại nhóm, thêm / bớt sinh viên, chuyển thành viên sang nhóm khác (BE-48), gỡ mentor (BE-80). */
export function useAdminTeamAction(teamId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (action: AdminTeamAction) => {
      switch (action.kind) {
        case 'add':
          return (await api.post(`/api/teams/${teamId}/members`, { studentId: action.studentId, reason: action.reason })).data
        case 'move':
          return (await api.post(`/api/teams/${teamId}/members/${action.studentId}/move`, { targetTeamId: action.targetTeamId })).data
        case 'remove':
          return (await api.post(`/api/teams/${teamId}/members/${action.studentId}/remove`, {})).data
        case 'unassignMentor':
          return (await api.delete(`/api/teams/${teamId}/mentor-assignment`)).data
        default:
          return (await api.post(`/api/teams/${teamId}/${action.kind}`)).data
      }
    },
    onSuccess: () => {
      for (const key of [['teams'], ['unmatched-students'], ['audit-logs'], ['advisor-matching', 'MENTOR']]) void queryClient.invalidateQueries({ queryKey: key })
    },
  })
}
