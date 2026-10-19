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

/** Trang gốc: vào đúng khung theo vai trò, chưa đăng nhập thì sang trang đăng nhập. */
function RootRedirect() {
  const { user } = useAuth()
  return <Navigate to={user ? shellHome[shellOf(user)] : '/dang-nhap'} replace />
}

/** Route con từ mục điều hướng; mỗi màn hình thật thay ScreenPlaceholder trong features/ của người phụ trách. */
function childRoutes(base: string, items: NavItem[]): RouteObject[] {
  const unique = new Map(items.map((i) => [i.to, i]))
  return [...unique.values()].map((item) => {
    const element = <ScreenPlaceholder screen={item.screen} title={item.label} owner={item.owner} />
    return item.to === base
      ? { index: true, element }
      : {
          path: item.to.slice(base.length + 1),
          element: item.to === '/sv/kham-pha' ? <TeamFormationOnly>{element}</TeamFormationOnly> : element,
        }
  })
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
    children: childRoutes('/admin', adminNav),
  },
  ...(['student', 'lecturer', 'mentor'] as const).map(
    (shell): RouteObject => ({
      path: shellHome[shell],
      element: (
        <RequireShell shell={shell}>
          <RoleShell shell={shell} />
        </RequireShell>
      ),
      children: childRoutes(shellHome[shell], allRoleItems(shell)),
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
