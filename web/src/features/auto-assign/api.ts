import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, ApiError, toApiError } from '@/shared/api/apiClient'
import type { Paged, TableQuery } from '@/shared/components/DataTable'
import type { CourseCode } from '@/shared/phase/usePhase'
import type { StudentRef, TeamCard, TeamDetail, TeamStatus } from '@/features/suggestions/api'

/** Schema AutoAssignResult (team.yaml) – kết quả ghép tự động gần nhất (W21). */
export interface AutoAssignResult {
  runAt: string
  filledTeams: TeamCard[]
  newTeams: TeamCard[]
  unassignedStudents: StudentRef[]
}

export const autoAssignKeys = {
  unmatched: (course: CourseCode) => ['unmatched-students', course] as const,
  result: (semesterId: string, course: CourseCode) => ['auto-assignment', 'result', semesterId, course] as const,
  teams: (course: CourseCode) => ['admin', 'teams', course] as const,
  team: (teamId: string) => ['teams', teamId] as const,
}

/** W20 – trang sinh viên chưa có nhóm cho DataTable. */
export const fetchUnmatched = (course: CourseCode) => async (query: TableQuery) =>
  (
    await api.get<Paged<StudentRef>>('/api/unmatched-students', {
      params: { courseCode: course, specializationId: query.filters.specializationId, search: query.search, page: query.page, pageSize: query.pageSize },
    })
  ).data

/** W22 – trang nhóm theo trạng thái cho DataTable. */
export const fetchTeams = (course: CourseCode, status: TeamStatus) => async (query: TableQuery) =>
  (
    await api.get<Paged<TeamCard>>('/api/teams', {
      params: { courseCode: course, status, search: query.search, sort: query.sort ?? 'name', page: query.page, pageSize: query.pageSize },
    })
  ).data

export function useRemindUnmatched() {
  return useMutation({
    mutationFn: async (course: CourseCode) => {
      await api.post('/api/unmatched-students/remind', { courseCode: course })
    },
  })
}

/** Kết quả gần nhất; null khi chưa chạy lần nào (404). */
export function useAutoAssignResult(semesterId: string | undefined, course: CourseCode) {
  return useQuery({
    queryKey: autoAssignKeys.result(semesterId ?? '', course),
    queryFn: async () => {
      try {
        return (await api.get<AutoAssignResult>('/api/auto-assignment/result', { params: { semesterId, courseCode: course } })).data
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) return null
        throw error
      }
    },
    enabled: !!semesterId,
  })
}

/** Chạy lại ghép tự động (W20, W21): Team gọi Matching.PlanAutoAssignment rồi áp dụng. */
export function useRunAutoAssign(semesterId: string | undefined, course: CourseCode) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async () => (await api.post<AutoAssignResult>('/api/auto-assignment/run', { semesterId, courseCode: course })).data,
    onSuccess: (result) => {
      queryClient.setQueryData(autoAssignKeys.result(semesterId ?? '', course), result)
      void queryClient.invalidateQueries({ queryKey: autoAssignKeys.unmatched(course) })
    },
  })
}

/** Chi tiết (thành viên) của các nhóm trong kết quả ghép. */
export function useTeamDetails(teamIds: string[]) {
  return useQueries({
    queries: teamIds.map((id) => ({
      queryKey: autoAssignKeys.team(id),
      queryFn: async () => (await api.get<TeamDetail>(`/api/teams/${id}`)).data,
    })),
  })
}

/** Một điều chỉnh của Admin ở W21: đưa sinh viên vào nhóm (`fromTeamId` null = sinh viên chưa được ghép). */
export interface MemberMove {
  studentId: string
  fromTeamId: string | null
  toTeamId: string
}

/** Lưu dở: `saved` điều chỉnh đầu đã lưu, điều chỉnh tiếp theo lỗi (`cause`). */
export class PartialSaveError extends Error {
  constructor(readonly saved: number, readonly cause: ApiError) {
    super(cause.title)
  }
}

/** Lưu lần lượt các điều chỉnh; dừng ở điều chỉnh lỗi đầu tiên và báo lại số đã lưu. */
export function useSaveMoves() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (moves: MemberMove[]) => {
      for (const [index, move] of moves.entries()) {
        try {
          if (move.fromTeamId) {
            await api.post(`/api/teams/${move.fromTeamId}/members/${move.studentId}/move`, { targetTeamId: move.toTeamId })
          } else {
            await api.post(`/api/teams/${move.toTeamId}/members`, { studentId: move.studentId, reason: 'Điều chỉnh sau ghép tự động' })
          }
        } catch (error) {
          throw new PartialSaveError(index, toApiError(error))
        }
      }
      return moves.length
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: ['teams'] })
      void queryClient.invalidateQueries({ queryKey: ['auto-assignment'] })
    },
  })
}

export function useFinalizeTeam(course: CourseCode) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ teamId, action }: { teamId: string; action: 'finalize' | 'reopen' }) =>
      (await api.post<TeamDetail>(`/api/teams/${teamId}/${action}`)).data,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: autoAssignKeys.teams(course) })
    },
  })
}

/** Xuất Excel danh sách nhóm (W22) và tải về. */
export async function exportTeams(semesterId: string | undefined, course: CourseCode) {
  const { data } = await api.get<Blob>('/api/teams/export', { params: { semesterId, courseCode: course }, responseType: 'blob' })
  const url = URL.createObjectURL(data)
  const link = Object.assign(document.createElement('a'), { href: url, download: `danh-sach-nhom-${course}.xlsx` })
  link.click()
  URL.revokeObjectURL(url)
}
