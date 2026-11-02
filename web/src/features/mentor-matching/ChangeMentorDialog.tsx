import { useId, useState, type FormEvent } from 'react'
import { Banner, Button, Dialog } from '@/shared/components/ui'
import { isFull, useChangeMentor, wouldExceedPreferred, type AdvisorMatchingRow, type ChangeMentorResult, type MentorLoad } from './api'
import { mentorMatchingErrorMessage } from './errors'

const NOTE_MAX = 500

/**
 * Đổi / gán mentor tay (W28, rule 20): chọn mentor (đã đủ số tối đa thì khóa), bắt buộc ghi lý do. Chọn mentor sẽ vượt số mong muốn
 * thì cảnh báo trước khi lưu (không chặn). Một mentor mỗi nhóm: đổi mentor thì mentor cũ mất quyền vào dự án ngay – dialog nói rõ
 * điều này và nút chính ghi "Đổi mentor" để xác nhận. Trang chỉ gắn dialog khi đang mở (`key` theo nhóm).
 */
export function ChangeMentorDialog({
  row,
  loads,
  onClose,
  onDone,
}: {
  row: AdvisorMatchingRow | null
  loads: MentorLoad[]
  onClose: () => void
  onDone: (message: string, result: ChangeMentorResult) => void
}) {
  const change = useChangeMentor()
  const formId = useId()
  const [mentorId, setMentorId] = useState('')
  const [note, setNote] = useState('')
  const [touched, setTouched] = useState(false)

  const isAssign = !row?.advisorId
  const verb = isAssign ? 'Gán mentor' : 'Đổi mentor'
  const selected = loads.find((l) => l.advisorId === mentorId)
  const mentorError = touched && !mentorId ? 'Chọn mentor.' : undefined
  const noteError = touched && !note.trim() ? 'Nhập lý do đổi mentor.' : undefined
  const serverError = change.error ? mentorMatchingErrorMessage(change.error) : null

  const submit = (e: FormEvent) => {
    e.preventDefault()
    setTouched(true)
    if (!row || !mentorId || !note.trim()) return
    change.mutate(
      { teamId: row.team.id, mentorId, note: note.trim() },
      {
        onSuccess: (result) => {
          const name = result.assignment?.mentorName ?? selected?.fullName ?? 'mentor'
          onDone(`Đã ${isAssign ? 'gán' : 'đổi sang'} ${name} cho nhóm ${row.team.name}`, result)
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
            Hiện tại: <span className="text-text-primary">{row.advisorName ?? 'Chưa có mentor'}</span>
          </p>
          {!isAssign && (
            <p className="text-text-secondary">
              Mỗi nhóm có một mentor. {row.advisorName} sẽ không còn vào được dự án của nhóm; lịch sử trao đổi vẫn giữ lại.
            </p>
          )}
          {serverError && (
            <Banner tone="error" title={serverError.title}>
              {serverError.detail}
            </Banner>
          )}
          <label className="flex flex-col gap-1">
            <span className="font-medium">Mentor</span>
            <select
              value={mentorId}
              onChange={(e) => setMentorId(e.target.value)}
              aria-invalid={!!mentorError}
              aria-describedby={mentorError ? `${formId}-mentor` : undefined}
              className={`h-10 rounded-sm border bg-surface px-3 ${mentorError ? 'border-error' : 'border-border-strong'}`}
            >
              <option value="">Chọn mentor</option>
              {options.map((l) => {
                const current = l.advisorId === row.advisorId
                const full = isFull(l)
                const wanted = l.preferredTeams ? ` · mong muốn ${l.preferredTeams}` : ''
                return (
                  <option key={l.advisorId} value={l.advisorId} disabled={current || full}>
                    {l.fullName} · {l.assignedCount}/{l.maxTeams ?? '∞'} nhóm{wanted}
                    {current ? ' · đang mentor' : full ? ' · đã đủ' : ''}
                  </option>
                )
              })}
            </select>
            {mentorError && (
              <span id={`${formId}-mentor`} className="text-caption text-error">
                {mentorError}
              </span>
            )}
          </label>
          {selected && wouldExceedPreferred(selected) && (
            <Banner tone="warning" title="Vượt số nhóm mong muốn">
              {selected.fullName} muốn mentor {selected.preferredTeams} nhóm; gán nhóm này sẽ thành {selected.assignedCount + 1}/{selected.maxTeams} nhóm.
            </Banner>
          )}
          <label className="flex flex-col gap-1">
            <span className="font-medium">Lý do</span>
            <textarea
              value={note}
              onChange={(e) => setNote(e.target.value.slice(0, NOTE_MAX))}
              rows={3}
              maxLength={NOTE_MAX}
              aria-invalid={!!noteError}
              aria-describedby={`${formId}-note`}
              placeholder="Ví dụ: Nhóm cần mentor có kinh nghiệm Fintech"
              className={`rounded-sm border bg-surface px-3 py-2 placeholder:text-text-secondary ${noteError ? 'border-error' : 'border-border-strong'}`}
            />
            <span id={`${formId}-note`} className={`flex justify-between text-caption ${noteError ? 'text-error' : 'text-text-secondary'}`}>
              <span>{noteError ?? 'Nhóm và mentor thấy lý do "Bộ môn phân công".'}</span>
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
