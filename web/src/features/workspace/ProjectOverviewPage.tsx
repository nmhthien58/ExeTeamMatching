import { Link } from 'react-router-dom'
import clsx from 'clsx'
import { EmptyState } from '@/shared/components/States'
import { Icon, Skeleton, StatusChip } from '@/shared/components/ui'
import { useAuth } from '@/shared/auth/AuthProvider'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useTaskBoard } from '@/features/tasks/api'
import { Avatar, Card, RoleChip } from './components'
import { formatDateTime, formatRelative } from './format'
import { useWorkspaceContext } from './context'
import { taskStats } from './stats'

/**
 * C01 – Dự án: tổng quan workspace (FE-39). Đầu trang có tên nhóm, thành viên và cố vấn; 4 thẻ số liệu; lối tắt;
 * "Mới cập nhật". Mentor thấy thêm thẻ tóm tắt với "Buổi mentor n/m".
 */
export function ProjectOverviewPage() {
  const { workspace, basePath, canEdit } = useWorkspaceContext()
  const { user } = useAuth()
  const tasks = useTaskBoard(workspace.id, {})
  const stats = taskStats(tasks.data ?? [], user?.studentId ?? undefined)
  const isNewTeam = tasks.isSuccess && tasks.data.length === 0 && (workspace.recentActivity?.length ?? 0) === 0

  return (
    <>
      <PageHeader title="Dự án" />
      <div className="flex flex-col gap-4">
        <TeamHeader />
        {workspace.myRole === 'MENTOR' && <MentorBrief />}

        <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
          {canEdit ? (
            <StatCard label="Việc của tôi" value={stats.mine} loading={tasks.isLoading} to={`${basePath}/viec?mine=1`} />
          ) : (
            <StatCard label="Bài nộp chờ duyệt" value={stats.pendingReview} loading={tasks.isLoading} to={`${basePath}/viec?kind=DELIVERABLE`} />
          )}
          <StatCard label="Sắp đến hạn" value={stats.dueSoon} loading={tasks.isLoading} to={`${basePath}/viec`} />
          <StatCard label="Họp sắp tới" value={workspace.upcomingMeetings?.length ?? (workspace.nextMeetingAt ? 1 : 0)} />
          <StatCard label="Câu hỏi chờ" value={workspace.openQuestionCount ?? 0} />
        </div>

        <Shortcuts />

        <Card>
          <h2 className="mb-2 text-subtitle font-semibold">Mới cập nhật</h2>
          {isNewTeam ? (
            <EmptyState
              icon="rocket_launch"
              title="Dự án vừa bắt đầu"
              description={canEdit ? 'Tạo việc đầu tiên để cả nhóm cùng theo dõi.' : 'Nhóm chưa có việc hay file nào.'}
              action={
                canEdit && (
                  <Link to={`${basePath}/viec?tao=1`} className="inline-flex min-h-10 items-center rounded-md bg-primary px-4 font-medium text-white">
                    Tạo việc
                  </Link>
                )
              }
            />
          ) : (
            <RecentActivity />
          )}
        </Card>
      </div>
    </>
  )
}

function TeamHeader() {
  const { workspace } = useWorkspaceContext()
  const people = workspace.people ?? []
  return (
    <Card>
      <div className="flex flex-wrap items-start gap-3">
        <div className="min-w-0 flex-1">
          <p className="line-clamp-2 text-display font-semibold">{workspace.teamName}</p>
          {workspace.projectName && <p className="text-body text-text-secondary">{workspace.projectName}</p>}
        </div>
        <StatusChip tone="info" icon="school">
          {workspace.courseCode}
        </StatusChip>
      </div>
      {workspace.previousWorkspaceId && (
        <p className="mt-2 flex items-center gap-1 text-body text-info-dark">
          <Icon name="history" size={20} className="text-info" />
          Tiếp tục từ EXE101
        </p>
      )}
      {people.length > 0 && (
        <ul className="mt-3 flex flex-wrap gap-x-4 gap-y-2" aria-label="Thành viên và cố vấn">
          {people.map((p) => (
            <li key={`${p.personType}-${p.personId}`} className="flex items-center gap-2">
              <Avatar name={p.displayName} isMentor={p.role === 'MENTOR'} />
              <span className="text-body">{p.displayName}</span>
              <RoleChip role={p.role} />
            </li>
          ))}
        </ul>
      )}
    </Card>
  )
}

