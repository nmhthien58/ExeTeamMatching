import { Link } from 'react-router-dom'
import { useMyTeam } from '@/features/suggestions/api'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Button, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { usePhases } from '@/shared/phase/usePhase'
import { useTeamChangeRequests } from './api'

const statusLabel = { PENDING: 'Chờ duyệt', APPROVED: 'Đã duyệt', REJECTED: 'Đã từ chối', CANCELLED: 'Đã hủy' } as const

export function StudentChangeRequestsPage() {
  const team = useMyTeam()
  const phases = usePhases()
  const requests = useTeamChangeRequests(team.data?.id)
  if (team.isLoading) return <p role="status" className="p-4">Đang tải nhóm…</p>
  if (team.error) return <ErrorState error={team.error} onRetry={() => void team.refetch()} />
  if (!team.data) return <EmptyState title="Bạn chưa có nhóm" description="Yêu cầu đổi nhóm chỉ áp dụng cho thành viên nhóm." />
  if (team.data.courseCode !== 'EXE201') return <EmptyState title="Yêu cầu đổi nhóm chỉ áp dụng cho EXE201" />
  if (requests.isLoading) return <p role="status" className="p-4">Đang tải yêu cầu…</p>
  if (requests.error) return <ErrorState error={requests.error} onRetry={() => void requests.refetch()} />

  return <>
    <PageHeader title="Yêu cầu đổi nhóm" />
    {team.data.myRelation === 'LEADER' && (phases.data?.find((phase) => phase.courseCode === 'EXE201')?.changeRequestRoundOpen ? <div className="mb-4"><Link to="/sv/yeu-cau-doi-nhom/tao"><Button>Gửi yêu cầu</Button></Link></div> : <p className="mb-4 text-text-secondary">Đợt đổi nhóm đã đóng; bạn vẫn có thể xem kết quả các yêu cầu trước.</p>)}
    {!requests.data?.length ? <EmptyState title="Chưa có yêu cầu đổi nhóm" description="Các yêu cầu của nhóm và kết quả duyệt sẽ xuất hiện tại đây." /> : (
      <ul className="space-y-3">
        {requests.data.map((request) => <li key={request.id} className="rounded-xl border border-border bg-surface p-4">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div><h2 className="font-semibold">{request.team.name}</h2><p className="mt-1 text-caption text-text-secondary">{request.roundName ?? 'Yêu cầu đổi nhóm'} · gửi lúc {new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(request.createdAt))}</p></div>
            <StatusChip tone={request.status === 'PENDING' ? 'warning' : request.status === 'APPROVED' ? 'success' : 'error'}>{statusLabel[request.status]}</StatusChip>
          </div>
          <p className="mt-3">{request.changedMemberCount} thay đổi · {request.changedPercent}% sĩ số mốc</p>
          <p className="mt-2 line-clamp-2 text-caption text-text-secondary">{request.reason}</p>
          <Link className="mt-3 inline-block text-primary-dark underline" to={`/sv/yeu-cau-doi-nhom/${request.id}`}>Xem chi tiết và kết quả</Link>
        </li>)}
      </ul>
    )}
  </>
}
