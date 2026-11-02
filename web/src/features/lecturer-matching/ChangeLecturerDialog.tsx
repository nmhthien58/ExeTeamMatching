import { useId, useState, type FormEvent } from 'react'
import { Banner, Button, Dialog } from '@/shared/components/ui'
import { isFull, useChangeInstructor, type AdvisorLoad, type AdvisorMatchingRow } from './api'
import { matchingErrorMessage } from './errors'

const NOTE_MAX = 500

/**
 * Đổi / gán giảng viên hướng dẫn tay (W19): chọn giảng viên (đã đủ số nhóm thì khóa), bắt buộc ghi lý do.
 * Lý do ghép của nhóm thành "Bộ môn phân công". Trang chỉ gắn dialog khi đang mở (mỗi nhóm một lần, `key` theo nhóm).
 */
export function ChangeLecturerDialog({
  row,
  loads,
  onClose,
  onDone,
}: {
  row: AdvisorMatchingRow | null
  loads: AdvisorLoad[]
  onClose: () => void
  onDone: (message: string) => void
}) {
  const change = useChangeInstructor()
  const formId = useId()
  const [lecturerId, setLecturerId] = useState('')
  const [note, setNote] = useState('')
  const [touched, setTouched] = useState(false)

  const isAssign = !row?.advisorId
  const verb = isAssign ? 'Gán giảng viên' : 'Đổi giảng viên'
  const lecturerError = touched && !lecturerId ? 'Chọn giảng viên.' : undefined
  const noteError = touched && !note.trim() ? 'Nhập lý do đổi giảng viên.' : undefined
  const serverError = change.error ? matchingErrorMessage(change.error) : null

  const submit = (e: FormEvent) => {
    e.preventDefault()
    setTouched(true)
    if (!row || !lecturerId || !note.trim()) return
    change.mutate(
      { teamId: row.team.id, lecturerId, note: note.trim() },
      {
        onSuccess: () => {
          const name = loads.find((l) => l.advisorId === lecturerId)?.fullName ?? 'giảng viên'
          onDone(`Đã ${isAssign ? 'gán' : 'đổi sang'} ${name} cho nhóm ${row.team.name}`)
        },
      },
    )
  }

  const options = [...loads].sort((a, b) => a.fullName.localeCompare(b.fullName, 'vi'))

  return (
    <Dialog
      open={!!row}
      onClose={onClose}
      title={`${verb} – ${row?.team.name ?? ''}`}
      actions={
        <>
          <Button variant="text" onClick={onClose} disabled={change.isPending}>
            Hủy
          </Button>
          <Button type="submit" form={formId} loading={change.isPending}>
            {verb}
          </Button>
        </>
      }
    >
      {row && (
        <form id={formId} onSubmit={submit} noValidate className="flex flex-col gap-4 text-text-primary">
          <p className="text-text-secondary">
            Hiện tại: <span className="text-text-primary">{row.advisorName ?? 'Chưa có giảng viên'}</span>
          </p>
          {serverError && (
            <Banner tone="error" title={serverError.title}>
              {serverError.detail}
            </Banner>
          )}
          <label className="flex flex-col gap-1">
            <span className="font-medium">Giảng viên</span>
            <select
              value={lecturerId}
              onChange={(e) => setLecturerId(e.target.value)}
              aria-invalid={!!lecturerError}
              aria-describedby={lecturerError ? `${formId}-lecturer` : undefined}
              className={`h-10 rounded-sm border bg-surface px-3 ${lecturerError ? 'border-error' : 'border-border-strong'}`}
            >
              <option value="">Chọn giảng viên</option>
              {options.map((l) => {
                const current = l.advisorId === row.advisorId
                const full = isFull(l)
                return (
                  <option key={l.advisorId} value={l.advisorId} disabled={current || full}>
                    {l.fullName} · {l.assignedCount}/{l.maxTeams ?? '∞'} nhóm{current ? ' · đang hướng dẫn' : full ? ' · đã đủ' : ''}
                  </option>
                )
              })}
            </select>
            {lecturerError && (
              <span id={`${formId}-lecturer`} className="text-caption text-error">
                {lecturerError}
              </span>
            )}
          </label>
          <label className="flex flex-col gap-1">
            <span className="font-medium">Lý do</span>
            <textarea
              value={note}
              onChange={(e) => setNote(e.target.value.slice(0, NOTE_MAX))}
              rows={3}
              maxLength={NOTE_MAX}
              aria-invalid={!!noteError}
              aria-describedby={`${formId}-note`}
              placeholder="Ví dụ: Cân bằng số nhóm giữa các giảng viên"
              className={`rounded-sm border bg-surface px-3 py-2 placeholder:text-text-secondary ${noteError ? 'border-error' : 'border-border-strong'}`}
            />
            <span id={`${formId}-note`} className={`flex justify-between text-caption ${noteError ? 'text-error' : 'text-text-secondary'}`}>
              <span>{noteError ?? 'Nhóm và giảng viên thấy lý do "Bộ môn phân công".'}</span>
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
