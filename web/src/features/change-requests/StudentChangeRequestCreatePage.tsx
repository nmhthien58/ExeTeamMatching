import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useMyTeam } from '@/features/suggestions/api'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Button } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { usePhases } from '@/shared/phase/usePhase'
import { usePendingTeamChanges, useSetProjectChange, useSubmitTeamChangeRequest } from './api'

const labels: Record<string, string> = { ADD: 'Thêm thành viên', REMOVE: 'Rời nhóm', SET_LEADER: 'Đổi trưởng nhóm', PROJECT_CHANGE: 'Đổi đề tài' }

export function StudentChangeRequestCreatePage() {
  const navigate = useNavigate()
  const team = useMyTeam()
  const phases = usePhases()
  const teamId = team.data?.id
  const pending = usePendingTeamChanges(teamId)
  const setProject = useSetProjectChange(teamId ?? '')
  const submit = useSubmitTeamChangeRequest(teamId ?? '')
  const [selected, setSelected] = useState<string[]>([])
  const [step, setStep] = useState<1 | 2>(1)
  const [reason, setReason] = useState('')
  const [projectName, setProjectName] = useState('')
  const [projectDescription, setProjectDescription] = useState('')
  const [confirming, setConfirming] = useState(false)

  if (team.isLoading) return <p role="status">Đang tải nhóm…</p>
  if (team.error) return <ErrorState error={team.error} onRetry={() => void team.refetch()} />
  if (!team.data) return <EmptyState title="Bạn chưa có nhóm" />
  if (team.data.courseCode !== 'EXE201') return <EmptyState title="Chỉ nhóm EXE201 được gửi yêu cầu đổi nhóm" />
  if (team.data.myRelation !== 'LEADER') return <EmptyState title="Chỉ trưởng nhóm được gửi yêu cầu" description="Bạn vẫn có thể xem yêu cầu hiện có của nhóm." />
  if (phases.isLoading) return <p role="status">Đang kiểm tra đợt đổi nhóm…</p>
  if (phases.error) return <ErrorState error={phases.error} onRetry={() => void phases.refetch()} />
  if (!phases.data?.find((phase) => phase.courseCode === 'EXE201')?.changeRequestRoundOpen) return <EmptyState title="Đợt đổi nhóm đã đóng" description="Bạn vẫn có thể xem kết quả các yêu cầu trước." />
  if (pending.isLoading) return <p role="status">Đang tải thay đổi chờ duyệt…</p>
  if (pending.error) return <ErrorState error={pending.error} onRetry={() => void pending.refetch()} />

  const items = pending.data?.pendingItems ?? []
  const chosen = items.filter((item) => selected.includes(item.id))
  const memberCount = team.data.memberCount + chosen.filter((item) => item.action === 'ADD').length - chosen.filter((item) => item.action === 'REMOVE').length
  const invalidSize = memberCount < 4 || memberCount > 6
  const projectedMembers = new Map(team.data.members.map((member) => [member.id, member.specialization?.code]))
  for (const item of chosen) {
    if (item.action === 'ADD' && item.student) projectedMembers.set(item.student.id, item.student.specialization?.code)
    if (item.action === 'REMOVE' && item.student) projectedMembers.delete(item.student.id)
  }
  const specializations = [...projectedMembers.values()]
  const lacksSpecialization = specializations.every(Boolean) && new Set(specializations).size < 2
  const changedMembers = chosen.filter((item) => item.action === 'ADD' || item.action === 'REMOVE').length
  const baselineSize = pending.data?.baselineMembers.length ?? 0
  const changedPercent = baselineSize ? Math.round(changedMembers * 10000 / baselineSize) / 100 : 0
  const toggle = (id: string) => setSelected((current) => current.includes(id) ? current.filter((item) => item !== id) : [...current, id])
  const saveProject = async () => {
    try {
      const item = await setProject.mutateAsync({ name: projectName.trim(), description: projectDescription.trim() })
      setSelected((current) => current.includes(item.id) ? current : [...current, item.id])
      setProjectName('')
      setProjectDescription('')
    } catch { /* Error is shown beside the form. */ }
  }
  const send = async () => {
    setConfirming(false)
    try {
      const created = await submit.mutateAsync({ itemIds: chosen.map((item) => item.id), reason: reason.trim() })
      navigate(`/sv/yeu-cau-doi-nhom/${created.id}`, { replace: true })
    } catch { /* Keep inputs for retry. */ }
  }

  return <>
    <PageHeader title="Gửi yêu cầu đổi nhóm" />
    <section className="max-w-3xl space-y-4 rounded-xl border border-border bg-surface p-4">
      <p className="font-semibold">Bước {step}/2 · {step === 1 ? 'Chọn thay đổi' : 'Lý do và xác nhận'}</p>
      {step === 1 ? <>
        <div><h2 className="font-semibold">Mốc EXE101</h2><ul className="mt-2 list-inside list-disc">{pending.data?.baselineMembers.map((member) => <li key={member.id}>{member.fullName} ({member.studentCode})</li>)}</ul></div>
        {!items.length ? <EmptyState title="Chưa có thay đổi để gửi" description="Tạo thay đổi thành viên hoặc đề tài trước." /> : <fieldset className="space-y-2"><legend className="font-semibold">Thay đổi chờ duyệt</legend>{items.map((item) => <label key={item.id} className="flex cursor-pointer items-start gap-3 rounded-lg border border-border p-3"><input type="checkbox" checked={selected.includes(item.id)} onChange={() => toggle(item.id)} /><span><span className="font-medium">{labels[item.action] ?? item.action}</span><span className="block text-text-secondary">{item.student ? `${item.student.fullName} (${item.student.studentCode})` : item.newProjectName ?? item.source}</span></span></label>)}</fieldset>}
        <fieldset className="space-y-2 rounded-lg bg-background p-4"><legend className="font-semibold">Đổi đề tài</legend><label className="block">Tên đề tài mới<input className="mt-1 block w-full rounded-lg border border-border-strong p-2" maxLength={200} value={projectName} onChange={(event) => setProjectName(event.target.value)} /></label><label className="block">Mô tả<textarea className="mt-1 block min-h-20 w-full rounded-lg border border-border-strong p-2" maxLength={4000} value={projectDescription} onChange={(event) => setProjectDescription(event.target.value)} /></label><Button variant="outline" disabled={projectName.trim().length < 2 || setProject.isPending} onClick={() => void saveProject()}>Lưu thay đổi đề tài</Button>{setProject.error && <p role="alert" className="text-error">Không lưu được đề tài. Kiểm tra đợt đổi nhóm rồi thử lại.</p>}</fieldset>
        {chosen.length > 0 && <p className={invalidSize ? 'text-error' : 'text-text-secondary'}>Sau thay đổi: {memberCount} thành viên. {invalidSize ? 'Nhóm cần từ 4 đến 6 thành viên.' : ''}</p>}
        {chosen.length > 0 && lacksSpecialization && <p className="rounded-lg bg-warning-container p-3 text-warning">Nhóm đang thiếu chuyên ngành thứ hai; Bộ môn sẽ xem lại khi chốt nhóm.</p>}
        <div className="flex gap-2"><Button disabled={!chosen.length || invalidSize} onClick={() => setStep(2)}>Tiếp tục</Button><Link to="/sv/yeu-cau-doi-nhom" className="self-center text-primary-dark underline">Hủy</Link></div>
      </> : <>
        <div className="grid gap-3 sm:grid-cols-2"><p className="rounded-lg bg-background p-4"><strong className="block text-title">{changedMembers}</strong>Thành viên thay đổi</p><p className="rounded-lg bg-background p-4"><strong className="block text-title">{changedPercent}%</strong>Tỷ lệ thay đổi so với mốc</p></div>
        <div><h2 className="font-semibold">Xem lại thay đổi</h2><ul className="mt-2 list-inside list-disc">{chosen.map((item) => <li key={item.id}>{labels[item.action] ?? item.action}: {item.student?.fullName ?? item.newProjectName ?? item.source}</li>)}</ul></div>
        <label className="block" htmlFor="change-request-reason"><span className="font-semibold">Lý do thay đổi</span><textarea id="change-request-reason" aria-label="Lý do thay đổi" className="mt-2 min-h-28 w-full rounded-lg border border-border-strong p-3" maxLength={1000} value={reason} onChange={(event) => setReason(event.target.value)} placeholder="Nêu lý do (10–1000 ký tự)" /><span className="block text-caption text-text-secondary">{reason.trim().length}/1000</span></label>
        {reason.length > 0 && reason.trim().length < 10 && <p role="alert" className="text-error">Lý do cần ít nhất 10 ký tự.</p>}
        {submit.error && <p role="alert" className="text-error">Không gửi được yêu cầu. Nội dung bạn nhập vẫn được giữ để thử lại.</p>}
        <div className="flex gap-2"><Button variant="outline" onClick={() => setStep(1)}>Quay lại</Button><Button disabled={reason.trim().length < 10 || !chosen.length || invalidSize || submit.isPending} onClick={() => setConfirming(true)}>Gửi yêu cầu đổi nhóm</Button></div>
      </>}
    </section>
    {confirming && <div role="presentation" className="fixed inset-0 z-50 grid place-items-center bg-scrim/40 p-6"><div role="alertdialog" aria-modal="true" aria-labelledby="confirm-title" className="w-full max-w-md rounded-xl bg-surface p-6"><h2 id="confirm-title" className="text-title font-semibold">Gửi yêu cầu đổi nhóm?</h2><p className="mt-2">Bộ môn sẽ xem xét {chosen.length} thay đổi. Sau khi gửi, bạn không thể sửa yêu cầu này.</p><div className="mt-5 flex justify-end gap-2"><Button variant="outline" onClick={() => setConfirming(false)}>Hủy</Button><Button onClick={() => void send()}>Xác nhận gửi</Button></div></div></div>}
  </>
}
