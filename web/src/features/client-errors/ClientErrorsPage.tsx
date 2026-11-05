import { useEffect, useState, type CSSProperties } from 'react'
import { useSearchParams } from 'react-router-dom'
import { SectionTitle, StatCard } from '@/features/dashboard/DashboardPage'
import { formatDateTime } from '@/features/workspace/format'
import { EmptyState, ErrorState, NoResults, TableSkeleton } from '@/shared/components/States'
import { Button, Icon, Skeleton } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { CRASHLYTICS_URL, useClientErrors, useClientErrorSummary, type ClientError, type ClientErrorSummary, type TopError } from './api'
import { ClientErrorDrawer } from './ClientErrorDrawer'
import { DailyErrorChart } from './DailyErrorChart'
import { KindChip } from './KindChip'

const fieldClass = 'h-10 rounded-sm border border-border-strong bg-surface px-3 text-body'

/**
 * W30 – Theo dõi lỗi ứng dụng (FE-50): số liệu 7 ngày, cột theo ngày, lỗi lặp lại nhiều nhất, bảng có lọc, ngăn chi tiết
 * hiện stack trace. Dữ liệu là bản app gửi thêm về backend (BE-87, MB-61); bản chính ở Crashlytics.
 * Không dùng DataTable vì API không có tìm kiếm. Bộ lọc nằm trên URL để F5 / chia sẻ giữ nguyên.
 */
export function ClientErrorsPage() {
  const summary = useClientErrorSummary()

  return (
    <>
      <PageHeader
        title="Theo dõi lỗi ứng dụng"
        action={
          <a
            href={CRASHLYTICS_URL}
            target="_blank"
            rel="noreferrer"
            className="inline-flex min-h-10 items-center gap-2 rounded-md bg-primary px-4 text-body font-medium text-white"
          >
            <Icon name="open_in_new" size={20} />
            Mở Crashlytics
          </a>
        }
      />
      <div className="flex flex-col gap-6">
        {summary.isError ? (
          <ErrorState error={summary.error} onRetry={() => void summary.refetch()} />
        ) : !summary.data ? (
          <SummarySkeleton />
        ) : (
          <Summary summary={summary.data} />
        )}
        <ErrorTable />
      </div>
    </>
  )
}

function Summary({ summary }: { summary: ClientErrorSummary }) {
  const hasErrors = summary.fatalCount + summary.nonFatalCount > 0
  return (
    <>
      <div className="grid gap-4 sm:grid-cols-3">
        <StatCard index={0} icon="dangerous" tone="primary" label="Fatal" value={summary.fatalCount} hint="7 ngày gần nhất" />
        <StatCard index={1} icon="report" tone="info" label="Non-fatal" value={summary.nonFatalCount} hint="7 ngày gần nhất" />
        <StatCard index={2} icon="group" tone="warning" label="Người dùng bị ảnh hưởng" value={summary.affectedUsers} hint="Không tính lỗi trước khi đăng nhập" />
      </div>
      <div className="grid gap-4 lg:grid-cols-2">
        <section className="stagger-in rounded-lg bg-surface p-6 shadow-elevation-1" style={{ '--i': 3 } as CSSProperties}>
          <SectionTitle icon="bar_chart" title="Lỗi theo ngày" hint="Tính theo giờ Việt Nam" />
          <DailyErrorChart days={summary.daily} />
        </section>
        <section className="stagger-in rounded-lg bg-surface p-6 shadow-elevation-1" style={{ '--i': 4 } as CSSProperties}>
          <SectionTitle icon="repeat" title="Lặp lại nhiều nhất" hint="Cùng loại lỗi và dòng đầu của stack" />
          {hasErrors ? <TopErrors errors={summary.topErrors} /> : <p className="text-text-secondary">Chưa có lỗi nào trong 7 ngày.</p>}
        </section>
      </div>
    </>
  )
}

function TopErrors({ errors }: { errors: TopError[] }) {
  return (
    <ol className="flex flex-col">
      {errors.map((error) => (
        <li key={`${error.exceptionType}|${error.firstStackLine}`} className="flex items-center gap-3 border-b border-border py-2 last:border-b-0">
          <div className="min-w-0 flex-1">
            <p className="truncate font-medium">{error.exceptionType}</p>
            <p className="truncate font-mono text-caption text-text-secondary">{error.firstStackLine || 'Không có stack trace'}</p>
          </div>
          <div className="text-right">
            <p className="font-semibold tabular-nums">{error.count.toLocaleString('vi-VN')} lần</p>
            <p className="whitespace-nowrap text-caption text-text-secondary">{formatDateTime(error.lastOccurredAt)}</p>
          </div>
        </li>
      ))}
    </ol>
  )
}

function SummarySkeleton() {
  return (
    <div role="status" aria-label="Đang tải số liệu" className="flex flex-col gap-4">
      <div className="grid gap-4 sm:grid-cols-3">
        {Array.from({ length: 3 }, (_, i) => (
          <Skeleton key={i} className="h-36 rounded-lg" />
        ))}
      </div>
      <Skeleton className="h-64 rounded-lg" />
    </div>
  )
}

