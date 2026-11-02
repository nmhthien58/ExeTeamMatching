import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import clsx from 'clsx'
import { toApiError } from '@/shared/api/apiClient'
import { EmptyState, ErrorState, NoResults, TableSkeleton } from '@/shared/components/States'
import { Banner, Button, Icon, Skeleton, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { timeLeft, useCourseParam } from '@/features/dashboard/adminApi'
import { CourseToggle } from '@/features/dashboard/CourseToggle'
import { lecturerReasonLabel } from '@/features/my_team/advisorsApi'
import { formatDateTime } from '@/features/workspace/format'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import {
  countInterestedLecturers,
  PAGE_SIZE,
  useConfirmMatching,
  useDiscardMatching,
  useLecturerMatchingOverview,
  usePreviewMatching,
  type AdvisorMatchingOverview,
  type AdvisorMatchingRow,
  type AdvisorMatchingRun,
} from './api'
import { ChangeLecturerDialog } from './ChangeLecturerDialog'
import { EarlyMatchingDialog } from './EarlyMatchingDialog'
import { matchingErrorMessage } from './errors'
import { LoadPanel } from './LoadPanel'

const FILTER_PARAM = 'chua-co-gv'

/** Lần xem trước đã được chốt / bỏ ở nơi khác: đóng dialog, báo trên trang. */
const isStale = (error: unknown) => toApiError(error).code === 'run-not-preview'

/**
 * W19 – Ghép giảng viên hướng dẫn (FE-25, quy tắc 9, 19).
 * Trước hạn: tiến độ (nhóm đã có nguyện vọng, giảng viên đang quan tâm) và "Ghép sớm" → xem trước → chốt.
 * Sau khi ghép: bảng nhóm · giảng viên · lý do · nguyện vọng · quan tâm, lọc "Chưa có giảng viên",
 * thanh tải từng giảng viên, đổi giảng viên tay kèm lý do.
 */
export function LecturerMatchingPage() {
  const [course, setCourse] = useCourseParam()
  const [params, setParams] = useSearchParams()
  const withoutAdvisor = params.get(FILTER_PARAM) === '1'
  const search = params.get('q') ?? ''
  const page = Math.max(1, Number(params.get('page')) || 1)
  const overview = useLecturerMatchingOverview(course, { withoutAdvisor, search, page })
  const data = overview.data

  const preview = usePreviewMatching(course)
  const confirm = useConfirmMatching()
  const discard = useDiscardMatching()
  const snackbar = useSnackbar()
  const [previewRun, setPreviewRun] = useState<AdvisorMatchingRun | null>(null)
  const [editing, setEditing] = useState<AdvisorMatchingRow | null>(null)
  const [pageError, setPageError] = useState<{ title: string; detail?: string } | null>(null)

  const update = (patch: Record<string, string | undefined>, resetPage = true) => {
    const next = new URLSearchParams(params)
    for (const [k, v] of Object.entries(patch)) {
      if (!v) next.delete(k)
      else next.set(k, v)
    }
    if (resetPage) next.delete('page')
    setParams(next, { replace: true })
  }

  const roundOpen = !!data?.matchingAt
  const applied = data?.lastRun?.status === 'APPLIED'
  const beforeMatching = roundOpen && !applied
  const pendingPreview = data?.lastRun?.status === 'PREVIEW' ? data.lastRun : null
  const canMatchEarly = roundOpen && (data?.teamsWithoutAdvisor ?? 0) > 0

  const startPreview = () => {
    setPageError(null)
    preview.mutate(undefined, {
      onSuccess: (run) => setPreviewRun(run),
      onError: (error) => setPageError(matchingErrorMessage(error)),
    })
  }

  const closePreview = () => {
    setPreviewRun(null)
    confirm.reset()
    discard.reset()
  }

  const onConfirm = () => {
    if (!previewRun) return
    confirm.mutate(previewRun.id, {
      onSuccess: (run) => {
        closePreview()
        snackbar.show(`Đã ghép giảng viên cho ${run.matchedCount} nhóm`)
      },
      onError: (error) => {
        if (isStale(error)) {
          closePreview()
          setPageError(matchingErrorMessage(error))
        }
      },
    })
  }

  const onDiscard = () => {
    if (!previewRun) return
    discard.mutate(previewRun.id, {
      onSuccess: () => {
        closePreview()
        snackbar.show('Đã bỏ kết quả xem trước')
      },
      onError: (error) => {
        if (isStale(error)) {
          closePreview()
          setPageError(matchingErrorMessage(error))
        }
      },
    })
  }

  const dialogError = confirm.error ? matchingErrorMessage(confirm.error) : discard.error ? matchingErrorMessage(discard.error) : null

  return (
    <>
      <PageHeader
        title="Ghép giảng viên"
        action={
          canMatchEarly && (
            <Button icon="bolt" onClick={startPreview} loading={preview.isPending}>
              Ghép sớm
            </Button>
          )
        }
      />

      <div className="flex flex-col gap-4">
        <CourseToggle value={course} onChange={setCourse} />

        {pageError && (
          <Banner
            tone="error"
            title={pageError.title}
            action={
              <Button variant="text" aria-label="Đóng thông báo" onClick={() => setPageError(null)}>
                <Icon name="close" size={20} />
              </Button>
            }
          >
            {pageError.detail}
          </Banner>
        )}

        {pendingPreview && !previewRun && (
          <Banner
            tone="warning"
            title="Có kết quả ghép sớm chưa chốt"
            action={
              <Button variant="outline" onClick={() => setPreviewRun(pendingPreview)}>
                Xem lại
              </Button>
            }
          >
            Tạo lúc {formatDateTime(pendingPreview.createdAt)} · {pendingPreview.matchedCount} nhóm
          </Banner>
        )}

        {overview.isError && !data ? (
          <ErrorState error={overview.error} onRetry={() => overview.refetch()} />
        ) : (
          <Summary data={data} isLoading={overview.isLoading} beforeMatching={beforeMatching} filtered={withoutAdvisor || !!search} />
        )}

        <div className="flex flex-col gap-4 2xl:flex-row 2xl:items-start">
          {!(overview.isError && !data) && (
            <aside className="2xl:order-last 2xl:w-80 2xl:shrink-0">
              <LoadPanel loads={data?.advisorLoads ?? []} isLoading={overview.isLoading} />
            </aside>
          )}
          <section aria-label="Nhóm và giảng viên" className="flex min-w-0 flex-1 flex-col gap-3">
            <Toolbar
              search={search}
              withoutAdvisor={withoutAdvisor}
              onSearch={(q) => update({ q: q || undefined })}
              onToggleFilter={() => update({ [FILTER_PARAM]: withoutAdvisor ? undefined : '1' })}
            />
            {overview.isError && data && <ErrorState error={overview.error} onRetry={() => overview.refetch()} />}
            <MatchingTable
              data={data}
              isLoading={overview.isLoading}
              isError={overview.isError}
              isFetching={overview.isFetching}
              withoutAdvisor={withoutAdvisor}
              search={search}
              beforeMatching={beforeMatching}
              onClear={() => update({ q: undefined, [FILTER_PARAM]: undefined })}
              onEdit={setEditing}
            />
            {data && <Pagination data={data} page={page} onPage={(p) => update({ page: String(p) }, false)} />}
          </section>
        </div>
      </div>

      {previewRun && (
        <EarlyMatchingDialog
          run={previewRun}
          onClose={closePreview}
          onConfirm={onConfirm}
          onDiscard={onDiscard}
          confirming={confirm.isPending}
          discarding={discard.isPending}
          error={dialogError}
        />
      )}
      {editing && (
        <ChangeLecturerDialog
          key={editing.team.id}
          row={editing}
          loads={data?.advisorLoads ?? []}
          onClose={() => setEditing(null)}
          onDone={(message) => {
            setEditing(null)
            snackbar.show(message)
          }}
        />
      )}
      {snackbar.element}
    </>
  )
}

/** Trước hạn: hạn tự ghép và 3 thẻ tiến độ. Sau khi ghép: dòng tóm tắt lần ghép và số nhóm còn thiếu. */
function Summary({
  data,
  isLoading,
  beforeMatching,
  filtered,
}: {
  data?: AdvisorMatchingOverview
  isLoading: boolean
  beforeMatching: boolean
  filtered: boolean
}) {
  if (isLoading || !data) {
    return (
      <div role="status" aria-label="Đang tải" className="grid gap-3 sm:grid-cols-3">
        {Array.from({ length: 3 }, (_, i) => (
          <Skeleton key={i} className="h-24" />
        ))}
      </div>
    )
  }

  if (beforeMatching && data.matchingAt) {
    const left = timeLeft(data.matchingAt)
    const total = !filtered ? data.totalCount : undefined
    const interested = countInterestedLecturers(data.items)
    const partial = data.totalPages != null ? data.totalPages > 1 : data.items.length >= PAGE_SIZE
    return (
      <div className="flex flex-col gap-3">
        <Banner tone={left.tone} title={`Hệ thống tự ghép lúc ${formatDateTime(data.matchingAt)}`}>
          {left.text}
        </Banner>
        <div className="grid gap-3 sm:grid-cols-3">
          <StatCard
            label="Nhóm đã có nguyện vọng"
            value={total != null ? `${data.teamsWithPreferences}/${total}` : String(data.teamsWithPreferences)}
            progress={total ? data.teamsWithPreferences / total : undefined}
          />
          <StatCard label="Giảng viên đang quan tâm" value={String(interested)} hint={partial ? 'Tính trên các nhóm đang hiện' : undefined} />
          <StatCard label="Nhóm chưa có giảng viên" value={String(data.teamsWithoutAdvisor)} />
        </div>
      </div>
    )
  }

  const run = data.lastRun?.status === 'APPLIED' ? data.lastRun : null
  const when = run ? ` lúc ${formatDateTime(run.appliedAt ?? run.createdAt)}` : ''
  return data.teamsWithoutAdvisor > 0 ? (
    <Banner tone="warning" title={`${data.teamsWithoutAdvisor} nhóm chưa có giảng viên`}>
      {run ? `Đã ghép${when}. ` : ''}Lọc "Chưa có giảng viên" để gán tay.
    </Banner>
  ) : (
    <Banner tone="success" title="Mọi nhóm đã có giảng viên">
      {run ? `Đã ghép${when}.` : undefined}
    </Banner>
  )
}

function StatCard({ label, value, progress, hint }: { label: string; value: string; progress?: number; hint?: string }) {
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
  onSearch,
  onToggleFilter,
}: {
  search: string
  withoutAdvisor: boolean
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
        Chưa có giảng viên
      </button>
    </div>
  )
}

