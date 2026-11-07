import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, ApiError } from '@/shared/api/apiClient'
import { Banner, Button, Dialog } from '@/shared/components/ui'
import { Card } from '@/features/workspace/components'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import type { TeamMember } from '@/features/suggestions/api'
import { refreshTeams } from './api'

/** Đề nghị đổi nhóm trưởng (FE-54, BE-88). myBallot null = chưa bỏ phiếu. */
export interface LeaderVote {
  id: string
  status: 'OPEN' | 'PASSED' | 'REJECTED' | 'EXPIRED' | 'CANCELLED'
  nominee?: { studentId: string; fullName: string } | null
  proposedBy?: { studentId: string; fullName: string } | null
  reason?: string | null
  expiresAt: string
  approveCount: number
  rejectCount: number
  requiredApprovals: number
  memberCount: number
  myBallot?: boolean | null
  canWithdraw: boolean
}

const voteKey = (teamId: string) => ['leader-vote', teamId] as const

/** Đề nghị đang mở; API trả 204 khi không có. */
export function useLeaderVote(teamId: string) {
  return useQuery({
    queryKey: voteKey(teamId),
    queryFn: async () => (await api.get<LeaderVote | ''>(`/api/teams/${teamId}/leader-votes/current`)).data || null,
  })
}

function useRefresh(teamId: string) {
  const client = useQueryClient()
  return () => {
    void client.invalidateQueries({ queryKey: voteKey(teamId) })
    refreshTeams(client)
  }
}

export function useOpenLeaderVote(teamId: string) {
  const refresh = useRefresh(teamId)
  return useMutation({
    mutationFn: async (input: { nomineeStudentId: string; reason?: string }) => (await api.post<LeaderVote>(`/api/teams/${teamId}/leader-votes`, input)).data,
    onSuccess: refresh,
  })
}

function useBallot(teamId: string, voteId: string) {
  const refresh = useRefresh(teamId)
  return useMutation({
    mutationFn: async (approve: boolean) => (await api.put<LeaderVote>(`/api/teams/${teamId}/leader-votes/${voteId}/ballot`, { approve })).data,
    onSettled: refresh,
  })
}

function useWithdraw(teamId: string, voteId: string) {
  const refresh = useRefresh(teamId)
  return useMutation({
    mutationFn: async () => {
      await api.delete(`/api/teams/${teamId}/leader-votes/${voteId}`)
    },
    onSettled: refresh,
  })
}

/** Quá nửa số thành viên: nhóm 4 cần 3, nhóm 5 cần 3, nhóm 6 cần 4. */
export const requiredApprovals = (memberCount: number) => Math.floor(memberCount / 2) + 1

export function timeLeft(expiresAt: string, now = Date.now()) {
  const minutes = Math.floor((new Date(expiresAt).getTime() - now) / 60_000)
  if (minutes < 1) return 'sắp hết hạn'
  return minutes < 60 ? `còn ${minutes} phút` : `còn ${Math.floor(minutes / 60)} giờ`
}

const resultText = (vote: LeaderVote) =>
  vote.status === 'PASSED' ? `${vote.nominee?.fullName ?? 'Thành viên'} đã trở thành nhóm trưởng`
    : vote.status === 'REJECTED' ? 'Đề nghị không được thông qua'
      : 'Đã ghi nhận phiếu của bạn'

/** S15 – thẻ đề nghị đang mở. Không có đề nghị hoặc tải lỗi thì không hiện. */
export function LeaderVoteCard({ teamId }: { teamId: string }) {
  const vote = useLeaderVote(teamId)
  return vote.data ? <OpenVote teamId={teamId} vote={vote.data} /> : null
}