function ErrorTable() {
  const [params, setParams] = useSearchParams()
  const filter = {
    kind: params.get('kind') ?? undefined,
    from: params.get('from') ?? undefined,
    to: params.get('to') ?? undefined,
    appVersion: params.get('version') ?? undefined,
    page: Math.max(1, Number(params.get('page')) || 1),
  }
  const invalidRange = !!(filter.from && filter.to && filter.from > filter.to)
  const errors = useClientErrors(filter)
  const hasFilters = !!(filter.kind || filter.from || filter.to || filter.appVersion)
  const [selected, setSelected] = useState<ClientError>()

  const update = (patch: Record<string, string | undefined>) => {
    const next = new URLSearchParams(params)
    for (const [k, v] of Object.entries(patch)) {
      if (v) next.set(k, v)
      else next.delete(k)
    }
    if (!('page' in patch)) next.delete('page')
    setParams(next, { replace: true })
  }
  const clearFilters = () => update({ kind: undefined, from: undefined, to: undefined, version: undefined })

  // Gõ phiên bản thì đợi 300 ms mới lọc, tránh gọi API mỗi phím.
  const [version, setVersion] = useState(filter.appVersion ?? '')
  useEffect(() => setVersion(filter.appVersion ?? ''), [filter.appVersion])
  useEffect(() => {
    const value = version.trim() || undefined
    if (value === filter.appVersion) return
    const timer = setTimeout(() => update({ version: value }), 300)
    return () => clearTimeout(timer)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [version])

  const data = errors.data
  const total = data?.totalCount ?? 0
  const pageSize = data?.pageSize ?? 20
  const first = total === 0 ? 0 : (filter.page - 1) * pageSize + 1
  const last = Math.min(filter.page * pageSize, total)

  return (
    <section aria-label="Danh sách lỗi" className="flex flex-col gap-3">
      <div className="flex flex-wrap items-end gap-3">
        <label className="flex flex-col gap-1">
          <span className="text-caption text-text-secondary">Loại</span>
          <select value={filter.kind ?? ''} onChange={(e) => update({ kind: e.target.value })} className={fieldClass}>
            <option value="">Tất cả</option>
            <option value="fatal">Fatal</option>
            <option value="non_fatal">Non-fatal</option>
          </select>
        </label>
        <label className="flex flex-col gap-1">
          <span className="text-caption text-text-secondary">Từ ngày</span>
          <input type="date" value={filter.from ?? ''} onChange={(e) => update({ from: e.target.value })} className={fieldClass} />
        </label>
        <label className="flex flex-col gap-1">
          <span className="text-caption text-text-secondary">Đến ngày</span>
          <input type="date" value={filter.to ?? ''} onChange={(e) => update({ to: e.target.value })} className={fieldClass} />
        </label>
        <label className="flex flex-col gap-1">
          <span className="text-caption text-text-secondary">Phiên bản app</span>
          <input type="search" value={version} maxLength={30} onChange={(e) => setVersion(e.target.value)} placeholder="1.0.0+1" className={fieldClass} />
        </label>
        {hasFilters && (
          <Button variant="text" onClick={clearFilters}>
            Xóa bộ lọc
          </Button>
        )}
      </div>

      {invalidRange ? (
        <p role="alert" className="text-error">
          "Đến ngày" phải sau "Từ ngày".
        </p>
      ) : (
        errors.isError && <ErrorState error={errors.error} onRetry={() => void errors.refetch()} />
      )}

      <div className="overflow-x-auto rounded-lg bg-surface shadow-elevation-1">
        {errors.isLoading ? (
          <TableSkeleton columns={5} />
        ) : !data || data.items.length === 0 ? (
          !errors.isError &&
          !invalidRange &&
          (hasFilters ? (
            <NoResults onClear={clearFilters} />
          ) : (
            <EmptyState icon="verified" title="Chưa có lỗi nào" description="Lỗi từ app sẽ hiện ở đây. Crash được gửi khi người dùng mở lại app." />
          ))
        ) : (
          <table className="w-full border-collapse text-body" aria-busy={errors.isFetching || undefined}>
            <thead className="bg-background">
              <tr>
                {['Thời điểm', 'Loại', 'Lỗi', 'Màn hình', 'Phiên bản'].map((h) => (
                  <th key={h} scope="col" className="h-11 px-4 text-left text-caption font-semibold text-text-secondary">
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {data.items.map((error) => (
                <tr key={error.id} className="h-[52px] border-b border-border last:border-b-0">
                  <td className="whitespace-nowrap px-4 tabular-nums">{formatDateTime(error.occurredAt)}</td>
                  <td className="px-4">
                    <KindChip kind={error.kind} />
                  </td>
                  <td className="max-w-[360px] px-4 py-2">
                    <button type="button" className="block max-w-full text-left text-info-dark hover:underline" onClick={() => setSelected(error)}>
                      <span className="block truncate font-medium">{error.exceptionType}</span>
                      <span className="block truncate text-caption text-text-secondary">{error.message}</span>
                    </button>
                  </td>
                  <td className="max-w-[200px] truncate px-4">{error.screen ?? '–'}</td>
                  <td className="whitespace-nowrap px-4 tabular-nums">{error.appVersion}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {total > 0 && (
        <nav aria-label="Phân trang" className="flex items-center justify-end gap-2 text-body text-text-secondary">
          <span className="tabular-nums">
            {first}–{last} trên {total.toLocaleString('vi-VN')}
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

      {selected && <ClientErrorDrawer error={selected} onClose={() => setSelected(undefined)} />}
    </section>
  )
}
