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
  api: { get: (...args: unknown[]) => get(...args), post: (...args: unknown[]) => post(...args), put: (...args: unknown[]) => put(...args) },
}))

const { minutesLines } = await import('./api')
const { workspaceRoutes } = await import('@/features/workspace/routes')
const { tokenStore } = await import('@/shared/api/tokenStore')
const { AuthProvider } = await import('@/shared/auth/AuthProvider')
const { PageHeaderProvider, usePageHeaderState } = await import('@/shared/layout/PageHeader')

async function type(element: HTMLElement, text: string) {
  await userEvent.click(element)
  await userEvent.paste(text)
}

function HeaderAction() {
  return <>{usePageHeaderState().action}</>
}

const vy = { personType: 'STUDENT', personId: 's1', displayName: 'Lê Vy', role: 'LEADER' }
const ha = { personType: 'LECTURER', personId: 'l1', displayName: 'Trần Thu Hà', role: 'INSTRUCTOR' }
const workspace = { id: 'w1', teamId: 't1', teamName: 'Nhóm EduSpark', courseCode: 'EXE201', myRole: 'LEADER', people: [vy] }
const card = { workspaceId: 'w1', teamName: 'Nhóm EduSpark', isMentorSession: false, slotCount: 1, location: null, myAnswerPending: false, isWeekly: false, weekNo: null }
const proposed = {
  ...card, id: 'm1', title: 'Chốt ý tưởng', status: 'PROPOSED', organizer: vy, slotCount: 2, myAnswerPending: true, startsAt: null,
  agenda: 'Bàn ý tưởng', suggestedSlotId: 'x2', cancelReason: null, minutes: null, createdTasks: [],
  slots: [
    { id: 'x1', startsAt: '2030-11-10T02:00:00Z', durationMinutes: 60, yesCount: 1, inviteeCount: 5, myAnswer: null, yesPeople: [] },
    { id: 'x2', startsAt: '2030-11-11T02:00:00Z', durationMinutes: 60, yesCount: 3, inviteeCount: 5, myAnswer: 'YES', yesPeople: [vy] },
  ],
}
const done = {
  ...card, id: 'm2', title: 'Họp với giảng viên', status: 'DONE', organizer: ha, startsAt: '2030-10-01T02:00:00Z', isWeekly: true, weekNo: 3,
  agenda: null, suggestedSlotId: null, cancelReason: null, slots: [], createdTasks: [{ id: 'k1', title: 'Làm landing page' }],
  minutes: { content: '- Làm landing page', recordedByName: 'Trần Thu Hà', recordedAt: '2030-10-01T04:00:00Z' },
}
const scheduledMine = { ...done, id: 'm3', title: 'Buổi mentor 1', status: 'SCHEDULED', organizer: vy, isWeekly: false, isMentorSession: true, minutes: null, createdTasks: [] }

function renderAt(path: string, overrides: Record<string, unknown> = {}) {
  get.mockImplementation(async (url: string) => {
    if (url in overrides) return { data: overrides[url] }
    if (url === '/api/workspaces/me') return { data: [workspace] }
    if (url === '/api/workspaces/w1') return { data: workspace }
    if (url === '/api/workspaces/w1/meetings') return { data: { mentorSessionsDone: 1, expectedMentorSessions: 3, items: [proposed, done, scheduledMine] } }
    if (url === '/api/meetings/m1') return { data: proposed }
    if (url === '/api/meetings/m2') return { data: done }
    if (url === '/api/meetings/m3') return { data: scheduledMine }
    throw new Error(`GET ${url} chưa giả lập`)
  })
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <AuthProvider>
        <PageHeaderProvider>
          <HeaderAction />
          <RouterProvider router={createMemoryRouter([{ path: '/sv', children: [workspaceRoutes('/sv')] }], { initialEntries: [path] })} />
        </PageHeaderProvider>
      </AuthProvider>
    </QueryClientProvider>,
  )
}

const drawer = () => screen.getByRole('dialog', { name: /Chốt ý tưởng|Họp với giảng viên|Buổi mentor 1/ })

