import { Link, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { fetchChangeRequest, changeRequestKeys } from './api'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { PageHeader } from '@/shared/layout/PageHeader'
import { Button, StatusChip } from '@/shared/components/ui'

const labels: Record<string, string> = { ADD: 'Thêm thành viên', REMOVE: 'Rời nhóm', SET_LEADER: 'Đổi trưởng nhóm', PROJECT_CHANGE: 'Đổi đề tài' }

export function StudentChangeRequestDetailPage() {
  const { id = '' } = useParams()
  const detail = useQuery({ queryKey: changeRequestKeys.detail(id), queryFn: () => fetchChangeRequest(id), enabled: !!id })
  if (detail.isLoading) return <p role="status" className="p-4">Đang tải yêu cầu…</p>
  if (detail.error) return <ErrorState error={detail.error} onRetry={() => void detail.refetch()} />
  if (!detail.data) return <EmptyState title="Không tìm thấy yêu cầu" />
  const request = detail.data
  const approved = request.status === 'APPROVED'
  return <>
    <PageHeader title="Chi tiết yêu cầu đổi nhóm" />
    <div className="mb-4"><Button variant="outline" icon="refresh" onClick={() => void detail.refetch()}>Làm mới trạng thái</Button></div>
    <section className="max-w-3xl space-y-4 rounded-xl border border-border bg-surface p-4">
      <div className="flex flex-wrap items-center justify-between gap-3"><h2 className="text-title font-semibold">{request.team.name}</h2><StatusChip tone={request.status === 'PENDING' ? 'warning' : approved ? 'success' : 'error'}>{request.status === 'PENDING' ? 'Chờ duyệt' : approved ? 'Đã duyệt' : 'Đã từ chối'}</StatusChip></div>
      <p><span className="font-medium">Đợt:</span> {request.roundName ?? '—'}</p>
      <ol aria-label="Tiến trình xử lý" className="space-y-2 border-l-2 border-primary pl-4">
        <li>Đã gửi · {new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(request.createdAt))}</li>
        <li>{request.status === 'PENDING' ? 'Đang chờ Bộ môn duyệt' : 'Bộ môn đã xử lý'}</li>
        {request.reviewedAt && <li>{approved ? 'Đã duyệt' : 'Đã từ chối'} · {new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(request.reviewedAt))}{request.reviewedByName ? ` · ${request.reviewedByName}` : ''}</li>}
      </ol>
      <p><span className="font-medium">Lý do:</span> {request.reason}</p>
      <p><span className="font-medium">Thay đổi:</span> {request.changedMemberCount} · {request.changedPercent}% sĩ số mốc</p>
      <div><h3 className="font-semibold">Mốc ban đầu</h3><ul className="mt-2 list-inside list-disc">{request.baselineMembers.map((member) => <li key={member.id}>{member.fullName} ({member.studentCode})</li>)}</ul></div>
      <div><h3 className="font-semibold">{approved ? 'Sau khi áp dụng' : 'Thành viên đề xuất'}</h3><ul className="mt-2 list-inside list-disc">{request.membersAfterChange.map((member) => <li key={member.id}>{member.fullName} ({member.studentCode})</li>)}</ul></div>
      <div><h3 className="font-semibold">Dòng thời gian thay đổi</h3><ol className="mt-2 space-y-2">{request.items.map((item) => <li key={item.id} className="rounded-lg bg-background p-3"><span className="font-medium">{labels[item.action] ?? item.action}</span><span className="ml-2 text-caption text-text-secondary">{item.student?.fullName ?? item.newProjectName ?? item.source}</span></li>)}</ol></div>
      {request.projectAfter && <p><span className="font-medium">{approved ? 'Đề tài sau thay đổi:' : 'Đề tài đề xuất:'}</span> {request.projectAfter.name}</p>}
      {request.status !== 'PENDING' && <div className={`rounded-lg p-3 ${approved ? 'bg-success-container' : 'bg-error-container'}`}><h3 className="font-semibold">{approved ? 'Kết quả duyệt' : 'Lý do từ chối'}</h3><p>{request.reviewNote || (approved ? 'Yêu cầu đã được chấp thuận.' : 'Bộ môn chưa ghi chú.')}</p>{request.reviewedByName && <p className="mt-1 text-caption">Người xử lý: {request.reviewedByName}</p>}</div>}
      {request.status === 'REJECTED' && <Link to="/sv/yeu-cau-doi-nhom/tao" className="inline-block rounded-md bg-primary px-4 py-2 font-medium text-white">Tạo yêu cầu mới</Link>}
      <Link to="/sv/yeu-cau-doi-nhom" className="inline-block text-primary-dark underline">Quay lại danh sách</Link>
    </section>
  </>
}
