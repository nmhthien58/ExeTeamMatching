import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const get = vi.fn()
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: { get: (...args: unknown[]) => get(...args) },
}))

const { AuditLogPage } = await import('./AuditLogPage')

const team = { id: 't1', name: 'Nhóm Bình Minh', courseCode: 'EXE101' }
const log = { id: 7, teamId: 't1', action: 'MEMBER_JOINED', actorName: 'Sinh viên', actorRole: 'Student', summary: 'SE180002 vào nhóm', occurredAt: '2026-10-27T03:05:00Z' }
const page = (items: unknown[]) => ({ items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1 })

function renderPage(path = '/admin/nhat-ky') {
  const router = createMemoryRouter([{ path: '/admin/nhat-ky', element: <AuditLogPage /> }], { initialEntries: [path] })
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
  return router
}

describe('W24 – Nhật ký hệ thống', () => {
  beforeEach(() => get.mockReset())

  it('hiện thời điểm, nhóm, người thực hiện, thay đổi; chọn nhóm thì lọc theo teamId trên URL', async () => {
    get.mockImplementation(async (url: string) => (url === '/api/teams' ? { data: page([team]) } : { data: page([log]) }))
    const router = renderPage()

    expect(await screen.findByText('SE180002 vào nhóm')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Nhóm Bình Minh' })).toBeInTheDocument()
    expect(screen.getByText('Sinh viên')).toBeInTheDocument()
    expect(screen.getByText('1–1 trên 1')).toBeInTheDocument()

    await userEvent.selectOptions(screen.getByLabelText('Nhóm'), 't1')
    expect(router.state.location.search).toBe('?teamId=t1')
    expect(get).toHaveBeenLastCalledWith('/api/audit-logs', expect.objectContaining({ params: expect.objectContaining({ teamId: 't1', page: 1 }) }))
  })

  it('chưa có nhật ký → trạng thái trống; ngày kết thúc trước ngày bắt đầu → báo lỗi', async () => {
    get.mockImplementation(async (url: string) => ({ data: url === '/api/teams' ? page([]) : page([]) }))
    renderPage('/admin/nhat-ky?from=2026-10-27&to=2026-10-01')
    expect(await screen.findByRole('alert')).toHaveTextContent('"Đến ngày" phải sau "Từ ngày".')
    expect(await screen.findByText('Không tìm thấy kết quả')).toBeInTheDocument()
  })
})
