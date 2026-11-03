import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { EmptyState, ErrorState, NoResults, TableSkeleton } from '@/shared/components/States'
import { Button, Icon } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { formatDateTime } from '@/features/workspace/format'
import { useSystemAuditLogs, useTeamOptions } from './api'

const fieldClass = 'h-10 rounded-sm border border-border-strong bg-surface px-3 text-body'

/**
 * W24 – Nhật ký hệ thống (FE-30, quy tắc 10): ai, lúc nào, thay đổi gì; lọc theo nhóm, tên người thực hiện, khoảng thời gian, phân trang 20.
 * Không dùng DataTable vì API không có tìm kiếm (DataTable luôn có ô tìm). Bộ lọc nằm trên URL để F5 / chia sẻ giữ nguyên.
 */
export function AuditLogPage() {
  const [params, setParams] = useSearchParams()
  const filter = {
    teamId: params.get('teamId') ?? undefined,
    actorName: params.get('actor') ?? undefined,
    from: params.get('from') ?? undefined,
    to: params.get('to') ?? undefined,
    page: Math.max(1, Number(params.get('page')) || 1),
  }
  const logs = useSystemAuditLogs(filter)
  const teams = useTeamOptions()
  const teamName = new Map((teams.data ?? []).map((t) => [t.id, t.name]))
  const hasFilters = !!(filter.teamId || filter.actorName || filter.from || filter.to)
  const invalidRange = !!(filter.from && filter.to && filter.from > filter.to)

  const update = (patch: Record<string, string | undefined>) => {
    const next = new URLSearchParams(params)
    for (const [k, v] of Object.entries(patch)) {
      if (v) next.set(k, v)
      else next.delete(k)
    }
    if (!('page' in patch)) next.delete('page')
    setParams(next, { replace: true })
  }

  // Gõ tên thì đợi 300 ms mới lọc, tránh gọi API mỗi phím.
  const [actor, setActor] = useState(filter.actorName ?? '')
  useEffect(() => setActor(filter.actorName ?? ''), [filter.actorName])
  useEffect(() => {
    const value = actor.trim() || undefined
    if (value === filter.actorName) return
    const timer = setTimeout(() => update({ actor: value }), 300)
    return () => clearTimeout(timer)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [actor])

  const data = logs.data
  const total = data?.totalCount ?? 0
  const from = total === 0 ? 0 : (filter.page - 1) * (data?.pageSize ?? 20) + 1
  const to = Math.min(filter.page * (data?.pageSize ?? 20), total)

  return (
    <>
      <PageHeader title="Nhật ký hệ thống" />
      <section className="flex flex-col gap-3">
        <div className="flex flex-wrap items-end gap-3">
          <label className="flex flex-col gap-1">
            <span className="text-caption text-text-secondary">Nhóm</span>
            <select value={filter.teamId ?? ''} onChange={(e) => update({ teamId: e.target.value })} className={fieldClass}>
              <option value="">Tất cả nhóm</option>
              {(teams.data ?? []).map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name} · {t.courseCode}
                </option>
              ))}
            </select>
          </label>
          <label className="flex flex-col gap-1">
            <span className="text-caption text-text-secondary">Người thực hiện</span>
            <input type="search" value={actor} maxLength={150} onChange={(e) => setActor(e.target.value)} placeholder="Tên" className={fieldClass} />
          </label>
          <label className="flex flex-col gap-1">
            <span className="text-caption text-text-secondary">Từ ngày</span>
            <input type="date" value={filter.from ?? ''} onChange={(e) => update({ from: e.target.value })} className={fieldClass} />
          </label>
          <label className="flex flex-col gap-1">
            <span className="text-caption text-text-secondary">Đến ngày</span>
            <input type="date" value={filter.to ?? ''} onChange={(e) => update({ to: e.target.value })} className={fieldClass} />
          </label>
          {hasFilters && (
            <Button variant="text" onClick={() => update({ teamId: undefined, actor: undefined, from: undefined, to: undefined })}>
              Xóa bộ lọc
            </Button>
          )}
        </div>

        {invalidRange ? (
          <p role="alert" className="text-error">
            "Đến ngày" phải sau "Từ ngày".
          </p>
        ) : (
          logs.isError && <ErrorState error={logs.error} onRetry={() => void logs.refetch()} />
        )}

        <div className="overflow-x-auto rounded-lg bg-surface shadow-elevation-1">
          {logs.isLoading ? (
            <TableSkeleton columns={4} />
          ) : !data || data.items.length === 0 ? (
            !logs.isError &&
            (hasFilters ? (
              <NoResults onClear={() => update({ teamId: undefined, from: undefined, to: undefined })} />
            ) : (
              <EmptyState icon="receipt_long" title="Chưa có nhật ký" description="Các thay đổi nhóm, thành viên, lời mời sẽ hiện ở đây." />
            ))
          ) : (
            <table className="w-full border-collapse text-body" aria-busy={logs.isFetching || undefined}>
              <thead className="bg-background">
                <tr>
                  {['Thời điểm', 'Nhóm', 'Người thực hiện', 'Thay đổi'].map((h) => (
                    <th key={h} scope="col" className="h-11 px-4 text-left text-caption font-semibold text-text-secondary">
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {data.items.map((log) => (
                  <tr key={log.id} className="h-[52px] border-b border-border last:border-b-0">
                    <td className="whitespace-nowrap px-4 tabular-nums">{formatDateTime(log.occurredAt)}</td>
                    <td className="px-4">
                      {log.teamId ? (
                        <button type="button" className="text-info-dark hover:underline" onClick={() => update({ teamId: log.teamId! })}>
                          {teamName.get(log.teamId) ?? 'Nhóm khác kỳ'}
                        </button>
                      ) : (
                        '–'
                      )}
                    </td>
                    <td className="px-4">{log.actorName}</td>
                    <td className="px-4">
                      {log.summary}
                      {log.reason && <span className="block text-caption text-text-secondary">Lý do: {log.reason}</span>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {total > 0 && (
          <nav aria-label="Phân trang" className="flex items-center justify-end gap-2 text-body text-text-secondary">
            <span className="tabular-nums">
              {from}–{to} trên {total.toLocaleString('vi-VN')}
            </span>
            <Button variant="outline" aria-label="Trang trước" disabled={filter.page <= 1} onClick={() => update({ page: String(filter.page - 1) })}>
              <Icon name="chevron_left" size={20} />
            </Button>
            <Button
              variant="outline"
              aria-label="Trang sau"
              disabled={filter.page >= (data?.totalPages ?? 1)}
              onClick={() => update({ page: String(filter.page + 1) })}
            >
              <Icon name="chevron_right" size={20} />
            </Button>
          </nav>
        )}
      </section>
    </>
  )
}
