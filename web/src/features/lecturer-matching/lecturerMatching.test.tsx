import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import type { AdvisorMatchingOverview, AdvisorMatchingRow, AdvisorMatchingRun } from './api'

const get = vi.fn()
const post = vi.fn()
const put = vi.fn()
const del = vi.fn()
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: {
    get: (...args: unknown[]) => get(...args),
    post: (...args: unknown[]) => post(...args),
    put: (...args: unknown[]) => put(...args),
    delete: (...args: unknown[]) => del(...args),
  },
}))

const { ApiError } = await import('@/shared/api/apiClient')
const { adminLecturerMatchingRoutes } = await import('./routes')
const { tokenStore } = await import('@/shared/api/tokenStore')
const { AuthProvider } = await import('@/shared/auth/AuthProvider')
const { PageHeaderProvider, usePageHeaderState } = await import('@/shared/layout/PageHeader')

const team = (id: string, name: string) => ({
  id,
  name,
  courseCode: 'EXE101' as const,
  status: 'ACTIVE' as const,
  domains: [],
  memberCount: 5,
  openSlots: 1,
})

const lecturer = (lecturerId: string, fullName: string) => ({ lecturerId, fullName })

const rows: AdvisorMatchingRow[] = [
  {
    team: team('t1', 'GreenBite'),
    advisorId: 'l1',
    advisorName: 'Trần Minh Khoa',
    reason: 'MUTUAL',
    preferenceRank: 1,
    preferences: [lecturer('l1', 'Trần Minh Khoa'), lecturer('l2', 'Lê Thu Hà')],
    interests: [{ id: 'i1', advisorId: 'l1', advisorName: 'Trần Minh Khoa', status: 'CLOSED' }],
  },
  {
    team: team('t2', 'EduMate'),
    advisorId: null,
    advisorName: null,
    reason: null,
    preferenceRank: null,
    preferences: [lecturer('l2', 'Lê Thu Hà')],
    interests: [{ id: 'i2', advisorId: 'l3', advisorName: 'Phạm Văn Nam', status: 'OPEN' }],
  },
]

const loads = [
  { advisorId: 'l1', fullName: 'Trần Minh Khoa', assignedCount: 5, maxTeams: 5 },
  { advisorId: 'l2', fullName: 'Lê Thu Hà', assignedCount: 2, maxTeams: 5 },
  { advisorId: 'l3', fullName: 'Phạm Văn Nam', assignedCount: 7, maxTeams: 6 },
]

const overview = (patch: Partial<AdvisorMatchingOverview> = {}): AdvisorMatchingOverview => ({
  matchingAt: null,
  lastRun: null,
  teamsWithPreferences: 2,
  teamsWithoutAdvisor: 1,
  items: rows,
  advisorLoads: loads,
  ...patch,
})

const previewRun: AdvisorMatchingRun = {
  id: 'run1',
  advisorKind: 'LECTURER',
  courseCode: 'EXE101',
  trigger: 'EARLY',
  status: 'PREVIEW',
  matchedCount: 1,
  unmatchedCount: 1,
  createdAt: '2026-11-01T03:00:00Z',
  appliedAt: null,
  plan: [
    { teamId: 't2', teamName: 'EduMate', advisorId: 'l2', advisorName: 'Lê Thu Hà', reason: 'PREFERENCE', preferenceRank: 1 },
    { teamId: 't3', teamName: 'FarmLink', advisorId: null, advisorName: null, reason: null, preferenceRank: null },
  ],
}

/** Hiện nút hành động chính của PageHeader như khung trang thật. */
function HeaderAction() {
  return <>{usePageHeaderState().action}</>
}

function renderAt(path = '/admin/ghep-giang-vien') {
  return render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <AuthProvider>
        <PageHeaderProvider>
          <HeaderAction />
          <RouterProvider router={createMemoryRouter([{ path: '/admin', children: adminLecturerMatchingRoutes }], { initialEntries: [path] })} />
        </PageHeaderProvider>
      </AuthProvider>
    </QueryClientProvider>,
  )
}

