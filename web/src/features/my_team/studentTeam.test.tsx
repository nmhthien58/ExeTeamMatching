import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const get = vi.fn()
const post = vi.fn()
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: {
    get: (...args: unknown[]) => get(...args),
    post: (...args: unknown[]) => post(...args),
    put: (...args: unknown[]) => post(...args),
    delete: (...args: unknown[]) => post(...args),
  },
}))

const { ApiError } = await import('@/shared/api/apiClient')
const { studentTeamRoutes } = await import('./routes')
const { tokenStore } = await import('@/shared/api/tokenStore')
const { AuthProvider } = await import('@/shared/auth/AuthProvider')
const { PageHeaderProvider } = await import('@/shared/layout/PageHeader')

const requirement = { memberCount: 2, minSize: 4, maxSize: 6, specializationCount: 1, minSpecializations: 2, isValid: false, missingSpecializations: [] }
const card = { id: 't1', name: 'Nhóm Bình Minh', projectName: 'EduSpark', courseCode: 'EXE101', status: 'DRAFT', domains: [], memberCount: 3, openSlots: 3, requirement: { ...requirement, memberCount: 3 } }
const team = (relation: string) => ({
  ...card,
  memberCount: 2,
  requirement,
  myRelation: relation,
  members: [{ id: 's1', studentCode: 'SE180001', fullName: 'Trưởng Nhóm', role: 'LEADER' }],
})

let routes: Record<string, unknown>

