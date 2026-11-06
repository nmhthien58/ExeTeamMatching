import { useState } from 'react'
import { ApiError, toApiError } from '@/shared/api/apiClient'
import { Button, Dialog } from '@/shared/components/ui'
import { useSaveSemester, type Semester, type SemesterInput } from './api'
import { Field, inputClass } from './Field'

interface Values {
  code: string
  name: string
  startDate: string
  endDate: string
  isCurrent: boolean
  mentoringStartWeek: string
  expectedMentorSessions: string
  slotBookingDeadlineWeek: string
  meetingSlotMinutes: string
  checkpoints: string[]
}

function initial(semester: Semester | null): Values {
  return {
    code: semester?.code ?? '',
    name: semester?.name ?? '',
    startDate: semester?.startDate ?? '',
    endDate: semester?.endDate ?? '',
    isCurrent: semester?.isCurrent ?? false,
    mentoringStartWeek: String(semester?.mentoringStartWeek ?? 6),
    expectedMentorSessions: String(semester?.expectedMentorSessions ?? 3),
    slotBookingDeadlineWeek: String(semester?.slotBookingDeadlineWeek ?? 2),
    meetingSlotMinutes: String(semester?.meetingSlotMinutes ?? 15),
    checkpoints: [0, 1, 2, 3].map((i) => String(semester?.checkpointTargetWeeks?.[i] ?? '')),
  }
}

/** Lỗi nhập liệu theo field, cùng quy tắc với `SemesterInputValidator` để báo ngay khi chưa gửi. */
export function validateSemester(v: Values): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!/^(SP|SU|FA)[0-9]{2}$/.test(v.code)) errors.code = 'Mã học kỳ dạng SP26, SU26, FA26.'
  if (!v.name.trim()) errors.name = 'Nhập tên học kỳ.'
  if (!v.startDate) errors.startDate = 'Chọn ngày bắt đầu.'
  if (!v.endDate) errors.endDate = 'Chọn ngày kết thúc.'
  else if (v.startDate && v.endDate <= v.startDate) errors.endDate = 'Ngày kết thúc phải sau ngày bắt đầu.'
  const inRange = (value: string, min: number, max: number) => Number.isInteger(Number(value)) && Number(value) >= min && Number(value) <= max && value !== ''
  if (!inRange(v.mentoringStartWeek, 1, 20)) errors.mentoringStartWeek = 'Từ tuần 1 đến 20.'
  if (!inRange(v.expectedMentorSessions, 1, 10)) errors.expectedMentorSessions = 'Từ 1 đến 10 buổi.'
  if (!inRange(v.slotBookingDeadlineWeek, 1, 10)) errors.slotBookingDeadlineWeek = 'Từ tuần 1 đến 10.'
  if (!inRange(v.meetingSlotMinutes, 5, 60)) errors.meetingSlotMinutes = 'Từ 5 đến 60 phút.'
  const filled = v.checkpoints.filter((c) => c !== '')
  if (filled.length > 0 && (filled.length < 4 || !filled.every((c) => inRange(c, 1, 20)))) {
    errors.checkpointTargetWeeks = 'Nhập đủ 4 tuần, mỗi tuần từ 1 đến 20, hoặc để trống cả 4.'
  }
  return errors
}

function toInput(v: Values): SemesterInput {
  return {
    code: v.code,
    name: v.name.trim(),
    startDate: v.startDate,
    endDate: v.endDate,
    isCurrent: v.isCurrent,
    mentoringStartWeek: Number(v.mentoringStartWeek),
    expectedMentorSessions: Number(v.expectedMentorSessions),
    slotBookingDeadlineWeek: Number(v.slotBookingDeadlineWeek),
    meetingSlotMinutes: Number(v.meetingSlotMinutes),
    checkpointTargetWeeks: v.checkpoints.every((c) => c === '') ? null : v.checkpoints.map(Number),
  }
}

