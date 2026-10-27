import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router-dom'
import { api, ApiError } from '@/shared/api/apiClient'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Banner, Button, Dialog, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { Card } from '@/features/workspace/components'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { refreshTeams, requestStatusLabel, timeLeft, type Invitation, type JoinRequest, type MemberChangeResult } from '@/features/my_team/api'
import { ListSkeleton } from '@/features/my_team/components'

/**
 * S12 – Lời mời và yêu cầu của tôi (FE-37, cùng spec MB-18): tab Lời mời nhận được (thời gian còn lại, Chấp nhận có dialog / Từ chối)
 * và tab Yêu cầu đã gửi (trạng thái, Hủy khi còn chờ).
 */
export function InvitationsPage() {
  const [tab, setTab] = useState<'received' | 'sent'>('received')
  return (
    <>
      <PageHeader title="Lời mời và yêu cầu" />
      <div className="flex max-w-3xl flex-col gap-4">
        <div role="tablist" className="flex gap-2 border-b border-border">
          {(
            [
              ['received', 'Lời mời'],
              ['sent', 'Yêu cầu đã gửi'],
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
        {tab === 'received' ? <Received /> : <Sent />}
      </div>
    </>
  )
}

function useRespond() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ kind, id }: { kind: 'accept' | 'decline' | 'cancel'; id: string }) =>
      (await api.post<MemberChangeResult | undefined>(kind === 'cancel' ? `/api/join-requests/${id}/cancel` : `/api/invitations/${id}/${kind}`)).data,
    onSettled: () => refreshTeams(queryClient),
  })
}

function Received() {
  const navigate = useNavigate()
  const snackbar = useSnackbar()
  const respond = useRespond()
  const [accepting, setAccepting] = useState<Invitation | null>(null)
  const invitations = useQuery({ queryKey: ['invitations', 'me'], queryFn: async () => (await api.get<Invitation[]>('/api/invitations/me')).data })
  const error = respond.error instanceof ApiError ? respond.error : null

  if (invitations.error) return <ErrorState error={invitations.error} onRetry={() => void invitations.refetch()} />
  if (invitations.isLoading) return <ListSkeleton />
  return (
    <div className="flex flex-col gap-3">
      {error && <Banner tone="error" title={error.title}>{error.detail}</Banner>}
      {invitations.data!.length === 0 && <EmptyState icon="mail" title="Chưa có lời mời" />}
      {invitations.data!.map((i) => (
        <Card key={i.id}>
          <div className="flex flex-wrap items-center justify-between gap-2">
            <span className="text-title font-semibold">{i.team?.name ?? 'Nhóm'}</span>
            <StatusChip tone={requestStatusLabel[i.status].tone}>{requestStatusLabel[i.status].text}</StatusChip>
          </div>
          {i.team?.projectName && <p className="text-text-secondary">{i.team.projectName}</p>}
          {i.message && <p className="mt-2">“{i.message}”</p>}
          {i.status === 'PENDING' && (
            <div className="mt-3 flex flex-wrap items-center gap-2">
              <span className="text-text-secondary">{timeLeft(i.expiresAt)}</span>
              {i.team && (
                <Link to={`/sv/kham-pha/nhom/${i.team.id}`} className="font-medium text-info-dark">
                  Xem nhóm
                </Link>
              )}
              <span className="flex-1" />
              <Button variant="text" disabled={respond.isPending} onClick={() => respond.mutate({ kind: 'decline', id: i.id })}>
                Từ chối
              </Button>
              <Button disabled={respond.isPending} onClick={() => setAccepting(i)}>
                Chấp nhận
              </Button>
            </div>
          )}
        </Card>
      ))}
      <Dialog
        open={!!accepting}
        onClose={() => setAccepting(null)}
        title="Vào nhóm?"
        actions={
          <>
            <Button variant="text" onClick={() => setAccepting(null)}>
              Hủy
            </Button>
            <Button
              loading={respond.isPending}
              onClick={() =>
                respond.mutate(
                  { kind: 'accept', id: accepting!.id },
                  {
                    onSuccess: (result) => {
                      setAccepting(null)
                      snackbar.show(result?.appliedImmediately === false ? 'Đã ghi nhận, chờ Bộ môn duyệt' : 'Đã vào nhóm')
                      navigate('/sv/nhom')
                    },
                    onError: () => setAccepting(null),
                  },
                )
              }
            >
              Vào nhóm
            </Button>
          </>
        }
      >
        Bạn sẽ vào {accepting?.team?.name ?? 'nhóm'}; các lời mời và yêu cầu khác sẽ bị hủy.
      </Dialog>
      {snackbar.element}
    </div>
  )
}

function Sent() {
  const respond = useRespond()
  const requests = useQuery({ queryKey: ['join-requests', 'me'], queryFn: async () => (await api.get<JoinRequest[]>('/api/join-requests/me')).data })
  const error = respond.error instanceof ApiError ? respond.error : null

  if (requests.error) return <ErrorState error={requests.error} onRetry={() => void requests.refetch()} />
  if (requests.isLoading) return <ListSkeleton />
  return (
    <div className="flex flex-col gap-3">
      {error && <Banner tone="error" title={error.title}>{error.detail}</Banner>}
      {requests.data!.length === 0 && <EmptyState icon="outgoing_mail" title="Chưa gửi yêu cầu nào" />}
      {requests.data!.map((r) => (
        <Card key={r.id} className="flex flex-wrap items-center gap-3">
          <span className="min-w-0 flex-1">
            <span className="block font-medium">{r.team?.name ?? 'Nhóm'}</span>
            <span className="text-text-secondary">{r.status === 'PENDING' ? timeLeft(r.expiresAt) : requestStatusLabel[r.status].text}</span>
          </span>
          {r.status === 'PENDING' ? (
            <Button variant="text" disabled={respond.isPending} onClick={() => respond.mutate({ kind: 'cancel', id: r.id })}>
              Hủy
            </Button>
          ) : (
            <StatusChip tone={requestStatusLabel[r.status].tone}>{requestStatusLabel[r.status].text}</StatusChip>
          )}
        </Card>
      ))}
    </div>
  )
}
