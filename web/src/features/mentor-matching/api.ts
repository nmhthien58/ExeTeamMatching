import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import type { CourseCode } from '@/shared/phase/usePhase'
import type { AdvisorMatchingOverview, AdvisorMatchingRow, AdvisorMatchingRun, OverviewQuery } from '@/features/lecturer-matching/api'

// Kiểu theo team.yaml, tag AdvisorMatching (W28 – ghép mentor, quy tắc 19, 20). Dùng chung schema với W19.

export type MentorMatchReason = 'MENTOR_INTEREST' | 'DOMAIN' | 'MANUAL'

/** Schema AdvisorLoad của mentor: `preferredTeams` null = mentor chưa đặt số mong muốn; `maxTeams` luôn có (Bộ môn đặt). */
export interface MentorLoad {
  advisorId: string
  fullName: string
  assignedCount: number
  preferredTeams: number | null
  maxTeams: number | null
}

export interface MentorMatchingOverview extends Omit<AdvisorMatchingOverview, 'advisorLoads'> {
  /** Đầu tuần bắt đầu mentor của học kỳ (mặc định tuần 6) – lúc hệ thống tự ghép; null khi học kỳ chưa có ngày bắt đầu. */
  matchingAt: string | null
  advisorLoads: MentorLoad[]
}

export type { AdvisorMatchingRow, AdvisorMatchingRun, OverviewQuery }

/** Schema MentorAssignment. */
export interface MentorAssignment {
  id: string
  teamId: string
  mentorId: string
  mentorName: string
  mentorOrganization: string | null
  isInternal: boolean
  status: 'ASSIGNED' | 'ENDED'
  reason: MentorMatchReason
  source: 'AUTO' | 'MANUAL'
  assignedAt: string
  endedAt: string | null
}

export interface MatchingWarning {
  code: 'ABOVE_PREFERRED'
  message: string
}

/** Kết quả `PUT /api/teams/{teamId}/mentor-assignment`. */
export interface ChangeMentorResult {
  assignment: MentorAssignment
  warnings: MatchingWarning[]
}

export const PAGE_SIZE = 20

export const mentorMatchingKeys = {
  all: ['advisor-matching', 'MENTOR'] as const,
  overview: (course: CourseCode, query: OverviewQuery) => ['advisor-matching', 'MENTOR', course, query] as const,
}

export function useMentorMatchingOverview(course: CourseCode, query: OverviewQuery) {
  return useQuery({
    queryKey: mentorMatchingKeys.overview(course, query),
    queryFn: async () =>
      (
        await api.get<MentorMatchingOverview>('/api/advisor-matching/MENTOR/overview', {
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
    // Số nhóm của mentor tính chung hai môn nên làm mới cả hai môn.
    void queryClient.invalidateQueries({ queryKey: mentorMatchingKeys.all })
    void queryClient.invalidateQueries({ queryKey: ['teams'] })
  }
}

/** Ghép sớm – bước 1: tính phương án cho nhóm chưa có mentor (không đổi dữ liệu). */
export function usePreviewMentorMatching(course: CourseCode) {
  const invalidate = useInvalidateMatching()
  return useMutation({
    mutationFn: async () => (await api.post<AdvisorMatchingRun>('/api/advisor-matching/MENTOR/preview', { courseCode: course })).data,
    onSuccess: invalidate,
  })
}

/** Ghép sớm – bước 2: áp dụng phương án. 409 `run-not-preview` khi đã áp dụng / bỏ. */
export function useConfirmMentorMatching() {
  const invalidate = useInvalidateMatching()
  return useMutation({
    mutationFn: async (runId: string) => (await api.post<AdvisorMatchingRun>(`/api/advisor-matching/runs/${runId}/confirm`)).data,
    onSettled: invalidate,
  })
}

export function useDiscardMentorMatching() {
  const invalidate = useInvalidateMatching()
  return useMutation({
    mutationFn: async (runId: string) => {
      await api.delete(`/api/advisor-matching/runs/${runId}`)
    },
    onSettled: invalidate,
  })
}

/**
 * Đổi / gán mentor tay (lý do "Bộ môn phân công", ghi chú bắt buộc). Vượt số mong muốn → `warnings` ABOVE_PREFERRED;
 * vượt tối đa → 409 `mentor-full`. Một mentor mỗi nhóm: mentor cũ mất quyền vào dự án ngay.
 */
export function useChangeMentor() {
  const invalidate = useInvalidateMatching()
  return useMutation({
    mutationFn: async ({ teamId, mentorId, note }: { teamId: string; mentorId: string; note: string }) =>
      (await api.put<ChangeMentorResult>(`/api/teams/${teamId}/mentor-assignment`, { mentorId, note })).data,
    onSuccess: invalidate,
  })
}

/** Mentor đã đủ số nhóm tối đa – không gán thêm được (rule 20). */
export const isFull = (load: MentorLoad) => load.maxTeams != null && load.assignedCount >= load.maxTeams

/** Gán thêm một nhóm thì vượt số mong muốn (vẫn trong tối đa) – cảnh báo, không chặn. */
export const wouldExceedPreferred = (load: MentorLoad) =>
  load.preferredTeams != null && load.preferredTeams > 0 && load.assignedCount + 1 > load.preferredTeams && !isFull(load)

/** Số mentor khác nhau đang quan tâm (lượt OPEN) trong các nhóm đã tải. */
export function countInterestedMentors(rows: AdvisorMatchingRow[]) {
  return new Set(rows.flatMap((r) => r.interests.filter((i) => i.status === 'OPEN').map((i) => i.advisorId))).size
}
