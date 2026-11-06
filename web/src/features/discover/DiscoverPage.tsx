import { useState, type CSSProperties, type ReactNode } from 'react'
import clsx from 'clsx'
import { Link, useSearchParams } from 'react-router-dom'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Button, Icon } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { Drawer } from '@/features/workspace/components'
import { useSpecializations, type TeamCard } from '@/features/suggestions/api'
import { DomainChips, TeamRequirementBar } from '@/features/suggestions/components'
import { useDomains } from '@/features/my_team/api'
import { ListSkeleton } from '@/features/my_team/components'
import { emptyFilter, filterCount, useTeamSearch, type TeamFilter } from './api'
import { SeekingPostsPanel } from './SeekingPosts'

const tabs = [
  { key: 'nhom', label: 'Nhóm đang tuyển', icon: 'groups' },
  { key: 'sinh-vien', label: 'Sinh viên tìm nhóm', icon: 'person_search' },
] as const

/**
 * Khám phá: tab "Nhóm đang tuyển" (S05) và "Sinh viên tìm nhóm" (S10). Tab đang chọn nằm trên URL (`?tab=sinh-vien`).
 * FE-52 (05/11/2026): bố cục cho màn hình rộng – tab dạng nút viên thuốc, bộ lọc nằm cố định cột trái từ 1280 px
 * (màn hẹp vẫn mở bộ lọc bằng drawer S06), banner "Gợi ý cho bạn", lưới thẻ nhóm 2–3 cột.
 */
export function DiscoverPage() {
  const [params, setParams] = useSearchParams()
  const tab = params.get('tab') === 'sinh-vien' ? 'sinh-vien' : 'nhom'
  return (
    <>
      <PageHeader title="Khám phá" />
      <div role="tablist" aria-label="Khám phá" className="mb-6 inline-flex gap-1 rounded-full bg-surface p-1 shadow-elevation-1">
        {tabs.map((t) => (
          <button
            key={t.key}
            type="button"
            role="tab"
            aria-selected={tab === t.key}
            onClick={() => setParams(t.key === 'nhom' ? {} : { tab: t.key }, { replace: true })}
            className={clsx(
              'inline-flex min-h-10 items-center gap-2 rounded-full px-5 font-medium transition-all duration-200',
              tab === t.key ? 'bg-ink text-white shadow-sm' : 'text-text-secondary hover:text-text-primary',
            )}
          >
            <Icon name={t.icon} size={20} filled={tab === t.key} />
            {t.label}
          </button>
        ))}
      </div>
      <div role="tabpanel">{tab === 'nhom' ? <TeamsPanel /> : <SeekingPostsPanel />}</div>
    </>
  )
}

/**
 * S05 – nhóm đang tuyển (FE-37, cùng spec MB-14): tìm kiếm, bộ lọc S06, lối vào "Gợi ý cho bạn" (S07),
 * thẻ nhóm "n/6 thành viên · còn k chỗ" không có chip trạng thái, phân trang.
 */
