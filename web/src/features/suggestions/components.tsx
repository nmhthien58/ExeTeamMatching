import type { ReactNode } from 'react'
import clsx from 'clsx'
import { Icon, StatusChip } from '@/shared/components/ui'
import { Avatar } from '@/features/workspace/components'
import { fitLabel, type CatalogRef, type StudentRef, type TeamCard } from './api'

/**
 * Thanh điều kiện nhóm dạng gọn (DESIGN.md 6.1): 6 chấm thành viên – chỗ đã có tô Primary, chỗ trống viền Border Strong –
 * luôn kèm chữ "4/6 thành viên"; người ngoài nhóm xem thì thêm "còn n chỗ".
 */
export function TeamRequirementBar({ memberCount, maxSize = 6, showOpenSlots = false }: { memberCount: number; maxSize?: number; showOpenSlots?: boolean }) {
  return (
    <span className="inline-flex items-center gap-2">
      <span className="flex gap-1" aria-hidden>
        {Array.from({ length: maxSize }, (_, i) => (
          <span key={i} className={clsx('size-3 rounded-full', i < memberCount ? 'bg-primary' : 'border border-border-strong')} />
        ))}
      </span>
      <span className="text-body">
        {memberCount}/{maxSize} thành viên{showOpenSlots && memberCount < maxSize && ` · còn ${maxSize - memberCount} chỗ`}
      </span>
    </span>
  )
}

/** Tối đa 2 chip domain rồi "+n" (thẻ nhóm). */
export function DomainChips({ domains }: { domains: CatalogRef[] }) {
  if (domains.length === 0) return null
  return (
    <span className="flex flex-wrap gap-2">
      {domains.slice(0, 2).map((d) => (
        <span key={d.id} className="inline-flex h-8 items-center rounded-full bg-secondary-container px-3 font-medium text-secondary-dark">
          {d.code} {d.name}
        </span>
      ))}
      {domains.length > 2 && <span className="inline-flex h-8 items-center px-1 text-text-secondary">+{domains.length - 2}</span>}
    </span>
  )
}

export function FitChip({ score }: { score: number }) {
  const fit = fitLabel(score)
  return (
    <StatusChip tone={fit.tone} icon="auto_awesome">
      {fit.text}
    </StatusChip>
  )
}

/** Lý do gợi ý do Matching trả về – mỗi dòng một ý, icon dấu tích. */
export function Reasons({ reasons }: { reasons: string[] }) {
  if (reasons.length === 0) return null
  return (
    <ul className="flex flex-col gap-1" aria-label="Lý do gợi ý">
      {reasons.map((r) => (
        <li key={r} className="flex items-start gap-2 text-body text-text-secondary">
          <Icon name="check" size={20} className="text-success" />
          {r}
        </li>
      ))}
    </ul>
  )
}

/** Thẻ nhóm do người ngoài nhóm xem (S07): không chip trạng thái, có số chỗ còn lại. */
export function TeamSuggestionCard({ team, score, reasons, action }: { team: TeamCard; score: number; reasons: string[]; action?: ReactNode }) {
  const missing = team.requirement?.missingSpecializations ?? []
  return (
    <article className="flex flex-col gap-2 rounded-lg bg-surface p-4 shadow-elevation-1">
      <div className="flex flex-wrap items-start gap-2">
        <div className="min-w-0 flex-1">
          <h2 className="line-clamp-2 text-subtitle font-semibold">{team.name}</h2>
          {team.projectName && <p className="text-body text-text-secondary">{team.projectName}</p>}
        </div>
        <FitChip score={score} />
      </div>
      <DomainChips domains={team.domains} />
      <TeamRequirementBar memberCount={team.memberCount} maxSize={team.requirement?.maxSize} showOpenSlots />
      {missing.length > 0 && <p className="text-body">Đang thiếu: {missing.map((m) => m.name).join(', ')}</p>}
      <Reasons reasons={reasons} />
      {action && <div className="flex justify-end">{action}</div>}
    </article>
  )
}

/** Thẻ sinh viên (DESIGN.md 6.1): avatar 40, họ tên, "MSSV · chuyên ngành", tối đa 3 chip kỹ năng, nút ở cuối. */
export function StudentCard({ student, skills = [], children, action }: { student: StudentRef; skills?: string[]; children?: ReactNode; action?: ReactNode }) {
  return (
    <article className="flex flex-col gap-2 rounded-lg bg-surface p-4 shadow-elevation-1">
      <div className="flex items-start gap-3">
        <Avatar name={student.fullName} size={40} />
        <div className="min-w-0 flex-1">
          <h2 className="line-clamp-2 text-subtitle font-semibold">{student.fullName}</h2>
          <p className="line-clamp-2 text-body text-text-secondary">
            {student.studentCode}
            {student.specialization && ` · ${student.specialization.name}`}
          </p>
        </div>
      </div>
      {skills.length > 0 && (
        <span className="flex flex-wrap gap-2">
          {skills.slice(0, 3).map((s) => (
            <span key={s} className="inline-flex h-8 items-center rounded-full border border-border px-3 font-medium text-text-secondary">
              {s}
            </span>
          ))}
        </span>
      )}
      {children}
      {action && <div className="flex justify-end">{action}</div>}
    </article>
  )
}
