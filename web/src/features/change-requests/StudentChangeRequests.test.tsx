import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { StudentChangeRequestCreatePage } from './StudentChangeRequestCreatePage'
import { StudentChangeRequestDetailPage } from './StudentChangeRequestDetailPage'
import { StudentChangeRequestsPage } from './StudentChangeRequestsPage'

const get = vi.fn()
const post = vi.fn()
const put = vi.fn()
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: { get: (...args: unknown[]) => get(...args), post: (...args: unknown[]) => post(...args), put: (...args: unknown[]) => put(...args) },
}))
vi.mock('@/shared/phase/usePhase', () => ({
  usePhases: () => ({ data: [{ courseCode: 'EXE201', changeRequestRoundOpen: true }], isLoading: false, error: null }),
}))

const team = { id: 'team-1', name: 'Nhóm Mầm Xanh', courseCode: 'EXE201', status: 'ACTIVE', domains: [], memberCount: 4, openSlots: 2, members: [], myRelation: 'LEADER' }
const request = { id: 'request-1', team: { id: 'team-1', name: 'Nhóm Mầm Xanh', courseCode: 'EXE201', memberCount: 4 }, roundId: 'round-1', roundName: 'W18', reason: 'Thành viên chuyển lịch học', changedMemberCount: 1, changedPercent: 25, status: 'PENDING', reviewedByName: null, reviewedAt: null, reviewNote: null, createdAt: '2026-10-31T10:00:00Z' }

function renderAt(path: string) {
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}><MemoryRouter initialEntries={[path]}><Routes>
    <Route path="/sv/yeu-cau-doi-nhom" element={<StudentChangeRequestsPage />} />
    <Route path="/sv/yeu-cau-doi-nhom/tao" element={<StudentChangeRequestCreatePage />} />
    <Route path="/sv/yeu-cau-doi-nhom/:id" element={<StudentChangeRequestDetailPage />} />
  </Routes></MemoryRouter></QueryClientProvider>)
}

describe('student change request screens (FE-42)', () => {
  beforeEach(() => {
    get.mockReset()
    post.mockReset()
    put.mockReset()
    get.mockImplementation(async (url: string) => {
      if (url === '/api/teams/me') return { data: team }
      if (url === '/api/teams/team-1/change-requests') return { data: [request] }
      if (url === '/api/teams/team-1/pending-changes') return { data: { baselineMembers: [], pendingItems: [{ id: 'item-1', action: 'ADD', source: 'MEMBER', student: { id: 'student-1', studentCode: 'SE000001', fullName: 'Lê An' }, createdAt: '2026-10-31T09:00:00Z' }], currentProject: null, wouldHaveMemberCount: 5, wouldHaveSpecializationCount: 2 } }
      if (url === '/api/change-requests/request-1') return { data: { ...request, baselineMembers: [], membersAfterChange: [], items: [], projectBefore: null, projectAfter: null } }
      throw new Error(`GET ${url} chưa giả lập`)
    })
  })

  it('lists requests with status and opens request detail', async () => {
    const user = userEvent.setup()
    renderAt('/sv/yeu-cau-doi-nhom')
    expect(await screen.findByText('Nhóm Mầm Xanh')).toBeInTheDocument()
    expect(screen.getByText('Chờ duyệt')).toBeInTheDocument()
    await user.click(screen.getByRole('link', { name: 'Xem chi tiết và kết quả' }))
    expect(await screen.findByText('Mốc ban đầu')).toBeInTheDocument()
    expect(screen.getByText('Thành viên chuyển lịch học')).toBeInTheDocument()
  })

  it('submits only selected pending changes and requires a sufficiently long reason', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue({ data: request })
    renderAt('/sv/yeu-cau-doi-nhom/tao')
    await user.click(await screen.findByRole('checkbox'))
    await user.click(screen.getByRole('button', { name: 'Tiếp tục' }))
    await user.type(screen.getByRole('textbox', { name: 'Lý do thay đổi' }), 'quá ngắn')
    const submitButton = screen.getByRole('button', { name: 'Gửi yêu cầu thay đổi' })
    expect(submitButton).toBeDisabled()
    expect(post).not.toHaveBeenCalled()
  })

  it('requires confirmation before submitting selected changes', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue({ data: request })
    renderAt('/sv/yeu-cau-doi-nhom/tao')
    await user.click(await screen.findByRole('checkbox'))
    await user.click(screen.getByRole('button', { name: 'Tiếp tục' }))
    await user.type(screen.getByRole('textbox', { name: 'Lý do thay đổi' }), 'Cần thêm thành viên cho nhóm')
    await user.click(screen.getByRole('button', { name: 'Gửi yêu cầu thay đổi' }))
    expect(post).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: 'Xác nhận gửi' }))
    expect(post).toHaveBeenCalledWith('/api/teams/team-1/change-requests', { itemIds: ['item-1'], reason: 'Cần thêm thành viên cho nhóm' })
  })

  it('saves a project change as a pending item', async () => {
    const user = userEvent.setup()
    put.mockResolvedValue({ data: { id: 'project-item', action: 'PROJECT_CHANGE', source: 'PROJECT', student: null, newProjectName: 'Đề tài mới' } })
    renderAt('/sv/yeu-cau-doi-nhom/tao')
    await user.type(await screen.findByRole('textbox', { name: 'Tên đề tài mới' }), 'Đề tài mới')
    await user.click(screen.getByRole('button', { name: 'Lưu thay đổi đề tài' }))
    expect(put).toHaveBeenCalledWith('/api/teams/team-1/pending-changes/project', { name: 'Đề tài mới', description: '' })
  })
})
