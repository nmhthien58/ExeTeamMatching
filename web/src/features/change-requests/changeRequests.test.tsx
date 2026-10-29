import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const get = vi.fn()
const post = vi.fn()
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: { get: (...args: unknown[]) => get(...args), post: (...args: unknown[]) => post(...args) },
}))

const { changeRequestRoutes } = await import('./routes')
const { PageHeaderProvider } = await import('@/shared/layout/PageHeader')

const request = {
  id: 'cr-1',
  team: { id: 'team-1', name: 'EduSpark', courseCode: 'EXE201', memberCount: 5 },
  roundId: 'round-1', roundName: 'Đợt đổi nhóm', reason: 'Thành viên chuyển lịch học.',
  changedMemberCount: 1, changedPercent: 20, status: 'PENDING', reviewedByName: null,
  reviewedAt: null, reviewNote: null, createdAt: '2026-10-21T08:00:00Z',
}
const detail = {
  ...request,
  baselineMembers: [{ id: 's1', studentCode: 'SE180001', fullName: 'Nguyễn An' }, { id: 's2', studentCode: 'SE180002', fullName: 'Trần Bình' }],
  membersAfterChange: [{ id: 's1', studentCode: 'SE180001', fullName: 'Nguyễn An' }, { id: 's3', studentCode: 'SE180003', fullName: 'Lê Chi' }],
  items: [], projectBefore: { name: 'Dự án cũ', description: 'Mô tả cũ' },
  projectAfter: { name: 'Dự án mới', description: 'Mô tả mới' },
}

async function getResponse(url: string) {
  if (url === '/api/semesters/current') return { data: { id: 'semester-1' } }
  if (url === '/api/registration-rounds') return { data: [{ id: 'round-1', name: 'Đợt đổi nhóm', roundType: 'CHANGE_REQUEST' }] }
  if (url === '/api/change-requests') return { data: { page: 1, pageSize: 20, totalCount: 1, totalPages: 1, items: [request] } }
  if (url === '/api/change-requests/cr-1') return { data: detail }
  throw new Error(url)
}

function renderAt(path: string) {
  const router = createMemoryRouter([{ path: '/admin', children: changeRequestRoutes }], { initialEntries: [path] })
  return render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><PageHeaderProvider><RouterProvider router={router} /></PageHeaderProvider></QueryClientProvider>)
}

