import { useState } from 'react'
import { Button } from '@/shared/components/ui'
import { Drawer } from '@/features/workspace/components'
import { Field, inputClass } from '@/features/semesters/Field'
import { useSaveLecturer, type Lecturer, type LecturerInput } from './api'
import { ApiBanner, DomainPicker, ErrorSummary, LockDialog } from './form'
import { useFieldErrors, validateLecturer, type LecturerValues as Values } from './rules'

const labels: Record<string, string> = {
  fullName: 'Họ tên',
  email: 'Email',
  phone: 'SĐT',
  domainIds: 'Chuyên môn',
  maxTeamsExe101: 'Tối đa EXE101',
  maxTeamsExe201: 'Tối đa EXE201',
}

const optionalNumber = (value: string) => (value.trim() === '' ? null : Number(value))

/**
 * Drawer thêm / sửa giảng viên (W11). Email dùng để cấp quyền đăng nhập; số nhóm tối đa theo môn, để trống là không giới hạn (rule 9).
 * Khóa giảng viên phải xác nhận: hồ sơ mentor gắn với họ khóa theo, mở khóa không tự mở mentor.
 */
export function LecturerDrawer({ lecturer, onClose, onSaved }: { lecturer: Lecturer | null; onClose: () => void; onSaved: (message: string) => void }) {
  const [values, setValues] = useState<Values>(() => ({
    fullName: lecturer?.fullName ?? '',
    email: lecturer?.email ?? '',
    phone: lecturer?.phone ?? '',
    domainIds: lecturer?.domains.map((d) => d.id) ?? [],
    maxTeamsExe101: lecturer?.maxTeamsExe101?.toString() ?? '',
    maxTeamsExe201: lecturer?.maxTeamsExe201?.toString() ?? '',
    isActive: lecturer?.isActive ?? true,
  }))
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [confirmLock, setConfirmLock] = useState(false)
  const save = useSaveLecturer()
  const { fieldError, banner } = useFieldErrors(errors, save.error, { 'lecturer-email-taken': 'email', 'email-exists': 'email' })
  const set = <K extends keyof Values>(key: K, value: Values[K]) => setValues((v) => ({ ...v, [key]: value }))

  const send = () => {
    const input: LecturerInput = {
      fullName: values.fullName.trim(),
      email: values.email.trim(),
      phone: values.phone.trim() || null,
      domainIds: values.domainIds,
      maxTeamsExe101: optionalNumber(values.maxTeamsExe101),
      maxTeamsExe201: optionalNumber(values.maxTeamsExe201),
      ...(lecturer ? { isActive: values.isActive } : {}),
    }
    save.mutate({ id: lecturer?.id, input }, { onSuccess: () => onSaved(lecturer ? 'Đã lưu giảng viên' : 'Đã thêm giảng viên') })
  }
  const submit = () => {
    const found = validateLecturer(values)
    setErrors(found)
    if (Object.keys(found).length > 0) return
    if (lecturer?.isActive && !values.isActive) setConfirmLock(true)
    else send()
  }

  return (
    <Drawer
      open
      onClose={onClose}
      title={lecturer ? `Sửa ${lecturer.fullName}` : 'Thêm giảng viên'}
      footer={
        <div className="flex justify-end gap-2">
          <Button variant="text" disabled={save.isPending} onClick={onClose}>Hủy</Button>
          <Button loading={save.isPending} onClick={submit}>Lưu</Button>
        </div>
      }
    >
      <div className="flex flex-col gap-4">
        <ErrorSummary errors={errors} labels={labels} />
        <ApiBanner error={banner} />
        <Field label="Họ tên" error={fieldError('fullName')}>
          <input value={values.fullName} maxLength={150} onChange={(e) => set('fullName', e.target.value)} className={inputClass} />
        </Field>
        <Field label="Email (dùng để đăng nhập)" error={fieldError('email')}>
          <input type="email" value={values.email} onChange={(e) => set('email', e.target.value)} className={inputClass} />
        </Field>
        <Field label="SĐT (không bắt buộc)" error={fieldError('phone')}>
          <input value={values.phone} onChange={(e) => set('phone', e.target.value)} className={inputClass} />
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Số nhóm tối đa EXE101" error={fieldError('maxTeamsExe101')}>
            <input type="number" min={0} value={values.maxTeamsExe101} placeholder="Không giới hạn" onChange={(e) => set('maxTeamsExe101', e.target.value)} className={inputClass} />
          </Field>
          <Field label="Số nhóm tối đa EXE201" error={fieldError('maxTeamsExe201')}>
            <input type="number" min={0} value={values.maxTeamsExe201} placeholder="Không giới hạn" onChange={(e) => set('maxTeamsExe201', e.target.value)} className={inputClass} />
          </Field>
        </div>
        <DomainPicker selected={values.domainIds} current={lecturer?.domains.map((d) => d.id) ?? []} onChange={(ids) => set('domainIds', ids)} error={fieldError('domainIds')} />
        {lecturer && (
          <label className="flex min-h-10 items-center justify-between gap-3 font-medium">
            Đang hoạt động
            <input type="checkbox" role="switch" className="size-5 accent-primary" checked={values.isActive} onChange={(e) => set('isActive', e.target.checked)} />
          </label>
        )}
      </div>
      <LockDialog
        open={confirmLock}
        title={`Khóa ${lecturer?.fullName ?? 'giảng viên'}?`}
        onCancel={() => setConfirmLock(false)}
        onConfirm={() => {
          setConfirmLock(false)
          send()
        }}
      >
        Giảng viên không đăng nhập được nữa. Nếu giảng viên kiêm mentor, hồ sơ mentor cũng bị khóa và gỡ khỏi các nhóm đang mentor;
        mở khóa giảng viên sau này không tự mở lại mentor.
      </LockDialog>
    </Drawer>
  )
}
