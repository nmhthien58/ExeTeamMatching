import { NavLink, Outlet } from 'react-router-dom'
import clsx from 'clsx'
import { Icon } from '@/shared/components/ui'
import { AccountMenu } from './AccountMenu'
import { adminNav } from './navigation'
import { PageHeaderProvider, usePageHeaderState } from './PageHeader'
import { RemoteNotice } from '@/shared/firebase/RemoteNotice'

/**
 * W25 – khung trang web quản trị (DESIGN.md 8.1): sidebar 240 px nền trắng, mục đang chọn nền Primary Container
 * chữ Primary Dark, icon Primary tô đặc; header 64 px với tiêu đề Display, nút hành động chính, menu tài khoản.
 */
export function AdminLayout() {
  return (
    <PageHeaderProvider>
      <div className="flex min-h-screen bg-background">
        <a href="#main" className="sr-only focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus:z-50 focus:rounded-sm focus:bg-surface focus:p-2">
          Bỏ qua điều hướng
        </a>
        <aside className="sticky top-0 flex h-screen w-60 shrink-0 flex-col border-r border-border bg-surface">
          <Logo />
          <nav aria-label="Điều hướng chính" className="flex-1 overflow-y-auto px-3 pb-4">
            <ul className="flex flex-col gap-1">
              {adminNav.map((item) => (
                <li key={item.to}>
                  <NavLink
                    to={item.to}
                    end={item.end}
                    className={({ isActive }) =>
                      clsx(
                        'flex min-h-10 items-center gap-3 rounded-md px-3 font-medium',
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
        <div className="flex min-w-0 flex-1 flex-col">
          <Header />
          <main id="main" className="mx-auto w-full max-w-[1440px] flex-1 px-6 pb-8">
            <RemoteNotice />
            <Outlet />
          </main>
        </div>
      </div>
    </PageHeaderProvider>
  )
}

function Header() {
  const { title, action } = usePageHeaderState()
  return (
    <header className="sticky top-0 z-20 flex h-16 items-center gap-4 bg-background/95 px-6 backdrop-blur">
      <h1 className="min-w-0 flex-1 truncate text-display font-semibold">{title}</h1>
      {action}
      <AccountMenu />
    </header>
  )
}

export function Logo({ compact = false }: { compact?: boolean }) {
  return (
    <div className={clsx('flex h-16 items-center gap-2', compact ? 'justify-center' : 'px-5')}>
      <span aria-hidden className="grid size-8 place-items-center rounded-md bg-brand-orange text-caption font-semibold text-text-primary">
        EXE
      </span>
      {!compact && <span className="text-subtitle font-semibold">Team Matching</span>}
    </div>
  )
}
