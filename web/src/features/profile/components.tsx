import { useState, type ReactNode } from 'react'
import clsx from 'clsx'
import { Banner, Button } from '@/shared/components/ui'
import { toApiError } from '@/shared/api/apiClient'
import { skills, type SkillName, type SkillRating, type StudentProfile, type StudentProfileInput } from './api'

export const cardClass = 'rounded-lg bg-surface p-4 shadow-elevation-1'

export function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className={cardClass}>
      <h2 className="mb-3 text-subtitle font-semibold">{title}</h2>
      {children}
    </section>
  )
}

/** Chữ viết tắt thay ảnh đại diện (dữ liệu import không có ảnh). */
export function Avatar({ name }: { name: string }) {
  const initials = name
    .split(' ')
    .filter(Boolean)
    .slice(-2)
    .map((w) => w[0])
    .join('')
    .toUpperCase()
  return (
    <span aria-hidden className="grid size-16 shrink-0 place-items-center rounded-full bg-primary-border text-title font-semibold text-primary-dark">
      {initials}
    </span>
  )
}

/** Biểu đồ kỹ năng: 5 hàng × 5 ô (S02, S04). `highlight`: kỹ năng cần nhấn mạnh. */
export function SkillBars({ ratings, highlight = [] }: { ratings: SkillRating[]; highlight?: SkillName[] }) {
  if (ratings.length === 0) return <p className="text-body text-text-secondary">Chưa đánh giá kỹ năng.</p>
  return (
    <ul className="flex flex-col gap-2">
      {skills.map(({ key, label }) => {
        const level = ratings.find((r) => r.skill === key)?.level ?? 0
        return (
          <li key={key} aria-label={`${label}, ${level} trên 5`} className="grid grid-cols-[136px_1fr_auto] items-center gap-3">
            <span className={clsx('text-body', highlight.includes(key) && 'font-semibold')}>{label}</span>
            <span aria-hidden className="grid grid-cols-5 gap-1">
              {[1, 2, 3, 4, 5].map((n) => (
                <span key={n} className={clsx('h-3 rounded-sm', n <= level ? 'bg-primary' : 'bg-neutral-container')} />
              ))}
            </span>
            <span aria-hidden className="text-caption text-text-secondary">{level}/5</span>
          </li>
        )
      })}
    </ul>
  )
}

/** Thanh kéo 1–5; `value = null` là "Chưa đánh giá", chạm vào thanh mới tính là đã đánh giá (kể cả đúng vị trí đang đứng). */
function SkillSlider({ label, value, onChange }: { label: string; value: number | null; onChange: (value: number) => void }) {
  return (
    <label className="flex flex-col gap-1">
      <span className="flex items-center justify-between">
        <span className="font-medium">{label}</span>
        <span className={clsx('text-body', value === null && 'text-text-secondary')}>{value === null ? 'Chưa đánh giá' : `${value}/5`}</span>
      </span>
      <input
        type="range"
        min={1}
        max={5}
        step={1}
        value={value ?? 1}
        aria-valuetext={value === null ? `${label}, chưa đánh giá` : `${label}, ${value} trên 5`}
        onChange={(e) => onChange(Number(e.target.value))}
        onClick={(e) => onChange(Number(e.currentTarget.value))}
        className={clsx('h-10 w-full accent-primary', value === null && 'opacity-50')}
      />
    </label>
  )
}

/**
 * Form hồ sơ dùng chung cho A05 và S03: 5 thanh kỹ năng, giới thiệu ≤ 500 ký tự (rule 16), không có ô liên lạc.
 * Nút lưu khóa và báo số kỹ năng còn thiếu; lỗi lưu giữ nguyên dữ liệu đã nhập.
 */
export function ProfileForm({
  profile,
  submitLabel,
  saving,
  error,
  extra,
  onSubmit,
}: {
  profile: StudentProfile
  submitLabel: string
  saving: boolean
  error: unknown
  extra?: (isSeekingTeam: boolean, set: (value: boolean) => void) => ReactNode
  onSubmit: (input: StudentProfileInput) => void
}) {
  const [levels, setLevels] = useState<Partial<Record<SkillName, number>>>(() => Object.fromEntries(profile.skills.map((s) => [s.skill, s.level])))
  const [description, setDescription] = useState(profile.description ?? '')
  const [isSeekingTeam, setIsSeekingTeam] = useState(profile.isSeekingTeam)
  const missing = skills.filter((s) => levels[s.key] === undefined).length
  const apiError = error ? toApiError(error) : null

  return (
    <form
      className="flex flex-col gap-4"
      onSubmit={(e) => {
        e.preventDefault()
        if (missing > 0) return
        onSubmit({
          skills: skills.map((s) => ({ skill: s.key, level: levels[s.key]! })),
          description: description.trim(),
          ...(extra ? { isSeekingTeam } : {}),
        })
      }}
    >
      {apiError && <Banner tone="error" title="Chưa lưu được hồ sơ">{apiError.detail ?? apiError.title}</Banner>}
      <Section title="Kỹ năng">
        <div className="flex flex-col gap-3">
          {skills.map((s) => (
            <SkillSlider key={s.key} label={s.label} value={levels[s.key] ?? null} onChange={(level) => setLevels((l) => ({ ...l, [s.key]: level }))} />
          ))}
        </div>
      </Section>
      <Section title="Giới thiệu">
        <label className="flex flex-col gap-2">
          <span className="sr-only">Giới thiệu ngắn về bạn</span>
          <textarea
            rows={4}
            maxLength={500}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="Bạn muốn làm gì trong nhóm?"
            className="rounded-sm border border-border-strong px-3 py-2"
          />
          <span className="self-end text-caption text-text-secondary">{description.length}/500</span>
        </label>
      </Section>
      {extra?.(isSeekingTeam, setIsSeekingTeam)}
      <div className="flex items-center justify-end gap-3">
        {missing > 0 && <p className="text-body text-text-secondary">Còn {missing} kỹ năng chưa đánh giá</p>}
        <Button type="submit" loading={saving} disabled={missing > 0}>
          {submitLabel}
        </Button>
      </div>
    </form>
  )
}
