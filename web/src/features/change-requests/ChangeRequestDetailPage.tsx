import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { toApiError } from '@/shared/api/apiClient'
import { Button, Dialog, StatusChip } from '@/shared/components/ui'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { changeRequestKeys, fetchChangeRequest, useChangeRequestDecision, type ChangeStudent } from './api'

function MemberList({ title, members, comparison }: { title: string; members: ChangeStudent[]; comparison: ChangeStudent[] }) {
  return (
    <section className="rounded-md border border-border bg-surface p-4">
      <h2 className="mb-3 text-subtitle font-semibold">{title} <span className="text-text-secondary">({members.length})</span></h2>
      {members.length === 0 ? <p className="text-body text-text-secondary">Không có thành viên.</p> : (
        <ul className="divide-y divide-border">
          {members.map((member) => {
            const unchanged = comparison.some((item) => item.id === member.id)
            return <li key={member.id} className="flex items-start justify-between gap-3 py-3 first:pt-0 last:pb-0">
              <span><span className="block font-medium">{member.fullName}</span><span className="text-caption text-text-secondary">{member.studentCode}{member.specialization?.name ? ` · ${member.specialization.name}` : ''}</span></span>
              <StatusChip tone={unchanged ? 'neutral' : title === 'Thành viên sau thay đổi' ? 'success' : 'error'}>{unchanged ? 'Giữ nguyên' : title === 'Thành viên sau thay đổi' ? 'Thêm' : 'Rời nhóm'}</StatusChip>
            </li>
          })}
        </ul>
      )}
    </section>
  )
}

/** W18 – compare the EXE101 baseline with pending membership and project changes. */
export function ChangeRequestDetailPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const snackbar = useSnackbar()
  const decision = useChangeRequestDecision()
  const [dialog, setDialog] = useState<'approve' | 'reject' | null>(null)
  const [note, setNote] = useState('')
  const [validation, setValidation] = useState('')
  const request = useQuery({ queryKey: changeRequestKeys.detail(id), queryFn: () => fetchChangeRequest(id), enabled: !!id })

  if (request.isPending) return <><PageHeader title="Chi tiết yêu cầu đổi nhóm" /><div role="status" aria-label="Đang tải" className="space-y-3">{Array.from({ length: 6 }, (_, i) => <div key={i} className="h-12 animate-pulse rounded bg-neutral-container" />)}</div></>
  if (request.isError) return <><PageHeader title="Chi tiết yêu cầu đổi nhóm" /><ErrorState error={request.error} onRetry={() => void request.refetch()} /></>
  const item = request.data
  if (!item) return <EmptyState title="Không tìm thấy yêu cầu" />
  const pending = item.status === 'PENDING'
  const error = decision.error ? toApiError(decision.error) : null

  const submit = () => {
    if (dialog === 'reject' && !note.trim()) {
      setValidation('Nhập ghi chú để từ chối yêu cầu.')
      return
    }
    if (!dialog) return
    decision.mutate({ id, decision: dialog, note: note.trim() || undefined }, {
      onSuccess: () => {
        snackbar.show(dialog === 'approve' ? 'Đã duyệt yêu cầu đổi nhóm' : 'Đã từ chối yêu cầu đổi nhóm')
        setDialog(null)
        navigate(`/admin/yeu-cau-doi-nhom?trang-thai=${dialog === 'approve' ? 'APPROVED' : 'REJECTED'}`)
      },
    })
  }

  return (
    <>
      <PageHeader title={`Yêu cầu đổi nhóm · ${item.team.name}`} action={<Button variant="outline" onClick={() => navigate(-1)}>Quay lại</Button>} />
      <div className="space-y-5">
        <section className="rounded-md border border-border bg-surface p-4">
          <div className="flex flex-wrap items-center justify-between gap-3"><div><p className="text-caption text-text-secondary">Đợt {item.roundName} · EXE201</p><p className="mt-1 text-body">Gửi ngày {new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium' }).format(new Date(item.createdAt))}</p></div><StatusChip tone={item.status === 'PENDING' ? 'warning' : item.status === 'APPROVED' ? 'success' : 'error'}>{item.status === 'PENDING' ? 'Chờ duyệt' : item.status === 'APPROVED' ? 'Đã duyệt' : 'Đã từ chối'}</StatusChip></div>
          <h2 className="mt-4 font-semibold">Lý do đổi nhóm</h2><p className="mt-1 whitespace-pre-wrap">{item.reason}</p>
          <div className="mt-4 flex flex-wrap gap-4 text-body"><span><strong>{item.changedMemberCount}</strong> thay đổi thành viên</span><span><strong>{item.changedPercent}%</strong> so với mốc EXE101</span></div>
          {item.reviewNote && <p className="mt-3 rounded bg-background p-3 text-body"><strong>Ghi chú duyệt:</strong> {item.reviewNote}</p>}
        </section>

        <div className="grid gap-4 lg:grid-cols-2">
          <MemberList title="Mốc EXE101" members={item.baselineMembers} comparison={item.membersAfterChange} />
          <MemberList title="Thành viên sau thay đổi" members={item.membersAfterChange} comparison={item.baselineMembers} />
        </div>

        {(item.projectBefore || item.projectAfter) && <section className="rounded-md border border-border bg-surface p-4"><h2 className="text-subtitle font-semibold">Thay đổi đề tài</h2><div className="mt-3 grid gap-4 md:grid-cols-2"><div><p className="text-caption text-text-secondary">Đề tài hiện tại</p><p className="font-medium">{item.projectBefore?.name ?? 'Chưa có đề tài'}</p><p>{item.projectBefore?.description}</p></div><div><p className="text-caption text-text-secondary">Đề tài đề xuất</p><p className="font-medium">{item.projectAfter?.name ?? 'Giữ nguyên'}</p><p>{item.projectAfter?.description}</p></div></div></section>}

        {pending && <div className="sticky bottom-0 flex justify-end gap-3 border-t border-border bg-background/95 py-3"><Button variant="outline" onClick={() => { setDialog('reject'); setNote(''); setValidation('') }}>Từ chối</Button><Button onClick={() => { setDialog('approve'); setNote(''); setValidation('') }}>Duyệt</Button></div>}
      </div>

      <Dialog open={dialog !== null} onClose={() => setDialog(null)} title={dialog === 'approve' ? 'Duyệt yêu cầu đổi nhóm?' : 'Từ chối yêu cầu đổi nhóm?'} actions={<><Button variant="text" onClick={() => setDialog(null)}>Hủy</Button><Button variant={dialog === 'reject' ? 'danger' : 'primary'} loading={decision.isPending} onClick={submit}>{dialog === 'approve' ? 'Duyệt' : 'Từ chối'}</Button></>}>
        {dialog === 'approve' ? 'Thay đổi thành viên và đề tài sẽ được áp dụng cho nhóm.' : <label className="block">Ghi chú bắt buộc<textarea aria-label="Ghi chú từ chối" maxLength={1000} value={note} onChange={(e) => { setNote(e.target.value); setValidation('') }} className="mt-2 min-h-28 w-full rounded-sm border border-border-strong bg-surface p-3 text-body" /></label>}
        {validation && <p role="alert" className="mt-2 text-error">{validation}</p>}
        {error && <p role="alert" className="mt-2 text-error">{error.detail ?? error.title}</p>}
      </Dialog>
      {snackbar.element}
    </>
  )
}
