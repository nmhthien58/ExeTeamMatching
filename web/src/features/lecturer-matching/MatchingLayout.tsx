import { useEffect, useState, type ComponentProps, type ReactNode } from 'react'
import clsx from 'clsx'
import { EmptyState, ErrorState, NoResults, TableSkeleton } from '@/shared/components/States'
import { Banner, Button, Icon, Skeleton, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import type { CourseCode } from '@/shared/phase/usePhase'
import { CourseToggle } from '@/features/dashboard/CourseToggle'
import { formatDateTime } from '@/features/workspace/format'
import { PAGE_SIZE, type AdvisorMatchingOverview, type AdvisorMatchingRun } from './api'
import { EarlyMatchingDialog } from './EarlyMatchingDialog'
import type { EarlyMatching, MatchingParams } from './useMatchingPage'

type OverviewData = Pick<AdvisorMatchingOverview, 'items' | 'totalCount' | 'totalPages' | 'teamsWithoutAdvisor' | 'lastRun'>

export interface OverviewState<T extends OverviewData> {
  data?: T
  isLoading: boolean
  isError: boolean
  isFetching: boolean
  error: unknown
  refetch: () => unknown
}

/**
 * Khung chung của W19, W28: tiêu đề, "Ghép sớm", chọn môn, thông báo lỗi, banner xem trước chưa chốt, tóm tắt, thanh tải,
 * bảng nhóm có tìm / lọc / phân trang. Phần khác nhau (tóm tắt, bảng, dialog) do từng trang truyền vào.
 */
export function MatchingLayout<T extends OverviewData>({
  title,
  advisorNoun,
  reasonLabel,
  tableLabel,
  course,
  onCourse,
  early,
  overview,
  params,
  summary,
  loadPanel,
  table,
  dialogs,
}: {
  title: string
  advisorNoun: string
  /** Nhãn lý do trong dialog xem trước; bỏ trống là nhãn của giảng viên. */
  reasonLabel?: ComponentProps<typeof EarlyMatchingDialog>['reasonLabel']
  tableLabel: string
  course: CourseCode
  onCourse: (course: CourseCode) => void
  early: EarlyMatching
  overview: OverviewState<T>
  params: MatchingParams
  summary: ReactNode
  loadPanel: ReactNode
  table: ReactNode
  dialogs: ReactNode
}) {
  const { data } = overview
  const failed = overview.isError && !data
  const pendingPreview = data?.lastRun?.status === 'PREVIEW' ? data.lastRun : null
  // Bộ môn ghép được mọi lúc khi còn nhóm chưa có cố vấn (rule 9, 20).
  const canMatchEarly = (data?.teamsWithoutAdvisor ?? 0) > 0

  return (
    <>
      <PageHeader
        title={title}
        action={
          canMatchEarly && (
            <Button icon="bolt" onClick={early.startPreview} loading={early.previewing}>
              Ghép sớm
            </Button>
          )
        }
      />

      <div className="flex flex-col gap-4">
        <CourseToggle value={course} onChange={onCourse} />

        {early.pageError && (
          <Banner
            tone="error"
            title={early.pageError.title}
            action={
              <Button variant="text" aria-label="Đóng thông báo" onClick={() => early.setPageError(null)}>
                <Icon name="close" size={20} />
              </Button>
            }
          >
            {early.pageError.detail}
          </Banner>
        )}

        {pendingPreview && !early.previewRun && <PendingPreview run={pendingPreview} onOpen={() => early.setPreviewRun(pendingPreview)} />}

        {failed ? <ErrorState error={overview.error} onRetry={() => overview.refetch()} /> : summary}

        <div className="flex flex-col gap-4 2xl:flex-row 2xl:items-start">
          {!failed && <aside className="2xl:order-last 2xl:w-80 2xl:shrink-0">{loadPanel}</aside>}
          <section aria-label={tableLabel} className="flex min-w-0 flex-1 flex-col gap-3">
            <Toolbar
              search={params.search}
              withoutAdvisor={params.withoutAdvisor}
              filterLabel={`Chưa có ${advisorNoun}`}
              onSearch={(q) => params.update({ q: q || undefined })}
              onToggleFilter={() => params.update({ [params.filterParam]: params.withoutAdvisor ? undefined : '1' })}
            />
            {overview.isError && data && <ErrorState error={overview.error} onRetry={() => overview.refetch()} />}
            {table}
            {data && <Pagination data={data} page={params.page} onPage={(p) => params.update({ page: String(p) }, false)} />}
          </section>
        </div>
      </div>

      {early.previewRun && (
        <EarlyMatchingDialog
          run={early.previewRun}
          onClose={early.closePreview}
          onConfirm={early.onConfirm}
          onDiscard={early.onDiscard}
          confirming={early.confirming}
          discarding={early.discarding}
          error={early.dialogError}
          advisorNoun={advisorNoun}
          reasonLabel={reasonLabel}
        />
      )}
      {dialogs}
      {early.snackbar.element}
    </>
  )
}

function PendingPreview({ run, onOpen }: { run: AdvisorMatchingRun; onOpen: () => void }) {
  return (
    <Banner
      tone="warning"
      title="Có kết quả ghép sớm chưa chốt"
      action={
        <Button variant="outline" onClick={onOpen}>
          Xem lại
        </Button>
      }
    >
      Tạo lúc {formatDateTime(run.createdAt)} · {run.matchedCount} nhóm
    </Banner>
  )
}

export function SummarySkeleton() {
  return (
    <div role="status" aria-label="Đang tải" className="grid gap-3 sm:grid-cols-3">
      {Array.from({ length: 3 }, (_, i) => (
        <Skeleton key={i} className="h-24" />
      ))}
    </div>
  )
}

/** Sau khi ghép: dòng tóm tắt lần ghép và số nhóm còn thiếu. */
export function MatchedSummary({ data, advisorNoun }: { data: OverviewData; advisorNoun: string }) {
  const run = data.lastRun?.status === 'APPLIED' ? data.lastRun : null
  const when = run ? ` lúc ${formatDateTime(run.appliedAt ?? run.createdAt)}` : ''
  return data.teamsWithoutAdvisor > 0 ? (
    <Banner tone="warning" title={`${data.teamsWithoutAdvisor} nhóm chưa có ${advisorNoun}`}>
      {run ? `Đã ghép${when}. ` : ''}Lọc "Chưa có {advisorNoun}" để gán tay.
    </Banner>
  ) : (
    <Banner tone="success" title={`Mọi nhóm đã có ${advisorNoun}`}>
      {run ? `Đã ghép${when}.` : undefined}
    </Banner>
  )
}

export function StatCard({ label, value, progress, hint }: { label: string; value: string; progress?: number; hint?: string }) {
  return (
    <div className="flex flex-col gap-1 rounded-lg bg-surface p-4 shadow-elevation-1">
      <span className="text-caption text-text-secondary">{label}</span>
      <span className="text-display font-semibold tabular-nums">{value}</span>
      {progress != null && (
        <div role="meter" aria-label={label} aria-valuemin={0} aria-valuemax={100} aria-valuenow={Math.round(progress * 100)} className="h-2 overflow-hidden rounded-full bg-neutral-container">
          <div className="h-full rounded-full bg-primary" style={{ width: `${Math.min(100, Math.round(progress * 100))}%` }} />
        </div>
      )}
      {hint && <span className="text-caption text-text-secondary">{hint}</span>}
    </div>
  )
}

function Toolbar({
  search,
  withoutAdvisor,
  filterLabel,
  onSearch,
  onToggleFilter,
}: {
  search: string
  withoutAdvisor: boolean
  filterLabel: string
  onSearch: (q: string) => void
  onToggleFilter: () => void
}) {
  const [text, setText] = useState(search)
  useEffect(() => setText(search), [search])
  // Gõ xong 300 ms mới gọi API.
  useEffect(() => {
    if (text.trim() === search) return
    const t = setTimeout(() => onSearch(text.trim()), 300)
    return () => clearTimeout(t)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [text])

  return (
    <div className="flex flex-wrap items-center gap-2">
      <label className="relative w-full max-w-[320px]">
        <span className="sr-only">Tìm nhóm</span>
        <Icon name="search" size={20} className="pointer-events-none absolute left-3 top-2.5 text-text-secondary" />
        <input
          type="search"
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder="Tên nhóm"
          className="h-10 w-full rounded-sm border border-border-strong bg-surface pl-10 pr-3 text-body placeholder:text-text-secondary"
        />
      </label>
      <button
        type="button"
        aria-pressed={withoutAdvisor}
        onClick={onToggleFilter}
        className={clsx(
          'inline-flex min-h-10 items-center gap-1 rounded-full px-4 font-medium',
          withoutAdvisor ? 'bg-primary text-white' : 'border border-border-strong bg-surface hover:bg-background',
        )}
      >
        {withoutAdvisor && <Icon name="check" size={20} />}
        {filterLabel}
      </button>
    </div>
  )
}

/** Bảng nhóm của W19, W28: đang tải, không tìm thấy, trống, rồi tới các dòng do trang truyền vào. */
export function MatchingTable<T extends OverviewData>({
  overview,
  params,
  columns,
  advisorNoun,
  emptyDescription,
  renderRow,
}: {
  overview: OverviewState<T>
  params: MatchingParams
  columns: string[]
  advisorNoun: string
  emptyDescription: string
  renderRow: (row: T['items'][number]) => ReactNode
}) {
  const items = overview.data?.items ?? []
  const clear = () => params.update({ q: undefined, [params.filterParam]: undefined })
  let body
  if (overview.isLoading) body = <TableSkeleton columns={columns.length + 1} />
  else if (items.length === 0 && !overview.isError) {
    if (params.search) body = <NoResults onClear={clear} />
    else if (params.withoutAdvisor)
      body = <EmptyState icon="task_alt" title={`Mọi nhóm đã có ${advisorNoun}`} action={<Button variant="outline" onClick={clear}>Xem tất cả nhóm</Button>} />
    else body = <EmptyState icon="groups" title="Chưa có nhóm nào" description={emptyDescription} />
  } else if (items.length > 0) {
    body = (
      <table className="w-full border-collapse text-body" aria-busy={overview.isFetching || undefined}>
        <thead className="sticky top-0 z-10 bg-background">
          <tr>
            {columns.map((h) => (
              <th key={h} scope="col" className="h-11 px-4 text-left text-caption font-semibold text-text-secondary">
                {h}
              </th>
            ))}
            <th scope="col" className="h-11 px-4 text-right text-caption font-semibold text-text-secondary">
              <span className="sr-only">Hành động</span>
            </th>
          </tr>
        </thead>
        <tbody>{items.map(renderRow)}</tbody>
      </table>
    )
  }
  return <div className="overflow-x-auto rounded-lg bg-surface shadow-elevation-1">{body}</div>
}

/** Ô cố vấn, lý do và nút đổi / gán của một dòng trong bảng W19, W28. */
export function AdvisorCell({ name, noun, beforeMatching, className }: { name: string | null; noun: string; beforeMatching: boolean; className: string }) {
  return (
    <td className={className}>
      {name ?? (beforeMatching ? <span className="text-text-secondary">–</span> : <StatusChip tone="warning">Chưa có {noun}</StatusChip>)}
    </td>
  )
}

export function ReasonCell({ reason }: { reason?: string }) {
  return <td className="px-4 py-3">{reason ? <StatusChip tone="info">{reason}</StatusChip> : <span className="text-text-secondary">–</span>}</td>
}

export function EditCell({ assigned, noun, teamName, onEdit }: { assigned: boolean; noun: string; teamName: string; onEdit: () => void }) {
  const verb = assigned ? 'Đổi' : 'Gán'
  return (
    <td className="px-4 py-3 text-right">
      <Button variant="text" icon={assigned ? 'swap_horiz' : 'person_add'} onClick={onEdit} aria-label={`${verb} ${noun} cho ${teamName}`}>
        {verb}
      </Button>
    </td>
  )
}

export function InterestsCell({ empty, children }: { empty: boolean; children: ReactNode }) {
  return (
    <td className="min-w-[160px] px-4 py-3">
      {empty ? <span className="text-text-secondary">–</span> : <ul className="flex flex-col gap-1">{children}</ul>}
    </td>
  )
}

/** "1–20 trên 58" khi API có `totalCount`; thiếu trường phân trang thì đoán trang sau theo số dòng. */
function Pagination({ data, page, onPage }: { data: OverviewData; page: number; onPage: (p: number) => void }) {
  const total = data.totalCount
  const hasNext = data.totalPages != null ? page < data.totalPages : data.items.length >= PAGE_SIZE
  if (page <= 1 && !hasNext) return null
  const from = (page - 1) * PAGE_SIZE + 1
  const to = from + data.items.length - 1
  return (
    <nav aria-label="Phân trang" className="flex items-center justify-end gap-2 text-body text-text-secondary">
      {data.items.length > 0 && (
        <span className="tabular-nums">
          {from}–{to}
          {total != null && ` trên ${total.toLocaleString('vi-VN')}`}
        </span>
      )}
      <Button variant="outline" aria-label="Trang trước" disabled={page <= 1} onClick={() => onPage(page - 1)}>
        <Icon name="chevron_left" size={20} />
      </Button>
      <Button variant="outline" aria-label="Trang sau" disabled={!hasNext} onClick={() => onPage(page + 1)}>
        <Icon name="chevron_right" size={20} />
      </Button>
    </nav>
  )
}
