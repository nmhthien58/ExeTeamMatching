import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const get = vi.fn()
const post = vi.fn()
const put = vi.fn()
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: { get: (...a: unknown[]) => get(...a), post: (...a: unknown[]) => post(...a), put: (...a: unknown[]) => put(...a) },
}))

const { ApiError } = await import('@/shared/api/apiClient')
const { tokenStore } = await import('@/shared/api/tokenStore')
const { AuthProvider } = await import('@/shared/auth/AuthProvider')
const { CollabHub } = await import('@/features/realtime/collabHub')
const { HubProvider } = await import('@/features/realtime/RealtimeStatusBar')
const { LecturerSchedulePage, TeamSchedulePage } = await import('./MeetingSchedulePages')

const slot = (id: string, startTime: string, state: string, extra = {}) => ({ id, dayOfWeek: 2, startTime, endTime: '', state, ...extra })
const teamSchedule = (extra = {}) => ({
  lecturerName: 'Cô Hạnh', status: 'PUBLISHED', location: 'https://meet.example/abc', bookingDeadline: '2099-11-09T16:59:00Z', canBook: true, mySlot: null,
  slots: [slot('a', '09:00', 'TAKEN'), slot('b', '09:15', 'FREE')],
  ...extra,
})

function fakeHub() {
  const handlers: Record<string, ((p: unknown) => void)[]> = {}
  const connection = {
    start: vi.fn(async () => {}), stop: vi.fn(async () => {}), invoke: vi.fn(async () => undefined),
    on: (e: string, h: (p: unknown) => void) => void (handlers[e] = [...(handlers[e] ?? []), h]),
    off: vi.fn(), onreconnecting: vi.fn(), onreconnected: vi.fn(), onclose: vi.fn(),
  }
  return { hub: new CollabHub(() => connection as never), connection, emit: (e: string, p: unknown) => handlers[e]?.forEach((h) => h(p)) }
}

function renderWith(element: React.ReactNode, hub = fakeHub().hub) {
  const router = createMemoryRouter([{ path: '/', element }])
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <AuthProvider>
        <HubProvider hub={hub}>
          <RouterProvider router={router} />
        </HubProvider>
      </AuthProvider>
    </QueryClientProvider>,
  )
}

function mockTeam(role: 'LEADER' | 'MEMBER', schedule: unknown) {
  get.mockImplementation(async (url: string) => {
    if (url === '/api/teams/me') return { data: { id: 't2', myRelation: role, courseCode: 'EXE201' } }
    // Dự án EXE101 của kỳ trước đứng trước: phải chọn đúng dự án của nhóm hiện tại.
    if (url === '/api/workspaces/me') return { data: [{ id: 'w-old', teamId: 't1' }, { id: 'w1', teamId: 't2' }] }
    if (url === '/api/workspaces/w1/meeting-schedule') return { data: typeof schedule === 'function' ? schedule() : schedule }
    throw new Error(`GET ${url} chưa giả lập`)
  })
}

