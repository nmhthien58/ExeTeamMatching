import { useMutation } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import type { CourseCode } from '@/shared/phase/usePhase'
import {
  countInterestedAdvisors,
  isFull,
  PAGE_SIZE,
  useAdvisorMatchingOverview,
  useConfirmMatching,
  useDiscardMatching,
  useInvalidateMatching,
  usePreviewMatching,
  type AdvisorMatchingOverview,
  type AdvisorMatchingRow,
  type AdvisorMatchingRun,
  type OverviewQuery,
} from '@/features/lecturer-matching/api'

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

export { countInterestedAdvisors as countInterestedMentors, isFull, PAGE_SIZE }

// Số nhóm của mentor tính chung hai môn: khóa truy vấn theo loại cố vấn nên làm mới cả hai môn.
export const useMentorMatchingOverview = (course: CourseCode, query: OverviewQuery) =>
  useAdvisorMatchingOverview<MentorMatchingOverview>('MENTOR', course, query)

export const usePreviewMentorMatching = (course: CourseCode) => usePreviewMatching(course, 'MENTOR')
export const useConfirmMentorMatching = () => useConfirmMatching('MENTOR')
export const useDiscardMentorMatching = () => useDiscardMatching('MENTOR')

/**
 * Đổi / gán mentor tay (lý do "Bộ môn phân công", ghi chú bắt buộc). Vượt số mong muốn → `warnings` ABOVE_PREFERRED;
 * vượt tối đa → 409 `mentor-full`. Một mentor mỗi nhóm: mentor cũ mất quyền vào dự án ngay.
 */
export function useChangeMentor() {
  const invalidate = useInvalidateMatching('MENTOR')
  return useMutation({
    mutationFn: async ({ teamId, mentorId, note }: { teamId: string; mentorId: string; note: string }) =>
      (await api.put<ChangeMentorResult>(`/api/teams/${teamId}/mentor-assignment`, { mentorId, note })).data,
    onSuccess: invalidate,
  })
}

/** Gán thêm một nhóm thì vượt số mong muốn (vẫn trong tối đa) – cảnh báo, không chặn. */
export const wouldExceedPreferred = (load: MentorLoad) =>
  load.preferredTeams != null && load.preferredTeams > 0 && load.assignedCount + 1 > load.preferredTeams && !isFull(load)
