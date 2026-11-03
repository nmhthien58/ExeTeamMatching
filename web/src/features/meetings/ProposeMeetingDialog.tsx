import { useState } from 'react'
import { toApiError } from '@/shared/api/apiClient'
import { Banner, Button, Dialog } from '@/shared/components/ui'
import { Field, inputClass } from '@/features/semesters/Field'
import { MAX_SLOTS, useAddSlot, useProposeMeeting, type SlotInput } from './api'

interface SlotValue {
  start: string
  duration: string
}

const emptySlot: SlotValue = { start: '', duration: '60' }

/** Một khung giờ: kiểm tra giống Collab (15–240 phút, không ở quá khứ). */
function toSlot(value: SlotValue, now = new Date()): SlotInput | string {
  if (!value.start) return 'Chọn ngày giờ.'
  const start = new Date(value.start)
  if (Number.isNaN(start.getTime())) return 'Ngày giờ không hợp lệ.'
  if (start <= now) return 'Khung giờ phải ở tương lai.'
  const duration = Number(value.duration)
  if (!Number.isInteger(duration) || duration < 15 || duration > 240) return 'Thời lượng từ 15 đến 240 phút.'
  return { startsAt: start.toISOString(), durationMinutes: duration }
}

function SlotFields({ index, value, error, onChange, onRemove }: {
  index: number
  value: SlotValue
  error?: string
  onChange: (value: SlotValue) => void
  onRemove?: () => void
}) {
  return (
    <fieldset className="flex flex-wrap items-end gap-2">
      <legend className="sr-only">Khung giờ {index + 1}</legend>
      <div className="min-w-48 flex-1">
        <Field label={`Khung ${index + 1}`} error={error}>
          <input type="datetime-local" value={value.start} onChange={(e) => onChange({ ...value, start: e.target.value })} className={inputClass} />
        </Field>
      </div>
      <div className="w-28">
        <Field label="Phút">
          <input type="number" min={15} max={240} step={15} value={value.duration} onChange={(e) => onChange({ ...value, duration: e.target.value })} className={inputClass} />
        </Field>
      </div>
      {onRemove && <Button variant="text" icon="close" aria-label={`Bỏ khung ${index + 1}`} onClick={onRemove} />}
    </fieldset>
  )
}

/** C08 đang tạo: tiêu đề, nội dung, nơi họp (phòng / link), 1–3 khung giờ. Một khung = họp đã chốt luôn. */
export function ProposeMeetingDialog({ workspaceId, onClose, onCreated }: {
  workspaceId: string
  onClose: () => void
  onCreated: (id: string, scheduled: boolean) => void
}) {
  const [title, setTitle] = useState('')
  const [agenda, setAgenda] = useState('')
  const [location, setLocation] = useState('')
  const [slots, setSlots] = useState<SlotValue[]>([emptySlot])
  const [errors, setErrors] = useState<Record<string, string>>({})
  const propose = useProposeMeeting(workspaceId)
  const apiError = propose.error ? toApiError(propose.error) : null

  const submit = () => {
    const found: Record<string, string> = {}
    if (!title.trim()) found.title = 'Nhập tiêu đề.'
    const parsed = slots.map((s) => toSlot(s))
    parsed.forEach((p, i) => typeof p === 'string' && (found[`slot${i}`] = p))
    setErrors(found)
    if (Object.keys(found).length > 0) return
    propose.mutate(
      { title: title.trim(), agenda: agenda.trim() || null, location: location.trim() || null, slots: parsed as SlotInput[] },
      { onSuccess: (m) => onCreated(m.id, m.status === 'SCHEDULED') },
    )
  }

  return (
    <Dialog
      open
      onClose={onClose}
      title="Đề xuất họp"
      actions={
        <>
          <Button variant="text" disabled={propose.isPending} onClick={onClose}>Hủy</Button>
          <Button loading={propose.isPending} onClick={submit}>{slots.length === 1 ? 'Tạo cuộc họp' : 'Gửi đề xuất'}</Button>
        </>
      }
    >
      <div className="flex flex-col gap-3">
        {apiError && <Banner tone="error" title={apiError.title}>{apiError.detail}</Banner>}
        <Field label="Tiêu đề" error={errors.title ?? apiError?.fieldErrors.title?.[0]}>
          <input value={title} maxLength={200} onChange={(e) => setTitle(e.target.value)} className={inputClass} />
        </Field>
        <Field label="Nơi họp – phòng hoặc link (không bắt buộc)">
          <input value={location} maxLength={500} onChange={(e) => setLocation(e.target.value)} className={inputClass} />
        </Field>
        <Field label="Nội dung (không bắt buộc)">
          <textarea rows={3} maxLength={2000} value={agenda} onChange={(e) => setAgenda(e.target.value)} className="rounded-sm border border-border-strong bg-surface px-3 py-2 text-body" />
        </Field>
        {slots.map((s, i) => (
          <SlotFields
            key={i}
            index={i}
            value={s}
            error={errors[`slot${i}`]}
            onChange={(v) => setSlots((all) => all.map((x, j) => (j === i ? v : x)))}
            onRemove={slots.length > 1 ? () => setSlots((all) => all.filter((_, j) => j !== i)) : undefined}
          />
        ))}
        {slots.length < MAX_SLOTS && (
          <Button variant="text" icon="add" className="self-start" onClick={() => setSlots((all) => [...all, emptySlot])}>Thêm khung giờ</Button>
        )}
        <p className="text-caption text-text-secondary">Một khung giờ thì cuộc họp được chốt luôn; nhiều khung thì mọi người chọn Có / Không rồi người tổ chức chốt.</p>
      </div>
    </Dialog>
  )
}

/** Người tổ chức thêm khung khi chưa khung nào đủ người (tối đa 3 khung đang mở). */
export function AddSlotDialog({ meetingId, onClose }: { meetingId: string; onClose: () => void }) {
  const [slot, setSlot] = useState<SlotValue>(emptySlot)
  const [error, setError] = useState<string>()
  const add = useAddSlot(meetingId)
  const apiError = add.error ? toApiError(add.error) : null
  return (
    <Dialog
      open
      onClose={onClose}
      title="Thêm khung giờ"
      actions={
        <>
          <Button variant="text" disabled={add.isPending} onClick={onClose}>Hủy</Button>
          <Button
            loading={add.isPending}
            onClick={() => {
              const parsed = toSlot(slot)
              if (typeof parsed === 'string') return setError(parsed)
              setError(undefined)
              add.mutate(parsed, { onSuccess: onClose })
            }}
          >
            Thêm
          </Button>
        </>
      }
    >
      {apiError && <Banner tone="error" title={apiError.title}>{apiError.detail}</Banner>}
      <SlotFields index={0} value={slot} error={error} onChange={setSlot} />
    </Dialog>
  )
}
