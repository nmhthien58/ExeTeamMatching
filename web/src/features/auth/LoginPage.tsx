import { useState, type CSSProperties } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { GoogleLogin } from '@react-oauth/google'
import { toApiError } from '@/shared/api/apiClient'
import { shellHome, shellOf, useAuth } from '@/shared/auth/AuthProvider'
import { env } from '@/shared/config/env'
import { Banner, Icon, Spinner } from '@/shared/components/ui'

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
    <main className="grid min-h-screen lg:grid-cols-[minmax(0,1.1fr)_minmax(0,1fr)]">
      <BrandPanel />
      <section className="grid place-items-center p-6">
        <div className="animate-fade-up w-full max-w-[400px] rounded-lg border border-border/60 bg-surface p-8 shadow-elevation-2">
          <span aria-hidden className="grid size-12 place-items-center rounded-[14px] bg-gradient-to-br from-brand-orange to-primary shadow-glow">
            <Icon name="groups" filled className="text-white" />
          </span>
          <h1 className="mt-6 text-display font-semibold tracking-[-0.01em]">Đăng nhập</h1>
          <p className="mt-1 text-text-secondary">Dùng tài khoản Google của trường để vào hệ thống.</p>

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

          <p className="mt-8 text-center text-caption text-text-secondary">Bộ môn EXE · Đại học FPT</p>
        </div>
      </section>
    </main>
  )
}

const highlights = [
  { icon: 'diversity_3', text: 'Sinh viên tự lập nhóm EXE101, ghép theo kỹ năng' },
  { icon: 'school', text: 'Ghép giảng viên và mentor tự động theo chuyên môn' },
  { icon: 'insights', text: 'Bộ môn theo dõi tiến độ từng đợt, chốt danh sách' },
]

/** Panel giới thiệu nền mực (chỉ hiện trên màn hình rộng). */
function BrandPanel() {
  return (
    <aside className="relative hidden overflow-hidden bg-ink p-12 text-ink-text lg:flex lg:flex-col lg:justify-between">
      <div aria-hidden className="pointer-events-none absolute -left-24 -top-24 size-96 rounded-full bg-brand-orange/25 blur-3xl" />
      <div aria-hidden className="pointer-events-none absolute -bottom-32 right-0 size-[28rem] rounded-full bg-primary/20 blur-3xl" />
      <div className="relative flex items-center gap-3">
        <span aria-hidden className="grid size-10 place-items-center rounded-[12px] bg-gradient-to-br from-brand-orange to-primary shadow-glow">
          <Icon name="groups" filled className="text-white" />
        </span>
        <span className="text-subtitle font-semibold text-white">EXE Team Matching</span>
      </div>
      <div className="relative max-w-[480px]">
        <h2 className="text-[40px] font-semibold leading-[1.15] tracking-[-0.02em] text-white">
          Lập nhóm khởi nghiệp, <span className="text-brand-orange">nhanh và minh bạch</span>.
        </h2>
        <ul className="mt-8 flex flex-col gap-4">
          {highlights.map((h, i) => (
            <li key={h.icon} className="stagger-in flex items-center gap-3" style={{ '--i': i } as CSSProperties}>
              <span aria-hidden className="grid size-9 place-items-center rounded-md bg-white/10">
                <Icon name={h.icon} size={20} className="text-brand-orange" />
              </span>
              <span>{h.text}</span>
            </li>
          ))}
        </ul>
      </div>
      <p className="relative text-caption text-ink-muted">EXE101 · EXE201 · Đại học FPT</p>
    </aside>
  )
}
