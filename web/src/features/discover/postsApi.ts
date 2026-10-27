import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, ApiError } from '@/shared/api/apiClient'
import type { Paged, StudentRef } from '@/features/suggestions/api'
import { refreshTeams } from '@/features/my_team/api'

// Kiểu theo docs/contracts/openapi/team.yaml (tag Posts) – S10, S11, S21. Chỉ trong đợt lập nhóm (quy tắc 18).

export interface RecruitmentPost {
  id: string
  title: string
  description?: string | null
  specializationIds: number[]
  status: 'OPEN' | 'CLOSED'
  updatedAt: string
}

/** `skills` lấy từ hồ sơ của Academic – có thể rỗng. */
export interface SeekingPost {
  id: string
  title: string
  description?: string | null
  domainIds: number[]
  student: StudentRef
  skills: { skill: string; level: number }[]
  status: 'OPEN' | 'CLOSED'
  updatedAt: string
}

export interface PostInput {
  title: string
  description?: string
}

/** Câu báo lỗi nghiệp vụ của bài đăng. */
export const postErrors: Record<string, string> = {
  'round-closed': 'Đợt lập nhóm đã đóng, không đăng hoặc sửa bài được.',
  'already-in-team': 'Bạn đã có nhóm nên không đăng bài tìm nhóm được.',
  'not-leader': 'Chỉ trưởng nhóm được đăng bài tuyển.',
}

const keys = { mine: ['seeking-posts', 'me'] as const, list: (specializationId: number | undefined, page: number) => ['seeking-posts', specializationId ?? null, page] }

/** S10 – sinh viên chưa có nhóm đang tìm nhóm, mới cập nhật trước. */
export function useSeekingPosts(specializationId: number | undefined, page: number) {
  return useQuery({
    queryKey: keys.list(specializationId, page),
    queryFn: async () => (await api.get<Paged<SeekingPost>>('/api/seeking-posts', { params: { specializationId, page, pageSize: 20 } })).data,
    placeholderData: (previous) => previous,
  })
}

/** S11 – bài đang mở của mình; null nếu chưa đăng (404). */
export function useMySeekingPost(enabled = true) {
  return useQuery({
    queryKey: keys.mine,
    queryFn: async () => {
      try {
        return (await api.get<SeekingPost>('/api/seeking-posts/me')).data
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) return null
        throw error
      }
    },
    enabled,
  })
}

export function useSaveMySeeking() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (action: { kind: 'save'; input: PostInput & { domainIds: number[] } } | { kind: 'remove' }) =>
      action.kind === 'save' ? (await api.put<SeekingPost>('/api/seeking-posts/me', action.input)).data : (await api.delete('/api/seeking-posts/me')).data,
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['seeking-posts'] }),
  })
}

/** S21 – trưởng nhóm tạo / sửa / đóng bài tuyển (mỗi nhóm một bài mở). */
export function useSaveRecruitment(teamId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (action: { kind: 'save'; input: PostInput & { specializationIds: number[] } } | { kind: 'close' }) =>
      action.kind === 'save'
        ? (await api.put<RecruitmentPost>(`/api/teams/${teamId}/recruitment-post`, action.input)).data
        : (await api.delete(`/api/teams/${teamId}/recruitment-post`)).data,
    onSuccess: () => refreshTeams(queryClient),
  })
}
