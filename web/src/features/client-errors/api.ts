import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import type { Paged } from '@/shared/components/DataTable'

// Kiểu theo docs/contracts/openapi/identity.yaml (tag ClientErrors).

export type ClientErrorKind = 'fatal' | 'non_fatal'

export interface ClientError {
  id: string
  kind: ClientErrorKind
  message: string
  exceptionType: string
  stackTrace?: string | null
  platform: string
  appVersion: string
  deviceModel?: string | null
  osVersion?: string | null
  userId?: string | null
  role?: string | null
  screen?: string | null
  occurredAt: string
  receivedAt: string
}

export interface DailyErrorCount {
  /** `yyyy-mm-dd` theo giờ Việt Nam. */
  date: string
  fatalCount: number
  nonFatalCount: number
}

export interface TopError {
  exceptionType: string
  firstStackLine: string
  count: number
  fatalCount: number
  lastOccurredAt: string
}

export interface ClientErrorSummary {
  fatalCount: number
  nonFatalCount: number
  affectedUsers: number
  daily: DailyErrorCount[]
  topErrors: TopError[]
}

export interface ClientErrorFilter {
  kind?: string
  /** `yyyy-mm-dd` theo giờ máy, tính cả ngày. */
  from?: string
  to?: string
  appVersion?: string
  page: number
}

export const CRASHLYTICS_URL = 'https://console.firebase.google.com/project/exe-team-matching/crashlytics'

const PAGE_SIZE = 20

/** Tự tải lại để Bộ môn thấy lỗi mới ngay khi app gửi lên, không phải F5. */
const REFRESH_MS = 30_000

/** API không tính mốc `to`, nên "đến ngày" là 0 giờ của ngày hôm sau. */
function endOfDay(date: string): string {
  const next = new Date(`${date}T00:00:00`)
  next.setDate(next.getDate() + 1)
  return next.toISOString()
}

export function useClientErrors(filter: ClientErrorFilter) {
  return useQuery({
    queryKey: ['client-errors', 'list', filter],
    queryFn: async () =>
      (
        await api.get<Paged<ClientError>>('/api/admin/client-errors', {
          params: {
            kind: filter.kind,
            from: filter.from ? new Date(`${filter.from}T00:00:00`).toISOString() : undefined,
            to: filter.to ? endOfDay(filter.to) : undefined,
            appVersion: filter.appVersion,
            page: filter.page,
            pageSize: PAGE_SIZE,
          },
        })
      ).data,
    placeholderData: keepPreviousData,
    refetchInterval: REFRESH_MS,
  })
}

export function useClientErrorSummary() {
  return useQuery({
    queryKey: ['client-errors', 'summary'],
    queryFn: async () => (await api.get<ClientErrorSummary>('/api/admin/client-errors/summary')).data,
    refetchInterval: REFRESH_MS,
  })
}
