import { useEffect, useRef, type ReactNode } from 'react'
import clsx from 'clsx'
import { Icon } from '@/shared/components/ui'
import type { PersonRef, WorkspaceRole } from './api'
import { initials } from './format'

/** Avatar chữ (DESIGN.md mục 5): chữ Primary Dark trên nền Primary Border; mentor có viền Secondary. */
export function Avatar({ name, size = 32, isMentor = false }: { name: string; size?: 24 | 32 | 40; isMentor?: boolean }) {
  return (
    <span
      title={name}
      aria-hidden
      className={clsx(
        'inline-grid shrink-0 place-items-center rounded-full bg-primary-border font-semibold text-primary-dark',
        size === 24 ? 'size-6 text-[10px]' : size === 32 ? 'size-8 text-caption' : 'size-10 text-body',
        isMentor && 'ring-2 ring-secondary',
      )}
    >
      {initials(name)}
    </span>
  )
}

/** Avatar xếp chồng 24 px (người làm việc, thành viên); quá `max` thì hiện "+n". Đọc được tên bằng trình đọc màn hình. */
export function AvatarStack({ people, max = 4, label }: { people: PersonRef[]; max?: number; label?: string }) {
  if (people.length === 0) return null
  const shown = people.slice(0, max)
  return (
    <span className="inline-flex items-center" aria-label={label ?? people.map((p) => p.displayName).join(', ')} role="img">
      {shown.map((p, i) => (
        <span key={p.personId} className={clsx('rounded-full ring-2 ring-surface', i > 0 && '-ml-2')}>
          <Avatar name={p.displayName} size={24} isMentor={p.role === 'MENTOR'} />
        </span>
      ))}
      {people.length > max && <span className="ml-1 text-caption text-text-secondary">+{people.length - max}</span>}
    </span>
  )
}

const roleLabels: Partial<Record<WorkspaceRole, string>> = {
  LEADER: 'Trưởng nhóm',
  INSTRUCTOR: 'Giảng viên',
  MENTOR: 'Mentor',
}

/** Nhãn vai trò cạnh tên (DESIGN.md 6.3): "Trưởng nhóm", "Giảng viên", "Mentor". Thành viên thường không có nhãn. */
export function RoleChip({ role }: { role?: WorkspaceRole | null }) {
  const label = role ? roleLabels[role] : undefined
  if (!label) return null
  return <span className="rounded-full bg-neutral-container px-2 py-0.5 text-caption font-semibold text-text-secondary">{label}</span>
}

/** Thẻ nền trắng bo 20, bóng mờ (thẻ trên nền Background – DESIGN.md 4.3). */
export function Card({ children, className }: { children: ReactNode; className?: string }) {
  return <section className={clsx('rounded-lg bg-surface p-4 shadow-elevation-1', className)}>{children}</section>
}

const openDrawers: symbol[] = []

/**
 * Drawer phải 480 px (bottom sheet của mobile trên web – DESIGN.md 8.2): Esc đóng, focus vào drawer khi mở,
 * trả focus về phần tử đã mở khi đóng.
 */
export function Drawer({ open, onClose, title, children, footer }: {
  open: boolean
  onClose: () => void
  title: string
  children: ReactNode
  footer?: ReactNode
}) {
  const ref = useRef<HTMLDivElement>(null)
  // Giữ onClose mới nhất mà không chạy lại effect (chạy lại sẽ cướp focus khỏi ô đang nhập).
  const onCloseRef = useRef(onClose)
  onCloseRef.current = onClose
  useEffect(() => {
    if (!open) return
    const opener = document.activeElement as HTMLElement | null
    const token = Symbol('drawer')
    openDrawers.push(token)
    ref.current?.focus()
    // Drawer lồng nhau (sửa việc trong chi tiết việc): Esc chỉ đóng drawer trên cùng.
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && openDrawers.at(-1) === token && onCloseRef.current()
    window.addEventListener('keydown', onKey)
    return () => {
      window.removeEventListener('keydown', onKey)
      openDrawers.splice(openDrawers.indexOf(token), 1)
      opener?.focus?.()
    }
  }, [open])
  if (!open) return null
  return (
    <div className="fixed inset-0 z-40 flex justify-end">
      <div className="absolute inset-0 bg-text-primary/40" onClick={onClose} aria-hidden />
      <div
        ref={ref}
        role="dialog"
        aria-modal="true"
        aria-label={title}
        tabIndex={-1}
        className="relative flex h-full w-full max-w-[480px] flex-col bg-surface shadow-elevation-2 outline-none"
      >
        <header className="flex h-16 items-center gap-2 border-b border-border px-6">
          <h2 className="min-w-0 flex-1 truncate text-title font-semibold">{title}</h2>
          <button type="button" onClick={onClose} className="grid size-10 place-items-center rounded-md hover:bg-background" aria-label="Đóng">
            <Icon name="close" />
          </button>
        </header>
        <div className="flex-1 overflow-y-auto px-6 py-4">{children}</div>
        {footer && <footer className="flex justify-end gap-2 border-t border-border px-6 py-4">{footer}</footer>}
      </div>
    </div>
  )
}
