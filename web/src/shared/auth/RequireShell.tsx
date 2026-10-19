import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { shellHome, shellOf, useAuth, type Shell } from './AuthProvider'
import { Forbidden } from '@/shared/components/States'

/**
 * Route guard: chưa đăng nhập → /dang-nhap (giữ đường dẫn để quay lại); sai khung → 403
 * kèm nút về đúng trang chủ. Kiểm tra quyền thật vẫn nằm ở API (JWT + policy).
 */
export function RequireShell({ shell, children }: { shell: Shell; children: ReactNode }) {
  const { user } = useAuth()
  const location = useLocation()

  if (!user) {
    return <Navigate to="/dang-nhap" replace state={{ from: location.pathname + location.search }} />
  }
  const own = shellOf(user)
  // Giảng viên có vai trò Mentor dùng khung giảng viên; không có khung riêng thứ hai.
  if (own !== shell) {
    return <Forbidden homeHref={shellHome[own]} />
  }
  return <>{children}</>
}
