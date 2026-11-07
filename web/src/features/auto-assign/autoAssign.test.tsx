import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { StudentRef } from '@/features/suggestions/api'

const get = vi.fn()
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: { get: (...args: unknown[]) => get(...args), post: vi.fn() },
}))

const { addMove, applyMoves, checkComposition } = await import('./compose')
const { timeLeft } = await import('@/features/dashboard/adminApi')
const { adminAutoAssignRoutes } = await import('./routes')
const { tokenStore } = await import('@/shared/api/tokenStore')
const { AuthProvider } = await import('@/shared/auth/AuthProvider')
const { PageHeaderProvider } = await import('@/shared/layout/PageHeader')

const student = (id: string, spec: number): StudentRef => ({ id, studentCode: `SE${id}`, fullName: `SV ${id}`, specialization: { id: spec, code: `C${spec}`, name: `Ngành ${spec}` } })

describe('W21 adjustments', () => {
  it('follows the rules set in W05', () => {
    const rules = { minSize: 2, maxSize: 3, minSpecializations: 3 }
    expect(checkComposition([student('1', 1), student('2', 2)], rules).problems).toEqual(['Cần ít nhất 3 chuyên ngành'])
    expect(checkComposition([student('1', 1), student('2', 2), student('3', 3), student('4', 3)], rules).tooMany).toBe(true)
  })

  it('checks 4–6 members and two specializations', () => {
    expect(checkComposition([student('1', 1), student('2', 1), student('3', 2), student('4', 2)]).isValid).toBe(true)
    expect(checkComposition([student('1', 1), student('2', 1), student('3', 1)]).problems).toEqual(['Thiếu 1 người', 'Cần ít nhất 2 chuyên ngành'])
    expect(checkComposition(Array.from({ length: 7 }, (_, i) => student(String(i), i))).tooMany).toBe(true)
  })

  it('applies moves from teams and from the unassigned list', () => {
    const a = { id: 'A', name: 'A', isNew: false, members: [student('1', 1), student('2', 2)] }
    const b = { id: 'B', name: 'B', isNew: true, members: [student('3', 1)] }
    const lone = student('9', 3)

    const draft = applyMoves([a, b], [lone], [
      { studentId: '1', fromTeamId: 'A', toTeamId: 'B' },
      { studentId: '9', fromTeamId: null, toTeamId: 'A' },
    ])

    expect(draft.teams[0].members.map((m) => m.id)).toEqual(['2', '9'])
    expect(draft.teams[1].members.map((m) => m.id)).toEqual(['3', '1'])
    expect(draft.unassigned).toEqual([])
    expect(a.members).toHaveLength(2) // không đổi dữ liệu gốc
  })

  it('merges consecutive moves of one student and drops a move back to the origin', () => {
    let moves = addMove([], { studentId: '1', fromTeamId: 'A', toTeamId: 'B' })
    moves = addMove(moves, { studentId: '1', fromTeamId: 'B', toTeamId: 'C' })
    expect(moves).toEqual([{ studentId: '1', fromTeamId: 'A', toTeamId: 'C' }])
    expect(addMove(moves, { studentId: '1', fromTeamId: 'C', toTeamId: 'A' })).toEqual([])
  })

  it('counts down to the deadline with the banner tone', () => {
    const now = new Date('2026-10-22T00:00:00Z')
    expect(timeLeft('2026-10-24T05:00:00Z', now)).toEqual({ text: 'Còn 2 ngày 5 giờ', tone: 'warning' })
    expect(timeLeft('2026-10-22T05:00:00Z', now)).toEqual({ text: 'Còn 5 giờ', tone: 'error' })
    expect(timeLeft('2026-11-02T00:00:00Z', now).tone).toBe('info')
  })
})

describe('admin screens', () => {
  beforeEach(() => {
    get.mockReset()
    tokenStore.save({
      accessToken: 'a',
      accessTokenExpiresAt: '',
      refreshToken: 'r',
      refreshTokenExpiresAt: '',
      user: { id: 'u', email: 'admin@fpt.edu.vn', fullName: 'Bộ môn', roles: ['Admin'] },
    })
  })

  function renderAt(path: string) {
    return render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <AuthProvider>
          <PageHeaderProvider>
            <RouterProvider router={createMemoryRouter([{ path: '/admin', children: adminAutoAssignRoutes }], { initialEntries: [path] })} />
          </PageHeaderProvider>
        </AuthProvider>
      </QueryClientProvider>,
    )
  }

  it('W02 shows statistics and the deadline countdown', async () => {
    get.mockImplementation(async (url: string) => {
      if (url === '/api/semesters/current') return { data: { id: 'sem', code: 'FA26', name: 'Fall 2026' } }
      if (url === '/api/registration-rounds/active')
        return { data: [{ id: 'r', roundType: 'TEAM_FORMATION', name: 'Lập nhóm EXE101', deadlineAt: new Date(Date.now() + 30.5 * 3600e3).toISOString() }] }
      if (url === '/api/statistics/overview')
        return {
          data: {
            studentsWithTeam: 900,
            studentsWithoutTeam: 100,
            teamsBySize: { '3': 4, '5': 120 },
            pendingChangeRequests: 3,
            teamsNeedingReview: 7,
            bySpecialization: [{ code: 'SE', count: 600 }],
            byDomain: [{ code: 'D04', count: 80 }],
          },
        }
      throw new Error(url)
    })

    renderAt('/admin')

    expect(await screen.findByText('Còn 1 ngày 6 giờ')).toBeInTheDocument()
    expect(await screen.findByText('90% sinh viên')).toBeInTheDocument()
    expect(screen.getByText('Nhóm cần xem lại').closest('a')).toHaveAttribute('href', '/admin/chua-co-nhom/chot?mon=EXE101')
    expect(screen.getByText('3 người')).toBeInTheDocument()
  })

  it('W02 asks to import students when the semester has no data', async () => {
    get.mockImplementation(async (url: string) => {
      if (url === '/api/semesters/current') return { data: { id: 'sem', code: 'FA26' } }
      if (url === '/api/registration-rounds/active') return { data: [] }
      return { data: { studentsWithTeam: 0, studentsWithoutTeam: 0, teamsBySize: {}, pendingChangeRequests: 0, teamsNeedingReview: 0, bySpecialization: [], byDomain: [] } }
    })

    renderAt('/admin?mon=EXE201')

    expect(await screen.findByText('Chưa có dữ liệu')).toBeInTheDocument()
    expect(screen.getByText(/Đợt lập nhóm EXE201 đã đóng/)).toBeInTheDocument()
  })

  it('W22 tells Admin when every team is finalized', async () => {
    get.mockImplementation(async (url: string) => {
      if (url === '/api/semesters/current') return { data: { id: 'sem', code: 'FA26' } }
      return { data: { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 } }
    })

    renderAt('/admin/chua-co-nhom/chot')

    expect(await screen.findByText('Đã chốt hết nhóm')).toBeInTheDocument()
    expect(get).toHaveBeenCalledWith('/api/teams', expect.objectContaining({ params: expect.objectContaining({ status: 'NEEDS_REVIEW', courseCode: 'EXE101' }) }))
  })
})
