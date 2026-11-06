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
  /** Chỉ mentor: số mong muốn; null = chưa đặt. */
  preferredTeams?: number | null
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

type AdvisorKind = AdvisorMatchingRun['advisorKind']

/** Bảng W19, W28 của một loại cố vấn. */
export function useAdvisorMatchingOverview<T>(kind: AdvisorKind, course: CourseCode, query: OverviewQuery) {
  return useQuery({
    queryKey: ['advisor-matching', kind, course, query],
    queryFn: async () =>
      (
        await api.get<T>(`/api/advisor-matching/${kind}/overview`, {
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

export const useLecturerMatchingOverview = (course: CourseCode, query: OverviewQuery) =>
  useAdvisorMatchingOverview<AdvisorMatchingOverview>('LECTURER', course, query)

export function useInvalidateMatching(kind: AdvisorKind) {
  const queryClient = useQueryClient()
  return () => {
    void queryClient.invalidateQueries({ queryKey: ['advisor-matching', kind] })
    void queryClient.invalidateQueries({ queryKey: ['teams'] })
  }
}

/** Ghép sớm – bước 1: tính phương án cho nhóm chưa có cố vấn (không đổi dữ liệu). */
export function usePreviewMatching(course: CourseCode, kind: AdvisorKind = 'LECTURER') {
  const invalidate = useInvalidateMatching(kind)
  return useMutation({
    mutationFn: async () => (await api.post<AdvisorMatchingRun>(`/api/advisor-matching/${kind}/preview`, { courseCode: course })).data,
    onSuccess: invalidate,
  })
}

/** Ghép sớm – bước 2: áp dụng phương án. 409 `run-not-preview` khi đã áp dụng / bỏ. */
export function useConfirmMatching(kind: AdvisorKind = 'LECTURER') {
  const invalidate = useInvalidateMatching(kind)
  return useMutation({
    mutationFn: async (runId: string) => (await api.post<AdvisorMatchingRun>(`/api/advisor-matching/runs/${runId}/confirm`)).data,
    onSettled: invalidate,
  })
}

export function useDiscardMatching(kind: AdvisorKind = 'LECTURER') {
  const invalidate = useInvalidateMatching(kind)
  return useMutation({
    mutationFn: async (runId: string) => {
      await api.delete(`/api/advisor-matching/runs/${runId}`)
    },
    onSettled: invalidate,
  })
}

/** Đổi giảng viên tay (lý do "Bộ môn phân công", ghi chú bắt buộc). 409 `lecturer-full` khi vượt tối đa. */
export function useChangeInstructor() {
  const invalidate = useInvalidateMatching('LECTURER')
  return useMutation({
    mutationFn: async ({ teamId, lecturerId, note }: { teamId: string; lecturerId: string; note: string }) =>
      (await api.put(`/api/teams/${teamId}/instructor`, { lecturerId, note })).data,
    onSuccess: invalidate,
  })
}

/** Giảng viên đã đủ số nhóm tối đa chưa. */
export const isFull = (load: AdvisorLoad) => load.maxTeams != null && load.assignedCount >= load.maxTeams

/** Số cố vấn khác nhau đang quan tâm (lượt OPEN) trong các nhóm đã tải. */
export function countInterestedAdvisors(rows: AdvisorMatchingRow[]) {
  return new Set(rows.flatMap((r) => r.interests.filter((i) => i.status === 'OPEN').map((i) => i.advisorId))).size
}