function MatchingTable({
  data,
  isLoading,
  isError,
  isFetching,
  withoutAdvisor,
  search,
  beforeMatching,
  onClear,
  onEdit,
}: {
  data?: AdvisorMatchingOverview
  isLoading: boolean
  isError: boolean
  isFetching: boolean
  withoutAdvisor: boolean
  search: string
  beforeMatching: boolean
  onClear: () => void
  onEdit: (row: AdvisorMatchingRow) => void
}) {
  const items = data?.items ?? []
  let body
  if (isLoading) body = <TableSkeleton columns={6} />
  else if (items.length === 0 && !isError) {
    if (search) body = <NoResults onClear={onClear} />
    else if (withoutAdvisor)
      body = <EmptyState icon="task_alt" title="Mọi nhóm đã có giảng viên" action={<Button variant="outline" onClick={onClear}>Xem tất cả nhóm</Button>} />
    else body = <EmptyState icon="groups" title="Chưa có nhóm nào" description="Nhóm lập trong đợt lập nhóm sẽ hiện ở đây." />
  } else if (items.length > 0) {
    body = (
      <table className="w-full border-collapse text-body" aria-busy={isFetching || undefined}>
        <thead className="sticky top-0 z-10 bg-background">
          <tr>
            {['Nhóm', 'Giảng viên', 'Lý do ghép', 'Nguyện vọng', 'Quan tâm'].map((h) => (
              <th key={h} scope="col" className="h-11 px-4 text-left text-caption font-semibold text-text-secondary">
                {h}
              </th>
            ))}
            <th scope="col" className="h-11 px-4 text-right text-caption font-semibold text-text-secondary">
              <span className="sr-only">Hành động</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {items.map((row) => (
            <Row key={row.team.id} row={row} beforeMatching={beforeMatching} onEdit={() => onEdit(row)} />
          ))}
        </tbody>
      </table>
    )
  }
  return <div className="overflow-x-auto rounded-lg bg-surface shadow-elevation-1">{body}</div>
}

