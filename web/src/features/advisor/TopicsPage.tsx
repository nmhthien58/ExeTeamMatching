import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { toApiError } from '@/shared/api/apiClient'
import { EmptyState, ErrorState, TableSkeleton } from '@/shared/components/States'
import { Banner, Button, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { Card } from '@/features/workspace/components'
import { useAdvisorDomains, useAdvisorTeamDetail, useInterestedTeams, useInterests, useLecturerPreferences, useToggleInterest, useTopics, type AdvisorKind, type CourseCode, type TopicRecommendation } from './api'

const root = (kind: AdvisorKind) => kind === 'LECTURER' ? '/gv' : '/mentor'
const dateText = (value: string) => new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value))

export function TopicsPage({ kind }: { kind: AdvisorKind }) {
  const [course, setCourse] = useState<CourseCode>('EXE101')
  const [tab, setTab] = useState<'suggested' | 'all' | 'chosen' | 'interested'>('suggested')
  const [undoTeamId, setUndoTeamId] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const topics = useTopics(kind, course)
  const allTopics = useTopics(kind, course, tab === 'all', 'all')
  const interests = useInterests(kind)
  const interestedTeams = useInterestedTeams(interests.data, tab === 'interested')
  const preferences = useLecturerPreferences(kind === 'LECTURER' ? course : undefined, kind === 'LECTURER')
  const domains = useAdvisorDomains(kind)
  const toggle = useToggleInterest(kind, course)
  const hasDomains = !!domains.data?.length
  const profileLoading = domains.isLoading

  useEffect(() => {
    if (!undoTeamId) return
    const timer = window.setTimeout(() => setUndoTeamId(null), 5000)
    return () => window.clearTimeout(timer)
  }, [undoTeamId])

  const act = async (teamId: string, interestId?: string) => {
    setActionError(null)
    try {
      await toggle.mutateAsync({ teamId, interestId })
      setUndoTeamId(interestId ? teamId : null)
    } catch (error) {
      setActionError(toApiError(error).detail ?? toApiError(error).title)
    }
  }

  const items = topics.data?.items ?? []
  const displayed = tab === 'suggested' ? items
    : tab === 'all' ? allTopics.data?.items ?? []
    : tab === 'chosen' ? (preferences.data ?? []).map((entry) => items.find((item) => item.team.id === entry.team.id) ?? {
      team: entry.team, matchedDomains: [], otherInterestedCount: 0, myPreferenceRank: entry.rank,
      myInterest: interests.data?.find((i) => i.teamId === entry.team.id && i.status === 'OPEN') ?? null,
      wasMyEXE101Team: false, hasAdvisor: !!entry.team.lecturerName,
    })
      : (interestedTeams.data ?? []).filter((team) => team.courseCode === course).map((team) => items.find((item) => item.team.id === team.id) ?? {
        team, matchedDomains: [], otherInterestedCount: 0, myInterest: interests.data?.find((i) => i.teamId === team.id),
        wasMyEXE101Team: false, hasAdvisor: kind === 'LECTURER' ? !!team.lecturerName : !!team.mentorName,
      })

  return <>
    <PageHeader title="Đề tài" />
    <div className="space-y-4">
      <div className="flex gap-2" aria-label="Chọn môn">
        {(['EXE101', 'EXE201'] as const).map((value) => <Button key={value} variant={course === value ? 'primary' : 'outline'} onClick={() => setCourse(value)}>{value}</Button>)}
      </div>
      {topics.error && <ErrorState error={topics.error} onRetry={() => void topics.refetch()} />}
      {tab === 'all' && allTopics.error && <ErrorState error={allTopics.error} onRetry={() => void allTopics.refetch()} />}
      {domains.error && <ErrorState error={domains.error} onRetry={() => void domains.refetch()} />}
      {tab === 'chosen' && preferences.error && <ErrorState error={preferences.error} onRetry={() => void preferences.refetch()} />}
      {tab === 'interested' && interests.error && <ErrorState error={interests.error} onRetry={() => void interests.refetch()} />}
      {tab === 'interested' && interestedTeams.error && <ErrorState error={interestedTeams.error} onRetry={() => void interestedTeams.refetch()} />}
      {actionError && <Banner tone="error" title={actionError} />}
      {undoTeamId && <Banner tone="success" title="Đã rút quan tâm" action={<Button variant="text" onClick={() => void act(undoTeamId)}>Hoàn tác</Button>} />}
      {topics.data && <Banner tone={topics.data.interestOpen ? 'info' : 'warning'} title={`Đang quan tâm ${topics.data.openInterestCount}/${topics.data.interestLimit}`}>
        {topics.data.interestOpen ? (topics.data.matchingAt ? `Ghép lúc ${dateText(topics.data.matchingAt)}. Quan tâm không giữ chỗ.` : 'Quan tâm không giữ chỗ; hệ thống sẽ ghép theo các tín hiệu.') : 'Ngoài giai đoạn quan tâm. Bạn vẫn có thể xem đề tài.'}
      </Banner>}
      <div role="tablist" aria-label="Đề tài" className="flex flex-wrap gap-2">
        <Button role="tab" aria-selected={tab === 'suggested'} variant={tab === 'suggested' ? 'primary' : 'outline'} onClick={() => setTab('suggested')}>Gợi ý</Button>
        <Button role="tab" aria-selected={tab === 'all'} variant={tab === 'all' ? 'primary' : 'outline'} onClick={() => setTab('all')}>Tất cả nhóm</Button>
        {kind === 'LECTURER' && <Button role="tab" aria-selected={tab === 'chosen'} variant={tab === 'chosen' ? 'primary' : 'outline'} onClick={() => setTab('chosen')}>Nhóm chọn tôi</Button>}
        <Button role="tab" aria-selected={tab === 'interested'} variant={tab === 'interested' ? 'primary' : 'outline'} onClick={() => setTab('interested')}>Đã quan tâm</Button>
      </div>
      {(topics.isLoading || profileLoading || (tab === 'all' && allTopics.isLoading) || (tab === 'chosen' && preferences.isLoading) || (tab === 'interested' && (interests.isLoading || interestedTeams.isLoading))) && <TableSkeleton columns={2} rows={3} />}
      {!topics.isLoading && !profileLoading && !hasDomains && tab !== 'all' && !topics.error && !domains.error && <EmptyState icon="school" title="Chưa khai báo chuyên môn" description="Cập nhật chuyên môn để xem đề tài phù hợp." action={<Link className="text-primary-dark underline" to={kind === 'LECTURER' ? '/gv/ho-so' : '/mentor/ho-so'}>Cập nhật chuyên môn</Link>} />}
      {!topics.isLoading && !profileLoading && (hasDomains || tab === 'all') && !topics.error && !(tab === 'interested' && interestedTeams.isLoading) && !(tab === 'all' && (allTopics.isLoading || allTopics.error)) && displayed.length === 0 && <EmptyState icon="lightbulb" title={tab === 'chosen' ? 'Chưa có nhóm chọn bạn' : tab === 'interested' ? 'Bạn chưa quan tâm đề tài nào' : tab === 'all' ? 'Chưa có nhóm nào cần cố vấn' : 'Chưa có đề tài phù hợp'} />}
      {!topics.isLoading && !profileLoading && (hasDomains || tab === 'all') && <div className="grid gap-3 md:grid-cols-2">
        {displayed.map((item) => {
          const interest = interests.data?.find((i) => i.teamId === item.team.id && i.status === 'OPEN') ?? (item.myInterest?.status === 'OPEN' ? item.myInterest : null)
          const full = (topics.data?.openInterestCount ?? 0) >= (topics.data?.interestLimit ?? 0)
          return <Card key={item.team.id}>
            <div className="flex items-start justify-between gap-2"><div>
              <Link className="font-semibold text-primary-dark underline" to={`${root(kind)}/de-tai/${item.team.id}`}>{item.team.projectName || item.team.name}</Link>
              <p className="text-text-secondary">{item.team.name} · {item.team.memberCount}/{item.team.requirement?.maxSize ?? 6} thành viên</p>
            </div>{item.myPreferenceRank && <StatusChip tone="info">Nguyện vọng {item.myPreferenceRank}</StatusChip>}</div>
            {item.wasMyEXE101Team && <p className="mt-2 text-info-dark">Đã hướng dẫn ở EXE101</p>}
            <p className="mt-2 text-body">{item.matchedDomains.map((d) => d.name).join(', ') || 'Chưa có chuyên môn trùng'}</p>
            <p className="text-caption text-text-secondary">{item.otherInterestedCount} người khác cũng quan tâm</p>
            <div className="mt-3">{item.hasAdvisor ? <StatusChip tone="neutral">Đã có {kind === 'LECTURER' ? 'giảng viên' : 'mentor'}</StatusChip>
              : <Button variant={interest ? 'outline' : 'primary'} disabled={toggle.isPending || (!interest && (!topics.data?.interestOpen || full))} onClick={() => void act(item.team.id, interest?.id)}>{interest ? 'Đã quan tâm · Rút lại' : 'Quan tâm'}</Button>}</div>
            {!interest && full && !item.hasAdvisor && <p className="mt-1 text-caption text-warning">Đã đạt giới hạn quan tâm. Rút bớt đề tài để chọn nhóm này.</p>}
          </Card>
        })}
      </div>}
    </div>
  </>
}

