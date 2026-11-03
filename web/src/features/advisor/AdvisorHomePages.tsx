import { useState } from 'react'
import { Link } from 'react-router-dom'
import { toApiError } from '@/shared/api/apiClient'
import { EmptyState, ErrorState, TableSkeleton } from '@/shared/components/States'
import { Banner, Button } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { usePhases } from '@/shared/phase/usePhase'
import { useMyWorkspaces } from '@/features/workspace/api'
import { Card } from '@/features/workspace/components'
import { useAdvisorActivity, useAdvisorQuestions, useAllCheckpoints, useLecturerPreferences, useLecturerTeams, useMentorProfile, useMentorTeams, useSavePreferredTeams, useTopics } from './api'

function Metric({ to, value, label }: { to: string; value: number; label: string }) {
  return <Link to={to} className="rounded-xl border border-border bg-surface p-4 hover:border-primary"><strong className="block text-title text-primary-dark">{value}</strong><span>{label}</span></Link>
}

export function LecturerHomePage() {
  const [course, setCourse] = useState<'EXE101' | 'EXE201'>('EXE101')
  const phases = usePhases()
  const forming = phases.data?.find((phase) => phase.courseCode === course)?.phase === 'TEAM_FORMATION'
  const teams = useLecturerTeams()
  const chosen = useLecturerPreferences(course)
  const topics101 = useTopics('LECTURER', 'EXE101')
  const topics201 = useTopics('LECTURER', 'EXE201')
  const workspaces = useMyWorkspaces()
  const questionsQuery = useAdvisorQuestions()
  const activity = useAdvisorActivity(workspaces.data, true)
  const checkpoints = useAllCheckpoints()
  const now = new Date()
  const todayMeetings = activity.data?.flatMap((row) => row.meetings.items).filter((meeting) => meeting.status === 'SCHEDULED' && meeting.startsAt && new Date(meeting.startsAt).toDateString() === now.toDateString()).length ?? 0
  const questions = questionsQuery.data ?? 0
  const overdue = activity.data?.flatMap((row) => row.deliverables).filter((item) => item.isOverdue && item.submissionStatus !== 'ACCEPTED').length ?? 0
  const missingCheckpoint = checkpoints.data?.reduce((total, progress) => total + (progress.items.some((item) => item.overdue && !item.presented) ? 1 : 0), 0) ?? 0
  const hasExe201 = teams.data?.instructing.some((team) => team.team.courseCode === 'EXE201')
  const selectedTopics = course === 'EXE101' ? topics101 : topics201

  return <><PageHeader title="Trang chủ giảng viên" /><div className="space-y-4">
    <div className="flex gap-2" aria-label="Chọn môn">{(['EXE101', 'EXE201'] as const).map((value) => <Button key={value} variant={course === value ? 'primary' : 'outline'} onClick={() => setCourse(value)}>{value}</Button>)}</div>
    {(teams.isLoading || phases.isLoading) && <TableSkeleton columns={3} rows={3} />}
    {teams.error && <ErrorState error={teams.error} onRetry={() => void teams.refetch()} />}
    {workspaces.error && <ErrorState error={workspaces.error} onRetry={() => void workspaces.refetch()} />}
    {questionsQuery.error && <ErrorState error={questionsQuery.error} onRetry={() => void questionsQuery.refetch()} />}
    {activity.error && <ErrorState error={activity.error} onRetry={() => void activity.refetch()} />}
    {forming ? <>
      <Banner tone="info" title={`${chosen.data?.length ?? 0} nhóm chọn tôi · Đang quan tâm ${selectedTopics.data?.openInterestCount ?? 0}/${selectedTopics.data?.interestLimit ?? 0}`}>
        {selectedTopics.data?.matchingAt && `Ghép lúc ${new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(selectedTopics.data.matchingAt))}`}
      </Banner>
      <div className="flex flex-wrap gap-2"><Link className="text-primary-dark underline" to="/gv/de-tai">Xem đề tài và nhóm chọn tôi</Link><Link className="text-primary-dark underline" to="/gv/ho-so">Cập nhật chuyên môn</Link></div>
    </> : <>
      <div className="grid gap-3 sm:grid-cols-3"><Metric to="/gv/tin-nhan" value={questions} label="Cần trả lời" /><Metric to="/gv/nhom" value={overdue} label="Trễ hạn nộp" /><Metric to={hasExe201 ? '/gv/lich' : '/gv/nhom'} value={hasExe201 ? todayMeetings : missingCheckpoint} label={hasExe201 ? 'Họp hôm nay' : 'Nhóm chưa trình bày CP'} /></div>
      {hasExe201 && <Card><h2 className="font-semibold">Lịch họp hằng tuần</h2><p className="text-text-secondary">Đăng khung họp cho nhóm EXE201 và theo dõi lịch đã đặt.</p><Link className="text-primary-dark underline" to="/gv/lich">Mở lịch họp</Link></Card>}
    </>}
    <Card><h2 className="font-semibold">Nhóm của tôi</h2><p>{teams.data?.instructing.length ?? 0} nhóm đang hướng dẫn</p><Link className="text-primary-dark underline" to="/gv/nhom">Xem danh sách nhóm</Link></Card>
  </div></>
}

