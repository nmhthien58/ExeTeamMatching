import type { CSSProperties } from 'react'
import { Link } from 'react-router-dom'
import clsx from 'clsx'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Icon, Skeleton } from '@/shared/components/ui'
import { useCountUp, useMounted } from '@/shared/lib/motion'
import { PageHeader } from '@/shared/layout/PageHeader'
import { formatDateTime } from '@/features/workspace/format'
import { timeLeft, useCourseParam, useCurrentSemester, useOpenTeamRound, useStatistics, type StatisticsOverview } from './adminApi'
import { CourseToggle } from './CourseToggle'

/**
 * W02 – Tổng quan (FE-08; giao diện FE-49): thẻ đầu trang (đếm ngược hạn lập nhóm, vòng % đã có nhóm), thẻ số liệu
 * (đã / chưa có nhóm, yêu cầu chờ duyệt, nhóm cần xem lại), nhóm theo sĩ số, phân bố theo chuyên ngành và domain.
 * Trạng thái: đợt đang mở; đợt đã khóa; chưa import; đang tải; lỗi.
 */
export function DashboardPage() {
  const [course, setCourse] = useCourseParam()
  const semester = useCurrentSemester()
  const round = useOpenTeamRound(course)
  const stats = useStatistics(semester.data?.id, course)
  const error = semester.error ?? stats.error
  const total = stats.data ? stats.data.studentsWithTeam + stats.data.studentsWithoutTeam : 0

  return (
    <>
      <PageHeader title="Tổng quan" action={<CourseToggle value={course} onChange={setCourse} />} />
      <div className="flex flex-col gap-6">
        <HeroCard
          course={course}
          semesterCode={semester.data?.code}
          round={round.data ? { name: round.data.name, deadlineAt: round.data.deadlineAt } : undefined}
          roundClosed={round.isSuccess && !round.data}
          withTeam={stats.data?.studentsWithTeam}
          total={total}
        />

        {error ? (
          <ErrorState error={error} onRetry={() => void (semester.error ? semester.refetch() : stats.refetch())} />
        ) : !stats.data ? (
          <DashboardSkeleton />
        ) : total === 0 ? (
          <div className="rounded-lg bg-surface shadow-elevation-1">
            <EmptyState
              icon="upload_file"
              title="Chưa có dữ liệu"
              description={`Import danh sách sinh viên ${course} của học kỳ để bắt đầu.`}
              action={
                <Link to="/admin/import" className="inline-flex min-h-10 items-center rounded-md bg-primary px-4 font-medium text-white">
                  Import sinh viên
                </Link>
              }
            />
          </div>
        ) : (
          <Statistics stats={stats.data} course={course} />
        )}
      </div>
    </>
  )
}

/** Thẻ đầu trang: nền mực có quầng cam, môn + học kỳ, đếm ngược hạn lập nhóm, vòng "% đã có nhóm". */
function HeroCard({
  course,
  semesterCode,
  round,
  roundClosed,
  withTeam,
  total,
}: {
  course: string
  semesterCode?: string
  round?: { name: string; deadlineAt: string }
  roundClosed: boolean
  withTeam?: number
  total: number
}) {
  const left = round ? timeLeft(round.deadlineAt) : undefined
  const percent = total > 0 && withTeam !== undefined ? Math.round((withTeam / total) * 100) : undefined
  return (
    <section aria-label="Tình hình đợt lập nhóm" className="relative overflow-hidden rounded-lg bg-ink p-7 text-white shadow-elevation-2">
      <span aria-hidden className="pointer-events-none absolute -right-24 -top-24 size-72 rounded-full bg-brand-orange/30 blur-3xl" />
      <span aria-hidden className="pointer-events-none absolute -bottom-28 left-1/3 size-64 rounded-full bg-info/25 blur-3xl" />
      <div className="relative flex flex-wrap items-center gap-8">
        <div className="min-w-0 flex-1">
          <p className="text-caption font-semibold uppercase tracking-[0.08em] text-ink-muted">
            {course}
            {semesterCode && ` · Học kỳ ${semesterCode}`}
          </p>
          {round && left ? (
            <>
              <h2 className="mt-2 text-[28px] font-semibold leading-9 tracking-[-0.01em]">{left.text}</h2>
              <p className="mt-1 text-body text-ink-text">
                {round.name} · hạn chót {formatDateTime(round.deadlineAt)}
              </p>
              <span
                className={clsx(
                  'mt-4 inline-flex h-7 items-center gap-1.5 rounded-full px-3 text-caption font-semibold',
                  left.tone === 'error' ? 'bg-error text-white' : left.tone === 'warning' ? 'bg-warning-container text-warning' : 'bg-white/10 text-white',
                )}
              >
                <Icon name="schedule" size={20} />
                Đợt lập nhóm đang mở
              </span>
            </>
          ) : (
            <h2 className="mt-2 text-[24px] font-semibold leading-8">
              {roundClosed ? `Đợt lập nhóm ${course} đã đóng hoặc chưa mở` : 'Đang tải đợt lập nhóm…'}
            </h2>
          )}
        </div>
        {percent !== undefined && <ProgressRing percent={percent} label={`${withTeam}/${total} sinh viên đã có nhóm`} />}
      </div>
    </section>
  )
}

