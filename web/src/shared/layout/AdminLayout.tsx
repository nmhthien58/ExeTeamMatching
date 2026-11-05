import { NavLink, Outlet, useLocation } from 'react-router-dom'
import clsx from 'clsx'
import { Icon } from '@/shared/components/ui'
import { AccountMenu } from './AccountMenu'
import { adminNav, type NavItem } from './navigation'
import { PageHeaderProvider, usePageHeaderState } from './PageHeader'
import { RemoteNotice } from '@/shared/firebase/RemoteNotice'

/**
 * W25 – khung trang web Bộ môn. Bản FE-49 (05/11/2026, xem docs/design-traceability.md): sidebar nền mực chia nhóm
 * (Học vụ · Nhóm và ghép · Vận hành), mục đang chọn nền cam nhạt + vạch cam; header kính mờ dính trên cùng;
 * nội dung trang xuất hiện mờ dần + trượt nhẹ khi đổi route (tắt khi giảm chuyển động).
 */
export function AdminLayout() {
  const { pathname } = useLocation()
  return (
    <PageHeaderProvider>
      <div className="flex min-h-screen">
        <a href="#main" className="sr-only focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus:z-50 focus:rounded-sm focus:bg-surface focus:p-2">
          Bỏ qua điều hướng
        </a>
        <aside className="sticky top-0 flex h-screen w-64 shrink-0 flex-col bg-ink text-ink-text">
          <BrandBlock />
          <nav aria-label="Điều hướng chính" className="ink-scroll flex-1 overflow-y-auto px-3 pb-4">
            {groupNav(adminNav).map(({ group, items }) => (
              <div key={group ?? 'main'} className="mt-4 first:mt-1">
                {group && <p className="px-3 pb-1.5 text-[11px] font-semibold uppercase tracking-[0.08em] text-ink-muted">{group}</p>}
                <ul className="flex flex-col gap-0.5">
                  {items.map((item) => (
                    <li key={item.to}>
                      <SideLink item={item} />
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </nav>
          <div className="border-t border-ink-border px-5 py-4 text-caption text-ink-muted">
            Bộ môn EXE · Đại học FPT
          </div>
        </aside>
        <div className="flex min-w-0 flex-1 flex-col">
          <Header />
          <main id="main" className="mx-auto w-full max-w-[1440px] flex-1 px-8 pb-10 pt-6">
            <RemoteNotice />
            {/* key theo đường dẫn: mỗi lần đổi trang, nội dung chạy lại animation vào trang */}
            <div key={pathname} className="animate-page-in">
              <Outlet />
            </div>
          </main>
        </div>
      </div>
    </PageHeaderProvider>
  )
}

function groupNav(items: NavItem[]) {
  const groups: { group?: string; items: NavItem[] }[] = []
  for (const item of items) {
    const last = groups[groups.length - 1]
    if (last && last.group === item.group) last.items.push(item)
    else groups.push({ group: item.group, items: [item] })
  }
  return groups
}

function SideLink({ item }: { item: NavItem }) {
  return (
    <NavLink
      to={item.to}
      end={item.end}
      className={({ isActive }) =>
        clsx(
          'group relative flex min-h-10 items-center gap-3 rounded-md px-3 text-body font-medium transition-colors duration-200',
          isActive ? 'bg-primary/15 text-white' : 'text-ink-text hover:bg-white/5 hover:text-white',
        )
      }
    >
      {({ isActive }) => (
        <>
          {/* vạch cam bên trái mục đang chọn */}
          <span
            aria-hidden
            className={clsx(
              'absolute left-0 top-2 bottom-2 w-[3px] rounded-full bg-brand-orange transition-all duration-300',
              isActive ? 'opacity-100' : 'scale-y-0 opacity-0',
            )}
          />
          <Icon
            name={item.icon}
            size={20}
            filled={isActive}
            className={clsx('transition-colors', isActive ? 'text-brand-orange' : 'text-ink-muted group-hover:text-ink-text')}
          />
          <span className="truncate">{item.label}</span>
        </>
      )}
    </NavLink>
  )
}

function BrandBlock() {
  return (
    <div className="flex h-[72px] items-center gap-3 px-5">
      <span aria-hidden className="grid size-10 place-items-center rounded-[12px] bg-gradient-to-br from-brand-orange to-primary shadow-glow">
        <Icon name="groups" filled className="text-white" />
      </span>
      <span className="flex flex-col leading-tight">
        <span className="text-subtitle font-semibold text-white">EXE Team Matching</span>
        <span className="text-caption text-ink-muted">Bộ môn EXE</span>
      </span>
    </div>
  )
}

function Header() {
  const { title, action } = usePageHeaderState()
  const today = new Date().toLocaleDateString('vi-VN', { weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric' })
  return (
    <header className="sticky top-0 z-20 flex h-[72px] items-center gap-4 border-b border-border/60 bg-background/70 px-8 backdrop-blur-xl backdrop-saturate-150">
      <div className="min-w-0 flex-1">
        <h1 className="truncate text-[22px] font-semibold leading-7 tracking-[-0.01em]">{title}</h1>
        <p className="text-caption capitalize text-text-secondary">{today}</p>
      </div>
      {action}
      <AccountMenu />
    </header>
  )
}

/** Logo nhỏ cho khung sinh viên / giảng viên / mentor (RoleShell). */
export function Logo({ compact = false }: { compact?: boolean }) {
  return (
    <div className={clsx('flex h-16 items-center gap-2', compact ? 'justify-center' : 'px-5')}>
      <span aria-hidden className="grid size-8 place-items-center rounded-md bg-gradient-to-br from-brand-orange to-primary">
        <Icon name="groups" size={20} filled className="text-white" />
      </span>
      {!compact && <span className="text-subtitle font-semibold">Team Matching</span>}
    </div>
  )
}
