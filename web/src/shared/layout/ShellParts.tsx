import type { ReactNode } from 'react'
import { NavLink } from 'react-router-dom'
import clsx from 'clsx'
import { Icon } from '@/shared/components/ui'
import type { NavItem } from './navigation'

/**
 * Phần dùng chung của khung Bộ môn (AdminLayout) và khung Sinh viên / Giảng viên / Mentor (RoleShell) – FE-49, FE-51:
 * sidebar nền mực, mục đang chọn có vạch cam, header kính mờ.
 */

/** Khối logo đầu sidebar; `compact` cho rail 80 px. */
export function BrandBlock({ subtitle, compact = false }: { subtitle: string; compact?: boolean }) {
  return (
    <div className={clsx('flex h-[72px] items-center gap-3', compact ? 'justify-center' : 'px-4')}>
      <span aria-hidden className="grid size-10 shrink-0 place-items-center rounded-[12px] bg-gradient-to-br from-brand-orange to-primary shadow-glow">
        <Icon name="groups" filled className="text-white" />
      </span>
      {!compact && (
        <span className="flex min-w-0 flex-col leading-tight">
          <span className="truncate text-[15px] font-semibold tracking-[-0.01em] text-white">EXE Team Matching</span>
          <span className="truncate text-caption text-ink-muted">{subtitle}</span>
        </span>
      )}
    </div>
  )
}

/** Mục điều hướng trên nền mực. `icon` thay icon mặc định (vd. chuông thông báo có số chưa đọc). */
export function SideLink({ item, compact = false, icon }: { item: NavItem; compact?: boolean; icon?: (active: boolean) => ReactNode }) {
  return (
    <NavLink
      to={item.to}
      end={item.end}
      title={compact ? item.label : undefined}
      className={({ isActive }) =>
        clsx(
          'group relative flex rounded-md font-medium transition-colors duration-200',
          compact ? 'min-h-14 flex-col items-center justify-center gap-1 text-caption' : 'min-h-10 items-center gap-3 px-3 text-body',
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
          <span className={clsx('transition-colors', isActive ? 'text-brand-orange' : 'text-ink-muted group-hover:text-ink-text')}>
            {icon ? icon(isActive) : <Icon name={item.icon} size={20} filled={isActive} />}
          </span>
          <span className="truncate">{item.label}</span>
        </>
      )}
    </NavLink>
  )
}

/** Header dính trên cùng, nền kính mờ: tiêu đề trang, dòng phụ (ngày hoặc bối cảnh), nút của trang, menu tài khoản. */
export function GlassHeader({ title, subtitle, action, account, compact = false }: {
  title: ReactNode
  subtitle?: ReactNode
  action?: ReactNode
  account: ReactNode
  compact?: boolean
}) {
  return (
    <header
      className={clsx(
        'sticky top-0 z-20 flex items-center gap-4 border-b border-border/60 bg-background/70 backdrop-blur-xl backdrop-saturate-150',
        compact ? 'h-16 px-4' : 'h-[72px] px-8',
      )}
    >
      <div className="min-w-0 flex-1">
        <h1 className={clsx('truncate font-semibold tracking-[-0.01em]', compact ? 'text-title' : 'text-[22px] leading-7')}>{title}</h1>
        {subtitle && !compact && <p className="truncate text-caption capitalize text-text-secondary">{subtitle}</p>}
      </div>
      {action}
      {account}
    </header>
  )
}
