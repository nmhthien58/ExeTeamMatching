import { render, renderHook, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useOpenQuestions } from '@/features/messages/api'
import { LecturerHomePage, MentorHomePage } from './AdvisorHomePages'
import { AdvisedTeamDetailPage } from './AdvisedTeamsPage'
import { TopicsPage } from './TopicsPage'
import { useAdvisorQuestions } from './api'

const get = vi.fn()
const post = vi.fn()
const put = vi.fn()
const del = vi.fn()
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: { get: (...args: unknown[]) => get(...args), post: (...args: unknown[]) => post(...args), put: (...args: unknown[]) => put(...args), delete: (...args: unknown[]) => del(...args) },
}))
const phaseState = vi.hoisted(() => ({ value: 'TEAM_FORMATION' }))
vi.mock('@/shared/phase/usePhase', () => ({ usePhases: () => ({ data: [{ courseCode: 'EXE201', phase: phaseState.value, mentoringStartsOn: '2027-01-01' }], isLoading: false }) }))
vi.mock('@/features/workspace/api', () => ({ useMyWorkspaces: () => ({ data: [{ id: 'w1', teamId: 't1', courseCode: 'EXE101', myRole: 'INSTRUCTOR' }], isLoading: false }) }))

const team = { id: 't1', name: 'Nhóm Một', projectName: 'Ứng dụng học tập', courseCode: 'EXE101', memberCount: 4, domains: [{ id: 1, name: 'Giáo dục' }], members: [{ id: 's1', fullName: 'Sinh viên A', specialization: { name: 'Kỹ thuật' } }], requirement: { maxSize: 6 } }

function setup(element: React.ReactNode, route = '/gv/de-tai', path = route) {
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    <MemoryRouter initialEntries={[route]}><Routes><Route path={path} element={element} /></Routes></MemoryRouter>
  </QueryClientProvider>)
}