/** Vòng tiến độ SVG, chạy từ 0 khi mở trang; có nhãn cho trình đọc màn hình. */
function ProgressRing({ percent, label }: { percent: number; label: string }) {
  const mounted = useMounted()
  const shown = useCountUp(percent)
  const r = 44
  const c = 2 * Math.PI * r
  return (
    <div role="img" aria-label={`${percent}% – ${label}`} className="flex items-center gap-4">
      <svg viewBox="0 0 100 100" className="size-28 -rotate-90" aria-hidden>
        <circle cx="50" cy="50" r={r} fill="none" stroke="rgb(255 255 255 / 10%)" strokeWidth="9" />
        <circle
          cx="50"
          cy="50"
          r={r}
          fill="none"
          stroke="url(#ring)"
          strokeWidth="9"
          strokeLinecap="round"
          strokeDasharray={c}
          strokeDashoffset={mounted ? c * (1 - percent / 100) : c}
          style={{ transition: 'stroke-dashoffset 1s cubic-bezier(0.22, 1, 0.36, 1)' }}
        />
        <defs>
          <linearGradient id="ring" x1="0" y1="0" x2="1" y2="1">
            <stop offset="0%" stopColor="#f37021" />
            <stop offset="100%" stopColor="#ffb27a" />
          </linearGradient>
        </defs>
      </svg>
      <div className="flex flex-col">
        <span className="text-[32px] font-semibold leading-9 tabular-nums">{shown}%</span>
        <span className="text-caption text-ink-text">{label}</span>
      </div>
    </div>
  )
}

function Statistics({ stats, course }: { stats: StatisticsOverview; course: string }) {
  const total = stats.studentsWithTeam + stats.studentsWithoutTeam
  const sizes = Object.entries(stats.teamsBySize).sort(([a], [b]) => Number(a) - Number(b))
  return (
    <>
      <div className="grid grid-cols-2 gap-4 xl:grid-cols-4">
        <StatCard
          index={0}
          icon="group_add"
          tone="success"
          label="Đã có nhóm"
          value={stats.studentsWithTeam}
          hint={`${Math.round((stats.studentsWithTeam / total) * 100)}% sinh viên`}
        />
        <StatCard index={1} icon="person_search" tone="warning" label="Chưa có nhóm" value={stats.studentsWithoutTeam} to={`/admin/chua-co-nhom?mon=${course}`} />
        <StatCard index={2} icon="swap_horiz" tone="info" label="Yêu cầu chờ duyệt" value={stats.pendingChangeRequests} to="/admin/doi-nhom" />
        <StatCard index={3} icon="rule" tone="primary" label="Nhóm cần xem lại" value={stats.teamsNeedingReview} to={`/admin/chua-co-nhom/chot?mon=${course}`} />
      </div>

      <section className="stagger-in rounded-lg bg-surface p-6 shadow-elevation-1" style={{ '--i': 4 } as CSSProperties}>
        <SectionTitle icon="bar_chart" title="Nhóm theo sĩ số" hint="Nhóm dưới 4 người tô màu cảnh báo" />
        {sizes.length === 0 ? (
          <p className="text-text-secondary">Chưa có nhóm nào.</p>
        ) : (
          <BarList items={sizes.map(([size, count]) => ({ label: `${size} người`, count, warn: Number(size) < 4 }))} unit="nhóm" />
        )}
      </section>

      <div className="grid gap-4 lg:grid-cols-2">
        <section className="stagger-in rounded-lg bg-surface p-6 shadow-elevation-1" style={{ '--i': 5 } as CSSProperties}>
          <SectionTitle icon="school" title="Theo chuyên ngành" />
          <BarList items={stats.bySpecialization.map((s) => ({ label: s.code, count: s.count }))} unit="sinh viên" />
        </section>
        <section className="stagger-in rounded-lg bg-surface p-6 shadow-elevation-1" style={{ '--i': 6 } as CSSProperties}>
          <SectionTitle icon="category" title="Theo domain" />
          <BarList items={stats.byDomain.map((d) => ({ label: d.code, count: d.count }))} unit="nhóm" />
        </section>
      </div>
    </>
  )
}

function SectionTitle({ icon, title, hint }: { icon: string; title: string; hint?: string }) {
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
function StatCard({
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

/** Thanh ngang theo số lượng, chạy từ 0 khi mở trang; danh sách đọc được bằng trình đọc màn hình. */
function BarList({ items, unit }: { items: { label: string; count: number; warn?: boolean }[]; unit: string }) {
  const mounted = useMounted()
  const max = Math.max(1, ...items.map((i) => i.count))
  if (items.length === 0) return <p className="text-text-secondary">Chưa có dữ liệu.</p>
  return (
    <ul className="flex flex-col gap-3">
      {[...items]
        .sort((a, b) => b.count - a.count)
        .map((item, i) => (
          <li key={item.label} className="grid grid-cols-[96px_1fr_48px] items-center gap-3">
            <span className="truncate font-medium">{item.label}</span>
            <span className="h-2.5 overflow-hidden rounded-full bg-neutral-container" aria-hidden>
              <span
                className={clsx(
                  'block h-full rounded-full bg-gradient-to-r',
                  item.warn ? 'from-warning to-[#c98a2b]' : 'from-info to-[#3b8fd0]',
                )}
                style={{
                  width: mounted ? `${(item.count / max) * 100}%` : '0%',
                  transition: `width 800ms cubic-bezier(0.22, 1, 0.36, 1) ${i * 60}ms`,
                }}
              />
            </span>
            <span className="text-right font-semibold tabular-nums" aria-label={`${item.count} ${unit}`}>
              {item.count}
            </span>
          </li>
        ))}
    </ul>
  )
}

function DashboardSkeleton() {
  return (
    <div role="status" aria-label="Đang tải số liệu" className="flex flex-col gap-4">
      <div className="grid grid-cols-2 gap-4 xl:grid-cols-4">
        {Array.from({ length: 4 }, (_, i) => (
          <Skeleton key={i} className="h-36 rounded-lg" />
        ))}
      </div>
      <Skeleton className="h-48 rounded-lg" />
    </div>
  )
}
