import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { afterEach, describe, expect, it } from 'vitest'
import { tokenStore, type Role, type UserInfo } from '@/shared/api/tokenStore'
import { AccountMenu } from '@/shared/layout/AccountMenu'
import { AuthProvider, shellOf, shellsOf } from './AuthProvider'
import { RequireShell } from './RequireShell'

const user = (...roles: Role[]): UserInfo => ({ id: 'u1', email: 'a@example.com', fullName: 'Người dùng', roles })

function signIn(...roles: Role[]) {
  tokenStore.save({
    accessToken: 'a',
    accessTokenExpiresAt: '2026-11-05T01:00:00Z',
    refreshToken: 'r',
    refreshTokenExpiresAt: '2026-11-12T01:00:00Z',
    user: user(...roles),
  })
}

function Where() {
  return <p data-testid="path">{useLocation().pathname}</p>
}

function renderAt(path: string) {
  render(
    <QueryClientProvider client={new QueryClient()}>
      <AuthProvider>
        <MemoryRouter initialEntries={[path]}>
          <Where />
          <AccountMenu />
          <Routes>
            <Route path="/admin/*" element={<RequireShell shell="admin"><p>Khu Bộ môn</p></RequireShell>} />
            <Route path="/sv/*" element={<RequireShell shell="student"><p>Khu sinh viên</p></RequireShell>} />
            <Route path="/gv/*" element={<RequireShell shell="lecturer"><p>Khu giảng viên</p></RequireShell>} />
            <Route path="/mentor/*" element={<RequireShell shell="mentor"><p>Khu mentor</p></RequireShell>} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </QueryClientProvider>,
  )
}

const openMenu = () => userEvent.click(screen.getByRole('button', { name: /Tài khoản/ }))

describe('Khung theo vai trò', () => {
  afterEach(() => tokenStore.clear())

  it('liệt kê đúng khung của từng tổ hợp vai trò, khung đầu là mặc định', () => {
    expect(shellsOf(user('Student'))).toEqual(['student'])
    expect(shellsOf(user('Admin'))).toEqual(['admin'])
    expect(shellsOf(user('Mentor'))).toEqual(['mentor'])
    expect(shellsOf(user('Admin', 'Student'))).toEqual(['admin', 'student'])
    expect(shellOf(user('Admin', 'Student'))).toBe('admin')
  })

  it('giảng viên kiêm mentor chỉ có khung giảng viên (user-flow mục 0)', () => {
    expect(shellsOf(user('Lecturer', 'Mentor'))).toEqual(['lecturer'])
  })

  it.each<[Role, string]>([
    ['Student', '/admin'],
    ['Lecturer', '/admin'],
    ['Mentor', '/admin'],
    ['Student', '/gv'],
    ['Lecturer', '/sv'],
    ['Mentor', '/sv'],
    ['Admin', '/sv'],
  ])('%s chỉ có một vai trò thì không vào được %s', (role, path) => {
    signIn(role)
    renderAt(path)

    expect(screen.getByText('Bạn không có quyền xem trang này')).toBeInTheDocument()
  })

  it('tài khoản Admin và Student vào được cả hai khung', () => {
    signIn('Admin', 'Student')
    renderAt('/sv')

    expect(screen.getByText('Khu sinh viên')).toBeInTheDocument()
    expect(screen.queryByText('Khu Bộ môn')).not.toBeInTheDocument()
  })

  it('tài khoản nhiều vai trò chuyển khung từ menu tài khoản', async () => {
    signIn('Admin', 'Student')
    renderAt('/admin')

    await openMenu()
    expect(screen.getByText('Đang xem: Bộ môn')).toBeInTheDocument()
    expect(screen.queryByRole('menuitem', { name: 'Chuyển sang Bộ môn' })).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('menuitem', { name: 'Chuyển sang Sinh viên' }))

    expect(screen.getByTestId('path')).toHaveTextContent('/sv')
    expect(screen.getByText('Khu sinh viên')).toBeInTheDocument()
  })

  it('tài khoản một vai trò không thấy mục chuyển khung', async () => {
    signIn('Student')
    renderAt('/sv')

    await openMenu()

    expect(screen.queryByRole('menuitem', { name: /Chuyển sang/ })).not.toBeInTheDocument()
    expect(screen.queryByText(/Đang xem/)).not.toBeInTheDocument()
  })
})
