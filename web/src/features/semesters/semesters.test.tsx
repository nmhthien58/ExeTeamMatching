import { render, screen, within } from '@testing-library/react'
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
const { adminSemesterRoutes } = await import('./routes')
const { PageHeaderProvider, usePageHeaderState } = await import('@/shared/layout/PageHeader')
const { validateSemester } = await import('./SemesterForm')
const { validateRound, toLocalInput } = await import('@/features/rounds/RoundForm')

function HeaderAction() {
  return <>{usePageHeaderState().action}</>
}

const fa = {
  id: 'fa', code: 'FA26', name: 'Fall 2026', startDate: '2026-09-07', endDate: '2026-12-26', isCurrent: true,
  mentoringStartWeek: 6, expectedMentorSessions: 3, slotBookingDeadlineWeek: 2, meetingSlotMinutes: 15, checkpointTargetWeeks: null,
}
const su = { ...fa, id: 'su', code: 'SU26', name: 'Summer 2026', isCurrent: false }
const scheduled = {
  id: 'r1', semesterId: 'fa', courseCode: 'EXE101', roundType: 'TEAM_FORMATION', name: 'Lập nhóm EXE101',
  opensAt: '2026-11-10T01:30:45.123Z', deadlineAt: '2026-11-20T10:00:00Z', status: 'SCHEDULED', closedAt: null,
}
const open = { ...scheduled, id: 'r2', name: 'Đợt đang mở', courseCode: 'EXE201', status: 'OPEN' }
const closed = { ...scheduled, id: 'r3', name: 'Đợt đã khóa', status: 'CLOSED' }

function renderAt(path: string, { semesters = [su, fa], rounds = [scheduled, open, closed] as unknown[] } = {}) {
  get.mockImplementation(async (url: string) => {
    if (url === '/api/semesters') return { data: semesters }
    if (url === '/api/registration-rounds') return { data: rounds }
    throw new Error(`GET ${url} chưa giả lập`)
  })
  const router = createMemoryRouter([{ path: '/admin', children: adminSemesterRoutes }], { initialEntries: [path] })
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <PageHeaderProvider>
        <HeaderAction />
        <RouterProvider router={router} />
      </PageHeaderProvider>
    </QueryClientProvider>,
  )
}

