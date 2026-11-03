import { useEffect, useRef, type ReactNode } from 'react'
import type { ApiError } from '@/shared/api/apiClient'
import { Banner, Button, Dialog, StatusChip } from '@/shared/components/ui'
import type { CatalogRef } from '@/features/suggestions/api'
import { useDomainCatalog } from './api'
import { MAX_DOMAINS } from './rules'

// Phần form dùng chung cho drawer giảng viên (W11) và mentor (W27).

/** Tóm tắt lỗi đầu form khi có từ 2 lỗi (DESIGN.md mục 6), focus vào để trình đọc màn hình đọc ngay. */
export function ErrorSummary({ errors, labels }: { errors: Record<string, string>; labels: Record<string, string> }) {
  const ref = useRef<HTMLDivElement>(null)
  const entries = Object.entries(errors)
  useEffect(() => {
    if (entries.length >= 2) ref.current?.focus()
  }, [entries.length])
  if (entries.length < 2) return null
  return (
    <div ref={ref} tabIndex={-1} role="alert" className="rounded-md bg-error-container px-4 py-3 text-error outline-none">
      <p className="font-semibold">Còn {entries.length} chỗ cần sửa</p>
      <ul className="list-disc pl-5 text-body">
        {entries.map(([key, message]) => (
          <li key={key}>
            {labels[key] ?? key}: {message}
          </li>
        ))}
      </ul>
    </div>
  )
}

export function ApiBanner({ error }: { error: ApiError | null }) {
  if (!error) return null
  return <Banner tone="error" title={error.title}>{error.detail}</Banner>
}

/** Chọn domain: chỉ domain đang dùng, cộng domain đã ngừng mà người này đang có (giống quy tắc ở Academic). */
export function DomainPicker({ selected, current, onChange, error }: {
  selected: number[]
  current: number[]
  onChange: (ids: number[]) => void
  error?: string
}) {
  const catalog = useDomainCatalog()
  const items = (catalog.data ?? []).filter((d) => d.isActive !== false || current.includes(d.id))
  const toggle = (id: number) => onChange(selected.includes(id) ? selected.filter((x) => x !== id) : [...selected, id])
  return (
    <fieldset className="flex flex-col gap-1">
      <legend className="text-body font-medium text-text-primary">Chuyên môn (domain)</legend>
      {catalog.isLoading && <p className="text-body text-text-secondary">Đang tải danh mục…</p>}
      <div className="grid max-h-56 grid-cols-1 gap-1 overflow-y-auto rounded-sm border border-border p-2">
        {items.map((d) => (
          <label key={d.id} className="flex min-h-10 items-center gap-2 text-body">
            <input type="checkbox" className="size-5 accent-primary" checked={selected.includes(d.id)} onChange={() => toggle(d.id)} />
            <span>
              <span className="font-medium">{d.code}</span> · {d.name}
            </span>
          </label>
        ))}
      </div>
      <span className="text-caption text-text-secondary">Đã chọn {selected.length}/{MAX_DOMAINS}</span>
      {error && <span className="text-body text-error">{error}</span>}
    </fieldset>
  )
}

export function DomainCodes({ domains }: { domains: CatalogRef[] }) {
  if (domains.length === 0) return <span className="text-text-secondary">Chưa khai báo</span>
  return <span title={domains.map((d) => `${d.code} · ${d.name}`).join('\n')}>{domains.map((d) => d.code).join(', ')}</span>
}

export function ActiveChip({ active }: { active: boolean }) {
  return active ? <StatusChip tone="success">Đang hoạt động</StatusChip> : <StatusChip tone="neutral" icon="lock">Đã khóa</StatusChip>
}

/** Hộp xác nhận trước khi khóa (hành động nguy hiểm đi qua dialog, DESIGN.md mục 6). */
export function LockDialog({ open, title, children, onCancel, onConfirm }: {
  open: boolean
  title: string
  children: ReactNode
  onCancel: () => void
  onConfirm: () => void
}) {
  return (
    <Dialog
      open={open}
      onClose={onCancel}
      title={title}
      actions={
        <>
          <Button variant="text" onClick={onCancel}>Hủy</Button>
          <Button variant="danger" onClick={onConfirm}>Khóa</Button>
        </>
      }
    >
      {children}
    </Dialog>
  )
}
