import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const get = vi.fn()
const post = vi.fn()
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: { get: (...args: unknown[]) => get(...args), post: (...args: unknown[]) => post(...args) },
}))

const { ApiError } = await import('@/shared/api/apiClient')
const { adminTeamRoutes } = await import('./routes')
const { PageHeaderProvider, usePageHeaderState } = await import('@/shared/layout/PageHeader')

/** Nút hành động chính của trang hiện ở header của khung (W25); test tự hiện nó. */
function HeaderAction() {
  return <>{usePageHeaderState().action}</>
}

const requirement = { memberCount: 3, minSize: 4, maxSize: 6, specializationCount: 1, minSpecializations: 2, isValid: false, missingSpecializations: [{ id: 2, code: 'MKT', name: 'Marketing' }] }
const card = {
  id: 't1', name: 'Nhóm Bình Minh', projectName: 'EduSpark', courseCode: 'EXE101', status: 'ACTIVE', memberCount: 3, openSlots: 3,
  domains: [{ id: 4, code: 'D04', name: 'Giáo dục' }], leader: { id: 's1', studentCode: 'SE180001', fullName: 'Trưởng Nhóm' }, requirement,
}
const other = { ...card, id: 't2', name: 'Nhóm Hoàng Hôn', memberCount: 4, requirement: { ...requirement, isValid: true } }
const detail = {
  ...card,
  ideaDescription: 'Học nhóm có AI',
  members: [
    { id: 's1', studentCode: 'SE180001', fullName: 'Trưởng Nhóm', role: 'LEADER', specialization: { id: 1, code: 'SE', name: 'Kỹ thuật phần mềm' } },
    { id: 's2', studentCode: 'SE180002', fullName: 'Thành Viên', role: 'MEMBER' },
  ],
}
const paged = (items: unknown[]) => ({ items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1 })

function renderAt(path: string) {
  get.mockImplementation(async (url: string, config?: { params?: Record<string, unknown> }) => {
    if (url === '/api/teams') return { data: paged(config?.params?.minOpenSlots ? [card, other] : [card]) }
    if (url === '/api/teams/t1') return { data: detail }
    if (url === '/api/teams/t1/audit-logs')
      return { data: paged([{ id: 1, teamId: 't1', action: 'TEAM_CREATED', actorName: 'Sinh viên', actorRole: 'Student', summary: 'Tạo nhóm Nhóm Bình Minh', occurredAt: '2026-10-27T03:00:00Z' }]) }
    if (url === '/api/unmatched-students') return { data: paged([{ id: 's9', studentCode: 'SE180009', fullName: 'Lê Vy' }]) }
    if (url === '/api/domains') return { data: [{ id: 4, code: 'D04', name: 'Giáo dục' }] }
    if (url === '/api/semesters/current') return { data: { id: 'sem', code: 'FA26' } }
    throw new Error(`GET ${url} chưa giả lập`)
  })
  const router = createMemoryRouter([{ path: '/admin', children: adminTeamRoutes }], { initialEntries: [path] })
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <PageHeaderProvider>
        <HeaderAction />
        <RouterProvider router={router} />
      </PageHeaderProvider>
    </QueryClientProvider>,
  )
  return router
}

describe('W15, W16 – nhóm của Bộ môn (FE-21, FE-22)', () => {
  beforeEach(() => {
    get.mockReset()
    post.mockReset()
    HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
      this.open = true
    }
    HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) {
      this.open = false
    }
  })

  it('W15: bảng như Group List, đánh dấu chưa đủ điều kiện, lọc gửi invalidOnly, bấm dòng mở W16', async () => {
    const router = renderAt('/admin/nhom')
    const user = userEvent.setup()

    const row = (await screen.findByText('Nhóm Bình Minh')).closest('tr')!
    expect(within(row).getByText('EduSpark')).toBeInTheDocument()
    expect(within(row).getByText('D04')).toBeInTheDocument()
    expect(within(row).getByText('3/6')).toBeInTheDocument()
    expect(within(row).getByText('Trưởng Nhóm')).toBeInTheDocument()
    expect(within(row).getByText('Chưa ghép')).toBeInTheDocument()
    expect(within(row).getByText('Chưa đủ điều kiện')).toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('Điều kiện'), 'true')
    expect(get).toHaveBeenLastCalledWith('/api/teams', expect.objectContaining({ params: expect.objectContaining({ courseCode: 'EXE101', invalidOnly: true }) }))

    await user.click(within((await screen.findByText('Nhóm Bình Minh')).closest('tr')!).getByText('EduSpark'))
    expect(router.state.location.pathname).toBe('/admin/nhom/t1')
  })

  it('W16: thành viên, ý tưởng, nhật ký; chốt nhóm qua dialog, lỗi 409 hiện đúng câu', async () => {
    post.mockRejectedValueOnce(new ApiError({ status: 409, code: 'composition-invalid', title: 'Nhóm chưa đủ điều kiện', detail: 'Cần ít nhất 4 thành viên.' }))
    renderAt('/admin/nhom/t1')
    const user = userEvent.setup()

    expect(await screen.findByText('Học nhóm có AI')).toBeInTheDocument()
    expect(screen.getByText('Thành viên (3/6)')).toBeInTheDocument()
    expect(screen.getByText(/thiếu Marketing/)).toBeInTheDocument()
    expect(await screen.findByText('Tạo nhóm Nhóm Bình Minh')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Chốt nhóm' }))
    const dialog = screen.getByRole('dialog', { name: 'Chốt nhóm?' })
    await user.click(within(dialog).getByRole('button', { name: 'Chốt nhóm' }))
    expect(post).toHaveBeenCalledWith('/api/teams/t1/finalize')
    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Cần ít nhất 4 thành viên.')
  })

  it('W16: thêm sinh viên chưa có nhóm và chuyển thành viên sang nhóm khác đều cần xác nhận', async () => {
    post.mockResolvedValue({ data: {} })
    renderAt('/admin/nhom/t1')
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Thêm thành viên' }))
    await user.click(await screen.findByRole('button', { name: 'Chọn' }))
    await user.click(within(screen.getByRole('dialog', { name: 'Thêm Lê Vy vào nhóm?' })).getByRole('button', { name: 'Thêm' }))
    expect(post).toHaveBeenCalledWith('/api/teams/t1/members', { studentId: 's9', reason: undefined })

    await user.click(screen.getAllByRole('button', { name: 'Chuyển nhóm' })[1])
    await user.selectOptions(await screen.findByLabelText('Nhóm đích'), 't2')
    await user.click(screen.getByRole('button', { name: 'Tiếp tục' }))
    await user.click(within(screen.getByRole('dialog', { name: 'Chuyển Thành Viên?' })).getByRole('button', { name: 'Chuyển' }))
    expect(post).toHaveBeenCalledWith('/api/teams/t1/members/s2/move', { targetTeamId: 't2' })
  })
})
