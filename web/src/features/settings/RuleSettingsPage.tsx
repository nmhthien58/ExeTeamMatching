import { useState } from 'react'
import { toApiError } from '@/shared/api/apiClient'
import { ErrorState } from '@/shared/components/States'
import { Banner, Button, Skeleton } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { Field, inputClass } from '@/features/semesters/Field'
import { fieldRules, parseReminders, useRuleSettings, useSaveRuleSettings, validateRules, type NumberField, type RuleSettings, type RuleValues } from './api'

const groups: { title: string; fields: NumberField[]; withReminders?: boolean }[] = [
  { title: 'Nhóm', fields: ['minTeamSize', 'maxTeamSize', 'minSpecializations'] },
  { title: 'Lời mời và xin vào nhóm', fields: ['invitationExpiryHours', 'maxIdeasPerJoinRequest'], withReminders: true },
  { title: 'Ghép giảng viên và mentor', fields: ['maxLecturerPreferences', 'interestExtraSlots', 'maxInterestsUnlimited'] },
  { title: 'File', fields: ['maxFileSizeMb'] },
]

const toValues = (s: RuleSettings): RuleValues => ({
  ...(Object.fromEntries(Object.keys(fieldRules).map((k) => [k, String(s[k as NumberField])])) as Record<NumberField, string>),
  reminderHours: s.reminderHours.join(', '),
})

/** W05 – Cấu hình quy tắc (FE-11): sĩ số, số chuyên ngành, hạn lời mời, mốc nhắc hạn chót và các cấu hình đợt 2 của BE-23. */
export function RuleSettingsPage() {
  const settings = useRuleSettings()
  return (
    <div className="mx-auto flex w-full max-w-[880px] flex-col gap-4">
      <PageHeader title="Cấu hình quy tắc" />
      {settings.isError ? (
        <ErrorState error={settings.error} onRetry={() => void settings.refetch()} />
      ) : !settings.data ? (
        <div role="status" aria-label="Đang tải" className="flex flex-col gap-3">
          <Skeleton className="h-40 w-full" />
          <Skeleton className="h-40 w-full" />
        </div>
      ) : (
        <RuleForm settings={settings.data} />
      )}
    </div>
  )
}

function RuleForm({ settings }: { settings: RuleSettings }) {
  const [values, setValues] = useState<RuleValues>(() => toValues(settings))
  const [errors, setErrors] = useState<Record<string, string>>({})
  const save = useSaveRuleSettings()
  const snackbar = useSnackbar()
  const apiError = save.error ? toApiError(save.error) : null
  const fieldError = (key: string) => errors[key] ?? apiError?.fieldErrors[key]?.[0]
  const initial = toValues(settings)
  const changed = (Object.keys(values) as (keyof RuleValues)[]).some((k) => values[k].trim() !== initial[k])

  const submit = () => {
    const found = validateRules(values)
    setErrors(found)
    if (Object.keys(found).length > 0) return
    const input = {
      ...(Object.fromEntries(Object.keys(fieldRules).map((k) => [k, Number(values[k as NumberField])])) as Record<NumberField, number>),
      reminderHours: parseReminders(values.reminderHours)!.sort((a, b) => b - a),
    }
    save.mutate(input, {
      onSuccess: (saved) => {
        setValues(toValues(saved))
        snackbar.show('Đã lưu cấu hình')
      },
    })
  }

  return (
    <form
      className="flex flex-col gap-4"
      noValidate
      onSubmit={(e) => {
        e.preventDefault()
        submit()
      }}
    >
      {apiError && Object.keys(apiError.fieldErrors).length === 0 && <Banner tone="error" title={apiError.title}>{apiError.detail}</Banner>}
      {Object.keys(errors).length >= 2 && (
        <Banner tone="error" title={`Còn ${Object.keys(errors).length} chỗ cần sửa`}>Sửa các ô báo lỗi bên dưới rồi lưu lại.</Banner>
      )}
      {groups.map((g) => (
        <section key={g.title} className="rounded-lg bg-surface p-4 shadow-elevation-1">
          <h2 className="mb-3 text-subtitle font-semibold">{g.title}</h2>
          <div className="grid gap-4 sm:grid-cols-2">
            {g.fields.map((f) => {
              const rule = fieldRules[f]
              return (
                <div key={f} className="flex flex-col gap-1">
                  <Field label={rule.unit ? `${rule.label} (${rule.unit})` : rule.label} error={fieldError(f)}>
                    <input
                      type="number"
                      min={rule.min}
                      max={rule.max}
                      value={values[f]}
                      onChange={(e) => setValues((v) => ({ ...v, [f]: e.target.value }))}
                      className={inputClass}
                    />
                  </Field>
                  {rule.hint && <span className="text-caption text-text-secondary">{rule.hint}</span>}
                </div>
              )
            })}
            {g.withReminders && (
              <div className="flex flex-col gap-1">
                <Field label="Mốc nhắc trước hạn chót (giờ)" error={fieldError('reminderHours')}>
                  <input value={values.reminderHours} onChange={(e) => setValues((v) => ({ ...v, reminderHours: e.target.value }))} className={inputClass} />
                </Field>
                <span className="text-caption text-text-secondary">Ví dụ: 72, 24 – nhắc trước 3 ngày và 1 ngày. Tối đa 5 mốc.</span>
              </div>
            )}
          </div>
        </section>
      ))}
      <div className="flex justify-end gap-2">
        <Button
          type="button"
          variant="text"
          disabled={!changed || save.isPending}
          onClick={() => {
            setValues(initial)
            setErrors({})
            save.reset()
          }}
        >
          Hoàn tác
        </Button>
        <Button type="submit" loading={save.isPending} disabled={!changed}>Lưu cấu hình</Button>
      </div>
      {snackbar.element}
    </form>
  )
}
