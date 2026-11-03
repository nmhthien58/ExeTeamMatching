import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { tokenStore } from '@/shared/api/tokenStore'
import { AuthProvider } from '@/shared/auth/AuthProvider'
import { PageHeaderProvider, usePageHeaderState } from '@/shared/layout/PageHeader'
import { Dialog, StatusChip, type StatusTone } from '@/shared/components/ui'
import { RoleShell } from '@/shared/layout/RoleShell'
import { ChangeRequestDetailPage } from '@/features/change-requests/ChangeRequestDetailPage'
import { ChangeRequestsPage } from '@/features/change-requests/ChangeRequestsPage'
import { LoadPanel } from '@/features/lecturer-matching/LoadPanel'
import { EarlyMatchingDialog } from '@/features/lecturer-matching/EarlyMatchingDialog'
import type { AdvisorMatchingRun } from '@/features/lecturer-matching/api'
import { AdvisedTeamDetailPage } from '@/features/advisor/AdvisedTeamsPage'

// Mock api client
const get = vi.fn()
const post = vi.fn()
const put = vi.fn()
const del = vi.fn()

vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: {
    get: (...args: unknown[]) => get(...args),
    post: (...args: unknown[]) => post(...args),
    put: (...args: unknown[]) => put(...args),
    delete: (...args: unknown[]) => del(...args),
  },
}))

// Mock phases and workspaces for role shells and advisor views
vi.mock('@/shared/phase/usePhase', () => ({
  usePhases: () => ({
    data: [
      { courseCode: 'EXE101', phase: 'TEAM_FORMATION', mentoringStartsOn: '2026-10-20' },
      { courseCode: 'EXE201', phase: 'SEMESTER_RUNNING', mentoringStartsOn: '2026-10-20' },
    ],
    isLoading: false,
  }),
  useIsTeamFormation: (courseCode?: string) => ({
    isTeamFormation: courseCode === 'EXE101',
    isLoading: false,
  }),
  useMyCourse: () => ({
    data: 'EXE101',
    isLoading: false,
  }),
}))

function HeaderBar() {
  const { title, action } = usePageHeaderState()
  return (
    <header data-testid="page-header">
      <h1>{title}</h1>
      {action}
    </header>
  )
}

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0 },
      mutations: { retry: false },
    },
  })
}

function renderWithProviders(element: React.ReactNode, path = '/') {
  const router = createMemoryRouter(
    [
      {
        path,
        element: (
          <>
            <HeaderBar />
            {element}
          </>
        ),
      },
    ],
    { initialEntries: [path] },
  )
  return render(
    <QueryClientProvider client={createTestQueryClient()}>
      <AuthProvider>
        <PageHeaderProvider>
          <RouterProvider router={router} />
        </PageHeaderProvider>
      </AuthProvider>
    </QueryClientProvider>,
  )
}

