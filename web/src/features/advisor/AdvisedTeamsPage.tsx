import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { api, toApiError } from '@/shared/api/apiClient'
import { EmptyState, ErrorState, TableSkeleton } from '@/shared/components/States'
import { Banner, Button, Dialog, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { lecturerReasonLabel, mentorReasonLabel } from '@/features/my_team/advisorsApi'
import { useMyWorkspaces } from '@/features/workspace/api'
import { Card } from '@/features/workspace/components'
import { useAdvisorActivity, useAdvisorTeamDetail, useAllCheckpoints, useCheckpoint, useLecturerTeams, useMarkCheckpoint, useMentorTeams, useUndoCheckpoint, type AdvisedTeam, type AdvisorKind, type CourseCode } from './api'

const root = (kind: AdvisorKind) => kind === 'LECTURER' ? '/gv' : '/mentor'
const reasonText = (item: AdvisedTeam, mentor: boolean) => mentor ? mentorReasonLabel(item.reason) : lecturerReasonLabel(item.reason, item.preferenceRank)

export function AdvisedTeamsPage({ kind }: { kind: AdvisorKind }) {
  const [course, setCourse] = useState<CourseCode>('EXE101')
  const [missing, setMissing] = useState(0)
  const [search, setSearch] = useState('')
  const lecturer = useLecturerTeams(kind === 'LECTURER' ? course : undefined, kind === 'LECTURER')
  const mentor = useMentorTeams(kind === 'MENTOR')
  const checkpoints = useAllCheckpoints(kind === 'LECTURER' && course === 'EXE101')
  const workspaces = useMyWorkspaces()
  const activity = useAdvisorActivity(workspaces.data, kind === 'LECTURER')
  const loading = kind === 'LECTURER' ? lecturer.isLoading : mentor.isLoading
  const error = kind === 'LECTURER' ? lecturer.error : mentor.error
  const teams = kind === 'LECTURER'
    ? [...(lecturer.data?.instructing ?? []).map((item) => ({ item, mentor: false })), ...(lecturer.data?.mentoring ?? []).map((item) => ({ item, mentor: true }))]
    : (mentor.data ?? []).map((item) => ({ item, mentor: true }))
  const displayed = teams.filter(({ item }) => item.team.name.toLocaleLowerCase('vi').includes(search.toLocaleLowerCase('vi'))
    && (!missing || (checkpoints.data ?? []).some((progress) => progress.workspaceId === workspaces.data?.find((workspace) => workspace.teamId === item.team.id)?.id && progress.items.some((cp) => cp.checkpointNo === missing && !cp.presented))))
  return <>
    <PageHeader title={kind === 'LECTURER' ? 'Nhóm tôi hướng dẫn' : 'Nhóm đang mentor'} />
    <div className="space-y-4">
      {kind === 'LECTURER' && <div className="flex gap-2" aria-label="Chọn môn">{(['EXE101', 'EXE201'] as const).map((value) => <Button key={value} variant={course === value ? 'primary' : 'outline'} onClick={() => { setCourse(value); setMissing(0) }}>{value}</Button>)}</div>}
      <label className="block">Tìm nhóm<input className="mt-1 block w-full rounded-md border border-border bg-surface px-3 py-2" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Tên nhóm" /></label>
      {kind === 'LECTURER' && course === 'EXE101' && <label className="block">Lọc Checkpoint
        <select className="mt-1 block w-full rounded-md border border-border bg-surface px-3 py-2" value={missing} onChange={(event) => setMissing(Number(event.target.value))}>
          <option value={0}>Tất cả nhóm</option>{[1, 2, 3, 4].map((no) => <option key={no} value={no}>Chưa trình bày CP{no}</option>)}
        </select></label>}
      {loading && <TableSkeleton columns={3} rows={4} />}
      {error && <ErrorState error={error} onRetry={() => void (kind === 'LECTURER' ? lecturer.refetch() : mentor.refetch())} />}
      {workspaces.error && <ErrorState error={workspaces.error} onRetry={() => void workspaces.refetch()} />}
      {checkpoints.error && missing > 0 && <ErrorState error={checkpoints.error} onRetry={() => void checkpoints.refetch()} />}
      {activity.error && <ErrorState error={activity.error} onRetry={() => void activity.refetch()} />}
      {!loading && !(missing && checkpoints.isLoading) && !error && displayed.length === 0 && <EmptyState icon="groups" title={search || missing ? 'Không tìm thấy nhóm' : 'Chưa có nhóm được ghép'} />}
      <div className="grid gap-3 md:grid-cols-2">{displayed.map(({ item, mentor: isMentor }) => {
        const workspace = workspaces.data?.find((w) => w.teamId === item.team.id)
        const progress = workspace && checkpoints.data?.find((cp) => cp.workspaceId === workspace.id)
        const row = activity.data?.find((entry) => entry.workspaceId === workspace?.id)
        const pendingSubmissions = row?.deliverables.filter((task) => task.submissionStatus === 'PENDING').length ?? 0
        const completed = row?.tasks.filter((task) => task.status === 'DONE').length ?? 0
        return <Card key={`${item.team.id}-${isMentor}`}>
          <div className="flex items-start justify-between gap-2"><Link className="font-semibold text-primary-dark underline" to={`${root(kind)}/nhom/${item.team.id}`}>{item.team.name}</Link>{isMentor && <StatusChip tone="info">Mentor</StatusChip>}</div>
          <p className="text-text-secondary">{item.team.projectName || 'Chưa đặt tên đề tài'} · {item.team.memberCount} thành viên</p>
          {reasonText(item, isMentor) && <p className="mt-2"><StatusChip tone="info">{reasonText(item, isMentor)}</StatusChip></p>}
          {progress && <p className="mt-2">Checkpoint {progress.doneCount}/4</p>}
          {kind === 'LECTURER' && row && <p className="text-caption">Tiến độ việc: {completed}/{row.tasks.length} · {pendingSubmissions} bài nộp chờ duyệt</p>}
          {row?.meetings.items.find((meeting) => meeting.status === 'SCHEDULED' && meeting.startsAt && new Date(meeting.startsAt) > new Date())?.startsAt && <p className="text-caption">Họp sắp tới: {new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(row.meetings.items.find((meeting) => meeting.status === 'SCHEDULED' && meeting.startsAt && new Date(meeting.startsAt) > new Date())!.startsAt!))}</p>}
        </Card>
      })}</div>
    </div>
  </>
}

