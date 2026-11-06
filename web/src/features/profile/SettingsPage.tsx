import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { toApiError } from '@/shared/api/apiClient'
import { useAuth } from '@/shared/auth/AuthProvider'
import { ErrorState } from '@/shared/components/States'
import { Banner, Button, Dialog, Skeleton } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useEmailPreferences, useSaveEmailPreferences } from './api'
import { cardClass, Section } from './components'

/** A07 – Cài đặt và tài khoản (FE-35): nhận email thông báo, thông tin ứng dụng, đăng xuất có dialog xác nhận. */
export function SettingsPage() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const preferences = useEmailPreferences()
  const save = useSaveEmailPreferences()
  const [confirming, setConfirming] = useState(false)
  const [leaving, setLeaving] = useState(false)
  const saveError = save.error ? toApiError(save.error) : null

  const signOut = async () => {
    setLeaving(true)
    await logout()
    navigate('/dang-nhap', { replace: true })
  }

  return (
    <>
      <PageHeader title="Cài đặt và tài khoản" />
      <div className="flex flex-col gap-4">
        {saveError && <Banner tone="error" title="Chưa lưu được cài đặt">{saveError.detail ?? saveError.title}</Banner>}
        <Section title="Thông báo">
          {preferences.error ? (
            <ErrorState error={preferences.error} onRetry={() => void preferences.refetch()} />
          ) : !preferences.data ? (
            <Skeleton className="h-10 w-full" />
          ) : (
            <label className="flex min-h-10 items-center justify-between gap-3">
              Nhận email thông báo
              <input
                type="checkbox"
                role="switch"
                className="size-5 accent-primary"
                checked={preferences.data.emailEnabled}
                disabled={save.isPending}
                onChange={(e) => save.mutate(e.target.checked)}
              />
            </label>
          )}
        </Section>
        <Section title="Ứng dụng">
          <p className="text-body">EXE Team Matching</p>
          <p className="text-body text-text-secondary">Hệ thống ghép nhóm môn EXE101, EXE201.</p>
        </Section>
        <section className={cardClass}>
          <p className="truncate font-semibold">{user?.fullName}</p>
          <p className="mb-3 truncate text-body text-text-secondary">{user?.email}</p>
          <Button variant="outline" icon="logout" onClick={() => setConfirming(true)}>Đăng xuất</Button>
        </section>
      </div>
      <Dialog
        open={confirming}
        onClose={() => setConfirming(false)}
        title="Đăng xuất?"
        actions={
          <>
            <Button variant="text" onClick={() => setConfirming(false)}>Hủy</Button>
            <Button variant="danger" loading={leaving} onClick={() => void signOut()}>Đăng xuất</Button>
          </>
        }
      >
        Bạn sẽ cần đăng nhập lại để tiếp tục.
      </Dialog>
    </>
  )
}
