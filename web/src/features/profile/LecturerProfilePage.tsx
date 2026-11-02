import { useState } from 'react'
import { toApiError } from '@/shared/api/apiClient'
import { ErrorState } from '@/shared/components/States'
import { Banner, Button, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { useDomains, useLecturerProfile, useSaveLecturerProfile, type LecturerProfile } from './api'
import { Avatar, cardClass, Section } from './components'
import { ProfileSkeleton } from './ProfilePages'

/**
 * L06 – Hồ sơ giảng viên (FE-35): domain chuyên môn là đầu vào gợi ý đề tài L07 (rule 19).
 * Số nhóm tối đa do Bộ môn đặt ở W11, ở đây chỉ đọc (academic.yaml, đợt 2 – 20/10/2026). Trạng thái: xem; chỉnh sửa.
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
      <section className={`${cardClass} flex items-center gap-4`}>
        <Avatar name={profile.fullName} />
        <div className="min-w-0">
          <h2 className="text-title font-semibold">{profile.fullName}</h2>
          <p className="text-body text-text-secondary">{profile.email}</p>
        </div>
      </section>
      <Section title="Lĩnh vực chuyên môn">
        {editing ? (
          <DomainEditor
            selected={profile.domains.map((d) => d.id)}
            onCancel={() => setEditing(false)}
            onSaved={() => {
              setEditing(false)
              snackbar.show('Đã lưu chuyên môn')
            }}
          />
        ) : (
          <div className="flex flex-col items-start gap-3">
            {profile.domains.length > 0 ? (
              <p className="flex flex-wrap gap-2">
                {profile.domains.map((d) => (
                  <StatusChip key={d.id} tone="neutral" icon="category">{d.name}</StatusChip>
                ))}
              </p>
            ) : (
              <p className="text-body text-text-secondary">Chưa khai báo chuyên môn. Hãy chọn để nhận gợi ý đề tài phù hợp.</p>
            )}
            <Button variant="outline" icon="edit" onClick={() => setEditing(true)}>Sửa chuyên môn</Button>
          </div>
        )}
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
      {snackbar.element}
    </div>
  )
}

function DomainEditor({ selected, onCancel, onSaved }: { selected: number[]; onCancel: () => void; onSaved: () => void }) {
  const domains = useDomains()
  const save = useSaveLecturerProfile()
  const [picked, setPicked] = useState(() => new Set(selected))
  const toggle = (id: number) =>
    setPicked((p) => {
      const next = new Set(p)
      if (!next.delete(id)) next.add(id)
      return next
    })

  if (domains.error) return <ErrorState error={domains.error} onRetry={() => void domains.refetch()} />
  const apiError = save.error ? toApiError(save.error) : null
  return (
    <div className="flex flex-col gap-3">
      {apiError && <Banner tone="error" title="Chưa lưu được chuyên môn">{apiError.detail ?? apiError.title}</Banner>}
      <div className="grid gap-1 sm:grid-cols-2">
        {domains.data?.map((d) => (
          <label key={d.id} className="flex min-h-10 items-center gap-2">
            <input type="checkbox" className="size-5 accent-primary" checked={picked.has(d.id)} onChange={() => toggle(d.id)} />
            {d.name}
          </label>
        ))}
      </div>
      <div className="flex justify-end gap-2">
        <Button variant="text" onClick={onCancel}>Hủy</Button>
        <Button loading={save.isPending} onClick={() => save.mutate([...picked], { onSuccess: onSaved })}>Lưu</Button>
      </div>
    </div>
  )
}
