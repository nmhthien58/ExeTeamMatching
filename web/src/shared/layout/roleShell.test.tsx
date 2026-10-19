import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { roleNav } from './navigation'

const get = vi.fn()
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: { get: (...args: unknown[]) => get(...args), post: vi.fn() },
}))

const { tokenStore } = await import('@/shared/api/tokenStore')
const { AuthProvider } = await import('@/shared/auth/AuthProvider')
const { TeamFormationOnly } = await import('@/shared/phase/PhaseGuard')

describe('roleNav', () => {
  it('swaps Discover for Project when the student course is no longer in team formation', () => {
    const open = roleNav('student', { isTeamFormation: true, isMentoringWindow: false }).map((i) => i.label)
    const closed = roleNav('student', { isTeamFormation: false, isMentoringWindow: false }).map((i) => i.label)
    expect(open).toContain('Khám phá')
    expect(closed).not.toContain('Khám phá')
    expect(closed).toContain('Dự án')
  })

  it('gives lecturers the Topics tab only during team formation and mentors the Teams tab from week 6', () => {
    expect(roleNav('lecturer', { isTeamFormation: true, isMentoringWindow: false }).map((i) => i.screen)).toContain('L07')
    expect(roleNav('lecturer', { isTeamFormation: false, isMentoringWindow: false }).map((i) => i.screen)).toContain('C07')
    expect(roleNav('mentor', { isTeamFormation: false, isMentoringWindow: false }).map((i) => i.screen)).toContain('L07')
    expect(roleNav('mentor', { isTeamFormation: false, isMentoringWindow: true }).map((i) => i.screen)).toContain('L04')
  })
})

describe('TeamFormationOnly', () => {
  beforeEach(() => {
    HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
      this.open = true
    }
    tokenStore.save({
      accessToken: 'a',
      accessTokenExpiresAt: '',
      refreshToken: 'r',
      refreshTokenExpiresAt: '',
      user: { id: 'u', email: 'vy@fpt.edu.vn', fullName: 'Vy', roles: ['Student'], studentId: 's1' },
    })
  })

  function renderDiscover() {
    return render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <AuthProvider>
          <MemoryRouter initialEntries={['/sv/kham-pha']}>
            <Routes>
              <Route path="/sv/kham-pha" element={<TeamFormationOnly>Danh sách nhóm</TeamFormationOnly>} />
              <Route path="/sv/du-an" element={<p>Trang dự án</p>} />
            </Routes>
          </MemoryRouter>
        </AuthProvider>
      </QueryClientProvider>,
    )
  }

  it('shows Discover while the round is open', async () => {
    get.mockImplementation(async (url: string) =>
      url.includes('phase') ? { data: [{ courseCode: 'EXE101', phase: 'TEAM_FORMATION' }] } : { data: { courseCode: 'EXE101' } },
    )
    renderDiscover()
    expect(await screen.findByText('Danh sách nhóm')).toBeInTheDocument()
  })

  it('shows the reminder dialog after the round closed and opens the project', async () => {
    get.mockImplementation(async (url: string) =>
      url.includes('phase')
        ? { data: [{ courseCode: 'EXE101', phase: 'SEMESTER_RUNNING', teamFormationDeadlineAt: '2026-09-17T11:00:00Z' }] }
        : { data: { courseCode: 'EXE101' } },
    )
    renderDiscover()
    expect(await screen.findByText('Đợt lập nhóm đã đóng')).toBeInTheDocument()
    expect(screen.queryByText('Danh sách nhóm')).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Mở dự án' }))
    expect(await screen.findByText('Trang dự án')).toBeInTheDocument()
  })
})
