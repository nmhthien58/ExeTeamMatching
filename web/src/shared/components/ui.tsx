import { forwardRef, useEffect, useRef, type ButtonHTMLAttributes, type ReactNode } from 'react'
import clsx from 'clsx'

/** Icon Material Symbols Rounded (DESIGN.md mục 5). Icon trang trí cạnh chữ cùng nghĩa: để `label` trống. */
export function Icon({
  name,
  filled = false,
  size = 24,
  label,
  className,
}: {
  name: string
  filled?: boolean
  size?: 20 | 24 | 48
  label?: string
  className?: string
}) {
  return (
    <span
      className={clsx('material-symbols-rounded shrink-0', filled && 'icon-filled', className)}
      style={{ fontSize: size, width: size, height: size, lineHeight: 1 }}
      aria-hidden={label ? undefined : true}
      aria-label={label}
      role={label ? 'img' : undefined}
    >
      {name}
    </span>
  )
}

type ButtonVariant = 'primary' | 'outline' | 'text' | 'danger'

const buttonStyles: Record<ButtonVariant, string> = {
  primary: 'bg-primary text-white hover:bg-primary-dark',
  outline: 'border border-primary bg-surface text-primary-dark hover:bg-primary-container',
  text: 'text-primary-dark hover:bg-primary-container',
  danger: 'bg-error text-white hover:brightness-90',
}

/**
 * Nút web cao 40 px (DESIGN.md 4.4). Mỗi màn hình một nút `primary`. `loading`: thay chữ bằng vòng xoay 20 px,
 * giữ chiều rộng, khóa bấm lần hai.
 */
export const Button = forwardRef<
  HTMLButtonElement,
  ButtonHTMLAttributes<HTMLButtonElement> & { variant?: ButtonVariant; icon?: string; loading?: boolean }
>(function Button({ variant = 'primary', icon, loading = false, className, children, disabled, ...rest }, ref) {
  return (
    <button
      ref={ref}
      type="button"
      {...rest}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      className={clsx(
        'relative inline-flex min-h-10 items-center justify-center gap-2 rounded-md px-4 text-body font-medium transition-colors',
        'disabled:cursor-not-allowed disabled:bg-border disabled:text-text-disabled disabled:border-transparent',
        buttonStyles[variant],
        className,
      )}
    >
      <span className={clsx('inline-flex items-center gap-2', loading && 'invisible')}>
        {icon && <Icon name={icon} size={20} />}
        {children}
      </span>
      {loading && <Spinner className="absolute" />}
    </button>
  )
})

export function Spinner({ className }: { className?: string }) {
  return (
    <span
      className={clsx('inline-block size-5 animate-spin rounded-full border-2 border-current border-t-transparent', className)}
      role="status"
      aria-label="Đang xử lý"
    />
  )
}

/** Năm nhóm màu trạng thái, mỗi nhóm một icon cố định (DESIGN.md mục 7). */
export type StatusTone = 'warning' | 'success' | 'error' | 'info' | 'neutral'

const toneStyles: Record<StatusTone, { cls: string; icon: string }> = {
  warning: { cls: 'bg-warning-container text-warning', icon: 'schedule' },
  success: { cls: 'bg-success-container text-success', icon: 'check_circle' },
  error: { cls: 'bg-error-container text-error', icon: 'cancel' },
  info: { cls: 'bg-info-container text-info-dark', icon: 'verified' },
  neutral: { cls: 'bg-neutral-container text-text-secondary', icon: 'remove_circle' },
}

/** Chip trạng thái: luôn có chữ, icon đứng trước; không bao giờ bị cắt chữ. */
export function StatusChip({ tone, children, icon }: { tone: StatusTone; children: ReactNode; icon?: string }) {
  const t = toneStyles[tone]
  return (
    <span className={clsx('inline-flex h-7 items-center gap-1 whitespace-nowrap rounded-full px-3 text-body font-medium', t.cls)}>
      <Icon name={icon ?? t.icon} size={20} />
      {children}
    </span>
  )
}

const bannerStyles: Record<'info' | 'success' | 'warning' | 'error', { cls: string; icon: string }> = {
  info: { cls: 'bg-info-container text-info-dark', icon: 'info' },
  success: { cls: 'bg-success-container text-success', icon: 'check_circle' },
  warning: { cls: 'bg-warning-container text-warning', icon: 'warning' },
  error: { cls: 'bg-error-container text-error', icon: 'error' },
}

/** Banner trong trang (lỗi tải, cảnh báo). Lỗi dùng `role="alert"`. */
export function Banner({
  tone = 'info',
  title,
  children,
  action,
}: {
  tone?: keyof typeof bannerStyles
  title: string
  children?: ReactNode
  action?: ReactNode
}) {
  const t = bannerStyles[tone]
  return (
    <div role={tone === 'error' ? 'alert' : 'status'} className={clsx('flex items-start gap-3 rounded-md px-4 py-3', t.cls)}>
      <Icon name={t.icon} />
      <div className="min-w-0 flex-1">
        <p className="font-semibold">{title}</p>
        {children && <div className="text-body text-text-primary">{children}</div>}
      </div>
      {action}
    </div>
  )
}

/** Skeleton đúng hình nội dung; không nhấp nháy khi người dùng bật giảm chuyển động. */
export function Skeleton({ className }: { className?: string }) {
  return <span aria-hidden className={clsx('block animate-pulse rounded-sm bg-neutral-container', className)} />
}

/**
 * Dialog dùng thẻ `<dialog>` gốc: Esc đóng, focus bị giữ trong dialog, trả focus về nút đã mở (DESIGN.md 8.1).
 */
export function Dialog({
  open,
  onClose,
  title,
  children,
  actions,
}: {
  open: boolean
  onClose: () => void
  title: string
  children: ReactNode
  actions: ReactNode
}) {
  const ref = useRef<HTMLDialogElement>(null)
  const previousFocusRef = useRef<HTMLElement | null>(null)
  useEffect(() => {
    const dialog = ref.current
    if (!dialog) return
    if (open && !dialog.open) {
      previousFocusRef.current = (document.activeElement as HTMLElement) ?? null
      dialog.showModal?.()
    }
    if (!open && dialog.open) {
      dialog.close()
      previousFocusRef.current?.focus?.()
    }
  }, [open])
  return (
    <dialog
      ref={ref}
      onClose={() => {
        onClose()
        previousFocusRef.current?.focus?.()
      }}
      aria-labelledby="dialog-title"
      className="m-auto w-[min(480px,calc(100vw-32px))] rounded-[16px] bg-surface p-6 text-text-primary shadow-elevation-2 backdrop:bg-text-primary/40"
    >
      <h2 id="dialog-title" className="text-title font-semibold">
        {title}
      </h2>
      <div className="mt-2 text-body text-text-secondary">{children}</div>
      <div className="mt-6 flex justify-end gap-2">{actions}</div>
    </dialog>
  )
}
