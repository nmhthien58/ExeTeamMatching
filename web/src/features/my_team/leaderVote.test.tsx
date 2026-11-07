import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '@/shared/api/apiClient'
import { LeaderVoteCard, ProposeLeaderDialog, requiredApprovals, timeLeft, type LeaderVote } from './LeaderVote'

const { get, post, put, del } = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn(), put: vi.fn(), del: vi.fn() }))

vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: { get, post, put, delete: del },
}))

const vote = (over: Partial<LeaderVote> = {}): LeaderVote => ({
  id: 'v1',
  status: 'OPEN',
  nominee: { studentId: 's2', fullName: 'Bình' },
  proposedBy: { studentId: 's1', fullName: 'An' },
  reason: 'Bạn ấy sát sao hơn',
  expiresAt: new Date(Date.now() + 5.5 * 3_600_000).toISOString(),
  approveCount: 2,
  rejectCount: 0,
  requiredApprovals: 3,
  memberCount: 5,
  myBallot: null,
  canWithdraw: false,
  ...over,
})

const wrap = (ui: React.ReactNode) =>
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>{ui}</QueryClientProvider>)

describe('FE-54 – bỏ phiếu đổi nhóm trưởng', () => {
  beforeEach(() => {
    for (const fn of [get, post, put, del]) fn.mockReset()
  })

  it('needs more than half of the members', () => {
    expect([2, 4, 5, 6].map(requiredApprovals)).toEqual([2, 3, 3, 4])
  })

  it('formats the time left', () => {
    const now = new Date('2026-11-07T10:00:00Z').getTime()
    expect(timeLeft('2026-11-07T15:30:00Z', now)).toBe('còn 5 giờ')
    expect(timeLeft('2026-11-07T10:20:00Z', now)).toBe('còn 20 phút')
    expect(timeLeft('2026-11-07T10:00:00Z', now)).toBe('sắp hết hạn')
  })

  it('shows nothing when no vote is open (204)', async () => {
    get.mockResolvedValue({ data: '' })
    wrap(<LeaderVoteCard teamId="t1" />)
    await waitFor(() => expect(get).toHaveBeenCalled())
    expect(screen.queryByRole('button', { name: 'Đồng ý' })).not.toBeInTheDocument()
  })

  it('shows progress and casts a ballot', async () => {
    get.mockResolvedValue({ data: vote() })
    put.mockResolvedValue({ data: vote({ status: 'PASSED' }) })
    wrap(<LeaderVoteCard teamId="t1" />)

    expect(await screen.findByText('An đề nghị Bình làm nhóm trưởng')).toBeInTheDocument()
    expect(screen.getByText(/2\/3 phiếu đồng ý/)).toBeInTheDocument()
    expect(screen.getByRole('progressbar', { name: 'Tiến độ phiếu đồng ý' })).toHaveAttribute('aria-valuenow', '2')
    expect(screen.queryByRole('button', { name: 'Rút đề nghị' })).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Đồng ý' }))

    expect(put).toHaveBeenCalledWith('/api/teams/t1/leader-votes/v1/ballot', { approve: true })
    expect(await screen.findByText('Bình đã trở thành nhóm trưởng')).toBeInTheDocument()
  })

  it('lets the voter change the ballot and the proposer withdraw', async () => {
    get.mockResolvedValue({ data: vote({ myBallot: true, canWithdraw: true }) })
    del.mockResolvedValue({})
    wrap(<LeaderVoteCard teamId="t1" />)

    expect(await screen.findByText('Bạn đã chọn: Đồng ý')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Đổi sang Không đồng ý' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Đồng ý' })).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Rút đề nghị' }))
    expect(del).toHaveBeenCalledWith('/api/teams/t1/leader-votes/v1')
  })

  it('keeps the card and shows the error when a ballot fails', async () => {
    get.mockResolvedValue({ data: vote() })
    put.mockRejectedValue(new ApiError({ status: 409, code: 'leader-vote-closed', title: 'Đề nghị đã kết thúc' }))
    wrap(<LeaderVoteCard teamId="t1" />)

    await userEvent.click(await screen.findByRole('button', { name: 'Không đồng ý' }))

    expect(await screen.findByText('Đề nghị đã kết thúc')).toBeInTheDocument()
    expect(screen.getByText('An đề nghị Bình làm nhóm trưởng')).toBeInTheDocument()
  })

  it('S16 sends the proposal with the required votes explained', async () => {
    post.mockResolvedValue({ data: vote() })
    const sent = vi.fn()
    wrap(
      <ProposeLeaderDialog
        teamId="t1"
        memberCount={5}
        nominee={{ id: 's2', fullName: 'Bình', studentCode: 'SE1', role: 'MEMBER' } as never}
        onClose={() => {}}
        onSent={sent}
      />,
    )

    expect(screen.getByText('Cần 3/5 thành viên đồng ý trong 48 giờ.')).toBeInTheDocument()
    await userEvent.type(screen.getByLabelText('Lý do (không bắt buộc)'), 'Sát sao')
    await userEvent.click(screen.getByRole('button', { name: 'Gửi đề nghị', hidden: true }))

    expect(post).toHaveBeenCalledWith('/api/teams/t1/leader-votes', { nomineeStudentId: 's2', reason: 'Sát sao' })
    await waitFor(() => expect(sent).toHaveBeenCalled())
  })
})
