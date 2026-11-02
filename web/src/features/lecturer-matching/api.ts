import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import type { CourseCode } from '@/shared/phase/usePhase'
import type { TeamCard } from '@/features/suggestions/api'

// Kiểu theo team.yaml, tag AdvisorMatching (W19 – ghép giảng viên hướng dẫn, quy tắc 9, 19).

export type LecturerMatchReason = 'MUTUAL' | 'PREFERENCE' | 'LECTURER_INTEREST' | 'PREVIOUS_INSTRUCTOR' | 'DOMAIN' | 'MANUAL'

export interface MatchingPlanItem {
  teamId: string
  teamName: string
  /** null = không ghép được (mọi giảng viên đã đầy). */
  advisorId: string | null
  advisorName: string | null
  reason: string | null
  preferenceRank: number | null
}

/** Schema AdvisorMatchingRun. */
export interface AdvisorMatchingRun {
  id: string
  advisorKind: 'LECTURER' | 'MENTOR'
  courseCode: CourseCode
  trigger: 'SCHEDULED' | 'EARLY'
  status: 'PREVIEW' | 'APPLIED' | 'DISCARDED'
  matchedCount: number
  unmatchedCount: number
  createdAt: string
  appliedAt: string | null
  plan: MatchingPlanItem[]
}

export interface LecturerRef {
  lecturerId: string
  fullName: string
}

export interface TopicInterest {
  id: string
  advisorId: string
  advisorName: string
  wasPreviousInstructor?: boolean
  status: 'OPEN' | 'WITHDRAWN' | 'CLOSED'
}

/** Schema AdvisorMatchingRow. */
export interface AdvisorMatchingRow {
  team: TeamCard
  advisorId: string | null
  advisorName: string | null
  reason: string | null
  preferenceRank: number | null
  /** Nguyện vọng 1–3 theo thứ tự. */
  preferences: LecturerRef[]
  interests: TopicInterest[]
}

/** Schema AdvisorLoad. `maxTeams` null = không giới hạn. */
export interface AdvisorLoad {
  advisorId: string
  fullName: string
  assignedCount: number
  maxTeams: number | null
}

export interface AdvisorMatchingOverview {
  /** Lúc hệ thống tự ghép (hạn lập nhóm); null khi đợt lập nhóm không mở. */
  matchingAt: string | null
  lastRun: AdvisorMatchingRun | null
  teamsWithPreferences: number
  teamsWithoutAdvisor: number
  items: AdvisorMatchingRow[]
  advisorLoads: AdvisorLoad[]
  // Trường phân trang đang được bổ sung ở contract – có thể thiếu.
  page?: number
  pageSize?: number
  totalCount?: number
  totalPages?: number
}

export interface OverviewQuery {
  withoutAdvisor: boolean
  search?: string
  page: number
}

export const PAGE_SIZE = 20

export const lecturerMatchingKeys = {
  all: ['advisor-matching', 'LECTURER'] as const,
  overview: (course: CourseCode, query: OverviewQuery) => ['advisor-matching', 'LECTURER', course, query] as const,
}

export function useLecturerMatchingOverview(course: CourseCode, query: OverviewQuery) {
  return useQuery({
    queryKey: lecturerMatchingKeys.overview(course, query),
    queryFn: async () =>
      (
        await api.get<AdvisorMatchingOverview>('/api/advisor-matching/LECTURER/overview', {
          params: {
            courseCode: course,
            withoutAdvisor: query.withoutAdvisor || undefined,
            search: query.search || undefined,
            page: query.page,
            pageSize: PAGE_SIZE,
          },
        })
      ).data,
    placeholderData: keepPreviousData,
  })
}

function useInvalidateMatching() {
  const queryClient = useQueryClient()
  return () => {
    void queryClient.invalidateQueries({ queryKey: lecturerMatchingKeys.all })
    void queryClient.invalidateQueries({ queryKey: ['teams'] })
  }
}

/** Ghép sớm – bước 1: tính phương án cho nhóm chưa có giảng viên (không đổi dữ liệu). */
export function usePreviewMatching(course: CourseCode) {
  const invalidate = useInvalidateMatching()
  return useMutation({
    mutationFn: async () => (await api.post<AdvisorMatchingRun>('/api/advisor-matching/LECTURER/preview', { courseCode: course })).data,
    onSuccess: invalidate,
  })
}

/** Ghép sớm – bước 2: áp dụng phương án. 409 `run-not-preview` khi đã áp dụng / bỏ. */
export function useConfirmMatching() {
  const invalidate = useInvalidateMatching()
  return useMutation({
    mutationFn: async (runId: string) => (await api.post<AdvisorMatchingRun>(`/api/advisor-matching/runs/${runId}/confirm`)).data,
    onSettled: invalidate,
  })
}

export function useDiscardMatching() {
  const invalidate = useInvalidateMatching()
  return useMutation({
    mutationFn: async (runId: string) => {
      await api.delete(`/api/advisor-matching/runs/${runId}`)
    },
    onSettled: invalidate,
  })
}

/** Đổi giảng viên tay (lý do "Bộ môn phân công", ghi chú bắt buộc). 409 `lecturer-full` khi vượt tối đa. */
export function useChangeInstructor() {
  const invalidate = useInvalidateMatching()
  return useMutation({
    mutationFn: async ({ teamId, lecturerId, note }: { teamId: string; lecturerId: string; note: string }) =>
      (await api.put(`/api/teams/${teamId}/instructor`, { lecturerId, note })).data,
    onSuccess: invalidate,
  })
}

/** Giảng viên đã đủ số nhóm tối đa chưa. */
export const isFull = (load: AdvisorLoad) => load.maxTeams != null && load.assignedCount >= load.maxTeams

/** Số giảng viên khác nhau đang quan tâm (lượt OPEN) trong các nhóm đã tải. */
export function countInterestedLecturers(rows: AdvisorMatchingRow[]) {
  return new Set(rows.flatMap((r) => r.interests.filter((i) => i.status === 'OPEN').map((i) => i.advisorId))).size
}
