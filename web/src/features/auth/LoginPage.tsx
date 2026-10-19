import { useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { GoogleLogin } from '@react-oauth/google'
import { toApiError } from '@/shared/api/apiClient'
import { shellHome, shellOf, useAuth } from '@/shared/auth/AuthProvider'
import { env } from '@/shared/config/env'
import { Banner, Spinner } from '@/shared/components/ui'

const errorText: Record<string, { title: string; detail: string }> = {
  'not-in-list': {
    title: 'Tài khoản chưa có trong danh sách',
    detail: 'Email này không có trong danh sách học kỳ. Hãy đăng nhập bằng email trường đã đăng ký, hoặc liên hệ Bộ môn EXE.',
  },
  'account-locked': { title: 'Tài khoản đang bị khóa', detail: 'Liên hệ Bộ môn EXE để mở lại.' },
  network: { title: 'Mất kết nối', detail: 'Kiểm tra mạng rồi bấm đăng nhập lại.' },
}

/**
 * W01 (và A03 bản web) – đăng nhập Google cho mọi vai trò (FE-04, FE-32). Gửi ID token tới Identity,
 * nhận JWT rồi vào đúng khung theo vai trò; nếu đang mở một trang trước khi đăng nhập thì quay lại trang đó.
 */
export function LoginPage() {
  const { user, loginWithGoogle } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<{ title: string; detail: string } | null>(null)
  const from = (location.state as { from?: string } | null)?.from

  if (user) return <Navigate to={from ?? shellHome[shellOf(user)]} replace />

  const onCredential = async (idToken?: string) => {
    if (!idToken) return
    setPending(true)
    setError(null)
    try {
      const signedIn = await loginWithGoogle(idToken)
      navigate(from ?? shellHome[shellOf(signedIn)], { replace: true })
    } catch (e) {
      const err = toApiError(e)
      setError(errorText[err.code] ?? { title: err.title, detail: err.detail ?? 'Vui lòng thử lại.' })
    } finally {
      setPending(false)
    }
  }

  return (
    <main className="grid min-h-screen place-items-center bg-background p-4">
      <div className="w-full max-w-[400px] rounded-lg bg-surface p-8 shadow-elevation-1">
        <div aria-hidden className="mb-6 h-1.5 w-16 rounded-full bg-brand-orange" />
        <h1 className="text-display font-semibold">EXE Team Matching</h1>
        <p className="mt-1 text-text-secondary">Đăng nhập bằng tài khoản Google của trường.</p>

        <div className="mt-8 flex min-h-11 items-center justify-center" aria-live="polite">
          {pending ? (
            <span className="inline-flex items-center gap-2 text-text-secondary">
              <Spinner /> Đang đăng nhập…
            </span>
          ) : env.googleClientId ? (
            <GoogleLogin
              onSuccess={(r) => onCredential(r.credential)}
              onError={() => setError({ title: 'Không mở được Google', detail: 'Thử lại hoặc tắt chặn cửa sổ bật lên.' })}
              text="signin_with"
              shape="pill"
              locale="vi"
            />
          ) : (
            <Banner tone="warning" title="Chưa cấu hình đăng nhập Google">
              Đặt <code>VITE_GOOGLE_CLIENT_ID</code> trong <code>web/.env.local</code> (xem docs/setup).
            </Banner>
          )}
        </div>

        {error && (
          <div className="mt-6">
            <Banner tone="error" title={error.title}>
              {error.detail}
            </Banner>
          </div>
        )}
      </div>
    </main>
  )
}
