import { useState } from 'react'
import { Button } from '@/shared/components/ui'
import { Drawer } from '@/features/workspace/components'
import { Field, inputClass } from '@/features/semesters/Field'
import { ApiBanner, DomainPicker, ErrorSummary, LockDialog } from '@/features/lecturers/form'
import { DEFAULT_MAX_TEAMS, useFieldErrors, validateMentor, type MentorValues as Values } from '@/features/lecturers/rules'
import { useLecturerOptions, useSaveMentor, type Mentor, type MentorInput } from './api'

const labels: Record<string, string> = {
  fullName: 'Họ tên',
  email: 'Email',
  organization: 'Tổ chức',
  lecturerId: 'Giảng viên',
  domainIds: 'Chuyên môn',
  maxTeams: 'Số nhóm tối đa',
}

/**
 * Drawer tạo / sửa mentor (W27, rule 20). Mentor trong trường có thể gắn một giảng viên: dùng email giảng viên, chỉ giảng viên đang hoạt động
 * và chưa có hồ sơ mentor. Khóa mentor phải xác nhận vì mentor bị gỡ khỏi các nhóm đang mentor.
 */
export function MentorDrawer({ mentor, onClose, onSaved }: { mentor: Mentor | null; onClose: () => void; onSaved: (message: string) => void }) {
  const [values, setValues] = useState<Values>(() => ({
    fullName: mentor?.fullName ?? '',
    email: mentor?.email ?? '',
    organization: mentor?.organization ?? '',
    isInternal: mentor?.isInternal ?? false,
    lecturerId: mentor?.lecturerId ?? '',
    domainIds: mentor?.domains.map((d) => d.id) ?? [],
    maxTeams: mentor?.maxTeams.toString() ?? DEFAULT_MAX_TEAMS,
    isActive: mentor?.isActive ?? true,
  }))
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [confirmLock, setConfirmLock] = useState(false)
  const lecturers = useLecturerOptions()
  const save = useSaveMentor()
  const { fieldError, banner } = useFieldErrors(errors, save.error, {
    'email-exists': 'email',
    'lecturer-locked': 'lecturerId',
    'lecturer-has-mentor': 'lecturerId',
    'max-below-current': 'maxTeams',
  })
  const set = <K extends keyof Values>(key: K, value: Values[K]) => setValues((v) => ({ ...v, [key]: value }))
  const linked = values.isInternal && values.lecturerId !== ''
  const lecturerOptions = (lecturers.data ?? []).filter((l) => l.isActive || l.id === mentor?.lecturerId)

  const send = () => {
    const input: MentorInput = {
      fullName: values.fullName.trim(),
      ...(linked ? {} : { email: values.email.trim() }),
      organization: values.organization.trim() || null,
      isInternal: values.isInternal,
      lecturerId: linked ? values.lecturerId : null,
      domainIds: values.domainIds,
      maxTeams: Number(values.maxTeams),
      ...(mentor ? { isActive: values.isActive } : {}),
    }
    save.mutate({ id: mentor?.id, input }, { onSuccess: () => onSaved(mentor ? 'Đã lưu mentor' : 'Đã thêm mentor') })
  }
  const submit = () => {
    const found = validateMentor(values)
    setErrors(found)
    if (Object.keys(found).length > 0) return
    if (mentor?.isActive && !values.isActive) setConfirmLock(true)
    else send()
  }

  return (
    <Drawer
      open
      onClose={onClose}
      title={mentor ? `Sửa ${mentor.fullName}` : 'Thêm mentor'}
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
        <label className="flex min-h-10 items-center justify-between gap-3 font-medium">
          Giảng viên / cán bộ của trường
          <input
            type="checkbox"
            role="switch"
            className="size-5 accent-primary"
            checked={values.isInternal}
            onChange={(e) => setValues((v) => ({ ...v, isInternal: e.target.checked, lecturerId: e.target.checked ? v.lecturerId : '' }))}
          />
        </label>
        {values.isInternal && (
          <Field label="Là giảng viên (không bắt buộc)" error={fieldError('lecturerId')}>
            <select value={values.lecturerId} onChange={(e) => set('lecturerId', e.target.value)} className={inputClass}>
              <option value="">Không gắn giảng viên</option>
              {lecturerOptions.map((l) => (
                <option key={l.id} value={l.id}>
                  {l.fullName} – {l.email}
                </option>
              ))}
            </select>
          </Field>
        )}
        {linked ? (
          <p className="text-body text-text-secondary">Dùng email của giảng viên để đăng nhập, chung một tài khoản với vai trò giảng viên.</p>
        ) : (
          <Field label="Email (do Bộ môn cấp, dùng để đăng nhập)" error={fieldError('email')}>
            <input type="email" value={values.email} onChange={(e) => set('email', e.target.value)} className={inputClass} />
          </Field>
        )}
        <Field label="Tổ chức (không bắt buộc)" error={fieldError('organization')}>
          <input value={values.organization} maxLength={150} onChange={(e) => set('organization', e.target.value)} className={inputClass} />
        </Field>
        <Field label="Số nhóm tối đa" error={fieldError('maxTeams')}>
          <input type="number" min={1} max={50} value={values.maxTeams} onChange={(e) => set('maxTeams', e.target.value)} className={inputClass} />
        </Field>
        {mentor && (
          <p className="text-body text-text-secondary">
            Đang mentor {mentor.currentTeamCount} nhóm · mentor muốn nhận {mentor.preferredTeams ?? 'chưa đặt'}
          </p>
        )}
        <DomainPicker selected={values.domainIds} current={mentor?.domains.map((d) => d.id) ?? []} onChange={(ids) => set('domainIds', ids)} error={fieldError('domainIds')} />
        {mentor && (
          <label className="flex min-h-10 items-center justify-between gap-3 font-medium">
            Đang hoạt động
            <input type="checkbox" role="switch" className="size-5 accent-primary" checked={values.isActive} onChange={(e) => set('isActive', e.target.checked)} />
          </label>
        )}
      </div>
      <LockDialog
        open={confirmLock}
        title={`Khóa ${mentor?.fullName ?? 'mentor'}?`}
        onCancel={() => setConfirmLock(false)}
        onConfirm={() => {
          setConfirmLock(false)
          send()
        }}
      >
        Mentor không đăng nhập được nữa và bị gỡ khỏi {mentor?.currentTeamCount ?? 0} nhóm đang mentor; các nhóm đó sẽ chưa có mentor.
      </LockDialog>
    </Drawer>
  )
}