describe('C09 – Lịch họp hằng tuần (FE-45)', () => {
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
    tokenStore.save({
      accessToken: 'a', accessTokenExpiresAt: '', refreshToken: 'r', refreshTokenExpiresAt: '',
      user: { id: 'u', email: 'sv@fpt.edu.vn', fullName: 'Trưởng Nhóm', roles: ['Student'], studentId: 's1' },
    })
  })

  it('trưởng nhóm chọn slot trống qua xác nhận; ô đã có nhóm không bấm được; nghe slotsChanged của đúng dự án', async () => {
    mockTeam('LEADER', teamSchedule())
    put.mockResolvedValue({ data: teamSchedule({ mySlot: slot('b', '09:15', 'MINE'), slots: [slot('a', '09:00', 'TAKEN'), slot('b', '09:15', 'MINE')] }) })
    const { hub, connection, emit } = fakeHub()
    renderWith(<TeamSchedulePage />, hub)
    const user = userEvent.setup()

    expect(await screen.findByRole('button', { name: 'Thứ Ba 09:00, đã có nhóm' })).toBeDisabled()
    await waitFor(() => expect(connection.invoke).toHaveBeenCalledWith('JoinWorkspace', 'w1'))
    await user.click(screen.getByRole('button', { name: 'Thứ Ba 09:15, còn trống' }))
    await user.click(within(screen.getByRole('dialog', { name: 'Chọn Thứ Ba 09:15?' })).getByRole('button', { name: 'Chọn slot' }))
    expect(put).toHaveBeenCalledWith('/api/workspaces/w1/slot-booking', { slotId: 'b' })
    expect(await screen.findByText('Họp hằng tuần · Thứ Ba 09:15')).toBeInTheDocument()

    const calls = get.mock.calls.filter(([url]) => url === '/api/workspaces/w1/meeting-schedule').length
    act(() => emit('slotsChanged', { scheduleId: 'sc1' }))
    await waitFor(() => expect(get.mock.calls.filter(([url]) => url === '/api/workspaces/w1/meeting-schedule').length).toBeGreaterThan(calls))
  })

  it('slot vừa bị nhóm khác chọn → báo lỗi và tải lại lưới', async () => {
    let taken = false
    mockTeam('LEADER', () => teamSchedule(taken ? { slots: [slot('a', '09:00', 'TAKEN'), slot('b', '09:15', 'TAKEN')] } : {}))
    put.mockImplementation(async () => {
      taken = true
      throw new ApiError({ status: 409, code: 'slot-taken', title: 'Slot vừa có nhóm khác chọn' })
    })
    renderWith(<TeamSchedulePage />)
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Thứ Ba 09:15, còn trống' }))
    await user.click(screen.getByRole('button', { name: 'Chọn slot' }))
    expect(await screen.findByText('Slot này vừa có nhóm khác chọn. Hãy chọn slot còn trống.')).toBeInTheDocument()
    expect(await screen.findByRole('button', { name: 'Thứ Ba 09:15, đã có nhóm' })).toBeDisabled()
  })

  it('thành viên chỉ xem slot của nhóm; chưa có lịch → trạng thái trống', async () => {
    mockTeam('MEMBER', teamSchedule({ canBook: false, mySlot: slot('b', '09:15', 'MINE') }))
    renderWith(<TeamSchedulePage />)
    expect(await screen.findByText('Họp hằng tuần · Thứ Ba 09:15')).toBeInTheDocument()
    expect(screen.getByText('Trưởng nhóm chọn slot cho nhóm.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Thứ Ba 09:00/ })).not.toBeInTheDocument()
  })

  it('chưa có lịch → "Giảng viên chưa đăng lịch"', async () => {
    mockTeam('LEADER', null)
    renderWith(<TeamSchedulePage />)
    expect(await screen.findByText('Giảng viên chưa đăng lịch')).toBeInTheDocument()
  })

  it('giảng viên: xem trước số slot, bắt lỗi giờ / link, lưu, cảnh báo thiếu slot, đăng lịch, đổi slot cho nhóm', async () => {
    const saved = {
      id: 'sc1', status: 'DRAFT', slotMinutes: 15, location: 'https://meet.example/abc', bookingDeadline: '2099-11-09T16:59:00Z',
      windows: [{ dayOfWeek: 2, startTime: '09:00', endTime: '09:30' }],
      slots: [slot('a', '09:00', 'TAKEN', { workspaceId: 'w2', teamName: 'Nhóm Bình Minh' }), slot('b', '09:15', 'FREE')],
      teamCount: 3, teamsWithoutSlot: [{ workspaceId: 'w3', teamName: 'Nhóm Hoàng Hôn' }], warnings: ['NOT_ENOUGH_SLOTS'],
    }
    get.mockImplementation(async (url: string) => {
      if (url === '/api/meeting-schedules/me') return { data: null }
      if (url === '/api/workspaces/me') return { data: [{ id: 'w2', courseCode: 'EXE201' }] }
      throw new Error(`GET ${url} chưa giả lập`)
    })
    put.mockResolvedValueOnce({ data: saved }).mockResolvedValueOnce({ data: { ...saved, status: 'PUBLISHED' } })
    post.mockResolvedValue({ data: { ...saved, status: 'PUBLISHED' } })
    renderWith(<LecturerSchedulePage />)
    const user = userEvent.setup()

    expect(await screen.findByText('Xem trước: 8 slot')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Lưu khung giờ' }))
    expect(screen.getByText('Nhập link họp.')).toBeInTheDocument()
    await user.clear(screen.getByLabelText('Kết thúc'))
    await user.type(screen.getByLabelText('Kết thúc'), '08:00')
    await user.type(screen.getByLabelText('Link họp'), 'https://meet.example/abc')
    await user.click(screen.getByRole('button', { name: 'Lưu khung giờ' }))
    expect(screen.getByText('Giờ kết thúc phải sau giờ bắt đầu.')).toBeInTheDocument()
    expect(put).not.toHaveBeenCalled()

    await user.clear(screen.getByLabelText('Kết thúc'))
    await user.type(screen.getByLabelText('Kết thúc'), '09:30')
    await user.click(screen.getByRole('button', { name: 'Lưu khung giờ' }))
    expect(put).toHaveBeenCalledWith('/api/meeting-schedules/me', { windows: [{ dayOfWeek: 2, startTime: '09:00', endTime: '09:30' }], slotMinutes: 15, location: 'https://meet.example/abc' })
    expect(await screen.findByText('2 slot · 3 nhóm · còn 1 nhóm chưa chọn')).toBeInTheDocument()
    expect(screen.getByText('Thiếu slot so với số nhóm')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Đăng lịch' }))
    expect(post).toHaveBeenCalledWith('/api/meeting-schedules/me/publish')

    await user.click(await screen.findByRole('button', { name: 'Thứ Ba 09:00, Nhóm Bình Minh' }))
    const dialog = screen.getByRole('dialog', { name: 'Đổi slot cho Nhóm Bình Minh' })
    await user.selectOptions(within(dialog).getByLabelText('Slot mới'), 'b')
    await user.click(within(dialog).getByRole('button', { name: 'Đổi slot' }))
    expect(put).toHaveBeenLastCalledWith('/api/meeting-schedules/me/bookings/w2', { slotId: 'b' })

    put.mockResolvedValueOnce({ data: { ...saved, status: 'PUBLISHED' } })
    await user.click(await screen.findByRole('button', { name: 'Xếp slot cho Nhóm Hoàng Hôn' }))
    const assign = screen.getByRole('dialog', { name: 'Xếp slot cho Nhóm Hoàng Hôn' })
    await user.selectOptions(within(assign).getByLabelText('Slot mới'), 'b')
    await user.click(within(assign).getByRole('button', { name: 'Xếp slot' }))
    expect(put).toHaveBeenLastCalledWith('/api/meeting-schedules/me/bookings/w3', { slotId: 'b' })
  })
})
