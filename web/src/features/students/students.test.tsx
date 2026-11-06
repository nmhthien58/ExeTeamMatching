import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const get = vi.fn()
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: { get: (...args: unknown[]) => get(...args) },
}))

const { ApiError } = await import('@/shared/api/apiClient')
const { adminStudentRoutes } = await import('./routes')
const { PageHeaderProvider } = await import('@/shared/layout/PageHeader')

const se = { id: 1, code: 'SE', name: 'Kỹ thuật phần mềm', isActive: true }
const mkt = { id: 2, code: 'MKT', name: 'Marketing', isActive: true }
const an = {
  id: 's1', studentCode: 'SE100001', fullName: 'Nguyễn Văn An', email: 'an@example.com', specialization: se,
  classCode: 'SE1801', courseCode: 'EXE101', hasTeam: true, isSeekingTeam: false,
}
const binh = { ...an, id: 's2', studentCode: 'MK100002', fullName: 'Lê Thị Bình', email: 'binh@example.com', specialization: mkt, hasTeam: false, isSeekingTeam: true }
const anProfile = {
  ...an, phone: '0901234567', contactEmailVisible: true, profileCompleted: true, description: 'Thích làm backend.', currentTeamId: 't1',
  skills: [{ skill: 'TECHNOLOGY', level: 4 }, { skill: 'UI_UX_DESIGN', level: 2 }, { skill: 'MANAGEMENT', level: 3 }, { skill: 'MARKETING', level: 1 }, { skill: 'COMMUNICATION_PLANNING', level: 2 }],
}
const binhProfile = { ...binh, phone: null, contactEmailVisible: true, profileCompleted: false, description: null, currentTeamId: null, skills: [] }
const page = <T,>(items: T[]) => ({ items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1 })

function respond(overrides: Record<string, unknown> = {}) {
  get.mockImplementation(async (url: string) => {
    if (url in overrides) {
      const value = overrides[url]
      if (value instanceof Error) throw value
      return { data: value }
    }
    if (url === '/api/students') return { data: page([an, binh]) }
    if (url === '/api/class-codes') return { data: [{ id: 1, code: 'SE1801', name: 'SE1801', isActive: true }] }
    if (url === '/api/specializations') return { data: [se, mkt] }
    if (url === '/api/students/s1') return { data: anProfile }
    if (url === '/api/students/s2') return { data: binhProfile }
    if (url === '/api/teams/t1') return { data: { id: 't1', name: 'Nhóm Sao Mai', memberCount: 5, lecturerName: 'Trần Thu Hà', members: [] } }
    throw new Error(`GET ${url} chưa giả lập`)
  })
}

function renderAt(path: string) {
  const router = createMemoryRouter([{ path: '/admin', children: adminStudentRoutes }], { initialEntries: [path] })
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <PageHeaderProvider>
        <RouterProvider router={router} />
      </PageHeaderProvider>
    </QueryClientProvider>,
  )
}

describe('W09, W10 – Sinh viên (FE-15, FE-16)', () => {
  beforeEach(() => {
    get.mockReset()
  })

  it('W09: lists students with specialization, class code and team status', async () => {
    respond()
    renderAt('/admin/sinh-vien')
    const row = (await screen.findByText('Nguyễn Văn An')).closest('tr')!
    expect(within(row).getByText(/SE100001/)).toBeInTheDocument()
    expect(within(row).getByText('Kỹ thuật phần mềm')).toBeInTheDocument()
    expect(within(row).getByText('Đã có nhóm')).toBeInTheDocument()
    expect(within(screen.getByText('Lê Thị Bình').closest('tr')!).getByText('Đang tìm nhóm')).toBeInTheDocument()
  })

  it('W09: sends filters from the URL and pages by 20', async () => {
    respond()
    renderAt('/admin/sinh-vien?classCode=SE1801&specializationId=2&hasTeam=false&courseCode=EXE201')
    await screen.findByText('Nguyễn Văn An')
    expect(get).toHaveBeenCalledWith('/api/students', {
      params: expect.objectContaining({ classCode: 'SE1801', specializationId: '2', hasTeam: 'false', courseCode: 'EXE201', page: 1, pageSize: 20 }),
    })
  })

  it('W09: shows the empty state with a link to import', async () => {
    respond({ '/api/students': page([]) })
    renderAt('/admin/sinh-vien')
    expect(await screen.findByText('Chưa có sinh viên')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Import sinh viên/ })).toHaveAttribute('href', '/admin/import')
  })

  it('W10: opens the drawer with phone, skills and the current team', async () => {
    respond()
    renderAt('/admin/sinh-vien')
    await userEvent.click(await screen.findByText('Nguyễn Văn An'))
    const drawer = await screen.findByRole('dialog', { name: 'Nguyễn Văn An' })
    expect(within(drawer).getByText('0901234567')).toBeInTheDocument()
    expect(within(drawer).getByLabelText('Công nghệ, 4 trên 5')).toBeInTheDocument()
    expect(within(drawer).getByText('Thích làm backend.')).toBeInTheDocument()
    expect(await within(drawer).findByRole('link', { name: /Nhóm Sao Mai/ })).toHaveAttribute('href', '/admin/nhom/t1')
  })

  it('W10: a student without a team or a finished profile', async () => {
    respond()
    renderAt('/admin/sinh-vien')
    await userEvent.click(await screen.findByText('Lê Thị Bình'))
    const drawer = await screen.findByRole('dialog', { name: 'Lê Thị Bình' })
    expect(within(drawer).getByText('Chưa có nhóm · đang tìm nhóm')).toBeInTheDocument()
    expect(within(drawer).getByText('Sinh viên chưa hoàn thiện hồ sơ.')).toBeInTheDocument()
    expect(get).not.toHaveBeenCalledWith('/api/teams/null')
  })

  it('W10: shows an error with retry when the profile fails', async () => {
    respond({ '/api/students/s1': new ApiError({ status: 500, code: 'x', title: 'Lỗi máy chủ' }) })
    renderAt('/admin/sinh-vien')
    await userEvent.click(await screen.findByText('Nguyễn Văn An'))
    const drawer = await screen.findByRole('dialog', { name: 'Chi tiết sinh viên' })
    await waitFor(() => expect(within(drawer).getByRole('button', { name: /Thử lại/ })).toBeInTheDocument())
  })
})
