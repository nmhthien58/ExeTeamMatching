import type { ReactNode } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { useAuth } from '@/shared/auth/AuthProvider'
import { ErrorState } from '@/shared/components/States'
import { Icon, Skeleton, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useIsTeamFormation } from '@/shared/phase/usePhase'
import { useMyTeam } from '@/features/suggestions/api'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { useMyProfile, useSaveProfile } from './api'
import { Avatar, cardClass, ProfileForm, Section, SkillBars } from './components'

const linkClass = 'inline-flex min-h-10 items-center rounded-md px-4 font-medium'

export function ProfileSkeleton() {
  return (
    <div className="flex flex-col gap-4">
      <Skeleton className="h-24 w-full" />
      <Skeleton className="h-48 w-full" />
    </div>
  )
}

/**
 * Sinh viên chưa đánh giá đủ 5 kỹ năng phải qua A05 trước (rule 16). Đọc hồ sơ thật thay vì `profileCompleted`
 * trong phiên đăng nhập, vì cờ đó không đổi sau khi lưu. Không đọc được hồ sơ thì không chặn.
 */
export function ProfileGate({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  const profile = useMyProfile(!!user?.studentId)
  if (profile.isLoading) return <ProfileSkeleton />
  if (profile.data?.profileCompleted === false) return <Navigate to="/sv/ho-so/hoan-thien" replace />
  return <>{children}</>
}

/** A05 – Hoàn thiện hồ sơ lần đầu (FE-35): thông tin import chỉ đọc, thu gọn; 5 thanh kỹ năng; giới thiệu. Lưu xong → S01. */
export function CompleteProfilePage() {
  const profile = useMyProfile()
  const save = useSaveProfile()
  const navigate = useNavigate()
  const data = profile.data

  if (data?.profileCompleted && !save.isSuccess) return <Navigate to="/sv" replace />
  return (
    <>
      <PageHeader title="Hoàn thiện hồ sơ" />
      {profile.error ? (
        <ErrorState error={profile.error} onRetry={() => void profile.refetch()} />
      ) : !data ? (
        <ProfileSkeleton />
      ) : (
        <div className="flex flex-col gap-4">
          <details className={cardClass}>
            <summary className="cursor-pointer font-medium">
              Thông tin từ bộ môn: {data.fullName} · {data.studentCode} · {data.specialization.name}
            </summary>
            <dl className="mt-3 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-body">
              <dt className="text-text-secondary">Họ tên</dt>
              <dd>{data.fullName}</dd>
              <dt className="text-text-secondary">MSSV</dt>
              <dd>{data.studentCode}</dd>
              <dt className="text-text-secondary">Chuyên ngành</dt>
              <dd>{data.specialization.name}</dd>
              <dt className="text-text-secondary">Mã lớp</dt>
              <dd>{data.classCode ?? 'Chưa có'}</dd>
            </dl>
          </details>
          <ProfileForm
            profile={data}
            submitLabel="Hoàn tất"
            saving={save.isPending}
            error={save.error}
            onSubmit={(input) => save.mutate(input, { onSuccess: () => navigate('/sv', { replace: true }) })}
          />
        </div>
      )}
    </>
  )
}

/** S02 – Hồ sơ của tôi (FE-35). Không có mục liên lạc (rule 16). Trạng thái: có nhóm; chưa có nhóm. */
export function MyProfilePage() {
  const profile = useMyProfile()
  const team = useMyTeam()
  const data = profile.data
  return (
    <>
      <PageHeader title="Hồ sơ của tôi" action={<Link to="/sv/ho-so/sua" className={`${linkClass} bg-primary text-white hover:bg-primary-dark`}>Sửa hồ sơ</Link>} />
      {profile.error ? (
        <ErrorState error={profile.error} onRetry={() => void profile.refetch()} />
      ) : !data ? (
        <ProfileSkeleton />
      ) : (
        <div className="flex flex-col gap-4">
          <section className={`${cardClass} flex items-center gap-4`}>
            <Avatar name={data.fullName} />
            <div className="min-w-0">
              <h2 className="text-title font-semibold">{data.fullName}</h2>
              <p className="text-body text-text-secondary">{[data.studentCode, data.specialization.name, data.classCode].filter(Boolean).join(' · ')}</p>
              {data.isSeekingTeam && <div className="mt-2"><StatusChip tone="info" icon="person_search">Đang tìm nhóm</StatusChip></div>}
            </div>
          </section>
          <Section title="Kỹ năng">
            <SkillBars ratings={data.skills} />
          </Section>
          <Section title="Giới thiệu">
            <p className="whitespace-pre-line text-body">{data.description || 'Chưa có giới thiệu.'}</p>
          </Section>
          <Section title="Nhóm hiện tại">
            {team.isLoading ? (
              <Skeleton className="h-6 w-48" />
            ) : team.error ? (
              <ErrorState error={team.error} onRetry={() => void team.refetch()} />
            ) : team.data ? (
              <Link to="/sv/nhom" className="font-medium text-primary-dark underline">{team.data.name}</Link>
            ) : (
              <p className="text-body text-text-secondary">Bạn chưa có nhóm.</p>
            )}
          </Section>
          <Link to="/sv/cai-dat" className={`${cardClass} flex min-h-10 items-center gap-2 font-medium hover:bg-background`}>
            <Icon name="settings" size={20} />
            Cài đặt và tài khoản
          </Link>
        </div>
      )}
    </>
  )
}

/** S03 – Chỉnh sửa hồ sơ (FE-35). Công tắc "Tôi đang tìm nhóm" chỉ có trong giai đoạn lập nhóm (rule 18). */
export function EditProfilePage() {
  const profile = useMyProfile()
  const save = useSaveProfile()
  const snackbar = useSnackbar()
  const { isTeamFormation } = useIsTeamFormation(profile.data?.courseCode)

  return (
    <>
      <PageHeader title="Chỉnh sửa hồ sơ" action={<Link to="/sv/ho-so" className={`${linkClass} text-primary-dark hover:bg-primary-container`}>Về hồ sơ</Link>} />
      {profile.error ? (
        <ErrorState error={profile.error} onRetry={() => void profile.refetch()} />
      ) : !profile.data ? (
        <ProfileSkeleton />
      ) : (
        <ProfileForm
          profile={profile.data}
          submitLabel="Lưu"
          saving={save.isPending}
          error={save.error}
          extra={
            isTeamFormation
              ? (seeking, set) => (
                  <label className={`${cardClass} flex min-h-10 items-center justify-between gap-3 font-medium`}>
                    Tôi đang tìm nhóm
                    <input type="checkbox" role="switch" className="size-5 accent-primary" checked={seeking} onChange={(e) => set(e.target.checked)} />
                  </label>
                )
              : undefined
          }
          onSubmit={(input) => save.mutate(input, { onSuccess: () => snackbar.show('Đã lưu hồ sơ') })}
        />
      )}
      {snackbar.element}
    </>
  )
}
