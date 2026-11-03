import clsx from 'clsx'
import { Icon } from '@/shared/components/ui'
import { DAY_NAMES, type ScheduleSlot } from './api'

const stateView = {
  FREE: { text: 'Còn trống', icon: 'event_available', className: 'border-border-strong bg-surface hover:bg-background' },
  TAKEN: { text: 'Đã có nhóm', icon: 'event_busy', className: 'border-border bg-neutral-container text-text-secondary' },
  MINE: { text: 'Nhóm bạn', icon: 'check_circle', className: 'border-primary bg-info-container font-semibold text-info-dark' },
} as const

/**
 * WeeklySlotGrid (flutter-handoff C09): mỗi ngày một cột (≥ 768 px) hoặc một khối, mỗi slot là nút có nhãn đọc được
 * "Thứ Ba 09:15, còn trống". Trạng thái có chữ + icon, không chỉ màu. `canPick` quyết định ô nào bấm được.
 */
export function WeeklySlotGrid({
  slots,
  canPick,
  onPick,
  errorSlotId,
}: {
  slots: ScheduleSlot[]
  canPick: (slot: ScheduleSlot) => boolean
  onPick: (slot: ScheduleSlot) => void
  errorSlotId?: string | null
}) {
  const days = [...new Set(slots.map((s) => s.dayOfWeek))].sort((a, b) => a - b)
  return (
    <div className="grid gap-4 md:auto-cols-fr md:grid-flow-col">
      {days.map((day) => (
        <section key={day} aria-label={DAY_NAMES[day]}>
          <h3 className="mb-2 font-semibold">{DAY_NAMES[day]}</h3>
          <ul className="grid grid-cols-2 gap-2 md:grid-cols-1">
            {slots
              .filter((s) => s.dayOfWeek === day)
              .sort((a, b) => a.startTime.localeCompare(b.startTime))
              .map((s) => {
                const view = stateView[s.state]
                const status = s.teamName ?? view.text
                return (
                  <li key={s.id}>
                    <button
                      type="button"
                      disabled={!canPick(s)}
                      onClick={() => onPick(s)}
                      aria-label={`${DAY_NAMES[day]} ${s.startTime}, ${s.teamName ?? view.text.toLowerCase()}`}
                      className={clsx(
                        'flex min-h-12 w-full items-center gap-2 rounded-md border px-3 py-2 text-left disabled:cursor-default',
                        view.className,
                        errorSlotId === s.id && 'border-error',
                      )}
                    >
                      <Icon name={view.icon} size={20} />
                      <span className="min-w-0 flex-1">
                        <span className="block font-medium">{s.startTime}</span>
                        <span className="block truncate text-caption">{status}</span>
                      </span>
                    </button>
                  </li>
                )
              })}
          </ul>
        </section>
      ))}
    </div>
  )
}
