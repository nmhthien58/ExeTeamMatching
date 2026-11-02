import { useId, useState, type FormEvent, type ReactNode } from 'react'
import { Banner, Button, Dialog } from '@/shared/components/ui'
import { isFull, type AdvisorLoad, type AdvisorMatchingRow } from './api'
import type { MatchingMessage } from './useMatchingPage'

const NOTE_MAX = 500

/**
 * Dialog đổi / gán cố vấn tay của W19, W28: chọn cố vấn (đã đủ số nhóm hoặc đang nhận nhóm này thì khóa), bắt buộc ghi lý do.
 * Lý do ghép của nhóm thành "Bộ môn phân công". Trang chỉ gắn dialog khi đang mở (mỗi nhóm một lần, `key` theo nhóm).
 */
export function ChangeAdvisorDialog<L extends AdvisorLoad>({
  row,
  loads,
  noun,
  label,
  currentSuffix,
  notePlaceholder,
  pending,
  error,
  notice,
  optionHint,
  warning,
  onClose,
  onSubmit,
}: {
  row: AdvisorMatchingRow | null
  loads: L[]
  /** "giảng viên" hoặc "mentor". */
  noun: string
  /** Nhãn ô chọn: "Giảng viên", "Mentor". */
  label: string
  /** Chú thích của người đang nhận nhóm, ví dụ "đang hướng dẫn". */
  currentSuffix: string
  notePlaceholder: string
  pending: boolean
  error: MatchingMessage | null
  notice?: ReactNode
  optionHint?: (load: L) => string
  warning?: (selected: L) => ReactNode
  onClose: () => void
  onSubmit: (advisorId: string, note: string, name: string | undefined, isAssign: boolean) => void
}) {
  const formId = useId()
  const [advisorId, setAdvisorId] = useState('')
  const [note, setNote] = useState('')
  const [touched, setTouched] = useState(false)

  const isAssign = !row?.advisorId
  const verb = `${isAssign ? 'Gán' : 'Đổi'} ${noun}`
  const selected = loads.find((l) => l.advisorId === advisorId)
  const advisorError = touched && !advisorId ? `Chọn ${noun}.` : undefined
  const noteError = touched && !note.trim() ? `Nhập lý do đổi ${noun}.` : undefined

  const submit = (e: FormEvent) => {
    e.preventDefault()
    setTouched(true)
    if (!row || !advisorId || !note.trim()) return
    onSubmit(advisorId, note.trim(), selected?.fullName, isAssign)
  }

  const options = [...loads].sort((a, b) => a.fullName.localeCompare(b.fullName, 'vi'))

  return (
    <Dialog
      open={!!row}
      onClose={onClose}
      title={`${verb} – ${row?.team.name ?? ''}`}
      actions={
        <>
          <Button variant="text" onClick={onClose} disabled={pending}>
            Hủy
          </Button>
          <Button type="submit" form={formId} loading={pending}>
            {verb}
          </Button>
        </>
      }
    >
      {row && (
        <form id={formId} onSubmit={submit} noValidate className="flex flex-col gap-4 text-text-primary">
          <p className="text-text-secondary">
            Hiện tại: <span className="text-text-primary">{row.advisorName ?? `Chưa có ${noun}`}</span>
          </p>
          {!isAssign && notice}
          {error && (
            <Banner tone="error" title={error.title}>
              {error.detail}
            </Banner>
          )}
          <label className="flex flex-col gap-1">
            <span className="font-medium">{label}</span>
            <select
              value={advisorId}
              onChange={(e) => setAdvisorId(e.target.value)}
              aria-invalid={!!advisorError}
              aria-describedby={advisorError ? `${formId}-advisor` : undefined}
              className={`h-10 rounded-sm border bg-surface px-3 ${advisorError ? 'border-error' : 'border-border-strong'}`}
            >
              <option value="">Chọn {noun}</option>
              {options.map((l) => {
                const current = l.advisorId === row.advisorId
                const full = isFull(l)
                return (
                  <option key={l.advisorId} value={l.advisorId} disabled={current || full}>
                    {l.fullName} · {l.assignedCount}/{l.maxTeams ?? '∞'} nhóm{optionHint?.(l) ?? ''}
                    {current ? ` · ${currentSuffix}` : full ? ' · đã đủ' : ''}
                  </option>
                )
              })}
            </select>
            {advisorError && (
              <span id={`${formId}-advisor`} className="text-caption text-error">
                {advisorError}
              </span>
            )}
          </label>
          {selected && warning?.(selected)}
          <label className="flex flex-col gap-1">
            <span className="font-medium">Lý do</span>
            <textarea
              value={note}
              onChange={(e) => setNote(e.target.value.slice(0, NOTE_MAX))}
              rows={3}
              maxLength={NOTE_MAX}
              aria-invalid={!!noteError}
              aria-describedby={`${formId}-note`}
              placeholder={notePlaceholder}
              className={`rounded-sm border bg-surface px-3 py-2 placeholder:text-text-secondary ${noteError ? 'border-error' : 'border-border-strong'}`}
            />
            <span id={`${formId}-note`} className={`flex justify-between text-caption ${noteError ? 'text-error' : 'text-text-secondary'}`}>
              <span>{noteError ?? `Nhóm và ${noun} thấy lý do "Bộ môn phân công".`}</span>
              <span className="tabular-nums text-text-secondary">
                {note.length}/{NOTE_MAX}
              </span>
            </span>
          </label>
        </form>
      )}
    </Dialog>
  )
}
