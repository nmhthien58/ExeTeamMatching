import clsx from 'clsx'
import { Skeleton, StatusChip } from '@/shared/components/ui'
import type { AdvisorLoad } from './api'

/** Thanh tải từng giảng viên so với số nhóm tối đa (W19); quá tải đứng đầu, rồi gần đầy. */
export function LoadPanel({ loads, isLoading }: { loads: AdvisorLoad[]; isLoading: boolean }) {
  const ratio = (l: AdvisorLoad) => (l.maxTeams ? l.assignedCount / l.maxTeams : 0)
  const sorted = [...loads].sort((a, b) => ratio(b) - ratio(a) || b.assignedCount - a.assignedCount || a.fullName.localeCompare(b.fullName, 'vi'))

  return (
    <section aria-labelledby="load-title" className="rounded-lg bg-surface p-4 shadow-elevation-1">
      <h2 id="load-title" className="text-subtitle font-semibold">
        Số nhóm của giảng viên
      </h2>
      {isLoading ? (
        <div role="status" aria-label="Đang tải" className="mt-3 grid gap-4 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-1">
          {Array.from({ length: 5 }, (_, i) => (
            <Skeleton key={i} className="h-8 w-full" />
          ))}
        </div>
      ) : sorted.length === 0 ? (
        <p className="mt-3 text-body text-text-secondary">Chưa có giảng viên nào dạy môn này.</p>
      ) : (
        <ul className="mt-3 grid max-h-[220px] gap-x-6 gap-y-3 overflow-y-auto sm:grid-cols-2 lg:grid-cols-3 2xl:max-h-[640px] 2xl:grid-cols-1">
          {sorted.map((l) => {
            const over = l.maxTeams != null && l.assignedCount > l.maxTeams
            const full = l.maxTeams != null && l.assignedCount === l.maxTeams
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
                    className="h-2 overflow-hidden rounded-full bg-neutral-container"
                  >
                    <div className={clsx('h-full rounded-full', over ? 'bg-error' : 'bg-primary')} style={{ width: `${percent}%` }} />
                  </div>
                ) : (
                  <span className="text-caption text-text-secondary">Không giới hạn</span>
                )}
                {over && (
                  <span>
                    <StatusChip tone="error" icon="error">
                      Quá tải
                    </StatusChip>
                  </span>
                )}
                {full && <span className="text-caption text-text-secondary">Đã đủ nhóm</span>}
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}
