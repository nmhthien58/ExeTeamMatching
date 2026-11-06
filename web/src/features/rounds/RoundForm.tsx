import { useState } from 'react'
import { toApiError } from '@/shared/api/apiClient'
import { Banner, Button, Dialog } from '@/shared/components/ui'
import type { CourseCode } from '@/shared/phase/usePhase'
import { COURSES } from '@/features/dashboard/adminApi'
import { useSemesterList } from '@/features/semesters/api'
import { Field, inputClass } from '@/features/semesters/Field'
import { useSaveRound, type Round, type RoundType } from './api'

export interface RoundFormTarget {
  round: Round | null
  semesterId?: string
}

export const roundTypeLabels: Record<RoundType, string> = {
  TEAM_FORMATION: 'Lập nhóm',
  CHANGE_REQUEST: 'Đổi nhóm',
}

/** ISO UTC → giá trị `datetime-local` theo giờ máy. */
export function toLocalInput(iso: string) {
  const d = new Date(iso)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

interface Values {
  semesterId: string
  courseCode: CourseCode
  roundType: RoundType
  name: string
  opensAt: string
  deadlineAt: string
}

/** Lỗi nhập liệu theo field, cùng quy tắc với `RoundInputValidator`. */
export function validateRound(v: Values): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!v.semesterId) errors.semesterId = 'Chọn học kỳ.'
  if (!v.name.trim()) errors.name = 'Nhập tên đợt.'
  if (!v.opensAt) errors.opensAt = 'Chọn giờ mở.'
  if (!v.deadlineAt) errors.deadlineAt = 'Chọn hạn chót.'
  else if (v.opensAt && new Date(v.deadlineAt) <= new Date(v.opensAt)) errors.deadlineAt = 'Hạn chót phải sau giờ mở.'
  if (v.roundType === 'CHANGE_REQUEST' && v.courseCode !== 'EXE201') errors.roundType = 'Đợt đổi nhóm chỉ có ở EXE201.'
  return errors
}

/**
 * Form tạo / sửa đợt (W04). Đợt chưa mở sửa được mọi trường; đợt đang mở chỉ đổi tên vì Job đã hẹn giờ khóa
 * theo hạn chót cũ (BE-16), nên gửi lại nguyên giờ gốc thay vì giá trị đã làm tròn phút của ô nhập.
 */
export function RoundForm({ target, onClose }: { target: RoundFormTarget; onClose: () => void }) {
  const { round } = target
  const semesters = useSemesterList()
  const [values, setValues] = useState<Values>(() => ({
    semesterId: round?.semesterId ?? target.semesterId ?? '',
    courseCode: round?.courseCode ?? 'EXE101',
    roundType: round?.roundType ?? 'TEAM_FORMATION',
    name: round?.name ?? '',
    opensAt: round ? toLocalInput(round.opensAt) : '',
    deadlineAt: round ? toLocalInput(round.deadlineAt) : '',
  }))
  const [errors, setErrors] = useState<Record<string, string>>({})
  const save = useSaveRound()
  // Mở từ nút ở header thì chưa có học kỳ: mặc định học kỳ hiện hành.
  const semesterId = values.semesterId || (semesters.data?.find((s) => s.isCurrent)?.id ?? '')
  const locked = round?.status === 'OPEN'
  const set = <K extends keyof Values>(key: K, value: Values[K]) => setValues((v) => ({ ...v, [key]: value }))
  const apiError = save.error ? toApiError(save.error) : null
  const fieldError = (key: string) => errors[key] ?? apiError?.fieldErrors[key]?.[0]
  const bannerError = apiError && Object.keys(apiError.fieldErrors).length === 0 ? apiError : null

  const changeCourse = (courseCode: CourseCode) =>
    setValues((v) => ({ ...v, courseCode, roundType: courseCode === 'EXE101' ? 'TEAM_FORMATION' : v.roundType }))

  const submit = () => {
    const found = validateRound({ ...values, semesterId })
    setErrors(found)
    if (Object.keys(found).length > 0) return
    save.mutate(
      {
        id: round?.id,
        input: {
          semesterId,
          courseCode: values.courseCode,
          roundType: values.roundType,
          name: values.name.trim(),
          opensAt: locked ? round.opensAt : new Date(values.opensAt).toISOString(),
          deadlineAt: locked ? round.deadlineAt : new Date(values.deadlineAt).toISOString(),
        },
      },
      { onSuccess: onClose },
    )
  }

  return (
    <Dialog
      open
      onClose={onClose}
      title={round ? 'Sửa đợt đăng ký' : 'Tạo đợt đăng ký'}
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
      <div className="flex flex-col gap-3">
        {locked && <Banner title="Đợt đang mở chỉ đổi được tên">Khóa đợt rồi tạo đợt mới nếu cần đổi thời gian.</Banner>}
        <Field label="Học kỳ" error={fieldError('semesterId')}>
          <select value={semesterId} disabled={locked} onChange={(e) => set('semesterId', e.target.value)} className={inputClass}>
            <option value="">Chọn học kỳ</option>
            {semesters.data?.map((s) => (
              <option key={s.id} value={s.id}>
                {s.code} – {s.name}
              </option>
            ))}
          </select>
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Môn" error={fieldError('courseCode')}>
            <select value={values.courseCode} disabled={locked} onChange={(e) => changeCourse(e.target.value as CourseCode)} className={inputClass}>
              {COURSES.map((c) => (
                <option key={c}>{c}</option>
              ))}
            </select>
          </Field>
          <Field label="Loại đợt" error={fieldError('roundType')}>
            <select value={values.roundType} disabled={locked} onChange={(e) => set('roundType', e.target.value as RoundType)} className={inputClass}>
              <option value="TEAM_FORMATION">{roundTypeLabels.TEAM_FORMATION}</option>
              {values.courseCode === 'EXE201' && <option value="CHANGE_REQUEST">{roundTypeLabels.CHANGE_REQUEST}</option>}
            </select>
          </Field>
        </div>
        <Field label="Tên đợt" error={fieldError('name')}>
          <input value={values.name} maxLength={100} onChange={(e) => set('name', e.target.value)} placeholder="Lập nhóm EXE101 FA26" className={inputClass} />
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Giờ mở" error={fieldError('opensAt')}>
            <input type="datetime-local" value={values.opensAt} disabled={locked} onChange={(e) => set('opensAt', e.target.value)} className={inputClass} />
          </Field>
          <Field label="Hạn chót" error={fieldError('deadlineAt')}>
            <input type="datetime-local" value={values.deadlineAt} disabled={locked} onChange={(e) => set('deadlineAt', e.target.value)} className={inputClass} />
          </Field>
        </div>
        {bannerError && (
          <p role="alert" className="text-error">
            {bannerError.detail ?? bannerError.title}
          </p>
        )}
      </div>
    </Dialog>
  )
}