/** Form tạo / sửa học kỳ (W03). Mở bằng `key` khác nhau để mỗi lần mở lấy lại giá trị ban đầu. */
export function SemesterForm({ semester, onClose }: { semester: Semester | null; onClose: () => void }) {
  const [values, setValues] = useState(() => initial(semester))
  const [errors, setErrors] = useState<Record<string, string>>({})
  const save = useSaveSemester()
  const set = <K extends keyof Values>(key: K, value: Values[K]) => setValues((v) => ({ ...v, [key]: value }))
  const apiError = save.error ? toApiError(save.error) : null
  const fieldError = (key: string) => errors[key] ?? apiError?.fieldErrors[key]?.[0]
  const bannerError = apiError && Object.keys(apiError.fieldErrors).length === 0 ? apiError : null

  const submit = () => {
    const found = validateSemester(values)
    setErrors(found)
    if (Object.keys(found).length > 0) return
    save.mutate({ id: semester?.id, input: toInput(values) }, { onSuccess: onClose })
  }

  const number = (key: 'mentoringStartWeek' | 'expectedMentorSessions' | 'slotBookingDeadlineWeek' | 'meetingSlotMinutes', label: string) => (
    <Field label={label} error={fieldError(key)}>
      <input type="number" value={values[key]} onChange={(e) => set(key, e.target.value)} className={inputClass} />
    </Field>
  )

  return (
    <Dialog
      open
      onClose={onClose}
      title={semester ? `Sửa học kỳ ${semester.code}` : 'Tạo học kỳ'}
      actions={
        <>
          <Button variant="outline" disabled={save.isPending} onClick={onClose}>
            Hủy
          </Button>
          <Button loading={save.isPending} onClick={submit}>
            Lưu
          </Button>
        </>
      }
    >
      <div className="flex max-h-[60vh] flex-col gap-3 overflow-y-auto pr-1">
        <div className="grid grid-cols-2 gap-3">
          <Field label="Mã học kỳ" error={fieldError('code')}>
            <input value={values.code} onChange={(e) => set('code', e.target.value.toUpperCase())} placeholder="FA26" className={inputClass} />
          </Field>
          <Field label="Tên học kỳ" error={fieldError('name')}>
            <input value={values.name} maxLength={50} onChange={(e) => set('name', e.target.value)} placeholder="Fall 2026" className={inputClass} />
          </Field>
          <Field label="Ngày bắt đầu" error={fieldError('startDate')}>
            <input type="date" value={values.startDate} onChange={(e) => set('startDate', e.target.value)} className={inputClass} />
          </Field>
          <Field label="Ngày kết thúc" error={fieldError('endDate')}>
            <input type="date" value={values.endDate} onChange={(e) => set('endDate', e.target.value)} className={inputClass} />
          </Field>
          {number('mentoringStartWeek', 'Tuần bắt đầu mentor')}
          {number('expectedMentorSessions', 'Số buổi mentor')}
          {number('slotBookingDeadlineWeek', 'Hạn chọn slot họp (tuần)')}
          {number('meetingSlotMinutes', 'Thời lượng slot (phút)')}
        </div>
        <fieldset className="flex flex-col gap-1">
          <legend className="text-body font-medium text-text-primary">Tuần mục tiêu Checkpoint 1–4 (EXE101, tùy chọn)</legend>
          <div className="grid grid-cols-4 gap-2">
            {values.checkpoints.map((week, i) => (
              <input
                key={i}
                type="number"
                aria-label={`Checkpoint ${i + 1}`}
                value={week}
                onChange={(e) => set('checkpoints', values.checkpoints.map((c, j) => (j === i ? e.target.value : c)))}
                className={inputClass}
              />
            ))}
          </div>
          {fieldError('checkpointTargetWeeks') && <span className="text-body text-error">{fieldError('checkpointTargetWeeks')}</span>}
        </fieldset>
        <label className="flex items-center gap-2 text-body text-text-primary">
          <input type="checkbox" checked={values.isCurrent} onChange={(e) => set('isCurrent', e.target.checked)} className="size-5" />
          Đặt làm học kỳ hiện hành
        </label>
        {bannerError instanceof ApiError && (
          <p role="alert" className="text-error">
            {bannerError.detail ?? bannerError.title}
          </p>
        )}
      </div>
    </Dialog>
  )
}
