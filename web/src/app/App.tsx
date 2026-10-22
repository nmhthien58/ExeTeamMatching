import { createBrowserRouter, Navigate, RouterProvider, type RouteObject } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { GoogleOAuthProvider } from '@react-oauth/google'
import { useState } from 'react'
import { createQueryClient } from '@/shared/api/queryClient'
import { AuthProvider, shellHome, shellOf, useAuth, type Shell } from '@/shared/auth/AuthProvider'
import { RequireShell } from '@/shared/auth/RequireShell'
import { env } from '@/shared/config/env'
import { NotFound } from '@/shared/components/States'
import { ScreenPlaceholder } from '@/shared/components/ScreenPlaceholder'
import { AdminLayout } from '@/shared/layout/AdminLayout'
import { RoleShell } from '@/shared/layout/RoleShell'
import { adminNav, roleNav, type NavItem } from '@/shared/layout/navigation'
import { TeamFormationOnly } from '@/shared/phase/PhaseGuard'
import { LoginPage } from '@/features/auth/LoginPage'
import { featureRoutes } from './featureRoutes'

/** Trang gốc: vào đúng khung theo vai trò, chưa đăng nhập thì sang trang đăng nhập. */
function RootRedirect() {
  const { user } = useAuth()
  return <Navigate to={user ? shellHome[shellOf(user)] : '/dang-nhap'} replace />
}

/**
 * Route con từ mục điều hướng; mục nào đã có màn hình thật trong `featureRoutes` (features/ của người phụ trách)
 * thì dùng route đó thay cho ScreenPlaceholder.
 */
function childRoutes(base: string, items: NavItem[], features: RouteObject[]): RouteObject[] {
  const provided = new Set(features.map((r) => (r.index ? base : `${base}/${r.path}`)))
  const unique = new Map(items.filter((i) => !provided.has(i.to)).map((i) => [i.to, i]))
  const placeholders = [...unique.values()].map((item): RouteObject => {
    const element = <ScreenPlaceholder screen={item.screen} title={item.label} owner={item.owner} />
    return item.to === base
      ? { index: true, element }
      : {
          path: item.to.slice(base.length + 1),
          element: item.to === '/sv/kham-pha' ? <TeamFormationOnly>{element}</TeamFormationOnly> : element,
        }
  })
  return [...placeholders, ...features]
}

/** Mọi route có thể có của khung (cả hai giai đoạn), để link cũ không thành 404. */
function allRoleItems(shell: Exclude<Shell, 'admin'>) {
  return [
    ...roleNav(shell, { isTeamFormation: true, isMentoringWindow: false }),
    ...roleNav(shell, { isTeamFormation: false, isMentoringWindow: true }),
  ]
}

const router = createBrowserRouter([
  { path: '/', element: <RootRedirect /> },
  { path: '/dang-nhap', element: <LoginPage /> },
  {
    path: '/admin',
    element: (
      <RequireShell shell="admin">
        <AdminLayout />
      </RequireShell>
    ),
    children: childRoutes('/admin', adminNav, featureRoutes.admin),
  },
  ...(['student', 'lecturer', 'mentor'] as const).map(
    (shell): RouteObject => ({
      path: shellHome[shell],
      element: (
        <RequireShell shell={shell}>
          <RoleShell shell={shell} />
        </RequireShell>
      ),
      children: childRoutes(shellHome[shell], allRoleItems(shell), featureRoutes[shell]),
    }),
  ),
  { path: '*', element: <NotFound /> },
])

export function App() {
  const [queryClient] = useState(createQueryClient)
  return (
    <GoogleOAuthProvider clientId={env.googleClientId || 'missing-client-id'}>
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <RouterProvider router={router} />
        </AuthProvider>
      </QueryClientProvider>
    </GoogleOAuthProvider>
  )
}
