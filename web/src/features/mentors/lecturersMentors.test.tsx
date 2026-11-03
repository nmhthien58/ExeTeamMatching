import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const get = vi.fn()
const post = vi.fn()
const put = vi.fn()
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: {
    get: (...args: unknown[]) => get(...args),
    post: (...args: unknown[]) => post(...args),
    put: (...args: unknown[]) => put(...args),
  },
}))

const { ApiError } = await import('@/shared/api/apiClient')
const { adminLecturerRoutes } = await import('@/features/lecturers/routes')
const { adminMentorRoutes } = await import('./routes')
const { validateLecturer, validateMentor } = await import('@/features/lecturers/rules')
const { PageHeaderProvider, usePageHeaderState } = await import('@/shared/layout/PageHeader')

function HeaderAction() {
  return <>{usePageHeaderState().action}</>
}

const domains = [
  { id: 1, code: 'D01', name: 'Giáo dục', isActive: true },
  { id: 2, code: 'D02', name: 'Y tế', isActive: true },
  { id: 3, code: 'D03', name: 'Đã ngừng', isActive: false },
]
const ha = { id: 'l1', fullName: 'Trần Thu Hà', email: 'ha@example.com', phone: null, domains: [domains[0]], maxTeamsExe101: 3, maxTeamsExe201: null, isActive: true }
const khoa = { ...ha, id: 'l2', fullName: 'Nguyễn Đức Khoa', email: 'khoa@example.com', isActive: false, domains: [] }
const vinh = {
  id: 'm1', fullName: 'Đỗ Quang Vinh', email: 'vinh@example.com', organization: 'Startup Demo', isInternal: false, lecturerId: null,
  domains: [domains[1]], maxTeams: 3, preferredTeams: 2, currentTeamCount: 2, isActive: true,
}
const semester = {
  id: 's1', code: 'SP90', name: 'Học kỳ demo', startDate: '2026-10-26', endDate: '2027-02-07', isCurrent: true,
  mentoringStartWeek: 8, expectedMentorSessions: 3, slotBookingDeadlineWeek: 3, meetingSlotMinutes: 15, checkpointTargetWeeks: null,
}
const page = <T,>(items: T[]) => ({ items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1 })

function respond(overrides: Record<string, unknown> = {}) {
  get.mockImplementation(async (url: string, config?: { params?: Record<string, unknown> }) => {
    if (url in overrides) {
      const value = overrides[url]
      if (value instanceof Error) throw value
      return { data: value }
    }
    if (url === '/api/domains') return { data: domains }
    if (url === '/api/lecturers') return { data: page([ha, khoa]) }
    if (url === '/api/mentors') return { data: page([vinh]) }
    if (url === '/api/semesters') return { data: [semester] }
    if (url === '/api/advisor-matching/LECTURER/overview') {
      return { data: { advisorLoads: config?.params?.courseCode === 'EXE101' ? [{ advisorId: 'l1', assignedCount: 2 }] : [] } }
    }
    throw new Error(`GET ${url} chưa giả lập`)
  })
}

function renderAt(path: string) {
  const router = createMemoryRouter([{ path: '/admin', children: [...adminLecturerRoutes, ...adminMentorRoutes] }], { initialEntries: [path] })
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <PageHeaderProvider>
        <HeaderAction />
        <RouterProvider router={router} />
      </PageHeaderProvider>
    </QueryClientProvider>,
  )
}

const drawer = () => screen.getByRole('dialog', { name: /Thêm|Sửa/ })

