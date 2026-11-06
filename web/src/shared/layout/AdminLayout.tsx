import { Outlet, useLocation } from 'react-router-dom'
import { AccountMenu } from './AccountMenu'
import { adminNav, type NavItem } from './navigation'
import { PageHeaderProvider, usePageHeaderState } from './PageHeader'
import { BrandBlock, GlassHeader, SideLink } from './ShellParts'
import { todayLabel } from '@/shared/lib/today'
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
          <BrandBlock subtitle="Bộ môn EXE" />
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

function Header() {
  const { title, action } = usePageHeaderState()
  return <GlassHeader title={title} subtitle={todayLabel()} action={action} account={<AccountMenu />} />
}
