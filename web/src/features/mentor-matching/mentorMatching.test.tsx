import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import type { AdvisorMatchingRow, AdvisorMatchingRun, MentorMatchingOverview } from './api'

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
const { adminMentorMatchingRoutes } = await import('./routes')
const { renderMatchingPage, signInAsAdmin, stubDialogElement } = await import('@/features/lecturer-matching/matchingTestSetup')

const team = (id: string, name: string, lecturerName: string | null) => ({
  id,
  name,
  courseCode: 'EXE101' as const,
  status: 'FINALIZED' as const,
  domains: [{ id: 7, code: 'D07', name: 'Fintech' }],
  memberCount: 5,
  openSlots: 1,
  lecturerName,
})

const rows: AdvisorMatchingRow[] = [
  {
    team: team('t1', 'GreenBite', 'Trần Minh Khoa'),
    advisorId: 'm1',
    advisorName: 'Lê Văn Tuấn',
    reason: 'MENTOR_INTEREST',
    preferenceRank: null,
    preferences: [],
    interests: [{ id: 'i1', advisorId: 'm1', advisorName: 'Lê Văn Tuấn', status: 'CLOSED' }],
  },
  {
    team: team('t2', 'EduMate', null),
    advisorId: null,
    advisorName: null,
    reason: null,
    preferenceRank: null,
    preferences: [],
    interests: [{ id: 'i2', advisorId: 'm2', advisorName: 'Phạm Thu Hà', status: 'OPEN' }],
  },
]

const loads = [
  { advisorId: 'm1', fullName: 'Lê Văn Tuấn', assignedCount: 3, preferredTeams: 2, maxTeams: 3 },
  { advisorId: 'm2', fullName: 'Phạm Thu Hà', assignedCount: 2, preferredTeams: 2, maxTeams: 4 },
  { advisorId: 'm3', fullName: 'Ngô Bảo Châu', assignedCount: 0, preferredTeams: null, maxTeams: 2 },
]

const overview = (patch: Partial<MentorMatchingOverview> = {}): MentorMatchingOverview => ({
  matchingAt: '2026-10-12T00:00:00+07:00',
  lastRun: null,
  teamsWithPreferences: 0,
  teamsWithoutAdvisor: 1,
  items: rows,
  advisorLoads: loads,
  ...patch,
})

const previewRun: AdvisorMatchingRun = {
  id: 'run1',
  advisorKind: 'MENTOR',
  courseCode: 'EXE101',
  trigger: 'EARLY',
  status: 'PREVIEW',
  matchedCount: 1,
  unmatchedCount: 1,
  createdAt: '2026-10-01T03:00:00Z',
  appliedAt: null,
  plan: [
    { teamId: 't2', teamName: 'EduMate', advisorId: 'm2', advisorName: 'Phạm Thu Hà', reason: 'MENTOR_INTEREST', preferenceRank: null },
    { teamId: 't3', teamName: 'FarmLink', advisorId: null, advisorName: null, reason: null, preferenceRank: null },
  ],
}

const renderAt = (path = '/admin/ghep-mentor') => renderMatchingPage(adminMentorMatchingRoutes, path)

const future = () => new Date(Date.now() + 5 * 24 * 3600e3).toISOString()

