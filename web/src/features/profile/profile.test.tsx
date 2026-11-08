import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider, type RouteObject } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const get = vi.fn()
const post = vi.fn()
const put = vi.fn()
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: { get: (...args: unknown[]) => get(...args), post: (...args: unknown[]) => post(...args), put: (...args: unknown[]) => put(...args) },
}))

const { ApiError } = await import('@/shared/api/apiClient')
const { completeProfileRoute, lecturerProfileRoutes, requireCompletedProfile, studentProfileRoutes } = await import('./routes')
const { tokenStore } = await import('@/shared/api/tokenStore')
const { AuthProvider } = await import('@/shared/auth/AuthProvider')
const { PageHeaderProvider } = await import('@/shared/layout/PageHeader')

const allSkills = [
  { skill: 'TECHNOLOGY', level: 4 },
  { skill: 'UI_UX_DESIGN', level: 2 },
  { skill: 'MANAGEMENT', level: 3 },
  { skill: 'MARKETING', level: 1 },
  { skill: 'COMMUNICATION_PLANNING', level: 5 },
]
const me = {
  id: 's1',
  studentCode: 'SE184118',
  fullName: 'Lê Thảo Vy',
  email: 'vy@example.test',
  major: { id: 1, code: 'SE', name: 'Kỹ thuật phần mềm' },
  specialization: { id: 1, code: 'SE', name: 'Kỹ thuật phần mềm' },
  classCode: 'EXE101SE01',
  courseCode: 'EXE101',
  hasTeam: false,
  isSeekingTeam: false,
  contactEmailVisible: true,
  skills: allSkills,
  description: 'Thích làm backend',
  profileCompleted: true,
}
const other = { ...me, id: 's2', studentCode: 'SE184119', fullName: 'Ngô Bảo Anh', email: '', contactEmailVisible: false, description: null }
const team = {
  id: 't1',
  name: 'EduSpark',
  courseCode: 'EXE101',
  status: 'ACTIVE',
  domains: [],
  memberCount: 3,
  openSlots: 3,
  members: [{ id: 's1', studentCode: 'SE184118', fullName: 'Lê Thảo Vy', role: 'LEADER' }],
  myRelation: 'LEADER',
  requirement: { memberCount: 3, minSize: 4, maxSize: 6, specializationCount: 1, minSpecializations: 2, isValid: false, missingSpecializations: [] },
}
const notFound = new ApiError({ status: 404, code: 'not-found', title: 'Không tìm thấy' })

/** API giả theo đường dẫn; thiếu đường dẫn nào thì lỗi ngay để lộ lời gọi ngoài dự kiến. */
function respond(routes: Record<string, unknown>) {
  const merged: Record<string, unknown> = {
    '/api/specializations': [{ id: 1, code: 'SE', name: 'Kỹ thuật phần mềm', isActive: true }],
    ...routes,
  }
  get.mockImplementation(async (url: string) => {
    if (!(url in merged)) throw new Error(`Chưa giả lập ${url}`)
    const value = merged[url]
    if (value instanceof Error) throw value
    return { data: value }
  })
}

const phase = { '/api/semesters/current/phase': [{ courseCode: 'EXE101', phase: 'TEAM_FORMATION' }] }

function renderAt(path: string, routes: RouteObject[], extra: RouteObject[] = []) {
  const router = createMemoryRouter(
    [
      { path: '/sv', children: routes },
      { path: '/gv', children: routes },
      { path: '/dang-nhap', element: <p>Trang đăng nhập</p> },
      ...extra,
    ],
    { initialEntries: [path] },
  )
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <AuthProvider>
        <PageHeaderProvider>
          <RouterProvider router={router} />
        </PageHeaderProvider>
      </AuthProvider>
    </QueryClientProvider>,
  )
  return router
}

