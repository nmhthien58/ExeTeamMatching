import { render } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider, type RouteObject } from 'react-router-dom'
import { tokenStore } from '@/shared/api/tokenStore'
import { AuthProvider } from '@/shared/auth/AuthProvider'
import { PageHeaderProvider, usePageHeaderState } from '@/shared/layout/PageHeader'

/** Phần dựng test chung của W19, W28. */

/** Hiện nút hành động chính của PageHeader như khung trang thật. */
function HeaderAction() {
  return <>{usePageHeaderState().action}</>
}

export function renderMatchingPage(routes: RouteObject[], path: string) {
  return render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <AuthProvider>
        <PageHeaderProvider>
          <HeaderAction />
          <RouterProvider router={createMemoryRouter([{ path: '/admin', children: routes }], { initialEntries: [path] })} />
        </PageHeaderProvider>
      </AuthProvider>
    </QueryClientProvider>,
  )
}

export function stubDialogElement() {
  HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
    this.open = true
  }
  HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) {
    this.open = false
  }
}

export function signInAsAdmin() {
  tokenStore.save({
    accessToken: 'a',
    accessTokenExpiresAt: '',
    refreshToken: 'r',
    refreshTokenExpiresAt: '',
    user: { id: 'u', email: 'admin@fpt.edu.vn', fullName: 'Bộ môn', roles: ['Admin'] },
  })
}