export function MentorHomePage() {
  const phases = usePhases()
  const start = phases.data?.map((phase) => phase.mentoringStartsOn).filter(Boolean).sort()[0]
  const beforeWeekSix = !!start && new Date(start) > new Date()
  const profile = useMentorProfile()
  const topics101 = useTopics('MENTOR', 'EXE101')
  const topics201 = useTopics('MENTOR', 'EXE201')
  const teams = useMentorTeams()
  const workspaces = useMyWorkspaces()
  const questionsQuery = useAdvisorQuestions()
  const activity = useAdvisorActivity(workspaces.data, false)
  const save = useSavePreferredTeams()
  const [editing, setEditing] = useState(false)
  const [preferred, setPreferred] = useState(1)
  const [error, setError] = useState<string | null>(null)
  const questions = questionsQuery.data ?? 0
  const meetings = activity.data?.flatMap((row) => row.meetings.items).filter((meeting) => meeting.status === 'SCHEDULED' && meeting.startsAt && new Date(meeting.startsAt) > new Date()).length ?? 0
  const pending = activity.data?.flatMap((row) => row.meetings.items).filter((meeting) => meeting.status === 'PROPOSED').length ?? 0
  const updatePreferred = async () => {
    setError(null)
    try { await save.mutateAsync(preferred); setEditing(false) }
    catch (cause) { setError(toApiError(cause).detail ?? toApiError(cause).title) }
  }

  return <><PageHeader title="Trang chủ mentor" /><div className="space-y-4">
    {(profile.isLoading || teams.isLoading || phases.isLoading) && <TableSkeleton columns={3} rows={3} />}
    {profile.error && <ErrorState error={profile.error} onRetry={() => void profile.refetch()} />}
    {teams.error && <ErrorState error={teams.error} onRetry={() => void teams.refetch()} />}
    {workspaces.error && <ErrorState error={workspaces.error} onRetry={() => void workspaces.refetch()} />}
    {questionsQuery.error && <ErrorState error={questionsQuery.error} onRetry={() => void questionsQuery.refetch()} />}
    {activity.error && <ErrorState error={activity.error} onRetry={() => void activity.refetch()} />}
    {error && <Banner tone="error" title={error} />}
    {profile.data && <Card><h2 className="font-semibold">Xin chào, {profile.data.fullName}</h2>
      <p>Đang nhận {profile.data.currentTeamCount} nhóm · mong muốn {profile.data.preferredTeams ?? profile.data.maxTeams} · tối đa {profile.data.maxTeams}</p>
      {!editing ? <Button variant="text" onClick={() => { setPreferred(profile.data?.preferredTeams ?? profile.data?.maxTeams ?? 1); setEditing(true) }}>Sửa số nhóm mong muốn</Button>
        : <div className="mt-2 flex flex-wrap items-end gap-2"><label>Số nhóm mong muốn<input className="ml-2 w-20 rounded-md border border-border p-2" type="number" min={1} max={profile.data.maxTeams} value={preferred} onChange={(event) => setPreferred(Number(event.target.value))} /></label><Button disabled={preferred < 1 || preferred > profile.data.maxTeams} loading={save.isPending} onClick={() => void updatePreferred()}>Lưu</Button><Button variant="text" onClick={() => setEditing(false)}>Hủy</Button></div>}
    </Card>}
    {beforeWeekSix ? <Card><h2 className="font-semibold">Đang quan tâm {(topics101.data?.openInterestCount ?? 0) + (topics201.data?.openInterestCount ?? 0)} đề tài · Ghép vào tuần 6</h2><Link className="text-primary-dark underline" to="/mentor/de-tai">Xem đề tài</Link></Card>
      : teams.data?.length === 0 ? <EmptyState icon="groups" title="Bộ môn đang hoàn tất ghép mentor" /> : <>
        <div className="grid gap-3 sm:grid-cols-3"><Metric to="/mentor/tin-nhan" value={questions} label="Cần trả lời" /><Metric to="/mentor/nhom" value={meetings} label="Họp sắp tới" /><Metric to="/mentor/nhom" value={pending} label="Chờ chốt lịch" /></div>
        <Card><h2 className="font-semibold">Nhóm đang mentor</h2><ul className="mt-2 space-y-2">{teams.data?.map((item) => {
          const workspace = workspaces.data?.find((w) => w.teamId === item.team.id)
          const session = activity.data?.find((row) => row.workspaceId === workspace?.id)?.meetings
          return <li key={item.team.id}><Link className="text-primary-dark underline" to={`/mentor/nhom/${item.team.id}`}>{item.team.name}</Link> · {item.team.projectName || 'Chưa đặt tên đề tài'}{session && <span> · Buổi mentor {session.mentorSessionsDone}/{session.expectedMentorSessions}</span>}</li>
        })}</ul></Card>
      </>}
  </div></>
}
