import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, Outlet, RouterProvider } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const get = vi.fn()
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: { get: (...args: unknown[]) => get(...args), post: vi.fn(), put: vi.fn() },
}))

const { lecturerCalendarRoutes } = await import('./routes')
const { tokenStore } = await import('@/shared/api/tokenStore')
const { AuthProvider } = await import('@/shared/auth/AuthProvider')
const { PageHeaderProvider, usePageHeaderState } = await import('@/shared/layout/PageHeader')

function HeaderAction() {
  return <>{usePageHeaderState().action}</>
}

const ha = { personType: 'LECTURER', personId: 'l1', displayName: 'Trần Thu Hà', role: 'INSTRUCTOR' }
const w1 = { id: 'w1', teamId: 't1', teamName: 'Nhóm EduSpark', courseCode: 'EXE201', myRole: 'INSTRUCTOR' }
const w2 = { id: 'w2', teamId: 't2', teamName: 'Nhóm GreenBox', courseCode: 'EXE101', myRole: 'INSTRUCTOR' }
const base = { isMentorSession: false, slotCount: 1, location: null, myAnswerPending: false, isWeekly: false, weekNo: null, organizer: ha }
const later = { ...base, id: 'm1', workspaceId: 'w1', teamName: 'Nhóm EduSpark', title: 'Họp tuần 5', status: 'SCHEDULED', startsAt: '2030-11-12T02:00:00Z', isWeekly: true, weekNo: 5 }
const sooner = { ...base, id: 'm2', workspaceId: 'w2', teamName: 'Nhóm GreenBox', title: 'Góp ý MVP', status: 'SCHEDULED', startsAt: '2030-11-10T02:00:00Z' }
const pending = { ...base, id: 'm3', workspaceId: 'w2', teamName: 'Nhóm GreenBox', title: 'Chốt phỏng vấn', status: 'PROPOSED', startsAt: null, slotCount: 2, myAnswerPending: true }
const detail = { ...sooner, agenda: null, slots: [], suggestedSlotId: null, cancelReason: null, minutes: null, createdTasks: [] }

function renderAt(path: string, overrides: Record<string, unknown> = {}) {
  get.mockImplementation(async (url: string) => {
    if (url in overrides) {
      const value = overrides[url]
      if (value instanceof Error) throw value
      return { data: value }
    }
    if (url === '/api/workspaces/me') return { data: [w1, w2] }
    if (url === '/api/workspaces/w1/meetings') return { data: { mentorSessionsDone: 0, expectedMentorSessions: 0, items: [later] } }
    if (url === '/api/workspaces/w2/meetings') return { data: { mentorSessionsDone: 0, expectedMentorSessions: 0, items: [sooner, pending] } }
    if (url === '/api/meetings/m2') return { data: detail }
    throw new Error(`GET ${url} chưa giả lập`)
  })
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <AuthProvider>
        <PageHeaderProvider>
          <RouterProvider
            router={createMemoryRouter([{ path: '/gv', element: <><HeaderAction /><Outlet /></>, children: lecturerCalendarRoutes }], { initialEntries: [path] })}
          />
        </PageHeaderProvider>
      </AuthProvider>
    </QueryClientProvider>,
  )
}

describe('C07 – Lịch của giảng viên (FE-36)', () => {
  beforeEach(() => {
    get.mockReset()
    HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
      this.open = true
    }
    HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) {
      this.open = false
    }
    tokenStore.save({
      accessToken: 'a', accessTokenExpiresAt: '', refreshToken: 'r', refreshTokenExpiresAt: '',
      user: { id: 'u', email: 'ha@fpt.edu.vn', fullName: 'Hà', roles: ['Lecturer'], lecturerId: 'l1' },
    })
  })

  it('gathers meetings of every team, upcoming sorted by time', async () => {
    renderAt('/gv/lich')
    const upcoming = await screen.findByRole('region', { name: 'Sắp tới (2)' })
    const rows = within(upcoming).getAllByRole('button')
    expect(rows[0]).toHaveTextContent('Nhóm GreenBox · Góp ý MVP')
    expect(rows[1]).toHaveTextContent('Nhóm EduSpark · Họp tuần 5')
    expect(within(screen.getByRole('region', { name: 'Chờ chốt (1)' })).getByText('Chưa trả lời')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Lịch họp hằng tuần EXE201' })).toHaveAttribute('href', '/gv/lich-hop')
  })

  it('opens C08 in a drawer', async () => {
    renderAt('/gv/lich')
    await userEvent.click(await screen.findByRole('button', { name: /Góp ý MVP/ }))
    expect(await screen.findByRole('dialog', { name: /Góp ý MVP/ })).toBeInTheDocument()
  })

  it('shows empty state when the lecturer has no team', async () => {
    renderAt('/gv/lich', { '/api/workspaces/me': [] })
    expect(await screen.findByText('Chưa có nhóm')).toBeInTheDocument()
  })

  it('shows error state when one team fails to load', async () => {
    renderAt('/gv/lich', { '/api/workspaces/w2/meetings': new Error('boom') })
    expect(await screen.findByRole('button', { name: /Thử lại/ })).toBeInTheDocument()
  })
})
