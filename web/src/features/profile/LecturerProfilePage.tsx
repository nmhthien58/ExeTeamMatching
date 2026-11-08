import { useState } from 'react'
import { toApiError } from '@/shared/api/apiClient'
import { ErrorState } from '@/shared/components/States'
import { Banner, Button, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { useDomains, useLecturerProfile, useSaveLecturerProfile, type LecturerProfile, type LecturerProfileInput } from './api'
import { Avatar, cardClass, Section } from './components'
import { ProfileSkeleton } from './ProfilePages'

/**
 * L06 – Hồ sơ giảng viên (FE-35, FE-57): domain chuyên môn là đầu vào gợi ý đề tài L07 (rule 19).
 * Số nhóm tối đa do Bộ môn đặt ở W11, giảng viên tự khai báo số nhóm mong muốn (<= tối đa) và chuyên môn.
 * Bắt buộc hoàn thiện trước khi tham gia ghép nhóm tự động (BE-93).
 */
export function LecturerProfilePage() {
  const profile = useLecturerProfile()
  return (
    <>
      <PageHeader title="Hồ sơ giảng viên" />
      {profile.error ? (
        <ErrorState error={profile.error} onRetry={() => void profile.refetch()} />
      ) : !profile.data ? (
        <ProfileSkeleton />
      ) : (
        <Details profile={profile.data} />
      )}
    </>
  )
}

const maxTeams = (value?: number | null) => value ?? 'Không giới hạn'

function Details({ profile }: { profile: LecturerProfile }) {
  const [editing, setEditing] = useState(false)
  const snackbar = useSnackbar()

  return (
    <div className="flex flex-col gap-4">
      {!profile.isProfileCompleted && (
        <Banner tone="warning" title="Hồ sơ chưa hoàn thiện">
          Bạn cần chọn ít nhất 1 hướng chuyên môn và khai báo số nhóm mong muốn hướng dẫn (tối đa{' '}
          {profile.maxTeamsExe101 ?? 4} nhóm cho EXE101 và {profile.maxTeamsExe201 ?? 4} nhóm cho EXE201) trước khi
          được tham gia ghép nhóm tự động.
        </Banner>
      )}

      <section className={`${cardClass} flex items-center gap-4`}>
        <Avatar name={profile.fullName} />
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <h2 className="text-title font-semibold">{profile.fullName}</h2>
            {profile.isProfileCompleted ? (
              <StatusChip tone="success">Đã hoàn thành</StatusChip>
            ) : (
              <StatusChip tone="warning">Chưa hoàn thiện</StatusChip>
            )}
          </div>
          <p className="text-body text-text-secondary">{profile.email}</p>
        </div>
      </section>

      {editing ? (
        <ProfileEditor
          profile={profile}
          onCancel={() => setEditing(false)}
          onSaved={() => {
            setEditing(false)
            snackbar.show('Đã lưu chuyên môn')
          }}
        />
      ) : (
        <>
          <Section title="Lĩnh vực chuyên môn">
            <div className="flex flex-col items-start gap-3">
              {profile.domains.length > 0 ? (
                <p className="flex flex-wrap gap-2">
                  {profile.domains.map((d) => (
                    <StatusChip key={d.id} tone="neutral" icon="category">
                      {d.name}
                    </StatusChip>
                  ))}
                </p>
              ) : (
                <p className="text-body text-text-secondary">
                  Chưa khai báo chuyên môn. Hãy chọn để nhận gợi ý đề tài và ghép nhóm phù hợp.
                </p>
              )}
              <Button variant="outline" icon="edit" onClick={() => setEditing(true)}>
                Sửa chuyên môn
              </Button>
            </div>
          </Section>

          <Section title="Số nhóm mong muốn hướng dẫn">
            <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-body">
              <dt className="text-text-secondary">EXE101</dt>
              <dd className="font-medium">
                {profile.desiredTeamsExe101 != null ? `${profile.desiredTeamsExe101} nhóm` : 'Chưa khai báo'}
              </dd>
              <dt className="text-text-secondary">EXE201</dt>
              <dd className="font-medium">
                {profile.desiredTeamsExe201 != null ? `${profile.desiredTeamsExe201} nhóm` : 'Chưa khai báo'}
              </dd>
            </dl>
          </Section>

          <Section title="Số nhóm tối đa">
            <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-body">
              <dt className="text-text-secondary">EXE101</dt>
              <dd>{maxTeams(profile.maxTeamsExe101)}</dd>
              <dt className="text-text-secondary">EXE201</dt>
              <dd>{maxTeams(profile.maxTeamsExe201)}</dd>
            </dl>
            <p className="mt-2 text-caption text-text-secondary">Do Bộ môn đặt.</p>
          </Section>
        </>
      )}

      {snackbar.element}
    </div>
  )
}

function ProfileEditor({
  profile,
  onCancel,
  onSaved,
}: {
  profile: LecturerProfile
  onCancel: () => void
  onSaved: () => void
}) {
  const domains = useDomains()
  const save = useSaveLecturerProfile()
  const [picked, setPicked] = useState(() => new Set(profile.domains.map((d) => d.id)))
  const [desired101, setDesired101] = useState<string>(
    profile.desiredTeamsExe101 != null ? String(profile.desiredTeamsExe101) : '',
  )
  const [desired201, setDesired201] = useState<string>(
    profile.desiredTeamsExe201 != null ? String(profile.desiredTeamsExe201) : '',
  )
  const [validationError, setValidationError] = useState<string | null>(null)

  const max101 = profile.maxTeamsExe101 ?? 4
  const max201 = profile.maxTeamsExe201 ?? 4

  const toggle = (id: number) =>
    setPicked((p) => {
      const next = new Set(p)
      if (!next.delete(id)) next.add(id)
      return next
    })

  const handleSubmit = () => {
    setValidationError(null)
    if (picked.size === 0) {
      setValidationError('Vui lòng chọn ít nhất 1 lĩnh vực chuyên môn.')
      return
    }

    const payload: LecturerProfileInput = {
      domainIds: [...picked],
    }

    if (desired101.trim() !== '') {
      const val101 = Number(desired101)
      if (isNaN(val101) || val101 < 0 || val101 > max101) {
        setValidationError(`Số nhóm mong muốn EXE101 phải từ 0 đến ${max101}.`)
        return
      }
      payload.desiredTeamsExe101 = val101
    }

    if (desired201.trim() !== '') {
      const val201 = Number(desired201)
      if (isNaN(val201) || val201 < 0 || val201 > max201) {
        setValidationError(`Số nhóm mong muốn EXE201 phải từ 0 đến ${max201}.`)
        return
      }
      payload.desiredTeamsExe201 = val201
    }

    save.mutate(payload, { onSuccess: onSaved })
  }

  if (domains.error) return <ErrorState error={domains.error} onRetry={() => void domains.refetch()} />
  const apiError = save.error ? toApiError(save.error) : null

  return (
    <div className="flex flex-col gap-4">
      {validationError && (
        <Banner tone="error" title="Thông tin chưa hợp lệ">
          {validationError}
        </Banner>
      )}
      {apiError && (
        <Banner tone="error" title="Chưa lưu được chuyên môn">
          {apiError.detail ?? apiError.title}
        </Banner>
      )}

      <Section title="Lĩnh vực chuyên môn (chọn ít nhất 1)">
        <div className="grid gap-1 sm:grid-cols-2">
          {domains.data?.map((d) => (
            <label key={d.id} className="flex min-h-10 items-center gap-2 cursor-pointer">
              <input
                type="checkbox"
                className="size-5 accent-primary"
                checked={picked.has(d.id)}
                onChange={() => toggle(d.id)}
              />
              <span className="text-body">{d.name}</span>
            </label>
          ))}
        </div>
      </Section>

      <Section title="Số nhóm mong muốn hướng dẫn">
        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <label htmlFor="desired101" className="block text-body font-medium mb-1">
              EXE101 (tối đa {max101} nhóm)
            </label>
            <input
              id="desired101"
              type="number"
              min={0}
              max={max101}
              value={desired101}
              onChange={(e) => setDesired101(e.target.value)}
              placeholder="Nhập số nhóm mong muốn"
              className="w-full rounded border border-border bg-surface px-3 py-2 text-body focus:border-primary focus:outline-none"
            />
            <p className="mt-1 text-caption text-text-secondary">Giới hạn bộ môn: {maxTeams(profile.maxTeamsExe101)}</p>
          </div>

          <div>
            <label htmlFor="desired201" className="block text-body font-medium mb-1">
              EXE201 (tối đa {max201} nhóm)
            </label>
            <input
              id="desired201"
              type="number"
              min={0}
              max={max201}
              value={desired201}
              onChange={(e) => setDesired201(e.target.value)}
              placeholder="Nhập số nhóm mong muốn"
              className="w-full rounded border border-border bg-surface px-3 py-2 text-body focus:border-primary focus:outline-none"
            />
            <p className="mt-1 text-caption text-text-secondary">Giới hạn bộ môn: {maxTeams(profile.maxTeamsExe201)}</p>
          </div>
        </div>
      </Section>

      <div className="flex justify-end gap-2">
        <Button variant="text" onClick={onCancel}>
          Hủy
        </Button>
        <Button loading={save.isPending} onClick={handleSubmit}>
          Lưu
        </Button>
      </div>
    </div>
  )
}
