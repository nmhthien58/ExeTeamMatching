import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import type { CourseCode } from '@/shared/phase/usePhase'

/** academic.yaml, tag Rounds. */
export type RoundType = 'TEAM_FORMATION' | 'CHANGE_REQUEST'
export type RoundStatus = 'SCHEDULED' | 'OPEN' | 'CLOSED'

export interface Round {
  id: string
  semesterId: string
  courseCode: CourseCode
  roundType: RoundType
  name: string
  opensAt: string
  deadlineAt: string
  status: RoundStatus
  closedAt: string | null
}

export type RoundInput = Pick<Round, 'semesterId' | 'courseCode' | 'roundType' | 'name' | 'opensAt' | 'deadlineAt'>

export function useRounds(semesterId: string | undefined, courseCode: CourseCode | undefined) {
  return useQuery({
    queryKey: ['registration-rounds', 'list', semesterId, courseCode],
    queryFn: async () => (await api.get<Round[]>('/api/registration-rounds', { params: { semesterId, courseCode } })).data,
    enabled: !!semesterId,
  })
}

/** Đổi đợt làm đổi banner hạn chót và giai đoạn môn (`phases`). */
function useInvalidateRounds() {
  const client = useQueryClient()
  return () => {
    void client.invalidateQueries({ queryKey: ['registration-rounds'] })
    void client.invalidateQueries({ queryKey: ['phases'] })
  }
}

/** Có `id` thì sửa, không có thì tạo ở trạng thái SCHEDULED. */
export function useSaveRound() {
  const invalidate = useInvalidateRounds()
  return useMutation({
    mutationFn: async ({ id, input }: { id?: string; input: RoundInput }) =>
      (id ? await api.put<Round>(`/api/registration-rounds/${id}`, input) : await api.post<Round>('/api/registration-rounds', input)).data,
    onSuccess: invalidate,
  })
}

export function useChangeRoundState() {
  const invalidate = useInvalidateRounds()
  return useMutation({
    mutationFn: async ({ id, action }: { id: string; action: 'open' | 'close' }) =>
      (await api.post<Round>(`/api/registration-rounds/${id}/${action}`)).data,
    onSuccess: invalidate,
  })
}
