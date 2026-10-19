import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '@/shared/auth/AuthProvider'
import { Icon } from '@/shared/components/ui'

/** Menu tài khoản: tên, email, Đăng xuất. Esc đóng và trả focus về nút. */
export function AccountMenu({ compact = false }: { compact?: boolean }) {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [open, setOpen] = useState(false)
  const buttonRef = useRef<HTMLButtonElement>(null)
  const menuRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        setOpen(false)
        buttonRef.current?.focus()
      }
    }
    const onClick = (e: MouseEvent) => {
      if (!menuRef.current?.contains(e.target as Node) && !buttonRef.current?.contains(e.target as Node)) setOpen(false)
    }
    window.addEventListener('keydown', onKey)
    window.addEventListener('mousedown', onClick)
    return () => {
      window.removeEventListener('keydown', onKey)
      window.removeEventListener('mousedown', onClick)
    }
  }, [open])

  if (!user) return null
  const initials = user.fullName
    .split(' ')
    .filter(Boolean)
    .slice(-2)
    .map((w) => w[0])
    .join('')
    .toUpperCase()

  return (
    <div className="relative">
      <button
        ref={buttonRef}
        type="button"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={`Tài khoản ${user.fullName}`}
        onClick={() => setOpen((o) => !o)}
        className="flex min-h-10 items-center gap-2 rounded-full p-1 pr-2 hover:bg-background"
      >
        <span className="grid size-8 place-items-center rounded-full bg-primary-border text-caption font-semibold text-primary-dark">{initials}</span>
        {!compact && <span className="max-w-40 truncate font-medium">{user.fullName}</span>}
        {!compact && <Icon name="expand_more" size={20} />}
      </button>
      {open && (
        <div ref={menuRef} role="menu" className="absolute right-0 z-30 mt-2 w-64 rounded-md bg-surface p-2 shadow-elevation-2">
          <div className="border-b border-border px-3 pb-2">
            <p className="truncate font-semibold">{user.fullName}</p>
            <p className="truncate text-text-secondary">{user.email}</p>
          </div>
          <button
            type="button"
            role="menuitem"
            onClick={async () => {
              await logout()
              navigate('/dang-nhap', { replace: true })
            }}
            className="mt-1 flex min-h-10 w-full items-center gap-2 rounded-sm px-3 text-left hover:bg-background"
          >
            <Icon name="logout" size={20} />
            Đăng xuất
          </button>
        </div>
      )}
    </div>
  )
}
