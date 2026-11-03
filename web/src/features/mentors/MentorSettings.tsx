import { useState } from 'react'
import { toApiError } from '@/shared/api/apiClient'
import { Banner, Button, Skeleton } from '@/shared/components/ui'
import { Field, inputClass } from '@/features/semesters/Field'
import { useSaveSemester, useSemesterList, type Semester } from '@/features/semesters/api'
import { isWholeIn } from '@/features/lecturers/rules'

/** Cài đặt kỳ cho mentor (W27, rule 20): tuần bắt đầu mentor, số buổi mentor dự kiến của học kỳ hiện hành. */
export function MentorSettings({ onSaved }: { onSaved: (message: string) => void }) {
  const semesters = useSemesterList()
  const current = semesters.data?.find((s) => s.isCurrent)
  if (semesters.isLoading) return <Skeleton className="h-24 w-full" />
  if (!current) {
    return <Banner tone="warning" title="Chưa có học kỳ hiện hành">Tạo hoặc chọn học kỳ hiện hành ở mục Học kỳ và đợt để đặt lịch mentor.</Banner>
  }
  return <SettingsForm key={current.id} semester={current} onSaved={onSaved} />
}

function SettingsForm({ semester, onSaved }: { semester: Semester; onSaved: (message: string) => void }) {
  const [week, setWeek] = useState(String(semester.mentoringStartWeek))
  const [sessions, setSessions] = useState(String(semester.expectedMentorSessions))
  const [errors, setErrors] = useState<Record<string, string>>({})
  const save = useSaveSemester()
  const apiError = save.error ? toApiError(save.error) : null
  const changed = week !== String(semester.mentoringStartWeek) || sessions !== String(semester.expectedMentorSessions)

  const submit = () => {
    const found: Record<string, string> = {}
    if (!isWholeIn(week, 1, 20)) found.week = 'Từ tuần 1 đến 20.'
    if (!isWholeIn(sessions, 1, 10)) found.sessions = 'Từ 1 đến 10 buổi.'
    setErrors(found)
    if (Object.keys(found).length > 0) return
    const { id, ...input } = semester
    save.mutate(
      { id, input: { ...input, mentoringStartWeek: Number(week), expectedMentorSessions: Number(sessions) } },
      { onSuccess: () => onSaved('Đã lưu cài đặt mentor') },
    )
  }

  return (
    <section className="rounded-lg bg-surface p-4 shadow-elevation-1" aria-labelledby="mentor-settings">
      <h2 id="mentor-settings" className="text-subtitle font-semibold">Lịch mentor của {semester.code}</h2>
      <p className="mb-3 text-body text-text-secondary">Hệ thống ghép mentor ở tuần bắt đầu; nhóm thấy "Buổi mentor n/số buổi".</p>
      <div className="flex flex-wrap items-end gap-3">
        <div className="w-44">
          <Field label="Tuần bắt đầu mentor" error={errors.week ?? apiError?.fieldErrors.mentoringStartWeek?.[0]}>
            <input type="number" min={1} max={20} value={week} onChange={(e) => setWeek(e.target.value)} className={inputClass} />
          </Field>
        </div>
        <div className="w-44">
          <Field label="Số buổi mentor dự kiến" error={errors.sessions ?? apiError?.fieldErrors.expectedMentorSessions?.[0]}>
            <input type="number" min={1} max={10} value={sessions} onChange={(e) => setSessions(e.target.value)} className={inputClass} />
          </Field>
        </div>
        <Button variant="outline" loading={save.isPending} disabled={!changed} onClick={submit}>Lưu cài đặt</Button>
      </div>
      {apiError && Object.keys(apiError.fieldErrors).length === 0 && (
        <p role="alert" className="mt-2 text-error">{apiError.detail ?? apiError.title}</p>
      )}
    </section>
  )
}
