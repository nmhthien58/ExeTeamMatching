import { useMounted } from '@/shared/lib/motion'
import type { DailyErrorCount } from './api'

const CHART_HEIGHT = 160

/** `2026-11-05` → `05/11`; cắt chuỗi để không lệch ngày theo múi giờ của trình duyệt. */
function dayLabel(date: string): string {
  const [, month, day] = date.split('-')
  return `${day}/${month}`
}

function describe(day: DailyErrorCount): string {
  return `${dayLabel(day.date)}: ${day.fatalCount} fatal, ${day.nonFatalCount} non-fatal`
}

function LegendItem({ className, label }: { className: string; label: string }) {
  return (
    <span className="inline-flex items-center gap-2 text-caption text-text-secondary">
      <span aria-hidden className={`size-2.5 rounded-[3px] ${className}`} />
      {label}
    </span>
  )
}

/**
 * Cột chồng fatal và non-fatal theo ngày; rê chuột vào cột để xem số. Có bảng ẩn cho trình đọc màn hình.
 * Cột không nhận Tab: chúng nằm trong vùng aria-hidden, focus vào đó là focus vào chỗ không có tên (CR-04, MB-59).
 */
export function DailyErrorChart({ days }: { days: DailyErrorCount[] }) {
  const mounted = useMounted()
  const peak = Math.max(1, ...days.map((d) => d.fatalCount + d.nonFatalCount))
  const height = (count: number) => (mounted ? (count / peak) * CHART_HEIGHT : 0)

  return (
    <div>
      <div className="mb-3 flex gap-4">
        <LegendItem className="bg-error" label="Fatal" />
        <LegendItem className="bg-info" label="Non-fatal" />
      </div>
      <div className="grid grid-cols-[auto_1fr] gap-x-2">
        <div className="flex flex-col justify-between text-caption tabular-nums text-text-secondary" style={{ height: CHART_HEIGHT }} aria-hidden>
          <span className="-translate-y-1/2">{peak}</span>
          <span className="translate-y-1/2">0</span>
        </div>
        <div className="flex items-end gap-2 border-b border-t border-border" style={{ height: CHART_HEIGHT }} aria-hidden>
          {days.map((day) => (
            <div key={day.date} className="group relative flex h-full flex-1 flex-col justify-end gap-0.5">
              <span
                role="tooltip"
                className="pointer-events-none absolute bottom-full left-1/2 z-10 mb-1 hidden -translate-x-1/2 whitespace-nowrap rounded-sm bg-ink px-2 py-1 text-caption text-white shadow-elevation-2 group-hover:block"
              >
                {describe(day)}
              </span>
              {[
                { count: day.fatalCount, color: 'bg-error', corner: 'rounded-t-[4px]' },
                { count: day.nonFatalCount, color: 'bg-info', corner: day.fatalCount === 0 ? 'rounded-t-[4px]' : '' },
              ].map(
                (bar) =>
                  bar.count > 0 && (
                    <span
                      key={bar.color}
                      className={`mx-auto block w-full max-w-10 ${bar.color} ${bar.corner}`}
                      style={{ height: height(bar.count), transition: 'height 800ms cubic-bezier(0.22, 1, 0.36, 1)' }}
                    />
                  ),
              )}
            </div>
          ))}
        </div>
        <span />
        <div className="mt-1 flex gap-2" aria-hidden>
          {days.map((day) => (
            <span key={day.date} className="flex-1 text-center text-caption tabular-nums text-text-secondary">
              {dayLabel(day.date)}
            </span>
          ))}
        </div>
      </div>
      <table className="sr-only">
        <caption>Số lỗi theo ngày</caption>
        <thead>
          <tr>
            <th scope="col">Ngày</th>
            <th scope="col">Fatal</th>
            <th scope="col">Non-fatal</th>
          </tr>
        </thead>
        <tbody>
          {days.map((day) => (
            <tr key={day.date}>
              <th scope="row">{dayLabel(day.date)}</th>
              <td>{day.fatalCount}</td>
              <td>{day.nonFatalCount}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
