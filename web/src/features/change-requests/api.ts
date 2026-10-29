import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import type { Paged, TableQuery } from '@/shared/components/DataTable'
import type { Round } from '@/features/dashboard/adminApi'

export type ChangeRequestStatus = 'PENDING' | 'APPROVED' | 'REJECTED' | 'CANCELLED'

export interface ChangeStudent {
  id: string
  studentCode: string
  fullName: string
  specialization?: { code: string; name: string } | null
}

export interface ChangeRequest {
  id: string
  team: { id: string; name: string; courseCode: 'EXE101' | 'EXE201'; memberCount: number }
  roundId: string
  roundName: string
  reason: string
  changedMemberCount: number
  changedPercent: number
  status: ChangeRequestStatus
  reviewedByName: string | null
  reviewedAt: string | null
  reviewNote: string | null
  createdAt: string
}

export interface ChangeItem {
  id: string
  action: 'ADD' | 'REMOVE' | 'SET_LEADER' | 'PROJECT_CHANGE'
  source: string
  student: ChangeStudent | null
  newProjectName?: string | null
  newProjectDescription?: string | null
  createdAt: string
}

export interface ChangeRequestDetail extends ChangeRequest {
  baselineMembers: ChangeStudent[]
  membersAfterChange: ChangeStudent[]
  items: ChangeItem[]
  projectBefore?: { name: string; description?: string | null } | null
  projectAfter?: { name: string; description?: string | null } | null
}

export const changeRequestKeys = {
  all: ['change-requests'] as const,
  list: (status: ChangeRequestStatus | undefined, roundId: string | undefined, query: TableQuery) =>
    [...changeRequestKeys.all, status, roundId, query] as const,
  detail: (id: string) => [...changeRequestKeys.all, id] as const,
}

export async function fetchChangeRequests(
  query: TableQuery,
  status?: ChangeRequestStatus,
  roundId?: string,
): Promise<Paged<ChangeRequest>> {
  const { data } = await api.get<Paged<ChangeRequest>>('/api/change-requests', {
    params: {
      page: query.page,
      pageSize: query.pageSize,
      status,
      roundId,
      search: query.search,
      sort: query.sort,
    },
  })
  return data
}

export async function fetchChangeRequest(id: string): Promise<ChangeRequestDetail> {
  return (await api.get<ChangeRequestDetail>(`/api/change-requests/${id}`)).data
}

export function useChangeRequestDecision() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, decision, note }: { id: string; decision: 'approve' | 'reject'; note?: string }) =>
      (await api.post<ChangeRequest>(`/api/change-requests/${id}/${decision}`, decision === 'reject' ? { note } : {})).data,
    onSuccess: () => client.invalidateQueries({ queryKey: changeRequestKeys.all }),
  })
}

export function useChangeRequestRounds(semesterId: string | undefined) {
  return useQuery({
    queryKey: ['registration-rounds', semesterId, 'change-request'],
    queryFn: async () => {
      const { data } = await api.get<Round[]>('/api/registration-rounds', {
        params: { semesterId, courseCode: 'EXE201' },
      })
      return data.filter((round) => round.roundType === 'CHANGE_REQUEST')
    },
    enabled: !!semesterId,
  })
}