describe('W28 – ghép mentor', () => {
  beforeAll(stubDialogElement)

  beforeEach(() => {
    get.mockReset()
    post.mockReset()
    put.mockReset()
    del.mockReset()
    signInAsAdmin()
  })

  it('shows a loading state first', async () => {
    get.mockReturnValue(new Promise(() => {}))

    renderAt()

    expect((await screen.findAllByRole('status', { name: 'Đang tải' })).length).toBeGreaterThan(0)
  })

  it('before the mentoring week shows progress and previews then confirms early matching', async () => {
    get.mockResolvedValue({ data: overview({ matchingAt: future() }) })
    post.mockImplementation(async (url: string) => {
      if (url === '/api/advisor-matching/MENTOR/preview') return { data: previewRun }
      if (url === '/api/advisor-matching/runs/run1/confirm') return { data: { ...previewRun, status: 'APPLIED' } }
      throw new Error(url)
    })

    renderAt()

    expect(await screen.findByText(/Hệ thống tự ghép mentor lúc/)).toBeInTheDocument()
    expect(screen.getByText('Mentor đang quan tâm').nextSibling).toHaveTextContent('1')
    expect(screen.getByText('Mentor còn nhận nhóm').nextSibling).toHaveTextContent('2/3')
    expect(get).toHaveBeenCalledWith('/api/advisor-matching/MENTOR/overview', expect.objectContaining({ params: expect.objectContaining({ courseCode: 'EXE101', page: 1 }) }))

    await userEvent.click(screen.getByRole('button', { name: 'Ghép sớm' }))
    const dialog = await screen.findByRole('dialog')
    expect(post).toHaveBeenCalledWith('/api/advisor-matching/MENTOR/preview', { courseCode: 'EXE101' })
    expect(within(dialog).getByText('Mentor quan tâm')).toBeInTheDocument()
    expect(within(dialog).getByText('Chưa có mentor')).toBeInTheDocument()

    await userEvent.click(within(dialog).getByRole('button', { name: 'Chốt kết quả' }))
    expect(await screen.findByText('Đã ghép mentor cho 1 nhóm')).toBeInTheDocument()
    expect(post).toHaveBeenCalledWith('/api/advisor-matching/runs/run1/confirm')
  })

  it('reports run-not-preview and discards a pending preview', async () => {
    get.mockResolvedValue({ data: overview({ matchingAt: future(), lastRun: previewRun }) })
    post.mockRejectedValue(new ApiError({ status: 409, code: 'run-not-preview', title: 'x' }))
    del.mockResolvedValue({ data: undefined })

    renderAt()

    await userEvent.click(await screen.findByRole('button', { name: 'Xem lại' }))
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Chốt kết quả' }))
    expect(await screen.findByText('Kết quả xem trước không còn dùng được')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Xem lại' }))
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Bỏ kết quả' }))
    expect(await screen.findByText('Đã bỏ kết quả xem trước')).toBeInTheDocument()
    expect(del).toHaveBeenCalledWith('/api/advisor-matching/runs/run1')
  })

  it('after matching lists teams with instructor, mentor, reason, interests and mentor loads', async () => {
    get.mockResolvedValue({
      data: overview({ lastRun: { ...previewRun, status: 'APPLIED', trigger: 'SCHEDULED', appliedAt: '2026-10-12T00:05:00Z' } }),
    })

    renderAt()

    const table = await screen.findByRole('table')
    expect(within(table).getByText('GreenBite')).toBeInTheDocument()
    expect(within(table).getByText('Trần Minh Khoa')).toBeInTheDocument()
    expect(within(table).getByText('Mentor quan tâm')).toBeInTheDocument()
    expect(within(table).getByText('Chưa có mentor')).toBeInTheDocument()
    expect(within(table).getAllByText('Fintech')).toHaveLength(2)
    expect(screen.getByText('1 nhóm chưa có mentor')).toBeInTheDocument()

    expect(screen.getByRole('meter', { name: 'Số nhóm của Phạm Thu Hà' })).toHaveAttribute('aria-valuemax', '4')
    expect(screen.getByText('3/3 nhóm')).toBeInTheDocument()
    expect(screen.getByText('Đã đủ nhóm')).toBeInTheDocument()
    expect(screen.getAllByText('Mong muốn 2 nhóm')).toHaveLength(2)
    expect(screen.getByText('Chưa đặt số mong muốn')).toBeInTheDocument()
  })

  it('filters teams without a mentor and shows the empty state', async () => {
    get.mockImplementation(async (_url: string, config: { params: { withoutAdvisor?: boolean } }) => ({
      data: config.params.withoutAdvisor ? overview({ items: [], teamsWithoutAdvisor: 0 }) : overview(),
    }))

    renderAt()

    await userEvent.click(await screen.findByRole('button', { name: 'Chưa có mentor', pressed: false }))
    expect(await screen.findByRole('button', { name: 'Xem tất cả nhóm' })).toBeInTheDocument()
    expect(get).toHaveBeenLastCalledWith('/api/advisor-matching/MENTOR/overview', expect.objectContaining({ params: expect.objectContaining({ withoutAdvisor: true }) }))
    expect(screen.queryByRole('button', { name: 'Ghép sớm' })).not.toBeInTheDocument()
  })

  it('shows the empty state when the course has no team', async () => {
    get.mockResolvedValue({ data: overview({ items: [], teamsWithoutAdvisor: 0, advisorLoads: [] }) })

    renderAt()

    expect(await screen.findByText('Chưa có nhóm nào')).toBeInTheDocument()
    expect(screen.getByText(/Chưa có mentor đang hoạt động/)).toBeInTheDocument()
  })

  it('assigns a mentor by hand: blocks full mentors, warns above the preferred number, handles mentor-full', async () => {
    get.mockResolvedValue({ data: overview() })
    put.mockRejectedValueOnce(new ApiError({ status: 409, code: 'mentor-full', title: 'Mentor đã đủ số nhóm', detail: 'Phạm Thu Hà đã mentor 4 nhóm trong học kỳ (tối đa 4).' }))
    put.mockResolvedValueOnce({
      data: {
        assignment: { id: 'a1', teamId: 't2', mentorId: 'm2', mentorName: 'Phạm Thu Hà', status: 'ASSIGNED', reason: 'MANUAL', source: 'MANUAL' },
        warnings: [{ code: 'ABOVE_PREFERRED', message: 'Phạm Thu Hà sẽ mentor 3 nhóm, vượt số mong muốn (2).' }],
      },
    })

    renderAt()

    await userEvent.click(await screen.findByRole('button', { name: 'Gán mentor cho EduMate' }))
    const dialog = screen.getByRole('dialog')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Gán mentor' }))
    expect(within(dialog).getByText('Chọn mentor.')).toBeInTheDocument()
    expect(within(dialog).getByText('Nhập lý do đổi mentor.')).toBeInTheDocument()
    expect(put).not.toHaveBeenCalled()

    const select = within(dialog).getByRole('combobox')
    expect(within(select).getByRole('option', { name: /Lê Văn Tuấn.*đã đủ/ })).toBeDisabled()
    await userEvent.selectOptions(select, 'm2')
    expect(within(dialog).getByText('Vượt số nhóm mong muốn')).toBeInTheDocument()
    await userEvent.selectOptions(select, 'm3')
    expect(within(dialog).queryByText('Vượt số nhóm mong muốn')).not.toBeInTheDocument()
    await userEvent.selectOptions(select, 'm2')

    await userEvent.type(within(dialog).getByRole('textbox'), 'Nhóm cần Fintech')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Gán mentor' }))
    expect(await within(dialog).findByText('Phạm Thu Hà đã mentor 4 nhóm trong học kỳ (tối đa 4).')).toBeInTheDocument()

    await userEvent.click(within(dialog).getByRole('button', { name: 'Gán mentor' }))
    expect(await screen.findByText('Đã gán Phạm Thu Hà cho nhóm EduMate. Lưu ý: vượt số nhóm mong muốn của mentor.')).toBeInTheDocument()
    expect(put).toHaveBeenLastCalledWith('/api/teams/t2/mentor-assignment', { mentorId: 'm2', note: 'Nhóm cần Fintech' })
  })

  it('replacing a mentor says the old mentor loses access', async () => {
    get.mockResolvedValue({ data: overview() })
    put.mockResolvedValue({ data: { assignment: { mentorName: 'Ngô Bảo Châu' }, warnings: [] } })

    renderAt()

    await userEvent.click(await screen.findByRole('button', { name: 'Đổi mentor cho GreenBite' }))
    const dialog = screen.getByRole('dialog')
    expect(within(dialog).getByText(/Lê Văn Tuấn sẽ không còn vào được dự án/)).toBeInTheDocument()
    expect(within(within(dialog).getByRole('combobox')).getByRole('option', { name: /Lê Văn Tuấn.*đang mentor/ })).toBeDisabled()

    await userEvent.selectOptions(within(dialog).getByRole('combobox'), 'm3')
    await userEvent.type(within(dialog).getByRole('textbox'), 'Nhóm xin đổi')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Đổi mentor' }))
    expect(await screen.findByText('Đã đổi sang Ngô Bảo Châu cho nhóm GreenBite')).toBeInTheDocument()
  })

  it('shows an error with retry when the overview fails', async () => {
    get.mockRejectedValueOnce(new ApiError({ status: 503, code: 'academic-unavailable', title: 'Chưa lấy được danh sách cố vấn' }))
    get.mockResolvedValue({ data: overview() })

    renderAt()

    expect(await screen.findByText('Chưa lấy được danh sách cố vấn')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Thử lại' }))
    await waitFor(() => expect(screen.getByRole('table')).toBeInTheDocument())
  })

  it('shows academic-unavailable when the preview cannot list mentors', async () => {
    get.mockResolvedValue({ data: overview({ matchingAt: future() }) })
    post.mockRejectedValue(new ApiError({ status: 503, code: 'academic-unavailable', title: 'Service Unavailable' }))

    renderAt()

    await userEvent.click(await screen.findByRole('button', { name: 'Ghép sớm' }))
    expect(await screen.findByText('Chưa lấy được danh sách mentor')).toBeInTheDocument()
  })
})
