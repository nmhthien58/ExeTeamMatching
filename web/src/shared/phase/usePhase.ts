import { useQuery } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import { useAuth } from '@/shared/auth/AuthProvider'

export type CourseCode = 'EXE101' | 'EXE201'

export interface CoursePhase {
  courseCode: CourseCode
  phase: 'TEAM_FORMATION' | 'SEMESTER_RUNNING'
  teamFormationDeadlineAt?: string | null
  changeRequestRoundOpen?: boolean
  mentoringStartsOn?: string
}

/** Giai đoạn của từng môn (rule 18) – đổi tab Khám phá ↔ Dự án. */
export function usePhases() {
  const { user } = useAuth()
  return useQuery({
    queryKey: ['phases'],
    queryFn: async () => (await api.get<CoursePhase[]>('/api/semesters/current/phase')).data,
    enabled: !!user,
    staleTime: 5 * 60_000,
  })
}

/** Môn của sinh viên đang đăng nhập (từ hồ sơ). */
export function useMyCourse() {
  const { user } = useAuth()
  return useQuery({
    queryKey: ['students', 'me', 'course'],
    queryFn: async () =>
      (await api.get<{ courseCode?: CourseCode }>('/api/students/me')).data.courseCode ?? null,
    enabled: !!user?.studentId,
    staleTime: 10 * 60_000,
  })
}

/**
 * Sinh viên đang ở giai đoạn lập nhóm của môn mình không? Chưa biết (đang tải / lỗi) thì coi như đang lập nhóm
 * để không giấu Khám phá nhầm; API vẫn trả 409 round-closed nếu đã đóng.
 */
export function useIsTeamFormation(courseCode?: CourseCode | null) {
  const phases = usePhases()
  const phase = phases.data?.find((p) => p.courseCode === courseCode) ?? phases.data?.[0]
  return { isTeamFormation: phase ? phase.phase === 'TEAM_FORMATION' : true, phase, isLoading: phases.isLoading }
}
