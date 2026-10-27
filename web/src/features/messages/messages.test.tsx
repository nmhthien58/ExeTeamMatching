import { act, render, screen } from '@testing-library/react'
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
const { tokenStore } = await import('@/shared/api/tokenStore')
const { AuthProvider } = await import('@/shared/auth/AuthProvider')
const { CollabHub } = await import('@/features/realtime/collabHub')
const { HubProvider } = await import('@/features/realtime/RealtimeStatusBar')
const { ChannelView } = await import('./ChannelView')
const { AdvisorMessagesPage } = await import('./MessagesPage')

const leader = { personType: 'STUDENT', personId: 's1', displayName: 'Trưởng Nhóm', role: 'LEADER' }
const lecturer = { personType: 'LECTURER', personId: 'l1', displayName: 'Cô Hạnh', role: 'INSTRUCTOR' }
const channel = { id: 'c1', workspaceId: 'w1', kind: 'ADVISORS' as const, unreadCount: 0 }
const msg = (id: string, body: string, author = leader, extra = {}) => ({
  id, channelId: 'c1', author, body, isDeleted: false, isAdvisorQuestion: false, replyCount: 0, createdAt: '2026-10-27T03:00:00Z', ...extra,
})

function fakeHub() {
  const handlers: Record<string, ((p: unknown) => void)[]> = {}
  const connection = {
    start: vi.fn(async () => {}),
    stop: vi.fn(async () => {}),
    invoke: vi.fn(async () => undefined),
    on: (e: string, h: (p: unknown) => void) => void (handlers[e] = [...(handlers[e] ?? []), h]),
    off: vi.fn(),
    onreconnecting: vi.fn(),
    onreconnected: vi.fn(),
    onclose: vi.fn(),
  }
  return { hub: new CollabHub(() => connection as never), connection, emit: (e: string, p: unknown) => handlers[e]?.forEach((h) => h(p)) }
}