describe('FE-31: Kiểm thử chất lượng và đóng gói web', () => {
  beforeEach(() => {
    get.mockReset()
    post.mockReset()
    put.mockReset()
    del.mockReset()

    HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
      this.open = true
    }
    HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) {
      this.open = false
    }

    tokenStore.save({
      accessToken: 'token-xyz',
      accessTokenExpiresAt: '',
      refreshToken: 'refresh-xyz',
      refreshTokenExpiresAt: '',
      user: {
        id: 'u-1',
        email: 'user@fpt.edu.vn',
        fullName: 'Người dùng FPT',
        roles: ['Admin', 'Student', 'Lecturer', 'Mentor'],
        studentId: 's-1',
      },
    })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  // =========================================================================
  // 1. Luồng duyệt/từ chối yêu cầu đổi nhóm (W17, W18)
  // =========================================================================
  describe('1. Yêu cầu đổi nhóm (W17, W18)', () => {
    const mockRequestDetail = {
      id: 'cr-101',
      team: { id: 'team-1', name: 'Nhóm Tiên Phong', courseCode: 'EXE201', memberCount: 5 },
      roundId: 'round-1',
      roundName: 'Đợt 1',
      reason: 'Cần thay đổi thành viên do lịch học.',
      changedMemberCount: 1,
      changedPercent: 20,
      status: 'PENDING',
      reviewedByName: null,
      reviewedAt: null,
      reviewNote: null,
      createdAt: '2026-10-25T10:00:00Z',
      baselineMembers: [
        { id: 'm1', studentCode: 'SE180001', fullName: 'Nguyễn Văn A' },
        { id: 'm2', studentCode: 'SE180002', fullName: 'Trần Thị B' },
      ],
      membersAfterChange: [
        { id: 'm1', studentCode: 'SE180001', fullName: 'Nguyễn Văn A' },
        { id: 'm3', studentCode: 'SE180003', fullName: 'Lê Văn C' },
      ],
      items: [],
      projectBefore: { name: 'Dự án Cũ', description: 'Mô tả dự án cũ' },
      projectAfter: { name: 'Dự án Mới', description: 'Mô tả dự án mới' },
    }

    it('hiển thị chi tiết so sánh thành viên mốc EXE101 và sau thay đổi', async () => {
      get.mockResolvedValueOnce({ data: mockRequestDetail })
      const router = createMemoryRouter(
        [
          {
            path: '/admin/yeu-cau-doi-nhom/:id',
            element: (
              <>
                <HeaderBar />
                <ChangeRequestDetailPage />
              </>
            ),
          },
        ],
        { initialEntries: ['/admin/yeu-cau-doi-nhom/cr-101'] },
      )
      render(
        <QueryClientProvider client={createTestQueryClient()}>
          <PageHeaderProvider>
            <RouterProvider router={router} />
          </PageHeaderProvider>
        </QueryClientProvider>,
      )

      expect(await screen.findByText('Yêu cầu đổi nhóm · Nhóm Tiên Phong')).toBeInTheDocument()
      expect(screen.getByText('Mốc EXE101')).toBeInTheDocument()
      expect(screen.getByText('Thành viên sau thay đổi')).toBeInTheDocument()
      expect(screen.getByText('Trần Thị B')).toBeInTheDocument()
      expect(screen.getByText('Lê Văn C')).toBeInTheDocument()
      expect(screen.getByText('Dự án Cũ')).toBeInTheDocument()
      expect(screen.getByText('Dự án Mới')).toBeInTheDocument()
    })

    it('khi từ chối thất bại do lỗi API, hiển thị thông báo lỗi và GIỮ NGUYÊN nội dung ghi chú đã nhập', async () => {
      get.mockResolvedValueOnce({ data: mockRequestDetail })
      post.mockRejectedValueOnce({
        response: { data: { title: 'Lỗi máy chủ khi từ chối', detail: 'Hệ thống đang bảo trì, vui lòng thử lại sau.' } },
      })

      const router = createMemoryRouter(
        [
          {
            path: '/admin/yeu-cau-doi-nhom/:id',
            element: (
              <>
                <HeaderBar />
                <ChangeRequestDetailPage />
              </>
            ),
          },
        ],
        { initialEntries: ['/admin/yeu-cau-doi-nhom/cr-101'] },
      )
      render(
        <QueryClientProvider client={createTestQueryClient()}>
          <PageHeaderProvider>
            <RouterProvider router={router} />
          </PageHeaderProvider>
        </QueryClientProvider>,
      )

      await screen.findByText('Yêu cầu đổi nhóm · Nhóm Tiên Phong')
      const rejectBtn = screen.getByRole('button', { name: 'Từ chối' })
      await userEvent.click(rejectBtn)

      const noteInput = screen.getByLabelText('Ghi chú từ chối') as HTMLTextAreaElement
      const inputReason = 'Lý do: Không đủ điều kiện thay đổi đề tài và thành viên quá 20%.'
      await userEvent.type(noteInput, inputReason)

      const dialogSubmitBtn = screen.getAllByRole('button', { name: 'Từ chối' }).at(-1)!
      await userEvent.click(dialogSubmitBtn)

      // Alert lỗi xuất hiện
      expect(await screen.findByRole('alert')).toBeInTheDocument()
      // Dữ liệu nhập vào textarea KHÔNG bị xóa
      expect(noteInput.value).toBe(inputReason)
    })

    it('W17: hiển thị trạng thái đang tải, rỗng và xử lý lỗi danh sách có nút Thử lại', async () => {
      let failed = true
      get.mockImplementation(async (url: string) => {
        if (url === '/api/semesters/current') return { data: { id: 'sem-1' } }
        if (url === '/api/registration-rounds') return { data: [{ id: 'r1', name: 'Đợt 1', roundType: 'CHANGE_REQUEST' }] }
        if (url === '/api/change-requests') {
          if (failed) throw new Error('Không thể kết nối đến máy chủ')
          return { data: { page: 1, pageSize: 20, totalCount: 0, totalPages: 0, items: [] } }
        }
        throw new Error(`Unexpected url ${url}`)
      })

      const router = createMemoryRouter(
        [{ path: '/admin/yeu-cau-doi-nhom', element: <ChangeRequestsPage /> }],
        { initialEntries: ['/admin/yeu-cau-doi-nhom'] },
      )
      render(
        <QueryClientProvider client={createTestQueryClient()}>
          <PageHeaderProvider>
            <RouterProvider router={router} />
          </PageHeaderProvider>
        </QueryClientProvider>,
      )

      // Kiểm tra ErrorState
      expect(await screen.findByText('Không tải được dữ liệu')).toBeInTheDocument()
      const retryBtn = screen.getByRole('button', { name: 'Thử lại' })

      // Khi nhấn Thử lại
      failed = false
      await userEvent.click(retryBtn)

      // Chuyển sang EmptyState
      expect(await screen.findByText('Chưa có yêu cầu chờ duyệt')).toBeInTheDocument()
    })
  })

  // =========================================================================
  // 2. Điều tiết giảng viên & Ghép cố vấn (W19, W28)
  // =========================================================================
  describe('2. Điều tiết giảng viên & Ghép cố vấn (W19, W28)', () => {
    const mockLoads = [
      { advisorId: 'l1', fullName: 'Thầy Trần Văn Minh', assignedCount: 6, maxTeams: 5 }, // Quá tải
      { advisorId: 'l2', fullName: 'Cô Nguyễn Thị Hoa', assignedCount: 4, maxTeams: 5 }, // Gần đầy
      { advisorId: 'l3', fullName: 'Thầy Lê Hoàng Nam', assignedCount: 1, maxTeams: 5 }, // Rảnh
    ]

    it('LoadPanel điều tiết: hiển thị tỷ lệ số nhóm, meter a11y và cảnh báo khi quá tải', () => {
      render(<LoadPanel loads={mockLoads} isLoading={false} title="Tải của giảng viên" />)

      expect(screen.getByText('Tải của giảng viên')).toBeInTheDocument()
      expect(screen.getByText('Thầy Trần Văn Minh')).toBeInTheDocument()
      expect(screen.getByText('6/5 nhóm')).toBeInTheDocument()

      const meters = screen.getAllByRole('meter')
      expect(meters).toHaveLength(3)
      expect(meters[0]).toHaveAttribute('aria-valuenow', '6')
      expect(meters[0]).toHaveAttribute('aria-valuemax', '5')
    })

    it('EarlyMatchingDialog: xem trước số nhóm ghép, xác nhận chốt và hủy kết quả', async () => {
      const onConfirm = vi.fn()
      const onDiscard = vi.fn()
      const onClose = vi.fn()

      const mockRun: AdvisorMatchingRun = {
        id: 'run-99',
        advisorKind: 'LECTURER',
        courseCode: 'EXE101',
        trigger: 'EARLY',
        status: 'PREVIEW',
        createdAt: '2026-10-25T10:00:00Z',
        appliedAt: null,
        matchedCount: 8,
        unmatchedCount: 2,
        plan: [
          { teamId: 't1', teamName: 'Nhóm Alpha', advisorId: 'l1', advisorName: 'Thầy Minh', reason: 'MUTUAL', preferenceRank: 1 },
          { teamId: 't2', teamName: 'Nhóm Beta', advisorId: 'l2', advisorName: 'Cô Hoa', reason: 'LECTURER_INTEREST', preferenceRank: null },
        ],
      }

      const { rerender } = render(
        <EarlyMatchingDialog
          run={mockRun}
          confirming={false}
          discarding={false}
          onConfirm={onConfirm}
          onDiscard={onDiscard}
          onClose={onClose}
        />,
      )

      expect(screen.getByText('Chốt kết quả ghép sớm?')).toBeInTheDocument()
      expect(screen.getByText('8 nhóm')).toBeInTheDocument()
      expect(screen.getByText('2 nhóm')).toBeInTheDocument()
      expect(screen.getByText('Nhóm Alpha')).toBeInTheDocument()
      expect(screen.getByText('Nhóm Beta')).toBeInTheDocument()

      // Bấm Chốt kết quả
      await userEvent.click(screen.getByRole('button', { name: 'Chốt kết quả' }))
      expect(onConfirm).toHaveBeenCalledTimes(1)

      // Bấm Bỏ kết quả
      await userEvent.click(screen.getByRole('button', { name: 'Bỏ kết quả' }))
      expect(onDiscard).toHaveBeenCalledTimes(1)

      // Rerender kèm lỗi
      rerender(
        <EarlyMatchingDialog
          run={mockRun}
          confirming={false}
          discarding={false}
          error={{ title: 'Lỗi khi chốt ghép', detail: 'Một nhóm đã được gán thủ công.' }}
          onConfirm={onConfirm}
          onDiscard={onDiscard}
          onClose={onClose}
        />,
      )
      expect(screen.getByText('Lỗi khi chốt ghép')).toBeInTheDocument()
      expect(screen.getByText('Một nhóm đã được gán thủ công.')).toBeInTheDocument()
    })
  })

  // =========================================================================
  // 3. C09 Lịch họp hằng tuần & Checkpoint đánh dấu/Hoàn tác (EXE101/201)
  // =========================================================================
  describe('3. Checkpoint đánh dấu & Hoàn tác (EXE101)', () => {
    const mockTeamDetail = {
      id: 'team-exe101',
      name: 'Nhóm Sáng Tạo',
      projectName: 'Nền tảng Quản lý Học tập',
      courseCode: 'EXE101',
      memberCount: 5,
      ideaDescription: 'Ý tưởng xây dựng app kết nối sinh viên.',
      members: [
        { id: 'mem-1', fullName: 'Phạm Minh Anh', specialization: { name: 'Kỹ thuật phần mềm' } },
      ],
    }

    const mockCheckpoints = {
      workspaceId: 'ws-101',
      teamName: 'Nhóm Sáng Tạo',
      doneCount: 0,
      items: [
        { checkpointNo: 1, presented: false, overdue: false, canUndo: false, comment: null },
        { checkpointNo: 2, presented: false, overdue: false, canUndo: false, comment: null },
        { checkpointNo: 3, presented: false, overdue: false, canUndo: false, comment: null },
        { checkpointNo: 4, presented: false, overdue: false, canUndo: false, comment: null },
      ],
    }

    it('đánh dấu Checkpoint, hiển thị banner thành công kèm nút Hoàn tác, bấm Hoàn tác gọi API DELETE', async () => {
      vi.mock('@/features/workspace/api', () => ({
        useMyWorkspaces: () => ({
          data: [{ id: 'ws-101', teamId: 'team-exe101', courseCode: 'EXE101', myRole: 'INSTRUCTOR' }],
          isLoading: false,
        }),
      }))

      get.mockImplementation(async (url: string) => {
        if (url === '/api/teams/team-exe101') return { data: mockTeamDetail }
        if (url === '/api/lecturer-teams/me') return { data: { instructing: [{ team: mockTeamDetail, reason: 'MUTUAL', preferenceRank: 1 }], mentoring: [] } }
        if (url === '/api/workspaces/ws-101/checkpoints') return { data: mockCheckpoints }
        if (url === '/api/workspaces/ws-101/tasks') return { data: [] }
        if (url === '/api/workspaces/ws-101/meetings') return { data: { items: [] } }
        throw new Error(`Unexpected GET ${url}`)
      })

      put.mockImplementation(async (url: string) => {
        if (url === '/api/workspaces/ws-101/checkpoints/1') {
          return {
            data: {
              ...mockCheckpoints,
              doneCount: 1,
              items: [
                { checkpointNo: 1, presented: true, overdue: false, canUndo: true, comment: 'Đạt yêu cầu vòng 1' },
                ...mockCheckpoints.items.slice(1),
              ],
            },
          }
        }
        throw new Error(`Unexpected PUT ${url}`)
      })

      del.mockImplementation(async (url: string) => {
        if (url === '/api/workspaces/ws-101/checkpoints/1') {
          return { data: null }
        }
        throw new Error(`Unexpected DELETE ${url}`)
      })

      const router = createMemoryRouter(
        [{ path: '/gv/nhom/:teamId', element: <AdvisedTeamDetailPage kind="LECTURER" /> }],
        { initialEntries: ['/gv/nhom/team-exe101'] },
      )
      render(
        <QueryClientProvider client={createTestQueryClient()}>
          <PageHeaderProvider>
            <RouterProvider router={router} />
          </PageHeaderProvider>
        </QueryClientProvider>,
      )

      expect(await screen.findByText('Nhóm Sáng Tạo')).toBeInTheDocument()
      expect(screen.getByText('Checkpoint 0/4')).toBeInTheDocument()

      // Bấm nút Đánh dấu Checkpoint
      const markBtn = screen.getByRole('button', { name: 'Đánh dấu Checkpoint' })
      await userEvent.click(markBtn)

      // Nhập nhận xét trong Dialog
      const commentInput = screen.getByLabelText(/Nhận xét/)
      await userEvent.type(commentInput, 'Đạt yêu cầu vòng 1')

      // Bấm Lưu
      const saveBtn = screen.getByRole('button', { name: 'Lưu' })
      await userEvent.click(saveBtn)

      // Kiểm tra API PUT được gọi với đúng tham số
      await waitFor(() => {
        expect(put).toHaveBeenCalledWith(
          '/api/workspaces/ws-101/checkpoints/1',
          expect.objectContaining({ presentedOn: expect.any(String), comment: 'Đạt yêu cầu vòng 1' }),
        )
      })

      // Xuất hiện Banner thành công với nút Hoàn tác
      expect(await screen.findByText('Đã ghi nhận Checkpoint 1')).toBeInTheDocument()
      const undoBtn = screen.getByRole('button', { name: 'Hoàn tác' })
      expect(undoBtn).toBeInTheDocument()

      // Bấm Hoàn tác
      await userEvent.click(undoBtn)
      await waitFor(() => {
        expect(del).toHaveBeenCalledWith('/api/workspaces/ws-101/checkpoints/1')
      })
    })

    it('khi đánh dấu thất bại do lỗi API, hiển thị thông báo lỗi và giữ nguyên nhận xét', async () => {
      get.mockImplementation(async (url: string) => {
        if (url === '/api/teams/team-exe101') return { data: mockTeamDetail }
        if (url === '/api/lecturer-teams/me') return { data: { instructing: [{ team: mockTeamDetail, reason: 'MUTUAL', preferenceRank: 1 }], mentoring: [] } }
        if (url === '/api/workspaces/ws-101/checkpoints') return { data: mockCheckpoints }
        if (url === '/api/workspaces/ws-101/tasks') return { data: [] }
        if (url === '/api/workspaces/ws-101/meetings') return { data: { items: [] } }
        throw new Error(`Unexpected GET ${url}`)
      })

      put.mockRejectedValueOnce({
        response: { data: { title: 'Không thể ghi nhận checkpoint', detail: 'Hạn chót CP1 đã qua.' } },
      })

      const router = createMemoryRouter(
        [{ path: '/gv/nhom/:teamId', element: <AdvisedTeamDetailPage kind="LECTURER" /> }],
        { initialEntries: ['/gv/nhom/team-exe101'] },
      )
      render(
        <QueryClientProvider client={createTestQueryClient()}>
          <PageHeaderProvider>
            <RouterProvider router={router} />
          </PageHeaderProvider>
        </QueryClientProvider>,
      )

      await screen.findByText('Nhóm Sáng Tạo')
      await userEvent.click(screen.getByRole('button', { name: 'Đánh dấu Checkpoint' }))

      const commentInput = screen.getByLabelText(/Nhận xét/) as HTMLTextAreaElement
      await userEvent.type(commentInput, 'Nhóm trình bày sơ sài')

      await userEvent.click(screen.getByRole('button', { name: 'Lưu' }))

      // Có ít nhất một alert báo lỗi xuất hiện
      await waitFor(() => {
        const alerts = screen.getAllByRole('alert')
        expect(alerts.length).toBeGreaterThanOrEqual(1)
      })

      // Dữ liệu nhận xét được giữ nguyên
      expect(commentInput.value).toBe('Nhóm trình bày sơ sài')
    })
  })

  // =========================================================================
  // 4. Khung Sinh viên, Giảng viên, Mentor ở 3 mức bề rộng (<600, 600-1023, >=1024)
  // =========================================================================
  describe('4. Khung Responsive 3 bề rộng (Sinh viên, Giảng viên, Mentor)', () => {
    function setViewportWidth(width: number) {
      window.innerWidth = width
      fireEvent(window, new Event('resize'))
    }

    it('< 600 px: hiển thị tab bar dưới (fixed bottom) cho Sinh viên, Giảng viên, Mentor', async () => {
      setViewportWidth(450)

      // Sinh viên
      const { unmount: unmountStudent } = renderWithProviders(<RoleShell shell="student" />, '/sv')
      const navStudent = screen.getByRole('navigation', { name: 'Điều hướng chính' })
      expect(navStudent).toHaveClass('fixed')
      expect(navStudent).toHaveClass('bottom-0')
      unmountStudent()

      // Giảng viên
      const { unmount: unmountLecturer } = renderWithProviders(<RoleShell shell="lecturer" />, '/gv')
      const navLecturer = screen.getByRole('navigation', { name: 'Điều hướng chính' })
      expect(navLecturer).toHaveClass('fixed')
      expect(navLecturer).toHaveClass('bottom-0')
      unmountLecturer()

      // Mentor
      const { unmount: unmountMentor } = renderWithProviders(<RoleShell shell="mentor" />, '/mentor')
      const navMentor = screen.getByRole('navigation', { name: 'Điều hướng chính' })
      expect(navMentor).toHaveClass('fixed')
      expect(navMentor).toHaveClass('bottom-0')
      unmountMentor()
    })

    it('600–1023 px: hiển thị Navigation Rail (compact side nav 80px) và vùng nội dung max-w-[720px]', async () => {
      setViewportWidth(800)

      const { unmount } = renderWithProviders(<RoleShell shell="student" />, '/sv')
      const aside = screen.getByRole('navigation', { name: 'Điều hướng chính' }).closest('aside')
      expect(aside).toHaveClass('w-20') // 80px
      const main = document.getElementById('main')
      expect(main).toHaveClass('max-w-[720px]')
      unmount()
    })

    it('≥ 1024 px: hiển thị Sidebar 240 px đầy đủ và vùng nội dung max-w-[1200px]', async () => {
      setViewportWidth(1280)

      const { unmount } = renderWithProviders(<RoleShell shell="lecturer" />, '/gv')
      const aside = screen.getByRole('navigation', { name: 'Điều hướng chính' }).closest('aside')
      expect(aside).toHaveClass('w-60') // 240px
      const main = document.getElementById('main')
      expect(main).toHaveClass('max-w-[1200px]')
      unmount()
    })
  })

  // =========================================================================
  // 5. Accessibility & Design Tokens (Focus, accessible name, contrast, text+icon)
  // =========================================================================
  describe('5. Accessibility & Design Tokens', () => {
    it('Focus: khi đóng Dialog, focus được trả về đúng phần tử đã kích hoạt mở', async () => {
      function DialogTestContainer() {
        const [open, setOpen] = useState(false)
        return (
          <div>
            <button id="trigger-btn" type="button" onClick={() => setOpen(true)}>
              Mở hộp thoại
            </button>
            <Dialog open={open} onClose={() => setOpen(false)} title="Hộp thoại kiểm tra" actions={<button onClick={() => setOpen(false)}>Đóng</button>}>
              Nội dung hộp thoại
            </Dialog>
          </div>
        )
      }

      const { useState } = await import('react')
      render(<DialogTestContainer />)

      const triggerBtn = screen.getByRole('button', { name: 'Mở hộp thoại' })
      triggerBtn.focus()
      expect(document.activeElement).toBe(triggerBtn)

      // Mở dialog
      await userEvent.click(triggerBtn)
      expect(screen.getByText('Hộp thoại kiểm tra')).toBeInTheDocument()

      // Đóng dialog
      const closeBtn = screen.getByRole('button', { name: 'Đóng' })
      await userEvent.click(closeBtn)

      // Kiểm tra focus đã được trả về trigger button
      await waitFor(() => {
        expect(document.activeElement).toBe(triggerBtn)
      })
    })

    it('Trạng thái luôn có chữ và icon: mọi StatusTone của StatusChip đều có icon và label', () => {
      const tones: { tone: StatusTone; label: string; expectedIcon: string }[] = [
        { tone: 'warning', label: 'Chờ duyệt', expectedIcon: 'schedule' },
        { tone: 'success', label: 'Đã duyệt', expectedIcon: 'check_circle' },
        { tone: 'error', label: 'Đã từ chối', expectedIcon: 'cancel' },
        { tone: 'info', label: 'Đã ghép', expectedIcon: 'verified' },
        { tone: 'neutral', label: 'Giữ nguyên', expectedIcon: 'remove_circle' },
      ]

      render(
        <div>
          {tones.map(({ tone, label }) => (
            <StatusChip key={tone} tone={tone}>
              {label}
            </StatusChip>
          ))}
        </div>,
      )

      tones.forEach(({ label, expectedIcon }) => {
        const chip = screen.getByText(label).closest('span')
        expect(chip).toBeInTheDocument()
        // Phải có icon tương ứng
        expect(within(chip!).getByText(expectedIcon)).toBeInTheDocument()
      })
    })

    it('Kiểm tra độ tương phản Design Token theo WCAG 2.2 AA (≥ 4.5:1)', () => {
      // Công thức tính relative luminance theo WCAG 2.1 / 2.2
      function getLuminance(hex: string): number {
        const cleanHex = hex.replace('#', '')
        const r = parseInt(cleanHex.substring(0, 2), 16) / 255
        const g = parseInt(cleanHex.substring(2, 4), 16) / 255
        const b = parseInt(cleanHex.substring(4, 6), 16) / 255

        const toLinear = (c: number) => (c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4))
        return 0.2126 * toLinear(r) + 0.7152 * toLinear(g) + 0.0722 * toLinear(b)
      }

      function getContrastRatio(hex1: string, hex2: string): number {
        const lum1 = getLuminance(hex1)
        const lum2 = getLuminance(hex2)
        const brightest = Math.max(lum1, lum2)
        const darkest = Math.min(lum1, lum2)
        return (brightest + 0.05) / (darkest + 0.05)
      }

      // Các cặp màu token chính thức từ DESIGN.md Bản 5
      const colorPairs = [
        { name: 'Primary (#C6510B) on White (#FFFFFF)', fg: '#C6510B', bg: '#FFFFFF', minRatio: 4.5 },
        { name: 'Primary Dark (#A8440F) on White (#FFFFFF)', fg: '#A8440F', bg: '#FFFFFF', minRatio: 4.5 },
        { name: 'Primary Dark (#A8440F) on Primary Container (#FFF3EB)', fg: '#A8440F', bg: '#FFF3EB', minRatio: 4.5 },
        { name: 'Success (#0A7E3A) on White (#FFFFFF)', fg: '#0A7E3A', bg: '#FFFFFF', minRatio: 4.5 },
        { name: 'Info Dark (#004C87) on White (#FFFFFF)', fg: '#004C87', bg: '#FFFFFF', minRatio: 4.5 },
        { name: 'Info Dark (#004C87) on Info Container (#E6F0F8)', fg: '#004C87', bg: '#E6F0F8', minRatio: 4.5 },
        { name: 'Error (#C62828) on White (#FFFFFF)', fg: '#C62828', bg: '#FFFFFF', minRatio: 4.5 },
        { name: 'Text Primary (#1A1D20) on White (#FFFFFF)', fg: '#1A1D20', bg: '#FFFFFF', minRatio: 4.5 },
        { name: 'Text Secondary (#5B6370) on White (#FFFFFF)', fg: '#5B6370', bg: '#FFFFFF', minRatio: 4.5 },
      ]

      colorPairs.forEach(({ name, fg, bg, minRatio }) => {
        const ratio = getContrastRatio(fg, bg)
        expect(ratio, `${name} has contrast ${ratio.toFixed(2)}:1, expected >= ${minRatio}:1`).toBeGreaterThanOrEqual(minRatio)
      })
    })
  })
})