describe('W11 – Giảng viên (FE-17)', () => {
  beforeEach(() => {
    get.mockReset()
    post.mockReset()
    put.mockReset()
    HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
      this.open = true
    }
    HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) {
      this.open = false
    }
  })

  it('lists lecturers with domains, load per course and status', async () => {
    respond()
    renderAt('/admin/giang-vien')
    await screen.findByText('2 / 3') // số nhóm đang nhận đã tải
    const row = screen.getByText('Trần Thu Hà').closest('tr')!
    expect(within(row).getByText('D01')).toBeInTheDocument()
    expect(within(row).getByText('2 / 3')).toBeInTheDocument()
    expect(within(row).getByText('– / ∞')).toBeInTheDocument()
    expect(within(screen.getByText('Nguyễn Đức Khoa').closest('tr')!).getByText('Đã khóa')).toBeInTheDocument()
  })

  it('still lists lecturers when the load from Team fails', async () => {
    respond({ '/api/advisor-matching/LECTURER/overview': new ApiError({ status: 503, code: 'x', title: 'Lỗi' }) })
    renderAt('/admin/giang-vien')
    await waitFor(() => expect(within(screen.getByText('Trần Thu Hà').closest('tr')!).getByText('– / 3')).toBeInTheDocument())
  })

  it('adds a lecturer and leaves an empty maximum as unlimited', async () => {
    respond()
    post.mockResolvedValue({ data: ha })
    renderAt('/admin/giang-vien')
    await userEvent.click(await screen.findByRole('button', { name: 'Thêm giảng viên' }))
    await userEvent.type(screen.getByLabelText('Họ tên'), ' Lê Hoàng Long ')
    await userEvent.type(screen.getByLabelText('Email (dùng để đăng nhập)'), 'long@example.com')
    await userEvent.type(screen.getByLabelText('Số nhóm tối đa EXE101'), '4')
    await userEvent.click(await within(drawer()).findByRole('checkbox', { name: /D02/ }))
    expect(within(drawer()).queryByRole('checkbox', { name: /D03/ })).not.toBeInTheDocument()
    await userEvent.click(within(drawer()).getByRole('button', { name: 'Lưu' }))

    await waitFor(() => expect(post).toHaveBeenCalledWith('/api/lecturers', {
      fullName: 'Lê Hoàng Long', email: 'long@example.com', phone: null, domainIds: [2], maxTeamsExe101: 4, maxTeamsExe201: null,
    }))
    expect(await screen.findByText('Đã thêm giảng viên')).toBeInTheDocument()
  })

  it('shows a duplicate email at the email field', async () => {
    respond()
    post.mockRejectedValue(new ApiError({ status: 409, code: 'email-exists', title: 'Email đã được dùng', detail: 'Email đã thuộc một mentor khác.' }))
    renderAt('/admin/giang-vien')
    await userEvent.click(await screen.findByRole('button', { name: 'Thêm giảng viên' }))
    await userEvent.type(screen.getByLabelText('Họ tên'), 'Trùng')
    await userEvent.type(screen.getByLabelText('Email (dùng để đăng nhập)'), 'vinh@example.com')
    await userEvent.click(within(drawer()).getByRole('button', { name: 'Lưu' }))
    expect(await within(drawer()).findByText('Email đã thuộc một mentor khác.')).toBeInTheDocument()
  })

  it('asks before locking and mentions the linked mentor', async () => {
    respond()
    put.mockResolvedValue({ data: { ...ha, isActive: false } })
    renderAt('/admin/giang-vien')
    await screen.findByText('2 / 3')
    await userEvent.click(screen.getByText('Trần Thu Hà'))
    await userEvent.click(within(drawer()).getByRole('switch', { name: 'Đang hoạt động' }))
    await userEvent.click(within(drawer()).getByRole('button', { name: 'Lưu' }))

    const confirm = await screen.findByRole('dialog', { name: 'Khóa Trần Thu Hà?', hidden: true })
    expect(within(confirm).getByText(/hồ sơ mentor cũng bị khóa/)).toBeInTheDocument()
    expect(put).not.toHaveBeenCalled()
    await userEvent.click(within(confirm).getByRole('button', { name: 'Khóa', hidden: true }))
    await waitFor(() => expect(put).toHaveBeenCalledWith('/api/lecturers/l1', expect.objectContaining({ isActive: false, maxTeamsExe201: null })))
  })

  it('validates like the server', () => {
    const valid = { fullName: 'A', email: 'a@b.vn', phone: '', domainIds: [], maxTeamsExe101: '', maxTeamsExe201: '200', isActive: true }
    expect(validateLecturer(valid)).toEqual({})
    expect(Object.keys(validateLecturer({ ...valid, fullName: ' ', email: 'x', phone: '123', maxTeamsExe101: '201', maxTeamsExe201: '1.5' })).sort())
      .toEqual(['email', 'fullName', 'maxTeamsExe101', 'maxTeamsExe201', 'phone'])
  })
})