describe('FE-41 advisor web', () => {
  beforeEach(() => {
    phaseState.value = 'TEAM_FORMATION'
    HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', '') }
    HTMLDialogElement.prototype.close = function () { this.removeAttribute('open') }
    get.mockReset()
    post.mockReset().mockResolvedValue({ data: { id: 'i1' } })
    put.mockReset().mockImplementation(async (url: string) => url === '/api/mentors/me'
      ? { data: { id: 'm1', fullName: 'Mentor A', maxTeams: 4, preferredTeams: 3, currentTeamCount: 1 } }
      : { data: { workspaceId: 'w1', doneCount: 1, items: [1, 2, 3, 4].map((checkpointNo) => ({ checkpointNo, presented: checkpointNo === 1, overdue: false, canUndo: checkpointNo === 1 })) } })
    del.mockReset().mockResolvedValue({ data: null })
    get.mockImplementation(async (url: string, config?: { params?: { scope?: string } }) => {
      if (url === '/api/lecturers/me') return { data: { domains: [{ id: 1, name: 'Giáo dục' }] } }
      if (url === '/api/mentors/me') return { data: { id: 'm1', fullName: 'Mentor A', maxTeams: 4, preferredTeams: 2, currentTeamCount: 1, domains: [{ id: 1, name: 'Giáo dục' }] } }
      if (url === '/api/topic-recommendations/me' && config?.params?.scope === 'all') return { data: { openInterestCount: 1, interestLimit: 3, interestOpen: true, items: [
        { team, matchedDomains: [{ id: 1, name: 'Giáo dục' }], otherInterestedCount: 2, myPreferenceRank: 1, myInterest: null, wasMyEXE101Team: false, hasAdvisor: false },
        { team: { ...team, id: 't2', name: 'Nhóm Kế Thừa', projectName: 'Đề tài kế thừa' }, matchedDomains: [], otherInterestedCount: 0, myPreferenceRank: null, myInterest: { id: 'i9', teamId: 't2', status: 'OPEN' }, wasMyEXE101Team: false, hasAdvisor: false },
      ] } }
      if (url === '/api/topic-recommendations/me') return { data: { openInterestCount: 0, interestLimit: 3, interestOpen: true, items: [{ team, matchedDomains: [{ id: 1, name: 'Giáo dục' }], otherInterestedCount: 2, myPreferenceRank: 1, myInterest: null, wasMyEXE101Team: false, hasAdvisor: false }] } }
      if (url === '/api/topic-interests/me') return { data: [] }
      if (url === '/api/lecturer-preferences/me') return { data: [] }
      if (url === '/api/mentor-teams/me') return { data: [] }
      if (url === '/api/advisor/questions') return { data: { totalCount: 0 } }
      if (url === '/api/workspaces/w1/meetings') return { data: { mentorSessionsDone: 0, expectedMentorSessions: 3, items: [] } }
      if (url === '/api/workspaces/w1/tasks') return { data: [] }
      if (url === '/api/lecturer-teams/me') return { data: { maxTeams: 4, instructing: [{ team, reason: 'MUTUAL', preferenceRank: 1 }], mentoring: [] } }
      if (url === '/api/teams/t1') return { data: team }
      if (url === '/api/workspaces/w1/checkpoints') return { data: { workspaceId: 'w1', doneCount: 0, items: [1, 2, 3, 4].map((checkpointNo) => ({ checkpointNo, presented: false, overdue: false, canUndo: false })) } }
      throw new Error(`Unexpected GET ${url}`)
    })
  })

  it('shows lecturer recommendations and sends interest through the existing API', async () => {
    setup(<TopicsPage kind="LECTURER" />)
    expect(await screen.findByText('Ứng dụng học tập')).toBeInTheDocument()
    expect(screen.getByText('Nguyện vọng 1')).toBeInTheDocument()
    await userEvent.setup().click(screen.getByRole('button', { name: 'Quan tâm' }))
    expect(post).toHaveBeenCalledWith('/api/teams/t1/topic-interests', { advisorKind: 'LECTURER' })
  })

  it('lists every team of the course in the "Tất cả nhóm" tab and lets the lecturer withdraw interest there', async () => {
    setup(<TopicsPage kind="LECTURER" />)
    expect(await screen.findByText('Ứng dụng học tập')).toBeInTheDocument()
    expect(screen.queryByText('Đề tài kế thừa')).not.toBeInTheDocument()
    const user = userEvent.setup()
    await user.click(screen.getByRole('tab', { name: 'Tất cả nhóm' }))
    expect(await screen.findByText('Đề tài kế thừa')).toBeInTheDocument()
    expect(get).toHaveBeenCalledWith('/api/topic-recommendations/me', { params: { advisorKind: 'LECTURER', courseCode: 'EXE101', page: 1, pageSize: 100, scope: 'all' } })
    expect(screen.getByText('Chưa có chuyên môn trùng')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Đã quan tâm · Rút lại' }))
    expect(del).toHaveBeenCalled()
  })

  it('lets mentor change preferred team count without calling lecturer APIs', async () => {
    setup(<MentorHomePage />, '/mentor')
    await userEvent.setup().click(await screen.findByRole('button', { name: 'Sửa số nhóm mong muốn' }))
    await userEvent.setup().clear(screen.getByRole('spinbutton', { name: 'Số nhóm mong muốn' }))
    await userEvent.setup().type(screen.getByRole('spinbutton', { name: 'Số nhóm mong muốn' }), '3')
    await userEvent.setup().click(screen.getByRole('button', { name: 'Lưu' }))
    expect(put).toHaveBeenCalledWith('/api/mentors/me', { preferredTeams: 3 })
    expect(get.mock.calls.some(([url]) => url === '/api/lecturers/me' || url === '/api/lecturer-teams/me')).toBe(false)
  })

  it('marks an EXE101 checkpoint for the instructor', async () => {
    setup(<AdvisedTeamDetailPage kind="LECTURER" />, '/gv/nhom/t1', '/gv/nhom/:teamId')
    await userEvent.setup().click(await screen.findByRole('button', { name: 'Đánh dấu Checkpoint' }))
    await userEvent.setup().click(screen.getByRole('button', { name: 'Lưu' }))
    await waitFor(() => expect(put).toHaveBeenCalledWith('/api/workspaces/w1/checkpoints/1', expect.objectContaining({ presentedOn: expect.any(String), comment: null })))
  })

  it('keeps the L01 question count and the C02 question list in separate caches', async () => {
    get.mockImplementation(async () => ({ data: { items: [], totalCount: 3 } }))
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>

    const count = renderHook(() => useAdvisorQuestions(), { wrapper })
    await waitFor(() => expect(count.result.current.data).toBe(3))

    // Từ L01 sang C02: nếu chung key, C02 đọc phải con số trong cache rồi sập ở `.map`.
    const list = renderHook(() => useOpenQuestions(true), { wrapper })
    expect(Array.isArray(list.result.current.data ?? [])).toBe(true)
    await waitFor(() => expect(list.result.current.data).toEqual([]))
  })

  it('counts open questions and overdue deliverables from their own APIs on L01', async () => {
    phaseState.value = 'SEMESTER_RUNNING'
    const previousGet = get.getMockImplementation()!
    get.mockImplementation(async (url: string, options?: unknown) => {
      if (url === '/api/advisor/questions') return { data: { totalCount: 2 } }
      if (url === '/api/workspaces/w1/tasks') return { data: [{ id: 'd1', kind: 'DELIVERABLE', isOverdue: true, submissionStatus: 'NOT_SUBMITTED' }] }
      return previousGet(url, options)
    })
    setup(<LecturerHomePage />, '/gv')
    expect(await screen.findByText('Cần trả lời')).toBeInTheDocument()
    await waitFor(() => expect(screen.getByText('Trễ hạn nộp').previousElementSibling).toHaveTextContent('1'))
    expect(screen.getByText('Cần trả lời').previousElementSibling).toHaveTextContent('2')
  })
})
