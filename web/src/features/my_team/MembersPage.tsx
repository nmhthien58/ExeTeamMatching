import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { ApiError } from '@/shared/api/apiClient'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Banner, Button, Dialog, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { Avatar, Card } from '@/features/workspace/components'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { useMyTeam, type TeamMember } from '@/features/suggestions/api'
import { useMemberAction, type FullTeam } from './api'
import { ListSkeleton } from './components'

type Pending = { kind: 'remove' | 'transfer'; member: TeamMember } | { kind: 'leave' }

/**
 * S17 – Thành viên nhóm (FE-37, cùng spec MB-22): trưởng nhóm chuyển quyền / xóa, thành viên rời nhóm; mọi thao tác có dialog.
 * Nhóm EXE201 kế thừa: thay đổi chỉ ghi nhận, chờ Bộ môn duyệt (quy tắc 7).
 */
export function MembersPage() {
  const team = useMyTeam()
  return (
    <>
      <PageHeader title="Thành viên" />
      {team.error ? (
        <ErrorState error={team.error} onRetry={() => void team.refetch()} />
      ) : team.isLoading ? (
        <ListSkeleton />
      ) : !team.data ? (
        <EmptyState icon="group_off" title="Bạn chưa có nhóm" />
      ) : (
        <Members team={team.data as FullTeam} />
      )}
    </>
  )
}

function Members({ team }: { team: FullTeam }) {
  const navigate = useNavigate()
  const snackbar = useSnackbar()
  const action = useMemberAction(team.id)
  const [pending, setPending] = useState<Pending | null>(null)
  const isLeader = team.myRelation === 'LEADER'
  const canChange = team.status === 'DRAFT' || team.status === 'ACTIVE' || !!team.isInherited
  const error = action.error instanceof ApiError ? action.error : null

  const confirm = () => {
    if (!pending) return
    const leaving = pending.kind === 'leave'
    action.mutate(pending.kind === 'leave' ? { kind: 'leave' } : { kind: pending.kind, studentId: pending.member.id }, {
      onSuccess: (result) => {
        setPending(null)
        snackbar.show(result.appliedImmediately ? 'Đã cập nhật' : 'Đã ghi nhận, chờ Bộ môn duyệt')
        if (leaving && result.appliedImmediately) navigate('/sv/nhom')
      },
      onError: () => setPending(null),
    })
  }

  const dialog =
    pending?.kind === 'leave'
      ? { title: 'Rời nhóm?', body: `Bạn sẽ rời ${team.name}.`, button: 'Rời nhóm' }
      : pending?.kind === 'remove'
        ? { title: 'Xóa khỏi nhóm?', body: `Xóa ${pending.member.fullName} khỏi nhóm.`, button: 'Xóa' }
        : pending?.kind === 'transfer'
          ? { title: 'Chuyển trưởng nhóm?', body: `${pending.member.fullName} sẽ là trưởng nhóm; bạn thành thành viên.`, button: 'Chuyển' }
          : null

  return (
    <div className="flex max-w-3xl flex-col gap-4">
      {error && <Banner tone="error" title={error.title}>{error.detail}</Banner>}
      {team.hasPendingChanges && (
        <StatusChip tone="warning" icon="hourglass_top">
          Có thay đổi chờ Bộ môn duyệt
        </StatusChip>
      )}
      <Card className="p-0">
        <ul>
          {team.members.map((m) => (
            <li key={m.id} className="flex min-h-16 flex-wrap items-center gap-3 border-b border-border px-4 py-2 last:border-b-0">
              <Avatar name={m.fullName} size={40} />
              <span className="min-w-0 flex-1">
                <span className="block font-medium">{m.fullName}</span>
                <span className="text-text-secondary">{[m.studentCode, m.specialization?.name].filter(Boolean).join(' · ')}</span>
              </span>
              {m.role === 'LEADER' && <StatusChip tone="info">Trưởng nhóm</StatusChip>}
              {isLeader && m.role !== 'LEADER' && canChange && (
                <span className="flex gap-1">
                  <Button variant="text" onClick={() => setPending({ kind: 'transfer', member: m })}>
                    Chuyển trưởng nhóm
                  </Button>
                  <Button variant="text" className="text-error" onClick={() => setPending({ kind: 'remove', member: m })}>
                    Xóa
                  </Button>
                </span>
              )}
            </li>
          ))}
        </ul>
      </Card>
      {!isLeader && canChange && (
        <Button variant="outline" icon="logout" className="self-start" onClick={() => setPending({ kind: 'leave' })}>
          Rời nhóm
        </Button>
      )}
      {isLeader && <p className="text-text-secondary">Muốn rời nhóm, hãy chuyển quyền trưởng nhóm trước.</p>}
      <Dialog
        open={!!dialog}
        onClose={() => setPending(null)}
        title={dialog?.title ?? ''}
        actions={
          <>
            <Button variant="text" onClick={() => setPending(null)}>
              Hủy
            </Button>
            <Button variant="danger" loading={action.isPending} onClick={confirm}>
              {dialog?.button}
            </Button>
          </>
        }
      >
        {dialog?.body}
      </Dialog>
      {snackbar.element}
    </div>
  )
}
