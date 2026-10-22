import { render, screen } from '@testing-library/react'
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
const { fitLabel } = await import('./api')
const { suggestionRoutes } = await import('./routes')
const { tokenStore } = await import('@/shared/api/tokenStore')
const { AuthProvider } = await import('@/shared/auth/AuthProvider')
const { PageHeaderProvider } = await import('@/shared/layout/PageHeader')

const team = {
  id: 't1',
  name: 'EduSpark',
  courseCode: 'EXE101',
  status: 'ACTIVE',
  domains: [],
  memberCount: 3,
  openSlots: 3,
  members: [],
  myRelation: 'LEADER',
  requirement: { memberCount: 3, minSize: 4, maxSize: 6, specializationCount: 1, minSpecializations: 2, isValid: false, missingSpecializations: [] },
}
const student = (id: string, name: string) => ({ id, studentCode: `SE18${id}`, fullName: name, specialization: { id: 5, code: 'GD', name: 'Thiết kế đồ họa' } })

function renderAt(path: string) {
  return render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <AuthProvider>
        <PageHeaderProvider>
          <RouterProvider router={createMemoryRouter([{ path: '/sv', children: suggestionRoutes }], { initialEntries: [path] })} />
        </PageHeaderProvider>
      </AuthProvider>
    </QueryClientProvider>,
  )
}

describe('suggestions', () => {
  beforeEach(() => {
    get.mockReset()
    post.mockReset()
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

  it('maps the matching score to a label instead of a percentage', () => {
    expect(fitLabel(0.72).text).toBe('Rất phù hợp')
    expect(fitLabel(0.4).text).toBe('Phù hợp')
    expect(fitLabel(0.1).text).toBe('Có thể phù hợp')
  })

  it('S18 lets the leader invite a suggestion and marks already invited students', async () => {
    get.mockImplementation(async (url: string) => {
      if (url === '/api/teams/me') return { data: team }
      if (url === '/api/teams/t1/suggested-members')
        return {
          data: {
            missingSpecializations: [{ id: 5, code: 'GD', name: 'Thiết kế đồ họa' }],
            items: [
              { student: student('01', 'Ngô Bảo Anh'), skills: ['UI/UX 5/5'], score: 0.7, reasons: ['Bù chuyên ngành nhóm đang thiếu'], alreadyInvited: false },
              { student: student('02', 'Phạm Gia Huy'), skills: [], score: 0.2, reasons: [], alreadyInvited: true },
            ],
          },
        }
      throw new Error(url)
    })
    post.mockResolvedValue({ data: {} })

    renderAt('/sv/nhom/goi-y')

    expect(await screen.findByText('Ngô Bảo Anh')).toBeInTheDocument()
    expect(screen.getByText('Bù chuyên ngành nhóm đang thiếu')).toBeInTheDocument()
    expect(screen.getByText('Đã mời')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Gửi lời mời' }))
    await userEvent.type(screen.getByLabelText(/Lời nhắn/), 'Nhóm cần bạn làm UI')
    await userEvent.click(screen.getAllByRole('button', { name: 'Gửi lời mời' }).at(-1)!)

    expect(post).toHaveBeenCalledWith('/api/teams/t1/invitations', { studentId: '01', message: 'Nhóm cần bạn làm UI' })
    expect(await screen.findByText('Đã gửi lời mời')).toBeInTheDocument()
  })

  it('S18 hides invitations for members who are not the leader', async () => {
    get.mockImplementation(async (url: string) =>
      url === '/api/teams/me'
        ? { data: { ...team, myRelation: 'MEMBER' } }
        : { data: { missingSpecializations: [], items: [{ student: student('01', 'Ngô Bảo Anh'), skills: [], score: 0.5, reasons: [], alreadyInvited: false }] } },
    )

    renderAt('/sv/nhom/goi-y')

    expect(await screen.findByText('Chỉ trưởng nhóm gửi được lời mời.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Gửi lời mời' })).not.toBeInTheDocument()
  })

  it('S30 tells a student without a team they will be auto-assigned', async () => {
    get.mockImplementation(async (url: string) => {
      if (url === '/api/teams/me') throw new ApiError({ status: 404, code: 'not-found', title: 'Không tìm thấy' })
      if (url.includes('phase')) return { data: [{ courseCode: 'EXE101', phase: 'SEMESTER_RUNNING', teamFormationDeadlineAt: '2026-09-17T11:00:00Z' }] }
      return { data: { courseCode: 'EXE101' } }
    })

    renderAt('/sv/nhom/da-khoa')

    expect(await screen.findByText('Bạn sẽ được ghép nhóm tự động')).toBeInTheDocument()
  })
})
