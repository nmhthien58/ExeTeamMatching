import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { NotificationsPage } from './NotificationsPage'
import { NotificationNavIcon } from './NotificationNavIcon'

const get = vi.fn()
const patch = vi.fn()
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: { get: (...args: unknown[]) => get(...args), patch: (...args: unknown[]) => patch(...args) },
}))

function renderPage(roleRoot: '/sv' | '/gv' = '/sv') {
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    <MemoryRouter initialEntries={[`${roleRoot}/thong-bao`]}><Routes>
      <Route path={`${roleRoot}/thong-bao`} element={<NotificationsPage roleRoot={roleRoot} />} />
      <Route path="/sv/nhom" element={<p>Nhóm của tôi</p>} />
      <Route path="/gv/du-an/:workspaceId" element={<p>Dự án của giảng viên</p>} />
    </Routes></MemoryRouter>
  </QueryClientProvider>)
}

describe('A06 web notifications (FE-42)', () => {
  beforeEach(() => {
    get.mockReset()
    patch.mockReset().mockResolvedValue({ data: null })
    get.mockImplementation(async (url: string) => url === '/api/notifications/unread-count'
      ? { data: { count: 1 } }
      : { data: { page: 1, totalPages: 1, items: [{ id: 'n-1', type: 'InvitationSent', title: 'Lời mời mới', content: 'Nhóm A mời bạn', linkUrl: 'app://teams/team-1', isRead: false, createdAt: '2026-11-02T08:00:00Z' }] } })
  })

  it('shows unread count and opens a related screen after marking read', async () => {
    const user = userEvent.setup()
    renderPage()
    expect(await screen.findByText('Lời mời mới')).toBeInTheDocument()
    expect(screen.getByText('1 thông báo chưa đọc')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /Lời mời mới/ }))
    expect(patch).toHaveBeenCalledWith('/api/notifications/n-1/read')
    expect(await screen.findByText('Nhóm của tôi')).toBeInTheDocument()
  })

  it('marks all notifications read', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('Lời mời mới')
    await user.click(screen.getByRole('button', { name: 'Đánh dấu đã đọc tất cả' }))
    expect(patch).toHaveBeenCalledWith('/api/notifications/read-all')
  })

  it('opens the lecturer workspace from a workspace notification', async () => {
    get.mockImplementation(async (url: string) => url === '/api/notifications/unread-count'
      ? { data: { count: 1 } }
      : { data: { page: 1, totalPages: 1, items: [{ id: 'n-2', type: 'TaskAssigned', title: 'Việc mới', content: 'Xem dự án', linkUrl: 'app://workspaces/workspace-1', isRead: false, createdAt: '2026-11-02T08:00:00Z' }] } })
    renderPage('/gv')
    await userEvent.setup().click(await screen.findByRole('button', { name: /Việc mới/ }))
    expect(await screen.findByText('Dự án của giảng viên')).toBeInTheDocument()
  })

  it('shows unread count on the navigation icon', async () => {
    get.mockImplementation(async (url: string) => url === '/api/notifications/unread-count' ? { data: { count: 103 } } : { data: { page: 1, totalPages: 0, items: [] } })
    render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><NotificationNavIcon /></QueryClientProvider>)
    expect(await screen.findByText('99+')).toBeInTheDocument()
    expect(screen.getByLabelText('103 thông báo chưa đọc')).toBeInTheDocument()
  })
})