describe('W03, W04 – học kỳ và đợt đăng ký (FE-09, FE-10)', () => {
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

  it('W03: danh sách có môn của kỳ và chip hiện hành', async () => {
    renderAt('/admin/hoc-ky')
    const row = (await screen.findByText('FA26')).closest('tr')!
    expect(within(row).getByText('Hiện hành')).toBeInTheDocument()
    expect(within(row).getByText('EXE101, EXE201')).toBeInTheDocument()
    expect(within(row).getByText(/07\/09\/2026/)).toBeInTheDocument()
    expect(within(screen.getByText('SU26').closest('tr')!).queryByText('Hiện hành')).not.toBeInTheDocument()
  })

  it('W03: chưa có học kỳ thì mời tạo; tạo học kỳ báo lỗi tại ô rồi mới gửi', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue({ data: fa })
    renderAt('/admin/hoc-ky', { semesters: [] })
    await user.click(await within(await screen.findByText('Chưa có học kỳ').then((e) => e.parentElement!)).findByRole('button', { name: 'Tạo học kỳ' }))

    await user.click(screen.getByRole('button', { name: 'Lưu' }))
    expect(await screen.findByText('Mã học kỳ dạng SP26, SU26, FA26.')).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()

    await user.type(screen.getByLabelText(/^Mã học kỳ/), 'fa27')
    await user.type(screen.getByLabelText(/^Tên học kỳ/), 'Fall 2027')
    await user.type(screen.getByLabelText(/^Ngày bắt đầu/), '2027-09-06')
    await user.type(screen.getByLabelText(/^Ngày kết thúc/), '2027-12-25')
    await user.click(screen.getByRole('button', { name: 'Lưu' }))
    expect(post).toHaveBeenCalledWith('/api/semesters', expect.objectContaining({ code: 'FA27', mentoringStartWeek: 6, checkpointTargetWeeks: null, isCurrent: false }))
  })

  it('W03: sửa dùng PUT, lỗi trùng mã từ server hiện tại ô', async () => {
    const user = userEvent.setup()
    put.mockRejectedValueOnce(new ApiError({ status: 400, code: 'validation', title: 'Dữ liệu không hợp lệ', fieldErrors: { code: ['Học kỳ SU26 đã tồn tại.'] } }))
    renderAt('/admin/hoc-ky')
    await user.click(await screen.findByRole('button', { name: 'Sửa FA26' }))
    await user.click(screen.getByRole('button', { name: 'Lưu' }))
    expect(await screen.findByText('Học kỳ SU26 đã tồn tại.')).toBeInTheDocument()
    expect(put).toHaveBeenCalledWith('/api/semesters/fa', expect.objectContaining({ code: 'FA26', isCurrent: true }))
  })

  it('W03: tuần checkpoint phải đủ 4 hoặc trống cả 4', () => {
    const base = { code: 'FA26', name: 'x', startDate: '2026-09-07', endDate: '2026-12-26', isCurrent: false, mentoringStartWeek: '6', expectedMentorSessions: '3', slotBookingDeadlineWeek: '2', meetingSlotMinutes: '15' }
    expect(validateSemester({ ...base, checkpoints: ['', '', '', ''] })).toEqual({})
    expect(validateSemester({ ...base, checkpoints: ['3', '6', '9', '12'] })).toEqual({})
    expect(validateSemester({ ...base, checkpoints: ['3', '', '', ''] }).checkpointTargetWeeks).toBeTruthy()
    expect(validateSemester({ ...base, endDate: '2026-09-01', checkpoints: ['', '', '', ''] }).endDate).toBeTruthy()
    expect(validateSemester({ ...base, meetingSlotMinutes: '61', checkpoints: ['', '', '', ''] }).meetingSlotMinutes).toBeTruthy()
  })

  it('W04: mặc định học kỳ hiện hành; thao tác theo trạng thái; lọc môn gửi lên API', async () => {
    const user = userEvent.setup()
    renderAt('/admin/hoc-ky?tab=dot')
    expect(await screen.findByText('Lập nhóm EXE101')).toBeInTheDocument()
    expect(get).toHaveBeenCalledWith('/api/registration-rounds', { params: { semesterId: 'fa', courseCode: undefined } })

    expect(screen.getByRole('button', { name: 'Mở Lập nhóm EXE101' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Khóa Đợt đang mở' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Sửa Đợt đã khóa' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Mở Đợt đã khóa' })).not.toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('Môn'), 'EXE201')
    expect(get).toHaveBeenLastCalledWith('/api/registration-rounds', { params: { semesterId: 'fa', courseCode: 'EXE201' } })
  })

  it('W04: mở và khóa cần xác nhận; lỗi đã có đợt mở hiện trong dialog', async () => {
    const user = userEvent.setup()
    post.mockRejectedValueOnce(new ApiError({ status: 409, code: 'round-already-open', title: 'Đã có đợt đang mở', detail: 'Môn EXE101 đang có một đợt mở.' }))
    renderAt('/admin/hoc-ky?tab=dot')

    await user.click(await screen.findByRole('button', { name: 'Mở Lập nhóm EXE101' }))
    expect(post).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: 'Mở đợt' }))
    expect(post).toHaveBeenCalledWith('/api/registration-rounds/r1/open')
    expect(await screen.findByRole('alert')).toHaveTextContent('Môn EXE101 đang có một đợt mở.')
    await user.click(screen.getByRole('button', { name: 'Hủy' }))

    post.mockResolvedValueOnce({ data: closed })
    await user.click(screen.getByRole('button', { name: 'Khóa Đợt đang mở' }))
    expect(screen.getByText(/không mở lại được/)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Khóa đợt' }))
    expect(post).toHaveBeenLastCalledWith('/api/registration-rounds/r2/close')
  })

  it('W04: tạo đợt; EXE101 không có loại đổi nhóm', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue({ data: scheduled })
    renderAt('/admin/hoc-ky?tab=dot', { rounds: [] })
    await screen.findByText('Chưa có đợt đăng ký')
    await user.click(screen.getAllByRole('button', { name: 'Tạo đợt' })[0])

    const dialog = within(screen.getByRole('dialog', { hidden: true }))
    expect(dialog.getByLabelText('Loại đợt')).not.toHaveTextContent('Đổi nhóm')
    await user.selectOptions(dialog.getByLabelText('Môn'), 'EXE201')
    await user.selectOptions(dialog.getByLabelText('Loại đợt'), 'CHANGE_REQUEST')
    await user.selectOptions(dialog.getByLabelText('Môn'), 'EXE101')
    expect(dialog.getByLabelText('Loại đợt')).toHaveValue('TEAM_FORMATION')

    await user.click(dialog.getByRole('button', { name: 'Lưu' }))
    expect(await dialog.findByText('Nhập tên đợt.')).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()

    await user.type(dialog.getByLabelText(/^Tên đợt/), 'Đợt mới')
    await user.type(dialog.getByLabelText(/^Giờ mở/), '2026-11-10T08:00')
    await user.type(dialog.getByLabelText(/^Hạn chót/), '2026-11-20T17:00')
    await user.click(dialog.getByRole('button', { name: 'Lưu' }))
    expect(post).toHaveBeenCalledWith('/api/registration-rounds', expect.objectContaining({
      semesterId: 'fa', courseCode: 'EXE101', roundType: 'TEAM_FORMATION', name: 'Đợt mới', opensAt: new Date('2026-11-10T08:00').toISOString(),
    }))
  })

  it('W04: học kỳ đang xem lưu trên URL, nút Tạo đợt ở header điền sẵn học kỳ đó', async () => {
    const user = userEvent.setup()
    renderAt('/admin/hoc-ky?tab=dot&hk=su')
    await screen.findByText('Lập nhóm EXE101')
    expect(get).toHaveBeenLastCalledWith('/api/registration-rounds', { params: { semesterId: 'su', courseCode: undefined } })
    await user.click(screen.getAllByRole('button', { name: 'Tạo đợt' })[0])
    expect(within(screen.getByRole('dialog', { hidden: true })).getByLabelText(/^Học kỳ/)).toHaveValue('su')
  })

  it('W04: đợt đang mở chỉ đổi tên và gửi lại nguyên giờ gốc', async () => {
    const user = userEvent.setup()
    put.mockResolvedValue({ data: open })
    renderAt('/admin/hoc-ky?tab=dot')
    await user.click(await screen.findByRole('button', { name: 'Sửa Đợt đang mở' }))

    expect(screen.getByText('Đợt đang mở chỉ đổi được tên')).toBeInTheDocument()
    expect(screen.getByLabelText('Hạn chót')).toBeDisabled()
    await user.clear(screen.getByLabelText('Tên đợt'))
    await user.type(screen.getByLabelText('Tên đợt'), 'Tên mới')
    await user.click(screen.getByRole('button', { name: 'Lưu' }))
    expect(put).toHaveBeenCalledWith('/api/registration-rounds/r2', expect.objectContaining({ name: 'Tên mới', opensAt: open.opensAt, deadlineAt: open.deadlineAt }))
  })

  it('W04: quy tắc form đợt', () => {
    const base = { semesterId: 'fa', courseCode: 'EXE101' as const, roundType: 'TEAM_FORMATION' as const, name: 'x', opensAt: '2026-11-10T08:00', deadlineAt: '2026-11-20T08:00' }
    expect(validateRound(base)).toEqual({})
    expect(validateRound({ ...base, deadlineAt: '2026-11-09T08:00' }).deadlineAt).toBe('Hạn chót phải sau giờ mở.')
    expect(validateRound({ ...base, roundType: 'CHANGE_REQUEST' }).roundType).toBe('Đợt đổi nhóm chỉ có ở EXE201.')
    expect(toLocalInput(new Date(2026, 10, 10, 8, 5).toISOString())).toBe('2026-11-10T08:05')
  })
})
