import { useEffect, useState } from 'react'
import { NavLink, Outlet } from 'react-router-dom'
import clsx from 'clsx'
import { Icon } from '@/shared/components/ui'
import type { Shell } from '@/shared/auth/AuthProvider'
import { usePhases, useIsTeamFormation, useMyCourse } from '@/shared/phase/usePhase'
import { AccountMenu } from './AccountMenu'
import { Logo } from './AdminLayout'
import { roleNav, type NavItem } from './navigation'
import { PageHeaderProvider, usePageHeaderState } from './PageHeader'

type Layout = 'tabbar' | 'rail' | 'sidebar'

/** Ba mức bề rộng (DESIGN.md 8.2): &lt; 600 tab bar dưới, 600–1023 rail 80 px, ≥ 1024 sidebar 240 px. */
export function useLayoutMode(): Layout {
  const get = (): Layout => (window.innerWidth < 600 ? 'tabbar' : window.innerWidth < 1024 ? 'rail' : 'sidebar')
  const [mode, setMode] = useState<Layout>(get)
  useEffect(() => {
    const onResize = () => setMode(get())
    window.addEventListener('resize', onResize)
    return () => window.removeEventListener('resize', onResize)
  }, [])
  return mode
}

/** Bối cảnh giai đoạn cho tab bar: Khám phá ↔ Dự án, tab Đề tài của giảng viên, cửa sổ mentor. */
function useShellContext(shell: Exclude<Shell, 'admin'>) {
  const myCourse = useMyCourse()
  const student = useIsTeamFormation(shell === 'student' ? myCourse.data : undefined)
  const phases = usePhases()
  const anyFormation = phases.data ? phases.data.some((p) => p.phase === 'TEAM_FORMATION') : true
  const mentoringStart = phases.data?.map((p) => p.mentoringStartsOn).filter(Boolean).sort()[0]
  return {
    isTeamFormation: shell === 'student' ? student.isTeamFormation : anyFormation,
    isMentoringWindow: mentoringStart ? new Date(mentoringStart) <= new Date() : false,
  }
}

/**
 * FE-32 – khung web cho Sinh viên, Giảng viên, Mentor: cùng mã màn hình với mobile, chỉ đổi bố cục theo bề rộng.
 */
export function RoleShell({ shell }: { shell: Exclude<Shell, 'admin'> }) {
  const items = roleNav(shell, useShellContext(shell))
  const mode = useLayoutMode()

  return (
    <PageHeaderProvider>
      <div className={clsx('flex min-h-screen bg-background', mode === 'tabbar' && 'flex-col')}>
        {mode !== 'tabbar' && <SideNav items={items} compact={mode === 'rail'} />}
        <div className="flex min-w-0 flex-1 flex-col">
          <TopBar compact={mode === 'tabbar'} />
          <main
            id="main"
            className={clsx(
              'mx-auto w-full flex-1 px-4 pb-8',
              mode === 'rail' && 'max-w-[720px]',
              mode === 'sidebar' && 'max-w-[1200px] px-6',
              mode === 'tabbar' && 'pb-24',
            )}
          >
            <Outlet />
          </main>
        </div>
        {mode === 'tabbar' && <TabBar items={items} />}
      </div>
    </PageHeaderProvider>
  )
}

function TopBar({ compact }: { compact: boolean }) {
  const { title, action } = usePageHeaderState()
  return (
    <header className="sticky top-0 z-20 flex h-16 items-center gap-3 bg-background/95 px-4 backdrop-blur">
      <h1 className={clsx('min-w-0 flex-1 truncate font-semibold', compact ? 'text-title' : 'text-display')}>{title}</h1>
      {action}
      <AccountMenu compact={compact} />
    </header>
  )
}

function SideNav({ items, compact }: { items: NavItem[]; compact: boolean }) {
  return (
    <aside className={clsx('sticky top-0 flex h-screen shrink-0 flex-col border-r border-border bg-surface', compact ? 'w-20' : 'w-60')}>
      <Logo compact={compact} />
      <nav aria-label="Điều hướng chính" className={clsx('flex-1', compact ? 'px-2' : 'px-3')}>
        <ul className="flex flex-col gap-1">
          {items.map((item) => (
            <li key={item.to}>
              <NavLink
                to={item.to}
                end={item.end}
                className={({ isActive }) =>
                  clsx(
                    'font-medium',
                    compact
                      ? 'flex min-h-14 flex-col items-center justify-center gap-1 rounded-md text-caption'
                      : 'flex min-h-10 items-center gap-3 rounded-md px-3',
                    isActive ? 'bg-primary-container text-primary-dark' : 'text-text-secondary hover:bg-background hover:text-text-primary',
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    <Icon name={item.icon} filled={isActive} className={isActive ? 'text-primary' : undefined} />
                    <span className="truncate">{item.label}</span>
                  </>
                )}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>
    </aside>
  )
}

/** Tab bar dưới như mobile: viên thuốc Primary Container cho mục đang chọn, nhãn Caption Bold, vùng chạm 48. */
function TabBar({ items }: { items: NavItem[] }) {
  return (
    <nav aria-label="Điều hướng chính" className="fixed inset-x-0 bottom-0 z-30 border-t border-border bg-surface pb-[env(safe-area-inset-bottom)]">
      <ul className="flex">
        {items.map((item) => (
          <li key={item.to} className="flex-1">
            <NavLink to={item.to} end={item.end} className="flex min-h-16 flex-col items-center justify-center gap-1">
              {({ isActive }) => (
                <>
                  <span className={clsx('grid h-8 w-16 place-items-center rounded-full', isActive && 'bg-primary-container')}>
                    <Icon name={item.icon} filled={isActive} className={isActive ? 'text-primary' : 'text-text-secondary'} />
                  </span>
                  <span className={clsx('text-caption font-semibold', isActive ? 'text-primary-dark' : 'text-text-secondary')}>{item.label}</span>
                </>
              )}
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  )
}
