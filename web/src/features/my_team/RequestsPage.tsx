import { useState } from 'react'
import { ApiError } from '@/shared/api/apiClient'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Banner, Button, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { Card } from '@/features/workspace/components'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { useMyTeam } from '@/features/suggestions/api'
import { SkillBars } from '@/features/profile/components'
import { requestStatusLabel, useRequestDecision, useSentInvitations, useTeamJoinRequests, type FullTeam } from './api'
import { ListSkeleton } from './components'

/**
 * S20 – Yêu cầu tham gia và lời mời đã gửi (FE-37, cùng spec MB-24, trưởng nhóm): tab Yêu cầu (ý tưởng hoặc "Chưa có ý tưởng" –
 * quy tắc 17, Nhận / Từ chối) và tab Lời mời đã gửi (trạng thái, thu hồi).
 */
export function RequestsPage() {
  const team = useMyTeam()
  const [tab, setTab] = useState<'requests' | 'invitations'>('requests')
  return (
    <>
      <PageHeader title="Yêu cầu và lời mời" />
      {team.error ? (
        <ErrorState error={team.error} onRetry={() => void team.refetch()} />
      ) : team.isLoading ? (
        <ListSkeleton />
      ) : team.data?.myRelation !== 'LEADER' ? (
        <EmptyState icon="lock" title="Chỉ trưởng nhóm xem được" />
      ) : (
        <div className="flex max-w-3xl flex-col gap-4">
          <div role="tablist" className="flex gap-2 border-b border-border">
            {(
              [
                ['requests', 'Yêu cầu'],
                ['invitations', 'Lời mời đã gửi'],
              ] as const
            ).map(([key, label]) => (
              <button
                key={key}
                role="tab"
                type="button"
                aria-selected={tab === key}
                onClick={() => setTab(key)}
                className={`min-h-12 px-4 font-medium ${tab === key ? 'border-b-2 border-primary text-primary-dark' : 'text-text-secondary'}`}
              >
                {label}
              </button>
            ))}
          </div>
          {tab === 'requests' ? <JoinRequests team={team.data as FullTeam} /> : <SentInvitations team={team.data as FullTeam} />}
        </div>
      )}
    </>
  )
}

function JoinRequests({ team }: { team: FullTeam }) {
  const requests = useTeamJoinRequests(team.id)
  const decide = useRequestDecision()
  const snackbar = useSnackbar()
  const isFull = team.memberCount >= (team.requirement?.maxSize ?? 6)
  const error = decide.error instanceof ApiError ? decide.error : null

  if (requests.error) return <ErrorState error={requests.error} onRetry={() => void requests.refetch()} />
  if (requests.isLoading) return <ListSkeleton />
  return (
    <div className="flex flex-col gap-3">
      {error && <Banner tone="error" title={error.title}>{error.detail}</Banner>}
      {requests.data!.length === 0 && <EmptyState icon="inbox" title="Chưa có yêu cầu" description="Yêu cầu tham gia sẽ hiện ở đây." />}
      {requests.data!.map((r) => (
        <Card key={r.id}>
          <p className="font-semibold">{r.student.fullName}</p>
          <p className="text-text-secondary">{[r.student.studentCode, r.student.specialization?.name].filter(Boolean).join(' · ')}</p>
          {r.studentDescription && <p className="mt-2 text-text-secondary">{r.studentDescription}</p>}
          {/* shortcut: chưa đánh dấu kỹ năng nhóm đang thiếu – API thành viên nhóm chưa trả kỹ năng; thêm highlight khi contract có. */}
          <div className="mt-3">
            <SkillBars ratings={r.studentSkills ?? []} />
          </div>
          {r.message && <p className="mt-2">“{r.message}”</p>}
          <div className="mt-2">
            {r.hasNoIdea ? (
              <StatusChip tone="neutral" icon="lightbulb">
                Chưa có ý tưởng
              </StatusChip>
            ) : (
              <ul className="flex flex-col gap-1">
                {r.ideas.map((idea) => (
                  <li key={idea.title}>
                    <span className="font-medium">{idea.title}</span>
                    {idea.description && <span className="text-text-secondary"> – {idea.description}</span>}
                  </li>
                ))}
              </ul>
            )}
          </div>
          <div className="mt-3 flex items-center justify-end gap-2">
            <Button variant="text" disabled={decide.isPending} onClick={() => decide.mutate({ kind: 'decline', id: r.id }, { onSuccess: () => snackbar.show('Đã từ chối') })}>
              Từ chối
            </Button>
            {isFull ? (
              <span className="text-text-secondary">Nhóm đã đủ</span>
            ) : (
              <Button
                loading={decide.isPending && decide.variables?.id === r.id && decide.variables.kind === 'accept'}
                disabled={decide.isPending}
                onClick={() =>
                  decide.mutate(
                    { kind: 'accept', id: r.id },
                    { onSuccess: (result) => snackbar.show(result?.appliedImmediately === false ? 'Đã thêm vào yêu cầu thay đổi (chưa gửi). Vào Yêu cầu thay đổi để gửi Bộ môn duyệt.' : `Đã nhận ${r.student.fullName}`) },
                  )
                }
              >
                Nhận
              </Button>
            )}
          </div>
        </Card>
      ))}
      {snackbar.element}
    </div>
  )
}

function SentInvitations({ team }: { team: FullTeam }) {
  const invitations = useSentInvitations(team.id)
  const decide = useRequestDecision()
  const error = decide.error instanceof ApiError ? decide.error : null

  if (invitations.error) return <ErrorState error={invitations.error} onRetry={() => void invitations.refetch()} />
  if (invitations.isLoading) return <ListSkeleton />
  return (
    <div className="flex flex-col gap-3">
      {error && <Banner tone="error" title={error.title}>{error.detail}</Banner>}
      {invitations.data!.length === 0 && <EmptyState icon="outgoing_mail" title="Chưa gửi lời mời nào" />}
      {invitations.data!.map((i) => (
        <Card key={i.id} className="flex flex-wrap items-center gap-3">
          <span className="min-w-0 flex-1">
            <span className="block font-medium">{i.invitee.fullName}</span>
            <span className="text-text-secondary">{i.invitee.studentCode}</span>
          </span>
          <StatusChip tone={requestStatusLabel[i.status].tone}>{requestStatusLabel[i.status].text}</StatusChip>
          {i.status === 'PENDING' && (
            <Button variant="text" disabled={decide.isPending} onClick={() => decide.mutate({ kind: 'cancel-invitation', id: i.id })}>
              Thu hồi
            </Button>
          )}
        </Card>
      ))}
    </div>
  )
}
