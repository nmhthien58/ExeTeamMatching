import clsx from 'clsx'
import { Skeleton, StatusChip } from '@/shared/components/ui'
import type { MentorLoad } from './api'

/**
 * Thanh tải từng mentor (W28, rule 20): số nhóm đang mentor (chung hai môn trong học kỳ) so với số mong muốn và số tối đa.
 * Vạch dọc trên thanh là số mong muốn; vượt mong muốn có chip cảnh báo, tới tối đa thì "Đã đủ nhóm". Gần đầy đứng đầu.
 */
export function MentorLoadPanel({ loads, isLoading }: { loads: MentorLoad[]; isLoading: boolean }) {
  const ratio = (l: MentorLoad) => (l.maxTeams ? l.assignedCount / l.maxTeams : 0)
  const sorted = [...loads].sort((a, b) => ratio(b) - ratio(a) || b.assignedCount - a.assignedCount || a.fullName.localeCompare(b.fullName, 'vi'))

  return (
    <section aria-labelledby="mentor-load-title" className="rounded-lg bg-surface p-4 shadow-elevation-1">
      <h2 id="mentor-load-title" className="text-subtitle font-semibold">
        Số nhóm của mentor
      </h2>
      <p className="mt-1 text-caption text-text-secondary">Tính chung EXE101 và EXE201 trong học kỳ.</p>
      {isLoading ? (
        <div role="status" aria-label="Đang tải" className="mt-3 grid gap-4 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-1">
          {Array.from({ length: 5 }, (_, i) => (
            <Skeleton key={i} className="h-8 w-full" />
          ))}
        </div>
      ) : sorted.length === 0 ? (
        <p className="mt-3 text-body text-text-secondary">Chưa có mentor đang hoạt động. Bộ môn thêm mentor ở mục Mentor.</p>
      ) : (
        <ul className="mt-3 grid max-h-[220px] gap-x-6 gap-y-3 overflow-y-auto sm:grid-cols-2 lg:grid-cols-3 2xl:max-h-[640px] 2xl:grid-cols-1">
          {sorted.map((l) => {
            const max = l.maxTeams
            const over = max != null && l.assignedCount > max
            const full = max != null && l.assignedCount === max
            const abovePreferred = !full && !over && l.preferredTeams != null && l.preferredTeams > 0 && l.assignedCount > l.preferredTeams
            const percent = max ? Math.min(100, Math.round((l.assignedCount / max) * 100)) : 0
            const preferredAt = max && l.preferredTeams ? Math.min(100, Math.round((l.preferredTeams / max) * 100)) : null
            return (
              <li key={l.advisorId} className="flex flex-col gap-1">
                <div className="flex items-center justify-between gap-2">
                  <span className="min-w-0 truncate text-body font-medium" title={l.fullName}>
                    {l.fullName}
                  </span>
                  <span className="shrink-0 text-body tabular-nums text-text-secondary">
                    {max != null ? `${l.assignedCount}/${max} nhóm` : `${l.assignedCount} nhóm`}
                  </span>
                </div>
                {max != null && (
                  <div
                    role="meter"
                    aria-label={`Số nhóm của ${l.fullName}`}
                    aria-valuemin={0}
                    aria-valuemax={max}
                    aria-valuenow={l.assignedCount}
                    className="relative h-2 overflow-hidden rounded-full bg-neutral-container"
                  >
                    <div className={clsx('h-full rounded-full', over ? 'bg-error' : abovePreferred ? 'bg-warning' : 'bg-primary')} style={{ width: `${percent}%` }} />
                    {preferredAt != null && preferredAt < 100 && (
                      <span aria-hidden className="absolute inset-y-0 w-0.5 bg-text-secondary" style={{ left: `${preferredAt}%` }} />
                    )}
                  </div>
                )}
                <span className="text-caption text-text-secondary">
                  {l.preferredTeams ? `Mong muốn ${l.preferredTeams} nhóm` : 'Chưa đặt số mong muốn'}
                </span>
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
