import type { CSSProperties } from 'react'
import { Link } from 'react-router-dom'
import clsx from 'clsx'
import { Icon } from '@/shared/components/ui'
import { useCountUp } from '@/shared/lib/motion'

/** Thẻ số liệu và tiêu đề khu vực dùng chung cho dashboard Bộ môn (FE-49) và trang chủ các vai trò (FE-51). */

export function SectionTitle({ icon, title, hint }: { icon: string; title: string; hint?: string }) {
  return (
    <div className="mb-5 flex items-center gap-3">
      <span className="grid size-9 place-items-center rounded-md bg-primary-container text-primary">
        <Icon name={icon} size={20} />
      </span>
      <div>
        <h2 className="text-subtitle font-semibold">{title}</h2>
        {hint && <p className="text-caption text-text-secondary">{hint}</p>}
      </div>
    </div>
  )
}

const statTones = {
  success: 'bg-success-container text-success',
  warning: 'bg-warning-container text-warning',
  info: 'bg-info-container text-info-dark',
  primary: 'bg-primary-container text-primary',
} as const

/** Thẻ số liệu: icon trong ô màu theo ý nghĩa, số đếm chạy, xuất hiện so le; bấm được thì có mũi tên. */
export function StatCard({
  index,
  icon,
  tone,
  label,
  value,
  hint,
  to,
}: {
  index: number
  icon: string
  tone: keyof typeof statTones
  label: string
  value: number
  hint?: string
  to?: string
}) {
  const shown = useCountUp(value)
  const body = (
    <>
      <div className="flex items-start justify-between">
        <span className={clsx('grid size-11 place-items-center rounded-[12px]', statTones[tone])}>
          <Icon name={icon} />
        </span>
        {to && (
          <Icon
            name="arrow_outward"
            size={20}
            className="text-text-secondary transition-transform group-hover:-translate-y-0.5 group-hover:translate-x-0.5"
          />
        )}
      </div>
      <span className="mt-4 text-[32px] font-semibold leading-9 tabular-nums tracking-[-0.01em]" aria-label={value.toLocaleString('vi-VN')}>
        {shown.toLocaleString('vi-VN')}
      </span>
      <span className="text-body font-medium text-text-secondary">{label}</span>
      {hint && <span className="mt-1 text-caption text-text-secondary">{hint}</span>}
    </>
  )
  const className = 'group stagger-in flex flex-col rounded-lg bg-surface p-5 shadow-elevation-1'
  const style = { '--i': index } as CSSProperties
  return to ? (
    <Link to={to} className={clsx(className, 'card-hover')} style={style}>
      {body}
    </Link>
  ) : (
    <div className={className} style={style}>
      {body}
    </div>
  )
}