function renderWith(element: React.ReactNode, hub = fakeHub().hub, path = '/') {
  const router = createMemoryRouter([{ path: '/', element }], { initialEntries: [path] })
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

describe('Tin nhắn C02, C03 (FE-38)', () => {
  beforeEach(() => {
    get.mockReset()
    post.mockReset()
    globalThis.crypto.randomUUID ??= () => '00000000-0000-4000-8000-000000000000'
    tokenStore.save({
      accessToken: 'a', accessTokenExpiresAt: '', refreshToken: 'r', refreshTokenExpiresAt: '',
      user: { id: 'u', email: 'sv@fpt.edu.vn', fullName: 'Trưởng Nhóm', roles: ['Student'], studentId: 's1' },
    })
  })

  it('C03: tin cũ ở trên, câu hỏi có chip Chờ trả lời; tin mới qua hub hiện ngay; đánh dấu đã đọc tin mới nhất', async () => {
    get.mockResolvedValue({ data: { items: [msg('m2', 'Cô ơi xem giúp mô hình kinh doanh', leader, { isAdvisorQuestion: true, questionStatus: 'OPEN' }), msg('m1', 'Chào cô')], nextCursor: null } })
    post.mockResolvedValue({ data: {} })
    const { hub, emit } = fakeHub()
    renderWith(<ChannelView channel={channel} title="Cố vấn" readOnly={false} isStudent />, hub)

    const items = await screen.findAllByText(/Chào cô|mô hình kinh doanh/)
    expect(items.map((i) => i.textContent)).toEqual(['Chào cô', 'Cô ơi xem giúp mô hình kinh doanh'])
    expect(screen.getByText('Chờ trả lời')).toBeInTheDocument()
    expect(post).toHaveBeenCalledWith('/api/channels/c1/read', { lastReadMessageId: 'm2' })

    act(() => emit('messageCreated', msg('m3', 'Cô sẽ góp ý chiều nay', lecturer)))
    expect(await screen.findByText('Cô sẽ góp ý chiều nay')).toBeInTheDocument()
    expect(screen.getByText('Giảng viên')).toBeInTheDocument()
  })

  it('C03: Enter gửi kèm "Hỏi cố vấn"; gửi lỗi giữ tin với Gửi lại, gửi lại dùng cùng clientMessageId', async () => {
    get.mockResolvedValue({ data: { items: [], nextCursor: null } })
    post.mockRejectedValueOnce(new ApiError({ status: 0, code: 'network', title: 'Mất kết nối' }))
    renderWith(<ChannelView channel={channel} title="Cố vấn" readOnly={false} isStudent />)
    const user = userEvent.setup()

    expect(await screen.findByText('Chưa có tin nhắn.')).toBeInTheDocument()
    await user.click(screen.getByRole('checkbox', { name: 'Hỏi cố vấn' }))
    await user.type(screen.getByRole('textbox', { name: 'Tin nhắn' }), 'Nhờ cô duyệt đề tài{Enter}')
    expect(await screen.findByRole('alert')).toHaveTextContent('Gửi lỗi')
    const first = post.mock.calls[0]
    expect(first[0]).toBe('/api/channels/c1/messages')
    expect(first[1]).toMatchObject({ body: 'Nhờ cô duyệt đề tài', isAdvisorQuestion: true })

    post.mockResolvedValueOnce({ data: msg('x', 'Nhờ cô duyệt đề tài') })
    await user.click(screen.getByRole('button', { name: 'Gửi lại' }))
    expect(post.mock.calls[1][1].clientMessageId).toBe(first[1].clientMessageId)
  })

  it('C03: Bộ môn chỉ xem, không có khung soạn tin', async () => {
    get.mockResolvedValue({ data: { items: [msg('m1', 'Chào cô')], nextCursor: null } })
    renderWith(<ChannelView channel={channel} title="Cố vấn" readOnly isStudent={false} />)
    expect(await screen.findByText('Bộ môn chỉ xem kênh này.')).toBeInTheDocument()
    expect(screen.queryByRole('textbox', { name: 'Tin nhắn' })).not.toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
  })

  it('C02 cố vấn: mỗi nhóm một dòng kênh Cố vấn; lọc Cần trả lời chỉ còn nhóm có câu hỏi chờ', async () => {
    tokenStore.save({
      accessToken: 'a', accessTokenExpiresAt: '', refreshToken: 'r', refreshTokenExpiresAt: '',
      user: { id: 'u2', email: 'gv@fpt.edu.vn', fullName: 'Cô Hạnh', roles: ['Lecturer'], lecturerId: 'l1' },
    })
    get.mockImplementation(async (url: string) => {
      if (url === '/api/workspaces/me') return { data: [{ id: 'w1', teamName: 'Nhóm A' }, { id: 'w2', teamName: 'Nhóm B' }] }
      if (url === '/api/workspaces/w1/channels') return { data: [{ ...channel, lastMessage: msg('m1', 'Cô ơi') }] }
      if (url === '/api/workspaces/w2/channels') return { data: [{ id: 'c2', workspaceId: 'w2', kind: 'ADVISORS', unreadCount: 2 }] }
      if (url === '/api/advisor/questions') return { data: { items: [{ message: msg('m1', 'Cô ơi'), workspaceId: 'w1', teamName: 'Nhóm A', status: 'OPEN', waitingHours: 3 }] } }
      return { data: { items: [], nextCursor: null } }
    })
    renderWith(<AdvisorMessagesPage />)
    const user = userEvent.setup()

    expect(await screen.findByRole('button', { name: /Nhóm B · Cố vấn/ })).toBeInTheDocument()
    expect(screen.getByLabelText('2 tin chưa đọc')).toBeInTheDocument()
    await user.click(screen.getByRole('radio', { name: 'Cần trả lời (1)' }))
    expect(screen.queryByRole('button', { name: /Nhóm B · Cố vấn/ })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Nhóm A · Cố vấn/ })).toHaveTextContent('Cần trả lời')
  })
})