function renderAt(path: string) {
  get.mockImplementation(async (url: string) => {
    if (url === '/api/semesters/current/phase') return { data: [{ courseCode: 'EXE101', phase: 'TEAM_FORMATION' }] }
    if (url === '/api/students/me') return { data: { courseCode: 'EXE101' } }
    if (url in routes) {
      const value = routes[url]
      if (value instanceof Error) throw value
      return { data: value }
    }
    throw new Error(`GET ${url} chưa giả lập`)
  })
  const router = createMemoryRouter([{ path: '/sv', children: studentTeamRoutes }], { initialEntries: [path] })
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

const notFound = new ApiError({ status: 404, code: 'not-found', title: 'Không tìm thấy' })

describe('student team screens (FE-37)', () => {
  beforeEach(() => {
    get.mockReset()
    post.mockReset()
    routes = {}
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
      user: { id: 'u', email: 'vy@fpt.edu.vn', fullName: 'Lê Vy', roles: ['Student'], studentId: 's9' },
    })
  })

  it('S15 without a team offers create and discover', async () => {
    routes = { '/api/teams/me': notFound }
    renderAt('/sv/nhom')
    expect(await screen.findByText('Bạn chưa có nhóm')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Tạo nhóm' })).toHaveAttribute('href', '/sv/nhom/tao')
  })

  it('S15 shows status with text and the leader shortcuts', async () => {
    routes = { '/api/teams/me': team('LEADER') }
    renderAt('/sv/nhom')
    expect(await screen.findByText('Chưa đủ người')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Yêu cầu và lời mời/ })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Sửa thông tin/ })).toBeInTheDocument()
  })

  it('S13 validates fields, picks a domain and creates the team', async () => {
    routes = { '/api/domains': [{ id: 4, code: 'D04', name: 'Software' }], '/api/teams/me': team('LEADER') }
    post.mockResolvedValue({ data: team('LEADER') })
    const router = renderAt('/sv/nhom/tao')
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Tạo nhóm' }))
    expect(screen.getByText('Tên nhóm cần ít nhất 2 ký tự.')).toBeInTheDocument()
    expect(screen.getByText('Chọn ít nhất một domain.')).toBeInTheDocument()

    await user.type(screen.getByLabelText('Tên nhóm'), 'Nhóm Mới')
    await user.click(screen.getByRole('button', { name: 'Chọn domain' }))
    await user.click(await screen.findByRole('checkbox', { name: /D04/ }))
    await user.click(screen.getByRole('button', { name: 'Chọn (1)' }))
    await user.click(screen.getByRole('button', { name: 'Tạo nhóm' }))

    expect(post).toHaveBeenCalledWith('/api/teams', expect.objectContaining({ name: 'Nhóm Mới', domainIds: [4], primaryDomainId: 4 }))
    expect(router.state.location.pathname).toBe('/sv/nhom')
  })

  it('S13 explains the course team limit and points to Khám phá', async () => {
    routes = { '/api/domains': [{ id: 4, code: 'D04', name: 'Software' }], '/api/teams/me': team('LEADER') }
    post.mockRejectedValue(new ApiError({ status: 409, code: 'team-limit-reached', title: 'Đã đủ số nhóm của môn', detail: 'Môn này đã có đủ 100 nhóm trong kỳ.' }))
    const router = renderAt('/sv/nhom/tao')
    const user = userEvent.setup()

    await user.type(await screen.findByLabelText('Tên nhóm'), 'Nhóm Mới')
    await user.click(screen.getByRole('button', { name: 'Chọn domain' }))
    await user.click(await screen.findByRole('checkbox', { name: /D04/ }))
    await user.click(screen.getByRole('button', { name: 'Chọn (1)' }))
    await user.click(screen.getByRole('button', { name: 'Tạo nhóm' }))

    expect(await screen.findByText('Môn này đã có đủ 100 nhóm trong kỳ.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Thử lại' })).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Sang Khám phá' }))
    expect(router.state.location.pathname).toBe('/sv/kham-pha')
  })

  it('S05 lists open teams with seats left', async () => {
    routes = { '/api/teams': { items: [card], page: 1, pageSize: 20, totalCount: 1, totalPages: 1 } }
    renderAt('/sv/kham-pha')
    expect(await screen.findByText('Nhóm Bình Minh')).toBeInTheDocument()
    expect(screen.getByText('3/6 thành viên · còn 3 chỗ')).toBeInTheDocument()
  })

  it('S08/S09 requires ideas or "no idea" before sending', async () => {
    routes = { '/api/teams/t1': team('NONE'), '/api/teams/me': notFound }
    post.mockResolvedValue({ data: {} })
    renderAt('/sv/kham-pha/nhom/t1')
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Xin tham gia' }))
    const drawer = screen.getByRole('dialog', { name: 'Xin tham gia' })
    await user.click(within(drawer).getByRole('button', { name: 'Gửi yêu cầu' }))
    expect(within(drawer).getByRole('alert')).toHaveTextContent('Thêm ít nhất 1 ý tưởng')
    expect(post).not.toHaveBeenCalled()

    await user.click(within(drawer).getByRole('checkbox', { name: 'Chưa có ý tưởng' }))
    await user.click(within(drawer).getByRole('button', { name: 'Gửi yêu cầu' }))
    expect(post).toHaveBeenCalledWith('/api/teams/t1/join-requests', { message: undefined, ideas: [], hasNoIdea: true })
  })

  it('S20 shows the applicant skills (SkillBars) and self-description; "Chưa đánh giá" when empty', async () => {
    const request = (id: string, extra = {}) => ({
      id, student: { id: `s-${id}`, studentCode: `SE18000${id}`, fullName: `Người xin ${id}` }, ideas: [], hasNoIdea: true, status: 'PENDING',
      createdAt: '2026-11-01T00:00:00Z', expiresAt: '2026-11-04T00:00:00Z', ...extra,
    })
    routes = {
      '/api/teams/me': team('LEADER'),
      '/api/teams/t1/join-requests': [
        request('7', { studentSkills: [{ skill: 'TECHNOLOGY', level: 4 }, { skill: 'MARKETING', level: 2 }], studentDescription: 'Thích làm frontend' }),
        request('8'),
      ],
      '/api/teams/t1/invitations': [],
    }
    renderAt('/sv/nhom/yeu-cau')
    expect(await screen.findByText('Thích làm frontend')).toBeInTheDocument()
    expect(screen.getByLabelText('Công nghệ, 4 trên 5')).toBeInTheDocument()
    expect(screen.getByText('Chưa đánh giá kỹ năng.')).toBeInTheDocument()
  })

  it('S08 explains why someone with a team cannot ask to join', async () => {
    routes = { '/api/teams/t1': team('NONE'), '/api/teams/me': team('LEADER') }
    renderAt('/sv/kham-pha/nhom/t1')
    expect(await screen.findByText('Bạn đã có nhóm.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Xin tham gia' })).not.toBeInTheDocument()
  })

  it('S10 lists students looking for a team and lets a leader with seats invite them', async () => {
    const seeking = { id: 'p1', title: 'Frontend', description: 'Làm giao diện', domainIds: [5], skills: [], status: 'OPEN', updatedAt: '',
      student: { id: 's7', studentCode: 'SE180007', fullName: 'Nguyễn An', specialization: { id: 2, code: 'MKT', name: 'Marketing' } } }
    routes = {
      '/api/seeking-posts': { items: [seeking], page: 1, pageSize: 20, totalCount: 1, totalPages: 1 },
      '/api/teams/me': team('LEADER'),
      '/api/specializations': [{ id: 2, code: 'MKT', name: 'Marketing' }],
      '/api/domains': [{ id: 5, code: 'D05', name: 'Giáo dục' }],
    }
    renderAt('/sv/kham-pha?tab=sinh-vien')
    expect(await screen.findByText('Nguyễn An')).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: 'Sinh viên tìm nhóm' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByText('SE180007 · Marketing')).toBeInTheDocument()
    expect(await screen.findByText('Giáo dục')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Mời vào nhóm' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Xem hồ sơ Nguyễn An/ })).toHaveAttribute('href', '/sv/sinh-vien/s7')
  })

  it('S11 needs a role before posting, then saves domains and returns to Discover', async () => {
    routes = { '/api/seeking-posts/me': notFound, '/api/domains': [{ id: 5, code: 'D05', name: 'Giáo dục' }] }
    post.mockResolvedValue({ data: {} })
    const router = renderAt('/sv/kham-pha/tim-nhom')
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Đăng bài' }))
    expect(screen.getByText('Nhập vai trò mong muốn.')).toBeInTheDocument()
    await user.type(screen.getByLabelText('Vai trò mong muốn'), 'Backend')
    await user.click(await screen.findByRole('button', { name: 'Giáo dục' }))
    await user.click(screen.getByRole('button', { name: 'Đăng bài' }))

    expect(post).toHaveBeenCalledWith('/api/seeking-posts/me', { title: 'Backend', description: undefined, domainIds: [5] })
    expect(router.state.location.pathname).toBe('/sv/kham-pha')
  })

  it('S21 shows seats left and saves the needed specializations of the open post', async () => {
    routes = {
      '/api/teams/me': { ...team('LEADER'), recruitmentPost: { id: 'r1', title: 'Cần BA', specializationIds: [] } },
      '/api/specializations': [{ id: 2, code: 'MKT', name: 'Marketing' }],
    }
    post.mockResolvedValue({ data: {} })
    renderAt('/sv/nhom/bai-tuyen')
    const user = userEvent.setup()

    expect(await screen.findByText('Còn 4 chỗ trống')).toBeInTheDocument()
    await user.click(await screen.findByRole('button', { name: 'Marketing' }))
    await user.click(screen.getByRole('button', { name: 'Lưu' }))
    expect(post).toHaveBeenCalledWith('/api/teams/t1/recruitment-post', { title: 'Cần BA', description: undefined, specializationIds: [2] })
  })

  it('S22 lists the team history with who and when', async () => {
    routes = {
      '/api/teams/me': team('MEMBER'),
      '/api/teams/t1/audit-logs': {
        items: [{ id: 2, teamId: 't1', action: 'MEMBER_JOINED', actorName: 'Sinh viên', actorRole: 'Student', summary: 'SE180002 vào nhóm', occurredAt: '2026-10-27T03:05:00Z' }],
        page: 1, pageSize: 20, totalCount: 1, totalPages: 1,
      },
    }
    renderAt('/sv/nhom/lich-su')
    expect(await screen.findByText('SE180002 vào nhóm')).toBeInTheDocument()
    expect(screen.getByText(/^Sinh viên · /)).toBeInTheDocument()
  })

  it('S15 shows advisors with match reason, Checkpoint n/4 and a shortcut to the project', async () => {
    routes = {
      '/api/teams/me': team('MEMBER'),
      '/api/teams/t1/lecturer': { lecturerName: 'Cô Hạnh', reason: 'PREFERENCE', preferenceRank: 1 },
      '/api/teams/t1/mentor': '',
      '/api/workspaces/me': [{ id: 'w-old', teamId: 'other', teamName: 'Nhóm cũ', courseCode: 'EXE101', myRole: 'MEMBER' }, { id: 'w1', teamId: card.id, teamName: 'Nhóm Bình Minh', courseCode: 'EXE101', myRole: 'MEMBER' }],
      '/api/workspaces/w1/checkpoints': { workspaceId: 'w1', doneCount: 2, items: [] },
    }
    renderAt('/sv/nhom')

    expect(await screen.findByText('Giảng viên: Cô Hạnh')).toBeInTheDocument()
    expect(screen.getByText('Nguyện vọng 1')).toBeInTheDocument()
    expect(screen.getByText('Mentor: Ghép từ tuần 6')).toBeInTheDocument()
    expect(await screen.findByText('Checkpoint 2/4')).toBeInTheDocument()
    expect(await screen.findByRole('link', { name: /Mở dự án/ })).toHaveAttribute('href', '/sv/du-an')
  })

  it('S15 shows the inherited project card and its timeline, newest first', async () => {
    routes = {
      '/api/teams/me': { ...team('MEMBER'), courseCode: 'EXE201', projectName: 'EduSpark AI' },
      '/api/teams/t1/project': {
        id: 'p1', name: 'EduSpark AI', originCourseCode: 'EXE101', originSemesterCode: 'SP26',
        versions: [
          { version: 2, name: 'EduSpark AI', changeType: 'PIVOT', reason: 'Thị trường nhỏ', semesterCode: 'SU26', courseCode: 'EXE201', createdAt: '2026-06-10T03:00:00Z' },
          { version: 1, name: 'EduSpark', changeType: 'CREATED', semesterCode: 'SP26', courseCode: 'EXE101', createdAt: '2026-01-10T03:00:00Z' },
        ],
      },
    }
    renderAt('/sv/nhom')
    const user = userEvent.setup()

    expect(await screen.findByText('Tiếp tục từ EXE101 · SP26')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Lịch sử' }))
    const items = within(screen.getByRole('list', { name: 'Dòng thời gian đề tài' })).getAllByRole('listitem')
    expect(items[0]).toHaveTextContent('Đổi đề tài: EduSpark → EduSpark AI · Thị trường nhỏ')
    expect(items[1]).toHaveTextContent('Tạo đề tài: EduSpark')
  })

  it('S12 accepts an invitation after confirming and opens the team', async () => {
    const expiresAt = new Date(Date.now() + 30 * 3600_000).toISOString()
    routes = {
      '/api/invitations/me': [{ id: 'i1', team: card, invitee: { id: 's9', studentCode: 'SE180009', fullName: 'Lê Vy' }, status: 'PENDING', createdAt: '', expiresAt }],
      '/api/teams/me': notFound,
    }
    post.mockResolvedValue({ data: { appliedImmediately: true, team: team('MEMBER') } })
    const router = renderAt('/sv/loi-moi')
    const user = userEvent.setup()

    expect(await screen.findByText('Còn 1 ngày 5 giờ')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Chấp nhận' }))
    await user.click(screen.getByRole('button', { name: 'Vào nhóm' }))
    expect(post).toHaveBeenCalledWith('/api/invitations/i1/accept')
    expect(router.state.location.pathname).toBe('/sv/nhom')
  })
})
