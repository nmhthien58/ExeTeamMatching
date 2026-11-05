import { useEffect, useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import clsx from 'clsx'
import { Icon } from '@/shared/components/ui'
import type { Shell } from '@/shared/auth/AuthProvider'
import { usePhases, useIsTeamFormation, useMyCourse } from '@/shared/phase/usePhase'
import { AccountMenu } from './AccountMenu'
import { roleNav, type NavItem } from './navigation'
import { PageHeaderProvider, usePageHeaderState } from './PageHeader'
import { BrandBlock, GlassHeader, SideLink } from './ShellParts'
import { todayLabel } from '@/shared/lib/today'
import { NotificationNavIcon } from '@/features/notifications/NotificationNavIcon'
import { RemoteNotice } from '@/shared/firebase/RemoteNotice'

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

const shellName: Record<Exclude<Shell, 'admin'>, string> = { student: 'Sinh viên', lecturer: 'Giảng viên', mentor: 'Mentor' }

/**
 * FE-32 – khung web cho Sinh viên, Giảng viên, Mentor: cùng mã màn hình với mobile, đổi bố cục theo bề rộng.
 * FE-51 (05/11/2026): cùng ngôn ngữ với khung Bộ môn (FE-49) – sidebar / rail nền mực, header kính mờ có ngày,
 * thẻ giai đoạn cuối sidebar, nội dung mờ dần khi đổi trang (tắt khi giảm chuyển động).
 */
export function RoleShell({ shell }: { shell: Exclude<Shell, 'admin'> }) {
  const items = roleNav(shell, useShellContext(shell))
  const mode = useLayoutMode()
  const { pathname } = useLocation()

  return (
    <PageHeaderProvider>
      <div className={clsx('flex min-h-screen', mode === 'tabbar' && 'flex-col')}>
        <a href="#main" className="sr-only focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus:z-50 focus:rounded-sm focus:bg-surface focus:p-2">
          Bỏ qua điều hướng
        </a>
        {mode !== 'tabbar' && <SideNav shell={shell} items={items} compact={mode === 'rail'} />}
        <div className="flex min-w-0 flex-1 flex-col">
          <TopBar compact={mode === 'tabbar'} />
          <main
            id="main"
            className={clsx(
              'mx-auto w-full flex-1',
              mode === 'tabbar' && 'px-4 pb-24 pt-4',
              mode === 'rail' && 'max-w-[720px] px-6 pb-10 pt-6',
              mode === 'sidebar' && 'max-w-[1200px] px-8 pb-10 pt-6',
            )}
          >
            <RemoteNotice />
            <div key={pathname} className="animate-page-in">
              <Outlet />
            </div>
          </main>
        </div>
        {mode === 'tabbar' && <TabBar items={items} />}
      </div>
    </PageHeaderProvider>
  )
}

function TopBar({ compact }: { compact: boolean }) {
  const { title, action } = usePageHeaderState()
  return <GlassHeader compact={compact} title={title} subtitle={todayLabel()} action={action} account={<AccountMenu compact={compact} />} />
}

function SideNav({ shell, items, compact }: { shell: Exclude<Shell, 'admin'>; items: NavItem[]; compact: boolean }) {
  return (
    <aside className={clsx('sticky top-0 flex h-screen shrink-0 flex-col bg-ink text-ink-text', compact ? 'w-20' : 'w-60')}>
      <BrandBlock subtitle={shellName[shell]} compact={compact} />
      <nav aria-label="Điều hướng chính" className={clsx('ink-scroll mt-2 flex-1 overflow-y-auto', compact ? 'px-2' : 'px-3')}>
        <ul className="flex flex-col gap-0.5">
          {items.map((item) => (
            <li key={item.to}>
              <SideLink
                item={item}
                compact={compact}
                icon={item.screen === 'A06' ? (active) => <NotificationNavIcon filled={active} /> : undefined}
              />
            </li>
          ))}
        </ul>
      </nav>
      {!compact && <PhaseCard shell={shell} />}
    </aside>
  )
}

/** Thẻ cuối sidebar: môn và giai đoạn hiện tại, hạn lập nhóm còn bao lâu (sinh viên); giảng viên / mentor xem giai đoạn chung. */
function PhaseCard({ shell }: { shell: Exclude<Shell, 'admin'> }) {
  const myCourse = useMyCourse()
  const phases = usePhases()
  const phase = shell === 'student' ? phases.data?.find((p) => p.courseCode === myCourse.data) ?? phases.data?.[0] : phases.data?.[0]
  if (!phase) return <div className="border-t border-ink-border px-5 py-4 text-caption text-ink-muted">Đại học FPT</div>
  const forming = phase.phase === 'TEAM_FORMATION'
  const days = phase.teamFormationDeadlineAt ? Math.ceil((new Date(phase.teamFormationDeadlineAt).getTime() - Date.now()) / 86_400_000) : undefined
  return (
    <div className="m-3 rounded-md border border-ink-border bg-white/5 p-4">
      <p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-ink-muted">
        {shell === 'student' ? phase.courseCode : 'Học kỳ hiện tại'}
      </p>
      <p className="mt-1 font-semibold text-white">{forming ? 'Đang lập nhóm' : 'Đang học kỳ'}</p>
      {forming && days !== undefined && days >= 0 && (
        <p className="mt-0.5 text-caption text-ink-text">
          Còn <span className="font-semibold text-brand-orange">{days} ngày</span> tới hạn lập nhóm
        </p>
      )}
    </div>
  )
}

/** Tab bar dưới như mobile: viên thuốc Primary Container cho mục đang chọn, nhãn Caption Bold, vùng chạm 48. */
function TabBar({ items }: { items: NavItem[] }) {
  return (
    <nav aria-label="Điều hướng chính" className="fixed inset-x-0 bottom-0 z-30 border-t border-border/60 bg-surface/85 pb-[env(safe-area-inset-bottom)] backdrop-blur-xl backdrop-saturate-150">
      <ul className="flex">
        {items.map((item) => (
          <li key={item.to} className="flex-1">
            <NavLink to={item.to} end={item.end} className="flex min-h-16 flex-col items-center justify-center gap-1">
              {({ isActive }) => (
                <>
                  <span className={clsx('grid h-8 w-16 place-items-center rounded-full', isActive && 'bg-primary-container')}>
                    {item.screen === 'A06' ? <NotificationNavIcon filled={isActive} /> : <Icon name={item.icon} filled={isActive} className={isActive ? 'text-primary' : 'text-text-secondary'} />}
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