export function TopicDetailPage({ kind }: { kind: AdvisorKind }) {
  const { teamId } = useParams()
  const detail = useAdvisorTeamDetail(teamId)
  const topics = useTopics(kind, detail.data?.courseCode, !!detail.data?.courseCode)
  const preferences = useLecturerPreferences(detail.data?.courseCode, kind === 'LECTURER' && !!detail.data)
  const interests = useInterests(kind)
  const toggle = useToggleInterest(kind, detail.data?.courseCode)
  const [error, setError] = useState<string | null>(null)
  const item: TopicRecommendation | undefined = topics.data?.items.find((candidate) => candidate.team.id === teamId)
  const rank = item?.myPreferenceRank ?? preferences.data?.find((entry) => entry.team.id === teamId)?.rank
  const interest = interests.data?.find((i) => i.teamId === teamId && i.status === 'OPEN') ?? (item?.myInterest?.status === 'OPEN' ? item.myInterest : null)
  const hasAdvisor = kind === 'LECTURER' ? !!detail.data?.lecturerName : !!detail.data?.mentorName
  const canAdd = !!topics.data?.interestOpen && topics.data.openInterestCount < topics.data.interestLimit
  const act = async () => {
    if (!teamId) return
    setError(null)
    try { await toggle.mutateAsync({ teamId, interestId: interest?.id }) }
    catch (cause) { setError(toApiError(cause).detail ?? toApiError(cause).title) }
  }
  return <>
    <PageHeader title="Chi tiết đề tài" />
    <Link className="mb-4 inline-block text-primary-dark underline" to={`${root(kind)}/de-tai`}>← Về danh sách đề tài</Link>
    {detail.isLoading && <TableSkeleton columns={2} rows={4} />}
    {detail.error && <ErrorState error={detail.error} onRetry={() => void detail.refetch()} />}
    {error && <Banner tone="error" title={error} />}
    {detail.data && <Card><h2 className="text-title font-semibold">{detail.data.projectName || detail.data.name}</h2>
      <p className="text-text-secondary">{detail.data.name} · {detail.data.courseCode} · {detail.data.memberCount} thành viên</p>
      <p className="mt-3">{detail.data.ideaDescription || 'Nhóm chưa mô tả đề tài.'}</p>
      <p className="mt-3">Chuyên môn: {detail.data.domains.map((d) => d.name).join(', ') || 'Chưa có'}</p>
      <h3 className="mt-4 font-semibold">Thành viên</h3><ul>{detail.data.members.map((member) => <li key={member.id}>{member.fullName} · {member.specialization?.name}</li>)}</ul>
      {rank && <p className="mt-3">Nhóm xếp bạn ở nguyện vọng {rank}.</p>}
      {item?.wasMyEXE101Team && <p>Đã hướng dẫn đề tài này ở EXE101.</p>}
      <div className="mt-4">{hasAdvisor ? <StatusChip tone="neutral">Đã có cố vấn</StatusChip> : <Button disabled={toggle.isPending || (!interest && !canAdd)} onClick={() => void act()}>{interest ? 'Rút quan tâm' : 'Quan tâm'}</Button>}</div>
    </Card>}
  </>
}