describe('C07, C08 – Lịch họp (FE-36)', () => {
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
      user: { id: 'u', email: 'vy@fpt.edu.vn', fullName: 'Vy', roles: ['Student'], studentId: 's1' },
    })
  })

  it('turns minutes into task titles', () => {
    expect(minutesLines('- Làm landing page\n\n2) Gọi khách hàng\n• Viết báo cáo')).toEqual(['Làm landing page', 'Gọi khách hàng', 'Viết báo cáo'])
  })

  it('C07 groups meetings and shows the mentor session count', async () => {
    renderAt('/sv/du-an/lich-hop')
    expect(await screen.findByText('Buổi mentor:')).toBeInTheDocument()
    expect(screen.getByText('1/3')).toBeInTheDocument()
    const pending = screen.getByRole('region', { name: 'Chờ chốt (1)' })
    expect(within(pending).getByText('Chốt ý tưởng')).toBeInTheDocument()
    expect(within(pending).getByText('Chưa trả lời')).toBeInTheDocument()
    expect(within(screen.getByRole('region', { name: 'Sắp tới (1)' })).getByText('Buổi mentor')).toBeInTheDocument()
    expect(within(screen.getByRole('region', { name: 'Đã họp (1)' })).getByText('Hằng tuần · tuần 3')).toBeInTheDocument()
  })

  it('C08 answers a slot and the organizer confirms the suggested one', async () => {
    put.mockResolvedValue({ data: { ...proposed, slots: [{ ...proposed.slots[0], myAnswer: 'NO' }, proposed.slots[1]] } })
    post.mockResolvedValue({ data: { ...proposed, status: 'SCHEDULED', startsAt: proposed.slots[1].startsAt } })
    renderAt('/sv/du-an/lich-hop?hop=m1')
    const group = await within(await screen.findByRole('dialog', { name: 'Chốt ý tưởng' })).findAllByRole('group')
    await userEvent.click(within(group[0]).getByRole('button', { name: 'Không' }))
    await waitFor(() => expect(put).toHaveBeenCalledWith('/api/meetings/m1/responses', { answers: [{ slotId: 'x1', answer: 'NO' }] }))
    expect(within(drawer()).getByText('Nhiều người chọn nhất')).toBeInTheDocument()
    await userEvent.click(within(group[1]).getByRole('button', { name: 'Chốt khung này' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith('/api/meetings/m1/confirm', { slotId: 'x2' }))
    expect(await screen.findByText('Đã chốt khung giờ')).toBeInTheDocument()
  })

  it('C08 proposes with validation; one slot creates a scheduled meeting', async () => {
    post.mockResolvedValue({ data: { ...scheduledMine, id: 'm9' } })
    renderAt('/sv/du-an/lich-hop')
    await userEvent.click(await screen.findByRole('button', { name: 'Đề xuất họp' }))
    const dialog = screen.getByRole('dialog', { name: 'Đề xuất họp', hidden: true })
    await userEvent.click(within(dialog).getByRole('button', { name: 'Tạo cuộc họp', hidden: true }))
    expect(within(dialog).getByText('Nhập tiêu đề.')).toBeInTheDocument()
    expect(within(dialog).getByText('Chọn ngày giờ.')).toBeInTheDocument()
    await type(within(dialog).getByRole('textbox', { name: /^Tiêu đề/, hidden: true }), 'Họp tuần')
    await type(within(dialog).getByLabelText(/^Khung 1/), '2030-12-01T09:00')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Thêm khung giờ', hidden: true }))
    expect(within(dialog).getByRole('button', { name: 'Gửi đề xuất', hidden: true })).toBeInTheDocument()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Bỏ khung 2', hidden: true }))
    await userEvent.click(within(dialog).getByRole('button', { name: 'Tạo cuộc họp', hidden: true }))
    await waitFor(() =>
      expect(post).toHaveBeenCalledWith('/api/workspaces/w1/meetings', {
        title: 'Họp tuần', agenda: null, location: null, slots: [{ startsAt: new Date('2030-12-01T09:00').toISOString(), durationMinutes: 60 }],
      }),
    )
    expect(await screen.findByText('Đã tạo cuộc họp')).toBeInTheDocument()
  })

  it('C08 organizer writes minutes; the meeting becomes held', async () => {
    put.mockResolvedValue({ data: { ...scheduledMine, status: 'DONE', minutes: { content: 'Xong', recordedByName: 'Lê Vy', recordedAt: '2030-10-01T05:00:00Z' } } })
    renderAt('/sv/du-an/lich-hop?hop=m3')
    await type(await within(await screen.findByRole('dialog', { name: 'Buổi mentor 1' })).findByRole('textbox', { name: 'Nội dung biên bản' }), 'Xong')
    await userEvent.click(within(drawer()).getByRole('button', { name: 'Lưu biên bản' }))
    await waitFor(() => expect(put).toHaveBeenCalledWith('/api/meetings/m3/minutes', { content: 'Xong' }))
    expect(await screen.findByText('Đã lưu biên bản')).toBeInTheDocument()
    expect(within(drawer()).getByText('Đã họp')).toBeInTheDocument()
    expect(within(drawer()).queryByRole('button', { name: 'Hủy cuộc họp' })).not.toBeInTheDocument()
  })

  it('C08 organizer cancels with a reason', async () => {
    post.mockResolvedValue({ data: { ...scheduledMine, status: 'CANCELLED', cancelReason: 'Trùng lịch thi' } })
    renderAt('/sv/du-an/lich-hop?hop=m3')
    await userEvent.click(await within(await screen.findByRole('dialog', { name: 'Buổi mentor 1' })).findByRole('button', { name: 'Hủy cuộc họp' }))
    const confirm = screen.getByRole('dialog', { name: 'Hủy cuộc họp?', hidden: true })
    expect(within(confirm).getByRole('button', { name: 'Hủy họp', hidden: true })).toBeDisabled()
    await type(within(confirm).getByRole('textbox', { hidden: true }), 'Trùng lịch thi')
    await userEvent.click(within(confirm).getByRole('button', { name: 'Hủy họp', hidden: true }))
    await waitFor(() => expect(post).toHaveBeenCalledWith('/api/meetings/m3/cancel', { reason: 'Trùng lịch thi' }))
  })

  it('C08 a member sees minutes and tasks of a meeting someone else held', async () => {
    renderAt('/sv/du-an/lich-hop?hop=m2')
    const d = await screen.findByRole('dialog', { name: 'Họp với giảng viên' })
    expect(within(d).getByText('- Làm landing page')).toBeInTheDocument()
    expect(within(d).getByText('Làm landing page')).toBeInTheDocument()
    expect(within(d).queryByRole('button', { name: 'Hủy cuộc họp' })).not.toBeInTheDocument()
    expect(within(d).queryByRole('textbox')).not.toBeInTheDocument()
  })
})