function Row({ row, beforeMatching, onEdit }: { row: AdvisorMatchingRow; beforeMatching: boolean; onEdit: () => void }) {
  const interests = row.interests.filter((i) => i.status === 'OPEN' || i.advisorId === row.advisorId)
  const reason = lecturerReasonLabel(row.reason, row.preferenceRank)
  return (
    <tr className="border-b border-border align-top last:border-b-0">
      <td className="min-w-[160px] px-4 py-3">
        <p className="font-medium">{row.team.name}</p>
        {row.team.projectName && <p className="text-caption text-text-secondary">{row.team.projectName}</p>}
      </td>
      <td className="min-w-[180px] px-4 py-3">
        {row.advisorName ?? (beforeMatching ? <span className="text-text-secondary">–</span> : <StatusChip tone="warning">Chưa có giảng viên</StatusChip>)}
      </td>
      <td className="px-4 py-3">{reason ? <StatusChip tone="info">{reason}</StatusChip> : <span className="text-text-secondary">–</span>}</td>
      <td className="min-w-[180px] px-4 py-3">
        {row.preferences.length === 0 ? (
          <span className="text-text-secondary">Chưa chọn</span>
        ) : (
          <ol className="flex flex-col gap-1">
            {row.preferences.map((p, i) => (
              <li key={p.lecturerId} className={clsx(p.lecturerId === row.advisorId && 'font-semibold')}>
                <span className="tabular-nums text-text-secondary">{i + 1}.</span> {p.fullName}
                {p.lecturerId === row.advisorId && <span className="sr-only"> (đã ghép)</span>}
              </li>
            ))}
          </ol>
        )}
      </td>
      <td className="min-w-[160px] px-4 py-3">
        {interests.length === 0 ? (
          <span className="text-text-secondary">–</span>
        ) : (
          <ul className="flex flex-col gap-1">
            {interests.map((i) => (
              <li key={i.id}>
                {i.advisorName}
                {i.wasPreviousInstructor && <span className="text-caption text-text-secondary"> · đã hướng dẫn EXE101</span>}
              </li>
            ))}
          </ul>
        )}
      </td>
      <td className="px-4 py-3 text-right">
        <Button variant="text" icon={row.advisorId ? 'swap_horiz' : 'person_add'} onClick={onEdit} aria-label={`${row.advisorId ? 'Đổi' : 'Gán'} giảng viên cho ${row.team.name}`}>
          {row.advisorId ? 'Đổi' : 'Gán'}
        </Button>
      </td>
    </tr>
  )
}

/** "1–20 trên 58" khi API có `totalCount`; thiếu trường phân trang thì đoán trang sau theo số dòng. */
function Pagination({ data, page, onPage }: { data: AdvisorMatchingOverview; page: number; onPage: (p: number) => void }) {
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
