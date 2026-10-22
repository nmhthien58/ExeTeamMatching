import { Link, NavLink, Outlet, useParams } from 'react-router-dom'
import clsx from 'clsx'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Icon, Skeleton } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { isStudentRole, useCurrentWorkspaceId, useWorkspace } from './api'
import { WorkspaceContext, type WorkspaceContextValue } from './context'

/**
 * Khung chung của C01, C04–C06: xác định workspace đang xem (`:workspaceId` với cố vấn, workspace của mình với sinh viên),
 * tải tổng quan một lần, xử lý trạng thái đang tải / lỗi / chưa có nhóm, và hiện thanh tab con.
 */
export function WorkspaceLayout({ basePath }: { basePath: string }) {
  const { workspaceId: routeId } = useParams()
  const current = useCurrentWorkspaceId()
  const workspace = useWorkspace(current.workspaceId)
  const base = routeId ? `${basePath}/${routeId}` : basePath

  if (current.noWorkspace) {
    return (
      <>
        <PageHeader title="Dự án" />
        <EmptyState
          icon="group_off"
          title="Bạn chưa có nhóm"
          description="Dự án mở khi bạn đã có nhóm. Xem nhóm của bạn hoặc chờ hệ thống ghép tự động."
          action={
            <Link to="/sv/nhom" className="inline-flex min-h-10 items-center rounded-md bg-primary px-4 font-medium text-white">
              Mở nhóm của tôi
            </Link>
          }
        />
      </>
    )
  }
  if (current.error || workspace.error) {
    return (
      <>
        <PageHeader title="Dự án" />
        <ErrorState error={current.error ?? workspace.error} onRetry={() => void workspace.refetch()} />
      </>
    )
  }
  if (!workspace.data) return <WorkspaceSkeleton />

  const value: WorkspaceContextValue = { workspace: workspace.data, basePath: base, canEdit: isStudentRole(workspace.data.myRole) }
  return (
    <WorkspaceContext.Provider value={value}>
      <WorkspaceTabs basePath={base} />
      <Outlet />
    </WorkspaceContext.Provider>
  )
}

/** Tab con của dự án; Tin nhắn và Lịch họp do module khác làm (C02, C07) nên mở từ lối tắt ở C01. */
function WorkspaceTabs({ basePath }: { basePath: string }) {
  const tabs = [
    { to: basePath, label: 'Tổng quan', icon: 'dashboard', end: true },
    { to: `${basePath}/viec`, label: 'Việc', icon: 'task_alt', end: false },
    { to: `${basePath}/file`, label: 'File', icon: 'folder', end: false },
  ]
  return (
    <nav aria-label="Mục của dự án" className="mb-4 flex gap-2 overflow-x-auto pt-1 [scrollbar-width:none]">
      {tabs.map((tab) => (
        <NavLink
          key={tab.to}
          to={tab.to}
          end={tab.end}
          className={({ isActive }) =>
            clsx(
              'inline-flex min-h-10 shrink-0 items-center gap-2 rounded-full px-4 font-medium',
              isActive ? 'bg-primary text-white' : 'border border-border-strong bg-surface text-text-primary hover:bg-background',
            )
          }
        >
          {({ isActive }) => (
            <>
              <Icon name={isActive ? 'check' : tab.icon} size={20} />
              {tab.label}
            </>
          )}
        </NavLink>
      ))}
    </nav>
  )
}

function WorkspaceSkeleton() {
  return (
    <div role="status" aria-label="Đang tải dự án" className="flex flex-col gap-4 pt-1">
      <PageHeader title="Dự án" />
      <Skeleton className="h-10 w-72 rounded-full" />
      <Skeleton className="h-28 w-full rounded-lg" />
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {Array.from({ length: 4 }, (_, i) => (
          <Skeleton key={i} className="h-24 rounded-lg" />
        ))}
      </div>
    </div>
  )
}
