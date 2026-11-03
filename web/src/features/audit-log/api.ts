import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import type { Paged } from '@/shared/components/DataTable'
import type { TeamCard } from '@/features/suggestions/api'

// Kiểu theo docs/contracts/openapi/team.yaml (tag AuditLog).

/** Một dòng nhật ký. `action` là mã lưu trong DB (MEMBER_JOINED…); `actorName` hiện theo vai trò. */
export interface AuditLog {
  id: number
  teamId?: string | null
  action: string
  actorName: string
  actorRole: string
  summary: string
  reason?: string | null
  occurredAt: string
}

export interface AuditLogFilter {
  teamId?: string
  /** `yyyy-mm-dd` theo giờ máy, tính cả ngày. */
  from?: string
  to?: string
  page: number
  actorName?: string
}

const PAGE_SIZE = 20

/** W24 – nhật ký hệ thống (Admin). */
export function useSystemAuditLogs(filter: AuditLogFilter) {
  return useQuery({
    queryKey: ['audit-logs', 'system', filter],
    queryFn: async () =>
      (
        await api.get<Paged<AuditLog>>('/api/audit-logs', {
          params: {
            teamId: filter.teamId,
            actorName: filter.actorName,
            from: filter.from ? new Date(`${filter.from}T00:00:00`).toISOString() : undefined,
            to: filter.to ? new Date(`${filter.to}T23:59:59.999`).toISOString() : undefined,
            page: filter.page,
            pageSize: PAGE_SIZE,
          },
        })
      ).data,
    placeholderData: keepPreviousData,
  })
}

/** S22, L05 – lịch sử một nhóm. */
export function useTeamAuditLogs(teamId: string | undefined, page: number) {
  return useQuery({
    queryKey: ['audit-logs', 'team', teamId, page],
    queryFn: async () => (await api.get<Paged<AuditLog>>(`/api/teams/${teamId}/audit-logs`, { params: { page, pageSize: PAGE_SIZE } })).data,
    enabled: !!teamId,
    placeholderData: keepPreviousData,
  })
}

/** Nhóm của học kỳ hiện hành cho bộ lọc W24 (Admin xem cả nhóm đủ người, đã giải tán). */
export function useTeamOptions() {
  return useQuery({
    queryKey: ['teams', 'audit-options'],
    queryFn: async () => (await api.get<Paged<TeamCard>>('/api/teams', { params: { sort: 'name', page: 1, pageSize: 100 } })).data.items,
    staleTime: 5 * 60_000,
  })
}

