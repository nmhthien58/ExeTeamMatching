import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const get = vi.fn()
const post = vi.fn()
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: { get: (...args: unknown[]) => get(...args), post: (...args: unknown[]) => post(...args) },
}))

const { projectRoutes } = await import('./routes')
const { PageHeaderProvider } = await import('@/shared/layout/PageHeader')

const sampleProject = {
  id: 'proj-1',
  name: 'Hệ thống Quản lý Ký túc xá Thông minh',
  description: 'Nền tảng IoT và Web quản lý ra vào ký túc xá',
  currentVersion: 2,
  originCourseCode: 'EXE101',
  originSemesterCode: 'SP26',
  versions: [
    {
      version: 1,
      name: 'Hệ thống Quản lý Ký túc xá',
      description: 'Nền tảng ban đầu',
      changeType: 'INITIAL',
      reason: 'Khởi tạo đề tài',
      semesterCode: 'SP26',
      courseCode: 'EXE101',
      changedByName: 'Nguyễn Văn A',
      createdAt: '2026-01-15T08:00:00Z',
    },
    {
      version: 2,
      name: 'Hệ thống Quản lý Ký túc xá Thông minh',
      description: 'Nền tảng IoT và Web quản lý ra vào ký túc xá',
      changeType: 'PIVOT',
      reason: 'Bổ sung module nhận diện khuôn mặt và IoT gateway',
      semesterCode: 'FA26',
      courseCode: 'EXE201',
      changedByName: 'Trần Văn B',
      createdAt: '2026-09-10T10:00:00Z',
    },
  ],
  teams: [
    {
      team: {
        id: 'team-1',
        name: 'DormTech101',
        courseCode: 'EXE101',
        status: 'ACTIVE',
        domains: [{ id: 1, code: 'D01', name: 'IoT' }],
        memberCount: 5,
        openSlots: 0,
        isProjectContinued: false,
      },
      semesterCode: 'SP26',
      keptMemberPercent: null,
      instructorName: 'TS. Nguyễn Hướng Dẫn',
      mentorName: 'ThS. Trần Mentor',
    },
    {
      team: {
        id: 'team-2',
        name: 'DormTech201',
        courseCode: 'EXE201',
        status: 'ACTIVE',
        domains: [{ id: 1, code: 'D01', name: 'IoT' }],
        memberCount: 5,
        openSlots: 0,
        isProjectContinued: true,
      },
      semesterCode: 'FA26',
      keptMemberPercent: 80,
      instructorName: 'TS. Nguyễn Hướng Dẫn',
      mentorName: 'ThS. Trần Mentor',
    },
  ],
}

const sampleUnlinkedTeam = {
  id: 'team-unlinked-1',
  name: 'SmartRetail',
  courseCode: 'EXE201',
  status: 'ACTIVE',
  domains: [{ id: 2, code: 'D02', name: 'Thương mại điện tử' }],
  memberCount: 4,
  openSlots: 1,
  isProjectContinued: false,
  leader: {
    id: 's-leader',
    studentCode: 'SE180123',
    fullName: 'Lê Hoàng Nam',
  },
}

async function getResponse(url: string) {
  if (url === '/api/semesters') {
    return {
      data: [
        { id: 'sem-sp26', code: 'SP26', name: 'Spring 2026' },
        { id: 'sem-fa26', code: 'FA26', name: 'Fall 2026' },
      ],
    }
  }
  if (url === '/api/projects') {
    return {
      data: {
        page: 1,
        pageSize: 20,
        totalCount: 1,
        totalPages: 1,
        items: [sampleProject],
      },
    }
  }
  if (url === '/api/projects/unlinked-teams') {
    return {
      data: [sampleUnlinkedTeam],
    }
  }
  throw new Error(`Unhandled GET: ${url}`)
}