function TeamsPanel() {
  const [draft, setDraft] = useState('')
  const [search, setSearch] = useState('')
  const [filter, setFilter] = useState<TeamFilter>(emptyFilter)
  const [page, setPage] = useState(1)
  const [filtering, setFiltering] = useState(false)
  const teams = useTeamSearch(search, filter, page)
  const count = filterCount(filter)
  const applyFilter = (f: TeamFilter) => {
    setFilter(f)
    setPage(1)
  }
  const clear = () => {
    setDraft('')
    setSearch('')
    applyFilter(emptyFilter)
  }

  return (
    <div className="grid items-start gap-6 xl:grid-cols-[280px_minmax(0,1fr)]">
      {/* Màn rộng: bộ lọc nằm luôn cột trái, đổi là áp dụng ngay. */}
      <aside aria-label="Bộ lọc nhóm" className="sticky top-24 hidden max-h-[calc(100vh-7.5rem)] overflow-y-auto rounded-lg bg-surface p-5 shadow-elevation-1 xl:block">
        <div className="mb-4 flex items-center justify-between">
          <h2 className="inline-flex items-center gap-2 text-subtitle font-semibold">
            <Icon name="tune" size={20} className="text-primary" />
            Bộ lọc
          </h2>
          {count > 0 && (
            <button type="button" onClick={() => applyFilter(emptyFilter)} className="text-caption font-medium text-info-dark hover:underline">
              Xóa tất cả
            </button>
          )}
        </div>
        <FilterFields value={filter} onChange={applyFilter} compact />
      </aside>

      <div className="flex min-w-0 flex-col gap-5">
        <form
          className="flex flex-wrap gap-3"
          onSubmit={(e) => {
            e.preventDefault()
            setSearch(draft.trim())
            setPage(1)
          }}
        >
          <label className="relative min-w-0 flex-1">
            <Icon name="search" size={20} className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-text-secondary" />
            <input
              aria-label="Tìm nhóm"
              placeholder="Tìm theo tên nhóm hoặc đề tài"
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              className="h-12 w-full rounded-full border border-border bg-surface pl-11 pr-4 shadow-sm"
            />
          </label>
          <Button type="submit" icon="search">
            Tìm
          </Button>
          <span className="xl:hidden">
            <Button variant="outline" icon="tune" onClick={() => setFiltering(true)}>
              Bộ lọc{count > 0 && ` (${count})`}
            </Button>
          </span>
        </form>

        <Link
          to="/sv/kham-pha/goi-y"
          className="card-hover group relative flex items-center gap-4 overflow-hidden rounded-lg bg-ink p-5 text-white shadow-elevation-2"
        >
          <span aria-hidden className="pointer-events-none absolute -right-16 -top-20 size-56 rounded-full bg-brand-orange/30 blur-3xl" />
          <span aria-hidden className="relative grid size-12 shrink-0 place-items-center rounded-[14px] bg-gradient-to-br from-brand-orange to-primary shadow-glow">
            <Icon name="auto_awesome" filled className="text-white" />
          </span>
          <span className="relative min-w-0 flex-1">
            <span className="block text-subtitle font-semibold">Gợi ý cho bạn</span>
            <span className="block text-caption text-ink-text">Nhóm đang thiếu đúng kỹ năng và chuyên ngành của bạn, kèm lý do</span>
          </span>
          <Icon name="arrow_forward" className="relative transition-transform group-hover:translate-x-1" />
        </Link>

        {teams.error ? (
          <ErrorState error={teams.error} onRetry={() => void teams.refetch()} />
        ) : teams.isLoading ? (
          <ListSkeleton />
        ) : teams.data!.items.length === 0 ? (
          <EmptyState
            icon="search_off"
            title="Không tìm thấy nhóm"
            description="Thử từ khóa khác hoặc bỏ bớt bộ lọc."
            action={(count > 0 || search) && <Button variant="outline" onClick={clear}>Xóa bộ lọc</Button>}
          />
        ) : (
          <>
            <p className="text-caption text-text-secondary">
              {teams.data!.totalCount} nhóm đang tuyển{search && ` khớp "${search}"`}
            </p>
            <div className="grid gap-4 md:grid-cols-2 2xl:grid-cols-3">
              {teams.data!.items.map((t, i) => (
                <TeamListCard key={t.id} team={t} index={i} />
              ))}
            </div>
            {teams.data!.totalPages > 1 && (
              <nav aria-label="Phân trang" className="flex items-center justify-center gap-3">
                <Button variant="outline" icon="chevron_left" disabled={page <= 1} onClick={() => setPage(page - 1)}>
                  Trước
                </Button>
                <span className="min-w-24 text-center tabular-nums text-text-secondary">
                  Trang {page}/{teams.data!.totalPages}
                </span>
                <Button variant="outline" disabled={page >= teams.data!.totalPages} onClick={() => setPage(page + 1)}>
                  Sau
                </Button>
              </nav>
            )}
          </>
        )}
      </div>
      {filtering && (
        <FilterDrawer
          initial={filter}
          onClose={() => setFiltering(false)}
          onApply={(f) => {
            applyFilter(f)
            setFiltering(false)
          }}
        />
      )}
    </div>
  )
}

