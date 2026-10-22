import { Link } from 'react-router-dom'
import clsx from 'clsx'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Banner, Skeleton } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { formatDateTime } from '@/features/workspace/format'
import { timeLeft, useCourseParam, useCurrentSemester, useOpenTeamRound, useStatistics, type StatisticsOverview } from './adminApi'
import { CourseToggle } from './CourseToggle'

/**
 * W02 – Tổng quan (FE-08): thẻ số liệu (đã / chưa có nhóm, yêu cầu chờ duyệt, nhóm cần xem lại), nhóm theo sĩ số,
 * phân bố theo chuyên ngành và domain, đếm ngược hạn chót. Trạng thái: đợt đang mở; đợt đã khóa; chưa import; đang tải.
 */
export function DashboardPage() {
  const [course, setCourse] = useCourseParam()
  const semester = useCurrentSemester()
  const round = useOpenTeamRound(course)
  const stats = useStatistics(semester.data?.id, course)
  const error = semester.error ?? stats.error

  return (
    <>
      <PageHeader title="Tổng quan" />
      <div className="flex flex-col gap-6">
        <div className="flex flex-wrap items-center gap-3">
          <CourseToggle value={course} onChange={setCourse} />
          {semester.data && <span className="text-body text-text-secondary">Học kỳ {semester.data.code}</span>}
        </div>

        {round.data ? (
          <Banner tone={timeLeft(round.data.deadlineAt).tone} title={timeLeft(round.data.deadlineAt).text}>
            {round.data.name} · hạn chót {formatDateTime(round.data.deadlineAt)}
          </Banner>
        ) : (
          round.isSuccess && (
            <div className="flex items-center gap-3 rounded-md bg-neutral-container px-4 py-3 font-semibold text-text-secondary">
              Đợt lập nhóm {course} đã đóng hoặc chưa mở
            </div>
          )
        )}

        {error ? (
          <ErrorState error={error} onRetry={() => void (semester.error ? semester.refetch() : stats.refetch())} />
        ) : !stats.data ? (
          <DashboardSkeleton />
        ) : stats.data.studentsWithTeam + stats.data.studentsWithoutTeam === 0 ? (
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

function Statistics({ stats, course }: { stats: StatisticsOverview; course: string }) {
  const total = stats.studentsWithTeam + stats.studentsWithoutTeam
  const sizes = Object.entries(stats.teamsBySize).sort(([a], [b]) => Number(a) - Number(b))
  return (
    <>
      <div className="grid grid-cols-2 gap-4 xl:grid-cols-4">
        <StatCard label="Đã có nhóm" value={stats.studentsWithTeam} hint={`${Math.round((stats.studentsWithTeam / total) * 100)}% sinh viên`} />
        <StatCard label="Chưa có nhóm" value={stats.studentsWithoutTeam} to={`/admin/chua-co-nhom?mon=${course}`} />
        <StatCard label="Yêu cầu chờ duyệt" value={stats.pendingChangeRequests} to="/admin/doi-nhom" />
        <StatCard label="Nhóm cần xem lại" value={stats.teamsNeedingReview} to={`/admin/chua-co-nhom/chot?mon=${course}`} />
      </div>

      <section className="rounded-lg bg-surface p-6 shadow-elevation-1">
        <h2 className="mb-4 text-subtitle font-semibold">Nhóm theo sĩ số</h2>
        {sizes.length === 0 ? (
          <p className="text-text-secondary">Chưa có nhóm nào.</p>
        ) : (
          <BarList items={sizes.map(([size, count]) => ({ label: `${size} người`, count, warn: Number(size) < 4 }))} unit="nhóm" />
        )}
      </section>

      <div className="grid gap-4 lg:grid-cols-2">
        <section className="rounded-lg bg-surface p-6 shadow-elevation-1">
          <h2 className="mb-4 text-subtitle font-semibold">Theo chuyên ngành</h2>
          <BarList items={stats.bySpecialization.map((s) => ({ label: s.code, count: s.count }))} unit="sinh viên" />
        </section>
        <section className="rounded-lg bg-surface p-6 shadow-elevation-1">
          <h2 className="mb-4 text-subtitle font-semibold">Theo domain</h2>
          <BarList items={stats.byDomain.map((d) => ({ label: d.code, count: d.count }))} unit="nhóm" />
        </section>
      </div>
    </>
  )
}

/** Thẻ số liệu web (DESIGN.md 8.1): nhãn Caption, con số Display, không icon trang trí. */
function StatCard({ label, value, hint, to }: { label: string; value: number; hint?: string; to?: string }) {
  const body = (
    <>
      <span className="text-caption font-semibold text-text-secondary">{label}</span>
      <span className="text-display font-semibold tabular-nums">{value.toLocaleString('vi-VN')}</span>
      {hint && <span className="text-caption text-text-secondary">{hint}</span>}
    </>
  )
  const className = 'flex flex-col gap-1 rounded-lg bg-surface p-6 shadow-elevation-1'
  return to ? (
    <Link to={to} className={clsx(className, 'hover:bg-primary-container/40')}>
      {body}
    </Link>
  ) : (
    <div className={className}>{body}</div>
  )
}

/** Thanh ngang theo số lượng – danh sách đọc được bằng trình đọc màn hình (không chỉ dựa vào hình). */
function BarList({ items, unit }: { items: { label: string; count: number; warn?: boolean }[]; unit: string }) {
  const max = Math.max(1, ...items.map((i) => i.count))
  if (items.length === 0) return <p className="text-text-secondary">Chưa có dữ liệu.</p>
  return (
    <ul className="flex flex-col gap-2">
      {[...items].sort((a, b) => b.count - a.count).map((item) => (
        <li key={item.label} className="grid grid-cols-[96px_1fr_64px] items-center gap-3">
          <span className="truncate font-medium">{item.label}</span>
          <span className="h-3 rounded-full bg-neutral-container" aria-hidden>
            <span className={clsx('block h-full rounded-full', item.warn ? 'bg-warning' : 'bg-info')} style={{ width: `${(item.count / max) * 100}%` }} />
          </span>
          <span className="text-right tabular-nums" aria-label={`${item.count} ${unit}`}>
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
          <Skeleton key={i} className="h-28 rounded-lg" />
        ))}
      </div>
      <Skeleton className="h-40 rounded-lg" />
    </div>
  )
}