describe('W19 – ghép giảng viên hướng dẫn', () => {
  beforeAll(() => {
    HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
      this.open = true
    }
    HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) {
      this.open = false
    }
  })

  beforeEach(() => {
    get.mockReset()
    post.mockReset()
    put.mockReset()
    del.mockReset()
    tokenStore.save({
      accessToken: 'a',
      accessTokenExpiresAt: '',
      refreshToken: 'r',
      refreshTokenExpiresAt: '',
      user: { id: 'u', email: 'admin@fpt.edu.vn', fullName: 'Bộ môn', roles: ['Admin'] },
    })
  })

  it('before the deadline shows progress and previews then confirms early matching', async () => {
    const deadline = new Date(Date.now() + 50 * 3600e3).toISOString()
    get.mockResolvedValue({ data: overview({ matchingAt: deadline, totalCount: 8 }) })
    post.mockImplementation(async (url: string) => {
      if (url === '/api/advisor-matching/LECTURER/preview') return { data: previewRun }
      if (url === '/api/advisor-matching/runs/run1/confirm') return { data: { ...previewRun, status: 'APPLIED' } }
      throw new Error(url)
    })

    renderAt()

    expect(await screen.findByText('2/8')).toBeInTheDocument()
    expect(screen.getByText('Nhóm đã có nguyện vọng')).toBeInTheDocument()
    expect(screen.getByText('Giảng viên đang quan tâm').nextSibling).toHaveTextContent('1')
    expect(screen.getByText(/Hệ thống tự ghép lúc/)).toBeInTheDocument()
    expect(get).toHaveBeenCalledWith('/api/advisor-matching/LECTURER/overview', expect.objectContaining({ params: expect.objectContaining({ courseCode: 'EXE101', page: 1 }) }))

    await userEvent.click(screen.getByRole('button', { name: 'Ghép sớm' }))
    const dialog = await screen.findByRole('dialog')
    expect(post).toHaveBeenCalledWith('/api/advisor-matching/LECTURER/preview', { courseCode: 'EXE101' })
    expect(within(dialog).getByText('Nguyện vọng 1')).toBeInTheDocument()
    expect(within(dialog).getByText('Chưa có giảng viên')).toBeInTheDocument()

    await userEvent.click(within(dialog).getByRole('button', { name: 'Chốt kết quả' }))
    expect(await screen.findByText('Đã ghép giảng viên cho 1 nhóm')).toBeInTheDocument()
    expect(post).toHaveBeenCalledWith('/api/advisor-matching/runs/run1/confirm')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('reports run-not-preview and discards a preview', async () => {
    get.mockResolvedValue({ data: overview({ matchingAt: new Date(Date.now() + 3600e3 * 30).toISOString(), lastRun: previewRun }) })
    post.mockRejectedValue(new ApiError({ status: 409, code: 'run-not-preview', title: 'x' }))
    del.mockResolvedValue({ data: undefined })

    renderAt()

    await userEvent.click(await screen.findByRole('button', { name: 'Xem lại' }))
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Chốt kết quả' }))
    expect(await screen.findByText('Kết quả xem trước không còn dùng được')).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Xem lại' }))
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Bỏ kết quả' }))
    expect(await screen.findByText('Đã bỏ kết quả xem trước')).toBeInTheDocument()
    expect(del).toHaveBeenCalledWith('/api/advisor-matching/runs/run1')
  })

  it('shows matching-unavailable when the preview cannot be computed', async () => {
    get.mockResolvedValue({ data: overview({ matchingAt: new Date(Date.now() + 3600e3 * 30).toISOString() }) })
    post.mockRejectedValue(new ApiError({ status: 503, code: 'matching-unavailable', title: 'Service Unavailable' }))

    renderAt()

    await userEvent.click(await screen.findByRole('button', { name: 'Ghép sớm' }))
    expect(await screen.findByText('Chưa tính được kết quả ghép')).toBeInTheDocument()
  })

  it('after matching lists teams with reasons, preferences, interests and lecturer loads', async () => {
    get.mockResolvedValue({
      data: overview({ lastRun: { ...previewRun, status: 'APPLIED', trigger: 'SCHEDULED', appliedAt: '2026-10-20T11:00:00Z' } }),
    })

    renderAt()

    const table = await screen.findByRole('table')
    expect(within(table).getByText('GreenBite')).toBeInTheDocument()
    expect(within(table).getByText('Hai bên cùng chọn')).toBeInTheDocument()
    expect(within(table).getByText('Chưa có giảng viên')).toBeInTheDocument()
    expect(within(table).getByText('Phạm Văn Nam')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Ghép sớm' })).toBeInTheDocument()
    expect(screen.getByText('1 nhóm chưa có giảng viên')).toBeInTheDocument()

    expect(screen.getByRole('meter', { name: 'Số nhóm của Lê Thu Hà' })).toHaveAttribute('aria-valuenow', '2')
    expect(screen.getByText('7/6 nhóm')).toBeInTheDocument()
    expect(screen.getByText('Quá tải')).toBeInTheDocument()
    expect(screen.getByText('Đã đủ nhóm')).toBeInTheDocument()
  })

  it('hides matching once every team has a lecturer', async () => {
    get.mockResolvedValue({ data: overview({ teamsWithoutAdvisor: 0 }) })

    renderAt()

    expect(await screen.findByRole('table')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Ghép sớm' })).not.toBeInTheDocument()
  })

  it('filters teams without a lecturer and shows the empty state', async () => {
    get.mockImplementation(async (_url: string, config: { params: { withoutAdvisor?: boolean } }) => ({
      data: config.params.withoutAdvisor ? overview({ items: [], teamsWithoutAdvisor: 0 }) : overview(),
    }))

    renderAt()

    await userEvent.click(await screen.findByRole('button', { name: 'Chưa có giảng viên', pressed: false }))
    expect(await screen.findByRole('button', { name: 'Xem tất cả nhóm' })).toBeInTheDocument()
    expect(get).toHaveBeenLastCalledWith('/api/advisor-matching/LECTURER/overview', expect.objectContaining({ params: expect.objectContaining({ withoutAdvisor: true }) }))
  })

  it('changes a lecturer by hand with a required reason and handles lecturer-full', async () => {
    get.mockResolvedValue({ data: overview() })
    put.mockRejectedValueOnce(new ApiError({ status: 409, code: 'lecturer-full', title: 'Giảng viên đã đủ số nhóm', detail: 'Lê Thu Hà đã hướng dẫn 5 nhóm (tối đa 5).' }))
    put.mockResolvedValueOnce({ data: {} })

    renderAt()

    await userEvent.click(await screen.findByRole('button', { name: 'Gán giảng viên cho EduMate' }))
    const dialog = screen.getByRole('dialog')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Gán giảng viên' }))
    expect(within(dialog).getByText('Chọn giảng viên.')).toBeInTheDocument()
    expect(within(dialog).getByText('Nhập lý do đổi giảng viên.')).toBeInTheDocument()
    expect(put).not.toHaveBeenCalled()

    const select = within(dialog).getByRole('combobox')
    expect(within(select).getByRole('option', { name: /Trần Minh Khoa.*đã đủ/ })).toBeDisabled()
    await userEvent.selectOptions(select, 'l2')
    await userEvent.type(within(dialog).getByRole('textbox'), 'Cân bằng số nhóm')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Gán giảng viên' }))
    expect(await within(dialog).findByText('Lê Thu Hà đã hướng dẫn 5 nhóm (tối đa 5).')).toBeInTheDocument()

    await userEvent.click(within(dialog).getByRole('button', { name: 'Gán giảng viên' }))
    expect(await screen.findByText('Đã gán Lê Thu Hà cho nhóm EduMate')).toBeInTheDocument()
    expect(put).toHaveBeenLastCalledWith('/api/teams/t2/instructor', { lecturerId: 'l2', note: 'Cân bằng số nhóm' })
  })

  it('shows an error with retry when the overview fails', async () => {
    get.mockRejectedValueOnce(new ApiError({ status: 503, code: 'academic-unavailable', title: 'Chưa lấy được danh sách cố vấn' }))
    get.mockResolvedValue({ data: overview() })

    renderAt()

    expect(await screen.findByText('Chưa lấy được danh sách cố vấn')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Thử lại' }))
    await waitFor(() => expect(screen.getByRole('table')).toBeInTheDocument())
  })
})
