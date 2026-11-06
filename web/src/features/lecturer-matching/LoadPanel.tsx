import clsx from 'clsx'
import { Skeleton, StatusChip } from '@/shared/components/ui'
import type { AdvisorLoad } from './api'

/**
 * Thanh tải từng cố vấn so với số nhóm tối đa (W19, W28); quá tải đứng đầu, rồi gần đầy. `showPreferred` (mentor, rule 20):
 * vạch dọc trên thanh là số mong muốn, vượt mong muốn có chip cảnh báo.
 */
export function LoadPanel({
  loads,
  isLoading,
  title = 'Số nhóm của giảng viên',
  subtitle,
  emptyText = 'Chưa có giảng viên nào dạy môn này.',
  showPreferred = false,
}: {
  loads: AdvisorLoad[]
  isLoading: boolean
  title?: string
  subtitle?: string
  emptyText?: string
  showPreferred?: boolean
}) {
  const ratio = (l: AdvisorLoad) => (l.maxTeams ? l.assignedCount / l.maxTeams : 0)
  const sorted = [...loads].sort((a, b) => ratio(b) - ratio(a) || b.assignedCount - a.assignedCount || a.fullName.localeCompare(b.fullName, 'vi'))

  return (
    <section aria-labelledby="load-title" className="rounded-lg bg-surface p-4 shadow-elevation-1">
      <h2 id="load-title" className="text-subtitle font-semibold">
        {title}
      </h2>
      {subtitle && <p className="mt-1 text-caption text-text-secondary">{subtitle}</p>}
      {isLoading ? (
        <div role="status" aria-label="Đang tải" className="mt-3 grid gap-4 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-1">
          {Array.from({ length: 5 }, (_, i) => (
            <Skeleton key={i} className="h-8 w-full" />
          ))}
        </div>
      ) : sorted.length === 0 ? (
        <p className="mt-3 text-body text-text-secondary">{emptyText}</p>
      ) : (
        <ul className="mt-3 grid max-h-[220px] gap-x-6 gap-y-3 overflow-y-auto sm:grid-cols-2 lg:grid-cols-3 2xl:max-h-[640px] 2xl:grid-cols-1">
          {sorted.map((l) => {
            const over = l.maxTeams != null && l.assignedCount > l.maxTeams
            const full = l.maxTeams != null && l.assignedCount === l.maxTeams
            const preferred = showPreferred ? l.preferredTeams : null
            const abovePreferred = !full && !over && !!preferred && l.assignedCount > preferred
            const preferredAt = l.maxTeams && preferred ? Math.min(100, Math.round((preferred / l.maxTeams) * 100)) : null
            const percent = l.maxTeams ? Math.min(100, Math.round((l.assignedCount / l.maxTeams) * 100)) : 0
            return (
              <li key={l.advisorId} className="flex flex-col gap-1">
                <div className="flex items-center justify-between gap-2">
                  <span className="min-w-0 truncate text-body font-medium" title={l.fullName}>
                    {l.fullName}
                  </span>
                  <span className="shrink-0 text-body tabular-nums text-text-secondary">
                    {l.maxTeams != null ? `${l.assignedCount}/${l.maxTeams} nhóm` : `${l.assignedCount} nhóm`}
                  </span>
                </div>
                {l.maxTeams != null ? (
                  <div
                    role="meter"
                    aria-label={`Số nhóm của ${l.fullName}`}
                    aria-valuemin={0}
                    aria-valuemax={l.maxTeams}
                    aria-valuenow={l.assignedCount}
                    className="relative h-2 overflow-hidden rounded-full bg-neutral-container"
                  >
                    <div className={clsx('h-full rounded-full', over ? 'bg-error' : abovePreferred ? 'bg-warning' : 'bg-primary')} style={{ width: `${percent}%` }} />
                    {preferredAt != null && preferredAt < 100 && (
                      <span aria-hidden className="absolute inset-y-0 w-0.5 bg-text-secondary" style={{ left: `${preferredAt}%` }} />
                    )}
                  </div>
                ) : (
                  <span className="text-caption text-text-secondary">Không giới hạn</span>
                )}
                {showPreferred && (
                  <span className="text-caption text-text-secondary">{preferred ? `Mong muốn ${preferred} nhóm` : 'Chưa đặt số mong muốn'}</span>
                )}
                {over && (
                  <span>
                    <StatusChip tone="error" icon="error">
                      Quá tải
                    </StatusChip>
                  </span>
                )}
                {full && <span className="text-caption text-text-secondary">Đã đủ nhóm</span>}
                {abovePreferred && (
                  <span>
                    <StatusChip tone="warning" icon="warning">
                      Vượt số mong muốn
                    </StatusChip>
                  </span>
                )}
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}