describe('change requests W17–W18', () => {
  afterEach(() => vi.unstubAllGlobals())
  beforeEach(() => {
    // Node's Request rejects jsdom's AbortSignal during client-side navigation.
    const NativeRequest = globalThis.Request
    vi.stubGlobal('Request', class extends NativeRequest {
      constructor(input: RequestInfo | URL, init?: RequestInit) {
        super(input, { ...init, signal: undefined })
      }
    })
    get.mockReset()
    post.mockReset()
    HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) { this.open = true }
    HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) { this.open = false }
    get.mockImplementation(getResponse)
    post.mockRejectedValue(new Error('Máy chủ chưa sẵn sàng'))
  })

  it('W17 shows status tabs, round filter, and changed member count and percent', async () => {
    renderAt('/admin/yeu-cau-doi-nhom')
    await waitFor(() => expect(screen.getByText('EduSpark')).toBeInTheDocument())
    expect(screen.getByRole('tab', { name: 'Chờ duyệt' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByText('20%')).toBeInTheDocument()
    expect(screen.getByLabelText('Đợt')).toBeInTheDocument()
  })

  it('W18 compares membership and project changes and requires a rejection note', async () => {
    renderAt('/admin/yeu-cau-doi-nhom/cr-1')
    expect(await screen.findByText('Dự án cũ')).toBeInTheDocument()
    expect(screen.getByText('Lê Chi')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Từ chối' }))
    await userEvent.click(screen.getAllByRole('button', { name: 'Từ chối' }).at(-1)!)
    expect(await screen.findByText('Nhập ghi chú để từ chối yêu cầu.')).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
    await userEvent.type(screen.getByLabelText('Ghi chú từ chối'), 'Không đủ thông tin')
    await userEvent.click(screen.getAllByRole('button', { name: 'Từ chối' }).at(-1)!)
    expect(post).toHaveBeenCalledWith('/api/change-requests/cr-1/reject', { note: 'Không đủ thông tin' })
  })

  it('W17 sends the selected status and round to the list API', async () => {
    renderAt('/admin/yeu-cau-doi-nhom')
    await screen.findByText('EduSpark')
    await userEvent.click(screen.getByRole('tab', { name: 'Đã duyệt' }))
    await waitFor(() => expect(get).toHaveBeenCalledWith('/api/change-requests', expect.objectContaining({
      params: expect.objectContaining({ status: 'APPROVED' }),
    })))
    await userEvent.selectOptions(screen.getByLabelText('Đợt'), 'round-1')
    await waitFor(() => expect(get).toHaveBeenCalledWith('/api/change-requests', expect.objectContaining({
      params: expect.objectContaining({ status: 'APPROVED', roundId: 'round-1' }),
    })))
  })

  it('W17 shows an empty state when there are no requests', async () => {
    get.mockImplementation(async (url: string) => url === '/api/change-requests'
      ? { data: { page: 1, pageSize: 20, totalCount: 0, totalPages: 0, items: [] } }
      : getResponse(url))
    renderAt('/admin/yeu-cau-doi-nhom')
    expect(await screen.findByText('Chưa có yêu cầu chờ duyệt')).toBeInTheDocument()
  })

  it('W17 retries after the list API fails', async () => {
    let failed = true
    get.mockImplementation(async (url: string) => {
      if (url === '/api/change-requests' && failed) throw new Error('Mất kết nối')
      return getResponse(url)
    })
    renderAt('/admin/yeu-cau-doi-nhom')
    expect(await screen.findByText('Không tải được dữ liệu')).toBeInTheDocument()
    failed = false
    await userEvent.click(screen.getByRole('button', { name: 'Thử lại' }))
    expect(await screen.findByText('EduSpark')).toBeInTheDocument()
  })

  it('W18 approves a pending request and returns to the approved list', async () => {
    post.mockResolvedValue({ data: { ...request, status: 'APPROVED' } })
    renderAt('/admin/yeu-cau-doi-nhom/cr-1')
    await screen.findByText('Dự án cũ')
    await userEvent.click(screen.getByRole('button', { name: 'Duyệt' }))
    await userEvent.click(screen.getAllByRole('button', { name: 'Duyệt' }).at(-1)!)
    await waitFor(() => expect(post).toHaveBeenCalledWith('/api/change-requests/cr-1/approve', {}))
    await waitFor(() => expect(screen.getByRole('tab', { name: 'Đã duyệt' })).toHaveAttribute('aria-selected', 'true'))
  })

  it('W18 rejects with a note and returns to the rejected list', async () => {
    post.mockResolvedValue({ data: { ...request, status: 'REJECTED' } })
    renderAt('/admin/yeu-cau-doi-nhom/cr-1')
    await screen.findByText('Dự án cũ')
    await userEvent.click(screen.getByRole('button', { name: 'Từ chối' }))
    await userEvent.type(screen.getByLabelText('Ghi chú từ chối'), 'Không đủ thông tin')
    await userEvent.click(screen.getAllByRole('button', { name: 'Từ chối' }).at(-1)!)
    await waitFor(() => expect(post).toHaveBeenCalledWith('/api/change-requests/cr-1/reject', { note: 'Không đủ thông tin' }))
    await waitFor(() => expect(screen.getByRole('tab', { name: 'Đã từ chối' })).toHaveAttribute('aria-selected', 'true'))
  })

  it('W18 keeps the decision dialog open when the API fails', async () => {
    renderAt('/admin/yeu-cau-doi-nhom/cr-1')
    await screen.findByText('Dự án cũ')
    await userEvent.click(screen.getByRole('button', { name: 'Duyệt' }))
    await userEvent.click(screen.getAllByRole('button', { name: 'Duyệt' }).at(-1)!)
    expect(await screen.findByRole('alert')).toBeInTheDocument()
    expect(screen.getByText('Dự án cũ')).toBeInTheDocument()
  })

  it('W18 hides decision actions after a request has been reviewed', async () => {
    get.mockImplementation(async (url: string) => {
      if (url === '/api/change-requests/cr-1') return { data: { ...detail, status: 'APPROVED' } }
      throw new Error(url)
    })
    renderAt('/admin/yeu-cau-doi-nhom/cr-1')
    await screen.findByText('Dự án cũ')
    expect(screen.queryByRole('button', { name: 'Duyệt' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Từ chối' })).not.toBeInTheDocument()
  })
})