describe('W27 – Mentor (FE-34)', () => {
  beforeEach(() => {
    get.mockReset()
    post.mockReset()
    put.mockReset()
    HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
      this.open = true
    }
  })

  it('lists mentors with type and current / preferred / maximum load', async () => {
    respond()
    renderAt('/admin/mentor')
    const row = (await screen.findByText('Đỗ Quang Vinh')).closest('tr')!
    expect(within(row).getByText('Ngoài trường')).toBeInTheDocument()
    expect(within(row).getByText('2 / 2 / 3')).toBeInTheDocument()
    expect(within(row).getByText('D02')).toBeInTheDocument()
  })

  it('creates an internal mentor linked to a lecturer without sending an email, max 3 by default', async () => {
    respond()
    post.mockResolvedValue({ data: vinh })
    renderAt('/admin/mentor')
    await userEvent.click(await screen.findByRole('button', { name: 'Thêm mentor' }))
    expect(screen.getByLabelText('Số nhóm tối đa')).toHaveValue(3)
    await userEvent.type(screen.getByLabelText('Họ tên'), 'Trần Thu Hà')
    await userEvent.click(within(drawer()).getByRole('switch', { name: 'Giảng viên / cán bộ của trường' }))
    const select = await within(drawer()).findByLabelText('Là giảng viên (không bắt buộc)')
    expect(within(select).queryByText(/Nguyễn Đức Khoa/)).not.toBeInTheDocument() // giảng viên đang khóa không gắn được
    await userEvent.selectOptions(select, 'l1')
    expect(within(drawer()).queryByLabelText(/Email/)).not.toBeInTheDocument()
    await userEvent.click(within(drawer()).getByRole('button', { name: 'Lưu' }))

    await waitFor(() => expect(post).toHaveBeenCalledWith('/api/mentors', {
      fullName: 'Trần Thu Hà', organization: null, isInternal: true, lecturerId: 'l1', domainIds: [], maxTeams: 3,
    }))
  })

  it('needs an email when the mentor is not linked and shows server conflicts at the right field', async () => {
    respond()
    post.mockRejectedValueOnce(new ApiError({ status: 409, code: 'email-exists', title: 'Email đã được dùng', detail: 'Email đã thuộc giảng viên.' }))
    renderAt('/admin/mentor')
    await userEvent.click(await screen.findByRole('button', { name: 'Thêm mentor' }))
    await userEvent.clear(screen.getByLabelText('Số nhóm tối đa'))
    await userEvent.click(within(drawer()).getByRole('button', { name: 'Lưu' }))
    expect(within(drawer()).getByRole('alert')).toHaveTextContent('Còn 3 chỗ cần sửa')
    expect(post).not.toHaveBeenCalled()

    // Ô đang lỗi: tên ô có thêm câu báo lỗi.
    await userEvent.type(within(drawer()).getByRole('textbox', { name: /^Họ tên/ }), 'Mentor Mới')
    await userEvent.type(within(drawer()).getByRole('textbox', { name: /^Email/ }), 'ha@example.com')
    await userEvent.type(within(drawer()).getByRole('spinbutton', { name: /^Số nhóm tối đa/ }), '2')
    await userEvent.click(within(drawer()).getByRole('button', { name: 'Lưu' }))
    expect(await within(drawer()).findByText('Email đã thuộc giảng viên.')).toBeInTheDocument()
  })

  it('reports a maximum below the current load at the maximum field', async () => {
    respond()
    put.mockRejectedValue(new ApiError({ status: 409, code: 'max-below-current', title: 'Thấp hơn số nhóm đang mentor', detail: 'Mentor đang có 2 nhóm.' }))
    renderAt('/admin/mentor')
    await userEvent.click(await screen.findByText('Đỗ Quang Vinh'))
    await userEvent.clear(screen.getByLabelText('Số nhóm tối đa'))
    await userEvent.type(screen.getByLabelText('Số nhóm tối đa'), '1')
    await userEvent.click(within(drawer()).getByRole('button', { name: 'Lưu' }))
    expect(await within(drawer()).findByText('Mentor đang có 2 nhóm.')).toBeInTheDocument()
  })

  it('asks before locking and says how many teams lose the mentor', async () => {
    respond()
    put.mockResolvedValue({ data: { ...vinh, isActive: false } })
    renderAt('/admin/mentor')
    await userEvent.click(await screen.findByText('Đỗ Quang Vinh'))
    await userEvent.click(within(drawer()).getByRole('switch', { name: 'Đang hoạt động' }))
    await userEvent.click(within(drawer()).getByRole('button', { name: 'Lưu' }))
    const confirm = await screen.findByRole('dialog', { name: 'Khóa Đỗ Quang Vinh?', hidden: true })
    expect(within(confirm).getByText(/gỡ khỏi 2 nhóm/)).toBeInTheDocument()
    await userEvent.click(within(confirm).getByRole('button', { name: 'Khóa', hidden: true }))
    await waitFor(() => expect(put).toHaveBeenCalledWith('/api/mentors/m1', expect.objectContaining({ isActive: false, email: 'vinh@example.com' })))
  })

  it('saves the mentoring start week and number of sessions of the current semester', async () => {
    respond()
    put.mockResolvedValue({ data: semester })
    renderAt('/admin/mentor')
    const week = await screen.findByLabelText('Tuần bắt đầu mentor')
    expect(screen.getByRole('button', { name: 'Lưu cài đặt' })).toBeDisabled()
    await userEvent.clear(week)
    await userEvent.type(week, '6')
    await userEvent.click(screen.getByRole('button', { name: 'Lưu cài đặt' }))
    await waitFor(() => expect(put).toHaveBeenCalledWith('/api/semesters/s1', expect.objectContaining({ code: 'SP90', mentoringStartWeek: 6, expectedMentorSessions: 3 })))
  })

  it('validates like the server', () => {
    const base = { fullName: 'A', email: '', organization: '', isInternal: true, lecturerId: 'l1', domainIds: [], maxTeams: '3', isActive: true }
    expect(validateMentor(base)).toEqual({})
    expect(Object.keys(validateMentor({ ...base, lecturerId: '', maxTeams: '51' })).sort()).toEqual(['email', 'maxTeams'])
  })
})