function renderAt(path: string) {
  const router = createMemoryRouter([{ path: '/admin', children: projectRoutes }], {
    initialEntries: [path],
  })
  return render(
    <QueryClientProvider
      client={
        new QueryClient({
          defaultOptions: {
            queries: { retry: false },
          },
        })
      }
    >
      <PageHeaderProvider>
        <RouterProvider router={router} />
      </PageHeaderProvider>
    </QueryClientProvider>,
  )
}

describe('W29 - Projects across semesters', () => {
  afterEach(() => vi.unstubAllGlobals())

  beforeEach(() => {
    // Node Request signal fix for jsdom
    const NativeRequest = globalThis.Request
    vi.stubGlobal(
      'Request',
      class extends NativeRequest {
        constructor(input: RequestInfo | URL, init?: RequestInit) {
          super(input, { ...init, signal: undefined })
        }
      },
    )

    get.mockReset()
    post.mockReset()

    HTMLDialogElement.prototype.showModal = function (this: HTMLDialogElement) {
      this.setAttribute('open', '')
      this.open = true
    }
    HTMLDialogElement.prototype.close = function (this: HTMLDialogElement) {
      this.removeAttribute('open')
      this.open = false
    }

    get.mockImplementation(getResponse)
    post.mockImplementation(async (url: string) => {
      if (url === `/api/projects/proj-1/link-team`) {
        return { data: sampleProject }
      }
      throw new Error(`Unhandled POST: ${url}`)
    })
  })

  it('renders projects table with lineage and version tags', async () => {
    renderAt('/admin/de-tai')

    await waitFor(() => {
      expect(document.querySelector('tbody')?.innerHTML).toBeTruthy()
    })
    // Project name and version
    expect(await screen.findByText('Hệ thống Quản lý Ký túc xá Thông minh')).toBeInTheDocument()
    expect(screen.getByText('v2')).toBeInTheDocument()

    // Lineage teams
    expect(screen.getByText('DormTech101')).toBeInTheDocument()
    expect(screen.getByText('DormTech201')).toBeInTheDocument()
    expect(screen.getByText('Giữ 80% TV')).toBeInTheDocument()

    // Instructors and Mentors
    expect(screen.getByText('TS. Nguyễn Hướng Dẫn')).toBeInTheDocument()
  })

  it('opens history modal and shows project versions', async () => {
    const user = userEvent.setup()
    renderAt('/admin/de-tai')

    await screen.findByText('Hệ thống Quản lý Ký túc xá Thông minh')

    const historyBtn = await screen.findByRole('button', {
      name: /Lịch sử/i,
    })
    await user.click(historyBtn)

    expect(await screen.findByText(/Phiên bản 1: Hệ thống Quản lý Ký túc xá/i)).toBeInTheDocument()
    expect(screen.getByText(/Phiên bản 2: Hệ thống Quản lý Ký túc xá Thông minh/i)).toBeInTheDocument()
    expect(screen.getByText(/Bổ sung module nhận diện khuôn mặt/i)).toBeInTheDocument()
  })

  it('switches to unlinked teams tab and links team to project', async () => {
    const user = userEvent.setup()
    renderAt('/admin/de-tai?semesterId=sem-fa26')

    const unlinkedTab = await screen.findByRole('tab', { name: /Chưa gắn đề tài/i })
    await user.click(unlinkedTab)

    expect(await screen.findByText('SmartRetail')).toBeInTheDocument()
    expect(screen.getByText('Lê Hoàng Nam')).toBeInTheDocument()

    const linkBtn = screen.getByRole('button', { name: 'Gắn vào đề tài' })
    await user.click(linkBtn)

    const input = await screen.findByLabelText(/Mã ID Đề tài/i)
    await user.type(input, 'proj-1')

    const confirmBtn = screen.getByRole('button', { name: 'Xác nhận gắn' })
    await user.click(confirmBtn)

    await waitFor(() => {
      expect(post).toHaveBeenCalledWith('/api/projects/proj-1/link-team', {
        teamId: 'team-unlinked-1',
      })
    })
  })
})
