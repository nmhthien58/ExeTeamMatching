import { useQuery } from '@tanstack/react-query'
import { useSearchParams } from 'react-router-dom'
import { api } from '@/shared/api/apiClient'
import type { CourseCode } from '@/shared/phase/usePhase'

export interface Semester {
  id: string
  code: string
  name: string
}

export interface Round {
  id: string
  semesterId: string
  courseCode: CourseCode
  roundType: 'TEAM_FORMATION' | 'CHANGE_REQUEST'
  name: string
  deadlineAt: string
  status: 'SCHEDULED' | 'OPEN' | 'CLOSED'
}

/** Thống kê W02 – GET /api/statistics/overview (team.yaml). */
export interface StatisticsOverview {
  studentsWithTeam: number
  studentsWithoutTeam: number
  teamsBySize: Record<string, number>
  pendingChangeRequests: number
  teamsNeedingReview: number
  bySpecialization: { code: string; count: number }[]
  byDomain: { code: string; count: number }[]
}

export const COURSES: CourseCode[] = ['EXE101', 'EXE201']

/** Môn đang xem ở các trang quản trị, lưu trên URL (`?mon=EXE201`) để chia sẻ link và giữ khi F5. */
export function useCourseParam(): [CourseCode, (course: CourseCode) => void] {
  const [params, setParams] = useSearchParams()
  const course: CourseCode = params.get('mon') === 'EXE201' ? 'EXE201' : 'EXE101'
  const set = (next: CourseCode) => {
    const p = new URLSearchParams(params)
    p.set('mon', next)
    p.delete('page')
    setParams(p, { replace: true })
  }
  return [course, set]
}

export function useCurrentSemester() {
  return useQuery({
    queryKey: ['semesters', 'current'],
    queryFn: async () => (await api.get<Semester>('/api/semesters/current')).data,
    staleTime: 10 * 60_000,
  })
}

/** Đợt lập nhóm đang mở của môn (null nếu đã đóng / chưa mở). */
export function useOpenTeamRound(course: CourseCode) {
  return useQuery({
    queryKey: ['registration-rounds', 'active', course],
    queryFn: async () =>
      (await api.get<Round[]>('/api/registration-rounds/active', { params: { courseCode: course } })).data
        .find((r) => r.roundType === 'TEAM_FORMATION') ?? null,
  })
}

export function useStatistics(semesterId: string | undefined, course: CourseCode) {
  return useQuery({
    queryKey: ['statistics', semesterId, course],
    queryFn: async () =>
      (await api.get<StatisticsOverview>('/api/statistics/overview', { params: { semesterId, courseCode: course } })).data,
    enabled: !!semesterId,
  })
}

/** "Còn 2 ngày 5 giờ", "Còn 5 giờ", "Còn 20 phút" (DESIGN.md mục 9) và mức cảnh báo theo banner hạn chót (6.1). */
export function timeLeft(deadline: string, now: Date = new Date()): { text: string; tone: 'info' | 'warning' | 'error' } {
  const ms = new Date(deadline).getTime() - now.getTime()
  const hours = Math.floor(ms / 3_600_000)
  const days = Math.floor(hours / 24)
  const text = days > 0 ? `Còn ${days} ngày ${hours % 24} giờ` : hours > 0 ? `Còn ${hours} giờ` : `Còn ${Math.max(0, Math.floor(ms / 60_000))} phút`
  return { text, tone: hours <= 24 ? 'error' : hours <= 72 ? 'warning' : 'info' }
}
