import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const get = vi.fn()
const post = vi.fn()
const patch = vi.fn()
const del = vi.fn()
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: {
    get: (...args: unknown[]) => get(...args),
    post: (...args: unknown[]) => post(...args),
    patch: (...args: unknown[]) => patch(...args),
    delete: (...args: unknown[]) => del(...args),
  },
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
    patch.mockReset()
    del.mockReset()
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

  it('C03: chọn file C06 và @nhắc tên rồi gửi; tin có file bấm vào để tải', async () => {
    const pdf = { id: 'f1', name: 'ke-hoach.pdf', currentVersion: 1, sizeBytes: 2048, contentType: 'application/pdf', updatedByName: 'Vy', updatedAt: '2026-10-27T03:00:00Z', isArchive: false }
    const archived = { ...pdf, id: 'f2', name: 'exe101.pdf', isArchive: true }
    const open = vi.spyOn(window, 'open').mockReturnValue(null)
    get.mockImplementation(async (url: string) => {
      if (url === '/api/workspaces/w1') return { data: { id: 'w1', people: [leader, lecturer] } }
      if (url === '/api/workspaces/w1/files') return { data: { breadcrumb: [], folders: [], files: [pdf, archived], canUpload: true } }
      if (url === '/api/files/f1/download-url') return { data: { url: 'https://s3/f1' } }
      return { data: { items: [msg('m1', 'Xem file', leader, { attachments: [pdf] })], nextCursor: null } }
    })
    post.mockResolvedValue({ data: msg('m2', 'gửi') })
    renderWith(<ChannelView channel={channel} title="Cố vấn" readOnly={false} isStudent />)
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Tải xuống ke-hoach.pdf' }))
    expect(open).toHaveBeenCalledWith('https://s3/f1', '_blank', 'noopener')

    await user.click(screen.getByRole('button', { name: 'Đính kèm' }))
    await user.click(await screen.findByRole('checkbox', { name: /ke-hoach\.pdf/ }))
    expect(screen.queryByRole('checkbox', { name: /exe101/ })).not.toBeInTheDocument()
    const box = screen.getByRole('textbox', { name: 'Tin nhắn' })
    await user.type(box, 'Cô @Tr')
    expect(screen.queryByRole('list', { name: 'Nhắc tên' })).not.toBeInTheDocument() // không tự nhắc mình
    await user.clear(box)
    await user.type(box, 'Cô @h')
    expect(await screen.findByRole('list', { name: 'Nhắc tên' })).toBeInTheDocument()
    await user.type(box, '{Enter}xem giúp em{Enter}')

    const sent = post.mock.calls.find((c) => c[0] === '/api/channels/c1/messages')?.[1]
    expect(sent).toMatchObject({ body: 'Cô @Cô Hạnh xem giúp em', attachmentFileIds: ['f1'], mentionPersonIds: ['l1'] })
    expect(screen.queryByRole('list', { name: 'File đính kèm' })).not.toBeInTheDocument()
    open.mockRestore()
  })

  it('C03: sửa tin của mình trong 15 phút; xóa có Hoàn tác 5 giây rồi mới gọi API; tin người khác không có nút', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const now = new Date().toISOString()
    get.mockResolvedValue({ data: { items: [msg('m2', 'Tin của cô', lecturer, { createdAt: now }), msg('m1', 'Họp lúc 9h', leader, { createdAt: now })], nextCursor: null } })
    post.mockResolvedValue({ data: {} })
    patch.mockResolvedValue({ data: msg('m1', 'Họp lúc 10h', leader, { createdAt: now, editedAt: now }) })
    del.mockResolvedValue({ data: undefined })
    renderWith(<ChannelView channel={channel} title="Cố vấn" readOnly={false} isStudent />)
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })

    expect(await screen.findByText('Họp lúc 9h')).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: 'Sửa' })).toHaveLength(1) // chỉ tin của mình
    await user.click(screen.getByRole('button', { name: 'Sửa' }))
    await user.clear(screen.getByRole('textbox', { name: 'Sửa tin nhắn' }))
    await user.type(screen.getByRole('textbox', { name: 'Sửa tin nhắn' }), 'Họp lúc 10h')
    await user.click(screen.getByRole('button', { name: 'Lưu' }))
    expect(patch).toHaveBeenCalledWith('/api/messages/m1', { body: 'Họp lúc 10h' })
    expect(await screen.findByText('Họp lúc 10h')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Xóa' }))
    expect(screen.queryByText('Họp lúc 10h')).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Hoàn tác' }))
    expect(screen.getByText('Họp lúc 10h')).toBeInTheDocument()
    act(() => void vi.advanceTimersByTime(6000))
    expect(del).not.toHaveBeenCalled()

    await user.click(screen.getByRole('button', { name: 'Xóa' }))
    act(() => void vi.advanceTimersByTime(5000))
    await vi.waitFor(() => expect(del).toHaveBeenCalledWith('/api/messages/m1'))
    expect(await screen.findByText('Tin nhắn đã bị xóa')).toBeInTheDocument()
    vi.useRealTimers()
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
