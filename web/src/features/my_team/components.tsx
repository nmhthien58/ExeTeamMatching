import { useState } from 'react'
import clsx from 'clsx'
import { Button, Skeleton } from '@/shared/components/ui'
import { SkillBars } from '@/features/profile/components'
import type { TeamMember } from '@/features/suggestions/api'

export const linkClass = 'inline-flex min-h-10 items-center rounded-md px-4 font-medium'

export function ListSkeleton({ count = 3 }: { count?: number }) {
  return (
    <div role="status" aria-label="Đang tải" className="flex flex-col gap-3">
      {Array.from({ length: count }, (_, i) => (
        <Skeleton key={i} className="h-24 rounded-lg" />
      ))}
    </div>
  )
}

/** Ô nhập có nhãn, bộ đếm, lỗi tại ô (DESIGN.md 6.1). */
export function Field({
  label,
  value,
  onChange,
  error,
  hint,
  maxLength,
  rows,
  disabled,
}: {
  label: string
  value: string
  onChange: (value: string) => void
  error?: string
  hint?: string
  maxLength?: number
  rows?: number
  disabled?: boolean
}) {
  const id = `field-${label}`
  const input = {
    id,
    value,
    disabled,
    maxLength,
    'aria-invalid': !!error,
    'aria-describedby': error || hint ? `${id}-note` : undefined,
    onChange: (e: { target: { value: string } }) => onChange(e.target.value),
    className: clsx('rounded-sm border px-3 py-2 disabled:bg-background', error ? 'border-error' : 'border-border-strong'),
  }
  return (
    <div className="flex flex-col gap-2">
      <label htmlFor={id} className="font-medium">
        {label}
      </label>
      {rows ? <textarea rows={rows} {...input} /> : <input {...input} />}
      <span id={`${id}-note`} className={clsx('flex justify-between text-caption', error ? 'text-error' : 'text-text-secondary')}>
        <span>{error ?? hint}</span>
        {maxLength && (
          <span className="text-text-secondary">
            {value.length}/{maxLength}
          </span>
        )}
      </span>
    </div>
  )
}

/** Nút "Kỹ năng" trên dòng thành viên (S06, S13): mở rộng dòng, hiện 5 kỹ năng tự đánh giá. Dòng cha cần flex-wrap. */
export function MemberSkillsToggle({ member }: { member: TeamMember }) {
  const [open, setOpen] = useState(false)
  return (
    <>
      <Button variant="text" icon={open ? 'expand_less' : 'expand_more'} aria-expanded={open} onClick={() => setOpen((v) => !v)}>
        Kỹ năng
      </Button>
      {open && (
        <div className="basis-full pb-2 pl-12">
          <SkillBars ratings={member.skills ?? []} />
        </div>
      )}
    </>
  )
}
