import type { ReactNode } from 'react'

export const inputClass = 'h-10 w-full rounded-sm border border-border-strong bg-surface px-3 text-body text-text-primary disabled:bg-background disabled:text-text-secondary'

/** Ô nhập có nhãn và lỗi ngay dưới ô, dùng cho form học kỳ và đợt đăng ký. */
export function Field({ label, error, children }: { label: string; error?: string; children: ReactNode }) {
  return (
    <label className="flex flex-col gap-1 text-body font-medium text-text-primary">
      {label}
      {children}
      {error && <span className="font-normal text-error">{error}</span>}
    </label>
  )
}