interface TeamSchedule { mySlot?: { dayOfWeek: number; startTime: string; endTime: string } | null }

export function AdvisedTeamDetailPage({ kind }: { kind: AdvisorKind }) {
  const { teamId } = useParams()
  const detail = useAdvisorTeamDetail(teamId)
  const workspaces = useMyWorkspaces()
  const workspace = workspaces.data?.find((item) => item.teamId === teamId)
  const activity = useAdvisorActivity(workspace ? [workspace] : undefined, kind === 'LECTURER')
  const row = activity.data?.[0]
  const checkpoint = useCheckpoint(kind === 'LECTURER' && detail.data?.courseCode === 'EXE101' ? workspace?.id : undefined)
  const schedule = useQuery({ queryKey: ['advisor', 'schedule', workspace?.id], queryFn: async () =>
    (await api.get<TeamSchedule | null>(`/api/workspaces/${workspace?.id}/meeting-schedule`)).data,
    enabled: kind === 'LECTURER' && detail.data?.courseCode === 'EXE201' && !!workspace?.id })
  const mark = useMarkCheckpoint(workspace?.id ?? '')
  const undo = useUndoCheckpoint(workspace?.id ?? '')
  const [open, setOpen] = useState(false)
  const [no, setNo] = useState(1)
  const [date, setDate] = useState(() => new Date().toLocaleDateString('en-CA'))
  const [comment, setComment] = useState('')
  const [actionError, setActionError] = useState<string | null>(null)
  const [lastMarked, setLastMarked] = useState<number | null>(null)
  useEffect(() => {
    if (lastMarked === null) return
    const timer = window.setTimeout(() => setLastMarked(null), 5000)
    return () => window.clearTimeout(timer)
  }, [lastMarked])
  const lecturer = useLecturerTeams(undefined, kind === 'LECTURER')
  const mentor = useMentorTeams(kind === 'MENTOR')
  const assignment = kind === 'LECTURER' ? [...(lecturer.data?.instructing ?? []), ...(lecturer.data?.mentoring ?? [])].find((item) => item.team.id === teamId) : mentor.data?.find((item) => item.team.id === teamId)
  const isMentor = kind === 'MENTOR' || lecturer.data?.mentoring.some((item) => item.team.id === teamId)

  const submit = async () => {
    if (!date || comment.length > 500) return
    setActionError(null)
    try { await mark.mutateAsync({ no, presentedOn: date, comment }); setOpen(false); setLastMarked(no); setComment('') }
    catch (error) { setActionError(toApiError(error).detail ?? toApiError(error).title) }
  }
  const undoLast = async () => {
    if (lastMarked === null) return
    setActionError(null)
    try { await undo.mutateAsync(lastMarked); setLastMarked(null) }
    catch (error) { setActionError(toApiError(error).detail ?? toApiError(error).title) }
  }

  return <>
    <PageHeader title="Chi tiết nhóm" />
    <Link className="mb-4 inline-block text-primary-dark underline" to={`${root(kind)}/nhom`}>← Về danh sách nhóm</Link>
    {(detail.isLoading || workspaces.isLoading) && <TableSkeleton columns={2} rows={4} />}
    {detail.error && <ErrorState error={detail.error} onRetry={() => void detail.refetch()} />}
    {workspaces.error && <ErrorState error={workspaces.error} onRetry={() => void workspaces.refetch()} />}
    {activity.error && <ErrorState error={activity.error} onRetry={() => void activity.refetch()} />}
    {actionError && <Banner tone="error" title={actionError} />}
    {lastMarked !== null && checkpoint.data?.items.some((cp) => cp.checkpointNo === lastMarked && cp.canUndo) && <Banner tone="success" title={`Đã ghi nhận Checkpoint ${lastMarked}`} action={<Button variant="text" onClick={() => void undoLast()}>Hoàn tác</Button>} />}
    {detail.data && <div className="space-y-4"><Card>
      <h2 className="text-title font-semibold">{detail.data.name}</h2>
      <p>{detail.data.projectName || 'Chưa đặt tên đề tài'} · {detail.data.courseCode}</p>
      {assignment && <p className="mt-2"><StatusChip tone="info">{reasonText(assignment, !!isMentor) || 'Đã ghép'}</StatusChip></p>}
      {isMentor && <p className="mt-2"><StatusChip tone="info">Mentor</StatusChip></p>}
      <p className="mt-3">{detail.data.ideaDescription || 'Nhóm chưa mô tả đề tài.'}</p>
      <h3 className="mt-4 font-semibold">Thành viên</h3><ul>{detail.data.members.map((member) => <li key={member.id}>{member.fullName} · {member.specialization?.name}</li>)}</ul>
    </Card>
      {workspace && <Card><h3 className="font-semibold">Dự án</h3>
        {kind === 'LECTURER' && row && <p>Việc hoàn thành: {row.tasks.filter((task) => task.status === 'DONE').length}/{row.tasks.length} · Bài nộp chờ duyệt: {row.deliverables.filter((task) => task.submissionStatus === 'PENDING').length}</p>}
        <Link className="text-primary-dark underline" to={`${root(kind)}/du-an/${workspace.id}`}>Mở không gian làm việc</Link>
      </Card>}
      {detail.data.courseCode === 'EXE101' && kind === 'LECTURER' && !isMentor && <Card><h3 className="font-semibold">Checkpoint {checkpoint.data?.doneCount ?? 0}/4</h3>
        {checkpoint.isLoading && <TableSkeleton columns={2} rows={4} />}
        {checkpoint.error && <ErrorState error={checkpoint.error} onRetry={() => void checkpoint.refetch()} />}
        <ul className="mt-2 space-y-2">{checkpoint.data?.items.map((cp) => <li key={cp.checkpointNo}><StatusChip tone={cp.presented ? 'success' : cp.overdue ? 'warning' : 'neutral'}>CP{cp.checkpointNo} · {cp.presented ? 'Đã trình bày' : 'Chưa trình bày'}</StatusChip>{cp.comment && <p className="ml-2 text-text-secondary">{cp.comment}</p>}</li>)}</ul>
        <Button className="mt-4" disabled={!workspace || !checkpoint.data?.items.some((cp) => !cp.presented)} onClick={() => { setNo(checkpoint.data?.items.find((cp) => !cp.presented)?.checkpointNo ?? 1); setOpen(true) }}>Đánh dấu Checkpoint</Button>
      </Card>}
      {detail.data.courseCode === 'EXE201' && kind === 'LECTURER' && !isMentor && <Card><h3 className="font-semibold">Lịch họp hằng tuần</h3>
        {schedule.isLoading && <p>Đang tải lịch…</p>}
        {schedule.error && <ErrorState error={schedule.error} onRetry={() => void schedule.refetch()} />}
        {schedule.data?.mySlot ? <p>Thứ {schedule.data.mySlot.dayOfWeek} · {schedule.data.mySlot.startTime}–{schedule.data.mySlot.endTime}</p> : <p>Nhóm chưa chốt lịch.</p>}
      </Card>}
    </div>}
    <Dialog open={open} onClose={() => setOpen(false)} title="Đánh dấu Checkpoint" actions={<><Button variant="text" onClick={() => setOpen(false)}>Hủy</Button><Button loading={mark.isPending} onClick={() => void submit()}>Lưu</Button></>}>
      <div className="space-y-3">
      <label className="block">Checkpoint<select className="mt-1 w-full rounded-md border border-border p-2" value={no} onChange={(event) => setNo(Number(event.target.value))}>{checkpoint.data?.items.filter((cp) => !cp.presented).map((cp) => <option key={cp.checkpointNo} value={cp.checkpointNo}>CP{cp.checkpointNo}</option>)}</select></label>
      <label className="block">Ngày trình bày<input className="mt-1 w-full rounded-md border border-border p-2" type="date" value={date} onChange={(event) => setDate(event.target.value)} /></label>
      <label className="block">Nhận xét (tối đa 500 ký tự)<textarea className="mt-1 w-full rounded-md border border-border p-2" maxLength={500} value={comment} onChange={(event) => setComment(event.target.value)} /></label>
      {actionError && <p role="alert" className="text-error">{actionError}</p>}
      </div>
    </Dialog>
  </>
}
