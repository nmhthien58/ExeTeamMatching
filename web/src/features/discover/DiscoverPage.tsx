import { useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Button, Icon } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { Card, Drawer } from '@/features/workspace/components'
import { useSpecializations, type TeamCard } from '@/features/suggestions/api'
import { DomainChips, TeamRequirementBar } from '@/features/suggestions/components'
import { useDomains } from '@/features/my_team/api'
import { ListSkeleton } from '@/features/my_team/components'
import { emptyFilter, filterCount, useTeamSearch, type TeamFilter } from './api'

/**
 * S05 – Khám phá, nhóm đang tuyển (FE-37, cùng spec MB-14): tìm kiếm, bộ lọc S06 (drawer), lối vào "Gợi ý cho bạn" (S07),
 * thẻ nhóm "n/6 thành viên · còn k chỗ" không có chip trạng thái, phân trang. Tab "Sinh viên tìm nhóm" (S10) ở MB-17.
 */
export function DiscoverPage() {
  const [draft, setDraft] = useState('')
  const [search, setSearch] = useState('')
  const [filter, setFilter] = useState<TeamFilter>(emptyFilter)
  const [page, setPage] = useState(1)
  const [filtering, setFiltering] = useState(false)
  const teams = useTeamSearch(search, filter, page)
  const count = filterCount(filter)
  const clear = () => {
    setDraft('')
    setSearch('')
    setFilter(emptyFilter)
    setPage(1)
  }

  return (
    <>
      <PageHeader title="Khám phá" />
      <div className="flex flex-col gap-4">
        <form
          className="flex flex-wrap gap-2"
          onSubmit={(e) => {
            e.preventDefault()
            setSearch(draft.trim())
            setPage(1)
          }}
        >
          <input
            aria-label="Tìm nhóm"
            placeholder="Tên nhóm hoặc đề tài"
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            className="min-h-10 min-w-0 flex-1 rounded-sm border border-border-strong px-3"
          />
          <Button variant="outline" type="submit" icon="search">
            Tìm
          </Button>
          <Button variant="outline" icon="tune" onClick={() => setFiltering(true)}>
            Bộ lọc{count > 0 && ` (${count})`}
          </Button>
        </form>
        <Card className="p-0">
          <Link to="/sv/kham-pha/goi-y" className="flex min-h-12 items-center gap-3 px-4 hover:bg-background">
            <Icon name="auto_awesome" />
            <span className="flex-1">Gợi ý cho bạn</span>
            <Icon name="chevron_right" />
          </Link>
        </Card>
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
            <div className="grid gap-3 md:grid-cols-2">
              {teams.data!.items.map((t) => (
                <TeamListCard key={t.id} team={t} />
              ))}
            </div>
            {teams.data!.totalPages > 1 && (
              <nav aria-label="Phân trang" className="flex items-center justify-center gap-3">
                <Button variant="text" disabled={page <= 1} onClick={() => setPage(page - 1)}>
                  Trước
                </Button>
                <span>
                  Trang {page}/{teams.data!.totalPages}
                </span>
                <Button variant="text" disabled={page >= teams.data!.totalPages} onClick={() => setPage(page + 1)}>
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
            setFilter(f)
            setPage(1)
            setFiltering(false)
          }}
        />
      )}
    </>
  )
}

export function TeamListCard({ team }: { team: TeamCard }) {
  const maxSize = team.requirement?.maxSize ?? 6
  return (
    <Link to={`/sv/kham-pha/nhom/${team.id}`} className="block rounded-lg bg-surface p-4 shadow-elevation-1 hover:shadow-elevation-2">
      <span className="block text-title font-semibold">{team.name}</span>
      {team.projectName && <span className="block truncate text-text-secondary">{team.projectName}</span>}
      <span className="mt-2 block">
        <DomainChips domains={team.domains} />
      </span>
      <span className="mt-2 block">
        <TeamRequirementBar memberCount={team.memberCount} maxSize={maxSize} showOpenSlots />
      </span>
    </Link>
  )
}

/** S06 – Bộ lọc và sắp xếp: domain, chuyên ngành đang thiếu, số chỗ trống, sắp xếp; "Xóa tất cả" / "Áp dụng (n)". */
function FilterDrawer({ initial, onClose, onApply }: { initial: TeamFilter; onClose: () => void; onApply: (f: TeamFilter) => void }) {
  const [f, setF] = useState(initial)
  const domains = useDomains().data ?? []
  const specializations = useSpecializations().data ?? []
  const chip = (selected: boolean, label: string, onClick: () => void, key: string | number) => (
    <button
      key={key}
      type="button"
      aria-pressed={selected}
      onClick={onClick}
      className={`min-h-10 rounded-full border px-3 ${selected ? 'border-primary bg-primary-container text-primary-dark' : 'border-border-strong'}`}
    >
      {selected && '✓ '}
      {label}
    </button>
  )
  const section = (title: string, children: ReactNode) => (
    <fieldset className="mb-4">
      <legend className="mb-2 font-semibold">{title}</legend>
      <div className="flex flex-wrap gap-2">{children}</div>
    </fieldset>
  )
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
      {section('Domain', domains.map((d) => chip(f.domain === d.code, d.code, () => setF({ ...f, domain: f.domain === d.code ? undefined : d.code }), d.id)))}
      {section(
        'Chuyên ngành đang thiếu',
        specializations.map((s) =>
          chip(f.missingSpecializationId === s.id, s.name, () => setF({ ...f, missingSpecializationId: f.missingSpecializationId === s.id ? undefined : s.id }), s.id),
        ),
      )}
      {section('Số chỗ trống', [1, 2, 3].map((n) => chip(f.minOpenSlots === n, `Từ ${n} chỗ`, () => setF({ ...f, minOpenSlots: f.minOpenSlots === n ? undefined : n }), n)))}
      {section(
        'Sắp xếp',
        (
          [
            ['-createdAt', 'Mới nhất'],
            ['-openSlots', 'Nhiều chỗ trống'],
            ['name', 'Tên A–Z'],
          ] as const
        ).map(([value, label]) => chip(f.sort === value, label, () => setF({ ...f, sort: value }), value)),
      )}
    </Drawer>
  )
}
