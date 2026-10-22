import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { TaskCard } from '@/features/tasks/api'

const get = vi.fn()
const post = vi.fn()
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: { get: (...args: unknown[]) => get(...args), post: (...args: unknown[]) => post(...args), patch: vi.fn(), delete: vi.fn() },
}))

const { dueChip, formatSize, initials } = await import('./format')
const { taskStats } = await import('./stats')
const { moveLocally } = await import('@/features/tasks/api')
const { validateUpload } = await import('@/features/files/api')
const { workspaceRoutes } = await import('./routes')
const { tokenStore } = await import('@/shared/api/tokenStore')
const { AuthProvider } = await import('@/shared/auth/AuthProvider')
const { PageHeaderProvider } = await import('@/shared/layout/PageHeader')

const now = new Date('2026-10-22T08:00:00+07:00')

const task = (patch: Partial<TaskCard>): TaskCard => ({
  id: crypto.randomUUID(),
  workspaceId: 'w1',
  teamName: 'Nhóm A',
  kind: 'TASK',
  title: 'Việc',
  status: 'TODO',
  position: 0,
  assignees: [],
  dueAt: null,
  isOverdue: false,
  submissionStatus: null,
  commentCount: 0,
  isArchive: false,
  ...patch,
})

describe('format', () => {
  it('labels due dates like the design spec', () => {
    expect(dueChip('2026-10-22T17:00:00+07:00', now).text).toBe('Hôm nay')
    expect(dueChip('2026-10-23T09:00:00+07:00', now).text).toBe('Ngày mai')
    expect(dueChip('2026-10-27T09:00:00+07:00', now)).toMatchObject({ text: 'Còn 5 ngày', tone: 'neutral' })
    expect(dueChip('2026-10-21T09:00:00+07:00', now)).toMatchObject({ text: 'Trễ 1 ngày', tone: 'error', icon: 'alarm' })
  })

  it('formats sizes and initials', () => {
    expect(formatSize(820 * 1024)).toBe('820 KB')
    expect(formatSize(2.4 * 1024 * 1024)).toBe('2,4 MB')
    expect(initials('Nguyễn Thị Minh Anh')).toBe('MA')
  })
})

describe('board logic', () => {
  it('moves a card into another column at a position and renumbers it', () => {
    const a = task({ title: 'A', status: 'TODO', position: 0 })
    const b = task({ title: 'B', status: 'DOING', position: 0 })
    const c = task({ title: 'C', status: 'DOING', position: 1 })

    const moved = moveLocally([a, b, c], a.id, 'DOING', 1)

    const doing = moved.filter((t) => t.status === 'DOING').sort((x, y) => x.position - y.position)
    expect(doing.map((t) => t.title)).toEqual(['B', 'A', 'C'])
    expect(doing.map((t) => t.position)).toEqual([0, 1, 2])
  })

  it('counts my open tasks, due soon and pending reviews', () => {
    const me = { personType: 'STUDENT' as const, personId: 's1', displayName: 'Vy' }
    const stats = taskStats(
      [
        task({ assignees: [me], dueAt: '2026-10-23T00:00:00Z' }),
        task({ assignees: [me], status: 'DONE' }),
        task({ dueAt: '2026-12-01T00:00:00Z' }),
        task({ kind: 'DELIVERABLE', submissionStatus: 'PENDING' }),
      ],
      's1',
      now,
    )
    expect(stats).toEqual({ mine: 1, dueSoon: 1, pendingReview: 1 })
  })

  it('rejects wrong types and files over 25 MB before uploading', () => {
    expect(validateUpload(new File(['x'], 'virus.exe'))).toMatch(/Chỉ nhận/)
    expect(validateUpload(new File(['x'], 'a.PDF'))).toBeNull()
    const big = new File(['x'], 'big.zip')
    Object.defineProperty(big, 'size', { value: 25 * 1024 * 1024 + 1 })
    expect(validateUpload(big)).toMatch(/25 MB/)
  })
})

describe('workspace screens', () => {
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

  const workspace = {
    id: 'w1',
    teamId: 't1',
    teamName: 'Nhóm EduSpark',
    courseCode: 'EXE201',
    myRole: 'LEADER',
    previousWorkspaceId: 'w0',
    people: [{ personType: 'STUDENT', personId: 's1', displayName: 'Lê Vy', role: 'LEADER' }],
  }

  function renderAt(path: string) {
    return render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <AuthProvider>
          <PageHeaderProvider>
            <RouterProvider
              router={createMemoryRouter(
                [{ path: '/sv', children: [workspaceRoutes('/sv'), { path: 'nhom', element: <p>Nhóm của tôi</p> }] }],
                { initialEntries: [path] },
              )}
            />
          </PageHeaderProvider>
        </AuthProvider>
      </QueryClientProvider>,
    )
  }

  it('C01 shows team, stat cards and the EXE101 continuation', async () => {
    get.mockImplementation(async (url: string) => {
      if (url === '/api/workspaces/me') return { data: [workspace] }
      if (url === '/api/workspaces/w1') return { data: workspace }
      if (url === '/api/workspaces/w1/tasks') return { data: [task({ assignees: [workspace.people[0]] as TaskCard['assignees'] })] }
      throw new Error(url)
    })

    renderAt('/sv/du-an')

    expect(await screen.findByText('Nhóm EduSpark')).toBeInTheDocument()
    expect(screen.getByText('Tiếp tục từ EXE101')).toBeInTheDocument()
    const mine = screen.getByText('Việc của tôi').closest('a')!
    expect(await within(mine).findByText('1')).toBeInTheDocument()
  })

  it('C01 tells a student without a team where to go', async () => {
    get.mockResolvedValue({ data: [] })

    renderAt('/sv/du-an')

    expect(await screen.findByText('Bạn chưa có nhóm')).toBeInTheDocument()
  })

  it('C06 pins the read-only EXE101 folder and asks before creating a new version', async () => {
    get.mockImplementation(async (url: string) => {
      if (url === '/api/workspaces/me') return { data: [workspace] }
      if (url === '/api/workspaces/w1') return { data: workspace }
      if (url === '/api/workspaces/w1/files')
        return {
          data: {
            folder: null,
            breadcrumb: [],
            canUpload: true,
            folders: [{ id: 'w0', name: 'EXE101 (kỳ trước)', isArchive: true, itemCount: 3 }],
            files: [{ id: 'f1', name: 'bao-cao.pdf', currentVersion: 2, sizeBytes: 1024, contentType: 'application/pdf', updatedByName: 'Vy', updatedAt: now.toISOString(), isArchive: false }],
          },
        }
      throw new Error(url)
    })
    renderAt('/sv/du-an/file')

    expect(await screen.findByText('EXE101 (kỳ trước)')).toBeInTheDocument()
    expect(screen.getByText('Chỉ xem')).toBeInTheDocument()

    const input = document.querySelector('input[type=file]') as HTMLInputElement
    await userEvent.upload(input, new File(['v3'], 'Bao-cao.pdf', { type: 'application/pdf' }))

    expect(await screen.findByText('Tạo phiên bản mới?')).toBeInTheDocument()
    expect(screen.getByText(/sẽ tạo v3/)).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
  })
})