/** Thẻ nhóm: ô chữ cái đầu, tên, đề tài, domain, "n/6 thành viên · còn k chỗ" (không chip trạng thái), "Xem nhóm"; nổi lên khi rê chuột. */
export function TeamListCard({ team, index = 0 }: { team: TeamCard; index?: number }) {
  const maxSize = team.requirement?.maxSize ?? 6
  return (
    <Link
      to={`/sv/kham-pha/nhom/${team.id}`}
      className="card-hover stagger-in group flex h-full flex-col gap-3 rounded-lg bg-surface p-5 shadow-elevation-1"
      style={{ '--i': index } as CSSProperties}
    >
      <span className="flex items-start gap-3">
        <span aria-hidden className="grid size-11 shrink-0 place-items-center rounded-[12px] bg-primary-container text-title font-semibold text-primary">
          {team.name.replace(/^Nhóm\s+/i, '').charAt(0).toUpperCase()}
        </span>
        <span className="min-w-0 flex-1">
          <span className="block truncate text-title font-semibold">{team.name}</span>
          {team.projectName && <span className="block truncate text-text-secondary">{team.projectName}</span>}
        </span>
      </span>
      <DomainChips domains={team.domains} />
      <span className="mt-auto flex flex-wrap items-center justify-between gap-x-3 gap-y-1 border-t border-border/70 pt-3">
        <TeamRequirementBar memberCount={team.memberCount} maxSize={maxSize} showOpenSlots />
        <span className="inline-flex items-center gap-1 text-caption font-semibold text-info-dark">
          Xem nhóm
          <Icon name="arrow_forward" size={20} className="transition-transform group-hover:translate-x-0.5" />
        </span>
      </span>
    </Link>
  )
}

/** Các nhóm lựa chọn của S06, dùng chung cho cột lọc (màn rộng) và drawer (màn hẹp). */
function FilterFields({ value: f, onChange, compact = false }: { value: TeamFilter; onChange: (f: TeamFilter) => void; compact?: boolean }) {
  const domains = useDomains().data ?? []
  const specializations = useSpecializations().data ?? []
  const chip = (selected: boolean, label: string, onClick: () => void, key: string | number, title?: string) => (
    <button
      key={key}
      type="button"
      aria-pressed={selected}
      title={title}
      onClick={onClick}
      className={clsx(
        'rounded-full border transition-colors',
        compact ? 'min-h-8 px-3 text-caption' : 'min-h-10 px-3',
        selected ? 'border-primary bg-primary-container font-medium text-primary-dark' : 'border-border-strong hover:border-primary/60',
      )}
    >
      {selected && '✓ '}
      {label}
    </button>
  )
  const section = (title: string, children: ReactNode) => (
    <fieldset className="mb-5 last:mb-0">
      <legend className={clsx('mb-2 font-semibold', compact && 'text-caption uppercase tracking-[0.06em] text-text-secondary')}>{title}</legend>
      <div className="flex flex-wrap gap-2">{children}</div>
    </fieldset>
  )
  return (
    <>
      {section('Domain', domains.map((d) => chip(f.domain === d.code, d.name, () => onChange({ ...f, domain: f.domain === d.code ? undefined : d.code }), d.id, d.name)))}
      {section(
        'Chuyên ngành đang thiếu',
        specializations.map((s) =>
          chip(f.missingSpecializationId === s.id, s.name, () => onChange({ ...f, missingSpecializationId: f.missingSpecializationId === s.id ? undefined : s.id }), s.id),
        ),
      )}
      {section('Số chỗ trống', [1, 2, 3].map((n) => chip(f.minOpenSlots === n, `Từ ${n} chỗ`, () => onChange({ ...f, minOpenSlots: f.minOpenSlots === n ? undefined : n }), n)))}
      {section(
        'Sắp xếp',
        (
          [
            ['-createdAt', 'Mới nhất'],
            ['-openSlots', 'Nhiều chỗ trống'],
            ['name', 'Tên A–Z'],
          ] as const
        ).map(([value, label]) => chip(f.sort === value, label, () => onChange({ ...f, sort: value }), value)),
      )}
    </>
  )
}

/** S06 – Bộ lọc và sắp xếp (drawer, màn hẹp): chọn rồi bấm "Áp dụng (n)"; "Xóa tất cả" bỏ mọi lựa chọn. */
function FilterDrawer({ initial, onClose, onApply }: { initial: TeamFilter; onClose: () => void; onApply: (f: TeamFilter) => void }) {
  const [f, setF] = useState(initial)
  return (
    <Drawer
      open
      onClose={onClose}
      title="Bộ lọc"
      footer={
        <>
          <Button variant="text" onClick={() => setF(emptyFilter)}>
            Xóa tất cả
          </Button>
          <Button onClick={() => onApply(f)}>Áp dụng ({filterCount(f)})</Button>
        </>
      }
    >
      <FilterFields value={f} onChange={setF} />
    </Drawer>
  )
}
