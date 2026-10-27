import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import type { Paged, TeamCard } from '@/features/suggestions/api'
import { refreshTeams, type FullTeam, type Idea } from '@/features/my_team/api'

/** Bộ lọc S06 (query của `GET /api/teams`). Mã lớp chưa có: Team Service chưa lọc được theo lớp. */
export interface TeamFilter {
  domain?: string
  missingSpecializationId?: number
  minOpenSlots?: number
  sort: '-createdAt' | 'name' | '-openSlots'
}

export const emptyFilter: TeamFilter = { sort: '-createdAt' }

export const filterCount = (f: TeamFilter) =>
  [f.domain, f.missingSpecializationId, f.minOpenSlots].filter((v) => v !== undefined).length + (f.sort === '-createdAt' ? 0 : 1)

/** S05 – nhóm đang tuyển của môn và học kỳ của mình (nhóm đủ người không có); ngoài đợt lập nhóm API trả 409 round-closed. */
export function useTeamSearch(search: string, filter: TeamFilter, page: number) {
  return useQuery({
    queryKey: ['teams', 'search', search, filter, page],
    queryFn: async () => (await api.get<Paged<TeamCard>>('/api/teams', { params: { search: search || undefined, ...filter, page, pageSize: 20 } })).data,
    placeholderData: (previous) => previous,
  })
}

/** S08 – chi tiết nhóm kèm quan hệ của người xem. */
export function useTeam(teamId: string) {
  return useQuery({ queryKey: ['teams', teamId], queryFn: async () => (await api.get<FullTeam>(`/api/teams/${teamId}`)).data })
}

/** S09 – 1–3 ý tưởng hoặc "Chưa có ý tưởng" (quy tắc 17). */
export function useJoinRequest(teamId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (body: { message?: string; ideas: Idea[]; hasNoIdea: boolean }) => (await api.post(`/api/teams/${teamId}/join-requests`, body)).data,
    onSuccess: () => refreshTeams(queryClient),
  })
}