/** Thẻ "Tóm tắt cho mentor" (DESIGN.md 6.3) với bộ đếm buổi mentor. */
function MentorBrief() {
  const { workspace } = useWorkspaceContext()
  const done = workspace.mentorSessionsDone ?? 0
  const expected = workspace.expectedMentorSessions ?? 3
  return (
    <Card className="bg-primary-container shadow-none">
      <div className="flex flex-wrap items-center gap-3">
        <p className="flex-1 text-subtitle font-semibold text-primary-dark">{workspace.projectName ?? workspace.teamName}</p>
        {done >= expected ? (
          <StatusChip tone="success">Đã đủ {expected} buổi</StatusChip>
        ) : (
          <span
            className="inline-flex h-7 items-center gap-2 rounded-full bg-info-container px-3 font-medium text-info-dark"
            aria-label={`Buổi mentor ${done} trên ${expected}`}
          >
            Buổi mentor {done}/{expected}
            <span className="flex gap-1" aria-hidden>
              {Array.from({ length: expected }, (_, i) => (
                <span key={i} className={clsx('size-2 rounded-full', i < done ? 'bg-primary' : 'border border-border-strong')} />
              ))}
            </span>
          </span>
        )}
      </div>
    </Card>
  )
}

function StatCard({ label, value, loading, to }: { label: string; value: number; loading?: boolean; to?: string }) {
  const content = (
    <>
      <span className="text-body text-text-secondary">{label}</span>
      {loading ? <Skeleton className="mt-1 h-8 w-12" /> : <span className="text-display font-semibold tabular-nums">{value}</span>}
    </>
  )
  const className = 'flex h-full flex-col justify-between gap-2 rounded-lg bg-surface p-4 shadow-elevation-1'
  return to ? (
    <Link to={to} className={clsx(className, 'hover:bg-primary-container/40')}>
      {content}
    </Link>
  ) : (
    <div className={className}>{content}</div>
  )
}

/** Lối tắt: Tin nhắn (C02) và Lịch họp (C07) thuộc module khác nhưng cùng nằm dưới đường dẫn dự án. */
function Shortcuts() {
  const { basePath } = useWorkspaceContext()
  const items = [
    { to: `${basePath}/tin-nhan`, label: 'Tin nhắn', icon: 'forum' },
    { to: `${basePath}/viec`, label: 'Việc', icon: 'task_alt' },
    { to: `${basePath}/file`, label: 'File', icon: 'folder' },
    { to: `${basePath}/lich-hop`, label: 'Lịch họp', icon: 'event' },
  ]
  return (
    <nav aria-label="Lối tắt" className="grid grid-cols-4 gap-3">
      {items.map((item) => (
        <Link
          key={item.to}
          to={item.to}
          className="flex min-h-20 flex-col items-center justify-center gap-1 rounded-lg bg-surface font-medium shadow-elevation-1 hover:bg-primary-container/40"
        >
          <Icon name={item.icon} className="text-primary" />
          {item.label}
        </Link>
      ))}
    </nav>
  )
}

function RecentActivity() {
  const { workspace } = useWorkspaceContext()
  const items = (workspace.recentActivity ?? []).slice(0, 5)
  const icons = { TASK: 'task_alt', FILE: 'description', MEETING: 'event', QUESTION: 'help' } as const
  if (items.length === 0) {
    return <p className="text-body text-text-secondary">Chưa có cập nhật mới.</p>
  }
  return (
    <ul className="flex flex-col">
      {items.map((item, i) => (
        <li key={i} className="flex min-h-12 items-center gap-3 border-b border-border py-2 last:border-none">
          <Icon name={icons[item.kind]} className="text-text-secondary" />
          <span className="min-w-0 flex-1 truncate">{item.title}</span>
          <time dateTime={item.at} title={formatDateTime(item.at)} className="text-caption text-text-secondary">
            {formatRelative(item.at)}
          </time>
        </li>
      ))}
    </ul>
  )
}
