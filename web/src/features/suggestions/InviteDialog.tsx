import { useState } from 'react'
import { toApiError } from '@/shared/api/apiClient'
import { Button, Dialog } from '@/shared/components/ui'
import type { StudentRef } from './api'

const inviteErrors: Record<string, string> = {
  'team-full': 'Nhóm đã đủ 6 người.',
  'already-in-team': 'Sinh viên này vừa vào một nhóm khác.',
  'pending-exists': 'Bạn đã mời sinh viên này, lời mời còn hiệu lực.',
  'round-closed': 'Đợt lập nhóm đã đóng, không gửi được lời mời.',
}

/** Dialog "Gửi lời mời?" kèm lời nhắn không bắt buộc (S18, S19). Lỗi nghiệp vụ hiện đúng câu, nội dung đã nhập giữ nguyên. */
export function InviteDialog({ student, sending, error, onSend, onClose }: {
  student: StudentRef | null
  sending: boolean
  error: unknown
  onSend: (message: string) => void
  onClose: () => void
}) {
  const [message, setMessage] = useState('')
  const apiError = error ? toApiError(error) : null
  return (
    <Dialog
      open={!!student}
      onClose={onClose}
      title="Gửi lời mời?"
      actions={
        <>
          <Button variant="text" onClick={onClose}>
            Hủy
          </Button>
          <Button loading={sending} onClick={() => onSend(message.trim())}>
            Gửi lời mời
          </Button>
        </>
      }
    >
      <p>{student?.fullName} có 72 giờ để chấp nhận lời mời.</p>
      <label className="mt-3 flex flex-col gap-2 text-text-primary">
        <span className="font-medium">Lời nhắn (không bắt buộc)</span>
        <textarea rows={3} maxLength={500} value={message} onChange={(e) => setMessage(e.target.value)}
          className="rounded-sm border border-border-strong px-3 py-2" />
        <span className="self-end text-caption text-text-secondary">{message.length}/500</span>
      </label>
      {apiError && <p role="alert" className="mt-2 text-error">{inviteErrors[apiError.code] ?? apiError.detail ?? apiError.title}</p>}
    </Dialog>
  )
}