function OpenVote({ teamId, vote }: { teamId: string; vote: LeaderVote }) {
  const ballot = useBallot(teamId, vote.id)
  const withdraw = useWithdraw(teamId, vote.id)
  const snackbar = useSnackbar()
  const busy = ballot.isPending || withdraw.isPending
  const error = [ballot.error, withdraw.error].find((e) => e instanceof ApiError) as ApiError | undefined
  const percent = Math.min(100, Math.round((vote.approveCount / Math.max(1, vote.requiredApprovals)) * 100))
  const cast = (approve: boolean) => ballot.mutate(approve, { onSuccess: (result) => snackbar.show(resultText(result)) })
  const voted = vote.myBallot

  return (
    <Card>
      <h3 className="font-semibold">{vote.proposedBy?.fullName ?? 'Một thành viên'} đề nghị {vote.nominee?.fullName ?? 'thành viên'} làm nhóm trưởng</h3>
      {vote.reason && <p className="mt-1 text-text-secondary">{vote.reason}</p>}
      <div
        role="progressbar"
        aria-label="Tiến độ phiếu đồng ý"
        aria-valuemin={0}
        aria-valuemax={vote.requiredApprovals}
        aria-valuenow={vote.approveCount}
        className="mt-3 h-2 overflow-hidden rounded-full bg-surface-variant"
      >
        <div className="h-full bg-primary" style={{ width: `${percent}%` }} />
      </div>
      <p className="mt-2 text-caption text-text-secondary">
        {vote.approveCount}/{vote.requiredApprovals} phiếu đồng ý · {timeLeft(vote.expiresAt)}
      </p>
      {error && <div className="mt-3"><Banner tone="error" title={error.title}>{error.detail}</Banner></div>}
      {voted != null && <p className="mt-2 text-body">Bạn đã chọn: {voted ? 'Đồng ý' : 'Không đồng ý'}</p>}
      <div className="mt-3 flex flex-wrap gap-2">
        {voted !== true && <Button disabled={busy} onClick={() => cast(true)}>{voted == null ? 'Đồng ý' : 'Đổi sang Đồng ý'}</Button>}
        {voted !== false && <Button variant="outline" disabled={busy} onClick={() => cast(false)}>{voted == null ? 'Không đồng ý' : 'Đổi sang Không đồng ý'}</Button>}
        {vote.canWithdraw && (
          <Button variant="text" disabled={busy} onClick={() => withdraw.mutate(undefined, { onSuccess: () => snackbar.show('Đã rút đề nghị') })}>
            Rút đề nghị
          </Button>
        )}
      </div>
      {snackbar.element}
    </Card>
  )
}

/** S16 – dialog đề nghị làm nhóm trưởng. */
export function ProposeLeaderDialog({ teamId, memberCount, nominee, onClose, onSent }: {
  teamId: string
  memberCount: number
  nominee: TeamMember | null
  onClose: () => void
  onSent: () => void
}) {
  const [reason, setReason] = useState('')
  const open = useOpenLeaderVote(teamId)
  const error = open.error instanceof ApiError ? open.error : null
  const send = () => {
    if (!nominee) return
    open.mutate({ nomineeStudentId: nominee.id, reason: reason.trim() || undefined }, {
      onSuccess: () => {
        setReason('')
        onSent()
      },
    })
  }

  return (
    <Dialog
      open={!!nominee}
      onClose={onClose}
      title="Đề nghị làm nhóm trưởng"
      actions={
        <>
          <Button variant="text" onClick={onClose}>Hủy</Button>
          <Button loading={open.isPending} onClick={send}>Gửi đề nghị</Button>
        </>
      }
    >
      <div className="flex flex-col gap-3">
        <p>Người được đề cử: {nominee?.fullName}</p>
        <label className="flex flex-col gap-1">
          <span className="text-body">Lý do (không bắt buộc)</span>
          <textarea
            value={reason}
            maxLength={300}
            rows={3}
            onChange={(e) => setReason(e.target.value)}
            className="rounded-md border border-border bg-surface p-2 text-body"
          />
        </label>
        <p className="text-caption text-text-secondary">Cần {requiredApprovals(memberCount)}/{memberCount} thành viên đồng ý trong 48 giờ.</p>
        {error && <Banner tone="error" title={error.title}>{error.detail}</Banner>}
      </div>
    </Dialog>
  )
}