describe('profile', () => {
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
      accessToken: 'a',
      accessTokenExpiresAt: '',
      refreshToken: 'r',
      refreshTokenExpiresAt: '',
      user: { id: 'u', email: 'vy@example.test', fullName: 'Lê Thảo Vy', roles: ['Student'], studentId: 's1' },
    })
  })

  it('A05 locks the button until all 5 skills are rated, then saves and opens S01', async () => {
    respond({ '/api/students/me': { ...me, skills: [], description: null, profileCompleted: false } })
    put.mockResolvedValue({ data: { ...me, profileCompleted: true } })
    const router = renderAt('/sv/ho-so/hoan-thien', [completeProfileRoute, { index: true, element: <p>Trang chủ</p> }])

    expect(await screen.findByText('Còn 5 kỹ năng chưa đánh giá')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Hoàn tất' })).toBeDisabled()
    expect(screen.queryByLabelText(/SĐT|Zalo/)).not.toBeInTheDocument()

    const sliders = screen.getAllByRole('slider')
    expect(sliders).toHaveLength(5)
    sliders.forEach((slider, i) => fireEvent.change(slider, { target: { value: String(i + 1 === 1 ? 2 : i + 1) } }))
    // Chạm đúng mức 1 (vị trí đang đứng của thanh chưa đánh giá) cũng tính là đã đánh giá.
    fireEvent.click(sliders[0])
    expect(screen.queryByText(/kỹ năng chưa đánh giá/)).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Hoàn tất' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/sv'))
    expect(put).toHaveBeenCalledWith('/api/students/me', expect.objectContaining({ description: '' }))
    expect(put.mock.calls[0][1].skills).toHaveLength(5)
  })

  it('A05 keeps what was typed when saving fails', async () => {
    respond({ '/api/students/me': { ...me, description: null, profileCompleted: false } })
    put.mockRejectedValue(new ApiError({ status: 500, code: 'server', title: 'Lỗi hệ thống' }))
    renderAt('/sv/ho-so/hoan-thien', [completeProfileRoute])

    await userEvent.type(await screen.findByRole('textbox'), 'Backend')
    await userEvent.click(screen.getByRole('button', { name: 'Hoàn tất' }))

    expect(await screen.findByText('Chưa lưu được hồ sơ')).toBeInTheDocument()
    expect(screen.getByRole('textbox')).toHaveValue('Backend')
  })

  it('sends students with an unfinished profile to A05 and lets them through afterwards', async () => {
    respond({ '/api/students/me': { ...me, skills: [], profileCompleted: false } })
    const router = renderAt('/sv', [completeProfileRoute, ...requireCompletedProfile([{ index: true, element: <p>Trang chủ</p> }])])
    await waitFor(() => expect(router.state.location.pathname).toBe('/sv/ho-so/hoan-thien'))
  })

  it('does not lock students out when the profile cannot be read', async () => {
    respond({ '/api/students/me': notFound })
    renderAt('/sv', requireCompletedProfile([{ index: true, element: <p>Trang chủ</p> }]))
    expect(await screen.findByText('Trang chủ')).toBeInTheDocument()
  })

  it('S02 shows skills, description and the current team without contact data', async () => {
    respond({ '/api/students/me': me, '/api/teams/me': team })
    renderAt('/sv/ho-so', studentProfileRoutes)

    expect(await screen.findByText('Lê Thảo Vy')).toBeInTheDocument()
    expect(screen.getByLabelText('Công nghệ, 4 trên 5')).toBeInTheDocument()
    expect(screen.getByText('Thích làm backend')).toBeInTheDocument()
    expect(await screen.findByRole('link', { name: 'EduSpark' })).toHaveAttribute('href', '/sv/nhom')
    expect(screen.queryByText('vy@example.test')).not.toBeInTheDocument()
  })

  it('S02 says so when the student has no team', async () => {
    respond({ '/api/students/me': me, '/api/teams/me': notFound })
    renderAt('/sv/ho-so', studentProfileRoutes)
    expect(await screen.findByText('Bạn chưa có nhóm.')).toBeInTheDocument()
  })

  it('S03 sends the seeking switch only during team formation', async () => {
    respond({ '/api/students/me': me, ...phase })
    put.mockResolvedValue({ data: { ...me, isSeekingTeam: true } })
    renderAt('/sv/ho-so/sua', studentProfileRoutes)

    await userEvent.click(await screen.findByRole('switch', { name: 'Tôi đang tìm nhóm' }))
    await userEvent.click(screen.getByRole('button', { name: 'Lưu' }))

    expect(await screen.findByText('Đã lưu hồ sơ')).toBeInTheDocument()
    expect(put.mock.calls[0][1]).toMatchObject({ isSeekingTeam: true })
  })

  it('S03 hides the seeking switch once the course has moved on', async () => {
    respond({ '/api/students/me': me, '/api/semesters/current/phase': [{ courseCode: 'EXE101', phase: 'SEMESTER_RUNNING' }] })
    put.mockResolvedValue({ data: me })
    renderAt('/sv/ho-so/sua', studentProfileRoutes)

    await screen.findByRole('button', { name: 'Lưu' })
    await waitFor(() => expect(screen.queryByRole('switch')).not.toBeInTheDocument())
    await userEvent.click(screen.getByRole('button', { name: 'Lưu' }))
    await waitFor(() => expect(put).toHaveBeenCalled())
    expect(put.mock.calls[0][1]).not.toHaveProperty('isSeekingTeam')
  })

  it('S04 lets the leader invite and then shows the invited state', async () => {
    respond({ '/api/students/s2': other, '/api/teams/me': team, '/api/teams/t1/invitations': [], ...phase })
    post.mockResolvedValue({ data: {} })
    renderAt('/sv/sinh-vien/s2', studentProfileRoutes)

    await userEvent.click(await screen.findByRole('button', { name: 'Gửi lời mời' }))
    await userEvent.click(screen.getAllByRole('button', { name: 'Gửi lời mời', hidden: true }).at(-1)!)

    expect(await screen.findByText('Đã mời')).toBeInTheDocument()
    expect(post).toHaveBeenCalledWith('/api/teams/t1/invitations', { studentId: 's2', message: undefined })
  })

  it('S04 shows a pending invitation', async () => {
    respond({ '/api/students/s2': other, '/api/teams/me': team, '/api/teams/t1/invitations': [{ invitee: { id: 's2' } }], ...phase })
    renderAt('/sv/sinh-vien/s2', studentProfileRoutes)
    expect(await screen.findByText('Đã mời')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Gửi lời mời' })).not.toBeInTheDocument()
  })

  it('S04 turns into "already has a team" when the invite is refused for that reason', async () => {
    respond({ '/api/students/s2': other, '/api/teams/me': team, '/api/teams/t1/invitations': [], ...phase })
    post.mockRejectedValue(new ApiError({ status: 409, code: 'already-in-team', title: 'Đã có nhóm' }))
    renderAt('/sv/sinh-vien/s2', studentProfileRoutes)

    await userEvent.click(await screen.findByRole('button', { name: 'Gửi lời mời' }))
    await userEvent.click(screen.getAllByRole('button', { name: 'Gửi lời mời', hidden: true }).at(-1)!)

    expect(await screen.findByText('Đã có nhóm')).toBeInTheDocument()
  })

  it('S04 gives non-leaders no invite and never asks for the team invitations', async () => {
    respond({ '/api/students/s2': other, '/api/teams/me': { ...team, myRelation: 'MEMBER', members: [] }, ...phase })
    renderAt('/sv/sinh-vien/s2', studentProfileRoutes)

    expect(await screen.findByText('Ngô Bảo Anh')).toBeInTheDocument()
    await waitFor(() => expect(get).toHaveBeenCalledWith('/api/teams/me'))
    expect(screen.queryByRole('button', { name: 'Gửi lời mời' })).not.toBeInTheDocument()
    expect(get).not.toHaveBeenCalledWith('/api/teams/t1/invitations', expect.anything())
  })

  it('S04 shows email to teammates only and never a phone number', async () => {
    const mate = { id: 's2', studentCode: 'SE184119', fullName: 'Ngô Bảo Anh', role: 'MEMBER', email: 'anh@example.test', phone: '0901234567' }
    respond({ '/api/students/s2': other, '/api/teams/me': { ...team, members: [mate] }, ...phase })
    renderAt('/sv/sinh-vien/s2', studentProfileRoutes)

    expect(await screen.findByText('anh@example.test')).toBeInTheDocument()
    expect(screen.getByText('Cùng nhóm với bạn')).toBeInTheDocument()
    expect(screen.queryByText('0901234567')).not.toBeInTheDocument()
  })

  it('S04 reports a student that does not exist', async () => {
    respond({ '/api/students/s9': notFound, '/api/teams/me': notFound })
    renderAt('/sv/sinh-vien/s9', studentProfileRoutes)
    expect(await screen.findByText('Không tìm thấy sinh viên')).toBeInTheDocument()
  })

  it('L06 edits domains and shows the limits set by the department as read-only', async () => {
    const lecturer = { id: 'l1', fullName: 'Trần Khoa', email: 'khoa@example.test', domains: [{ id: 1, code: 'D01', name: 'Giáo dục' }], maxTeamsExe101: 5, maxTeamsExe201: null }
    respond({ '/api/lecturers/me': lecturer, '/api/domains': [{ id: 1, code: 'D01', name: 'Giáo dục' }, { id: 2, code: 'D02', name: 'Y tế' }, { id: 3, code: 'D03', name: 'Đã tắt', isActive: false }] })
    put.mockResolvedValue({ data: { ...lecturer, domains: [{ id: 1, code: 'D01', name: 'Giáo dục' }, { id: 2, code: 'D02', name: 'Y tế' }] } })
    renderAt('/gv/ho-so', lecturerProfileRoutes)

    expect(await screen.findByText('Không giới hạn')).toBeInTheDocument()
    expect(screen.queryByRole('spinbutton')).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Sửa chuyên môn' }))
    await userEvent.click(await screen.findByRole('checkbox', { name: 'Y tế' }))
    expect(screen.queryByRole('checkbox', { name: 'Đã tắt' })).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Lưu' }))

    expect(await screen.findByText('Đã lưu chuyên môn')).toBeInTheDocument()
    expect(put).toHaveBeenCalledWith('/api/lecturers/me', { domainIds: [1, 2] })
  })

  it('A07 toggles email and logs out only after confirming', async () => {
    respond({ '/api/notifications/preferences': { emailEnabled: true } })
    put.mockResolvedValue({})
    post.mockResolvedValue({})
    const router = renderAt('/sv/cai-dat', studentProfileRoutes)

    const toggle = await screen.findByRole('switch', { name: 'Nhận email thông báo' })
    expect(toggle).toBeChecked()
    await userEvent.click(toggle)
    await waitFor(() => expect(put).toHaveBeenCalledWith('/api/notifications/preferences', { emailEnabled: false }))
    await waitFor(() => expect(screen.getByRole('switch', { name: 'Nhận email thông báo' })).not.toBeChecked())

    await userEvent.click(screen.getByRole('button', { name: 'Đăng xuất' }))
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(screen.getAllByRole('button', { name: 'Đăng xuất', hidden: true }).at(-1)!)
    await waitFor(() => expect(router.state.location.pathname).toBe('/dang-nhap'))
    expect(post).toHaveBeenCalledWith('/api/auth/logout', { refreshToken: 'r' })
  })
})
