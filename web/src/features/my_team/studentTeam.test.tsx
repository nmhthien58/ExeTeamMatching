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
    expect(await screen.findByText('Nháp')).toBeInTheDocument()
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

  it('S08 explains why someone with a team cannot ask to join', async () => {
    routes = { '/api/teams/t1': team('NONE'), '/api/teams/me': team('LEADER') }
    renderAt('/sv/kham-pha/nhom/t1')
    expect(await screen.findByText('Bạn đã có nhóm.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Xin tham gia' })).not.toBeInTheDocument()
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
