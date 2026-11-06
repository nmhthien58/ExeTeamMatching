import { useQueries } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router-dom'
import { api } from '@/shared/api/apiClient'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Skeleton } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useMyWorkspaces } from '@/features/workspace/api'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { meetingKeys, type MeetingCard, type MeetingList } from './api'
import { MeetingDrawer } from './MeetingDrawer'
import { MeetingRow } from './MeetingsPage'

const time = (m: MeetingCard) => (m.startsAt ? new Date(m.startsAt).getTime() : 0)

const sections: { id: string; title: string; match: (m: MeetingCard) => boolean; sort: (a: MeetingCard, b: MeetingCard) => number }[] = [
  { id: 'proposed', title: 'Chờ chốt', match: (m) => m.status === 'PROPOSED', sort: (a, b) => a.teamName.localeCompare(b.teamName, 'vi') },
  { id: 'scheduled', title: 'Sắp tới', match: (m) => m.status === 'SCHEDULED', sort: (a, b) => time(a) - time(b) },
  { id: 'past', title: 'Đã họp', match: (m) => m.status === 'DONE' || m.status === 'CANCELLED', sort: (a, b) => time(b) - time(a) },
]

/**
 * C07 – Lịch của giảng viên (`/gv/lich`, FE-36): gom lịch họp mọi nhóm đang hướng dẫn / làm mentor.
 * Chưa có API "họp của tôi" trên mọi workspace nên đọc `/api/workspaces/me` rồi lịch họp từng workspace
 * (cùng khóa cache với C07 trong dự án, thao tác ở drawer C08 tự làm mới danh sách).
 * Đề xuất họp mới làm trong lịch họp của từng dự án.
 */
export function AdvisorCalendarPage() {
  const workspaces = useMyWorkspaces()
  const list = workspaces.data ?? []
  const meetings = useQueries({
    queries: list.map((w) => ({
      queryKey: meetingKeys.list(w.id),
      queryFn: async () => (await api.get<MeetingList>(`/api/workspaces/${w.id}/meetings`)).data,
    })),
  })
  const [params, setParams] = useSearchParams()
  const openId = params.get('hop')
  const open = (id: string | null) => setParams(id ? { hop: id } : {}, { replace: true })
  const snackbar = useSnackbar()

  const failed = workspaces.error ?? meetings.find((q) => q.isError)?.error
  const loading = workspaces.isLoading || meetings.some((q) => q.isLoading)
  const all = meetings.flatMap((q) => q.data?.items ?? [])
  const roleOf = (workspaceId: string) => list.find((w) => w.id === workspaceId)?.myRole ?? 'INSTRUCTOR'
  const openMeeting = all.find((m) => m.id === openId)
  const hasExe201 = list.some((w) => w.courseCode === 'EXE201' && w.myRole === 'INSTRUCTOR')
  const weeklyLink = hasExe201 ? (
    <Link className="text-primary-dark underline" to="/gv/lich-hop">Lịch họp hằng tuần EXE201</Link>
  ) : undefined

  return (
    <>
      <PageHeader title="Lịch" action={weeklyLink} />
      {failed ? (
        <ErrorState
          error={failed}
          onRetry={() => {
            void workspaces.refetch()
            meetings.filter((q) => q.isError).forEach((q) => void q.refetch())
          }}
        />
      ) : loading ? (
        <div className="flex flex-col gap-3" role="status" aria-label="Đang tải">
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-16 w-full" />
        </div>
      ) : list.length === 0 ? (
        <EmptyState icon="groups" title="Chưa có nhóm" description="Khi được ghép làm giảng viên hướng dẫn hoặc mentor, lịch họp của các nhóm sẽ hiện ở đây." />
      ) : all.length === 0 ? (
        <EmptyState icon="event" title="Chưa có cuộc họp" description="Các nhóm chưa có cuộc họp nào. Mở lịch họp của từng dự án để đề xuất họp." />
      ) : (
        <div className="flex flex-col gap-6">
          {sections.map((s) => {
            const items = all.filter(s.match).sort(s.sort)
            if (items.length === 0) return null
            return (
              <section key={s.id} aria-labelledby={`calendar-${s.id}`} className="flex flex-col gap-2">
                <h2 id={`calendar-${s.id}`} className="text-subtitle font-semibold">{s.title} ({items.length})</h2>
                <ul className="flex flex-col gap-2">
                  {items.map((m) => (
                    <li key={m.id}>
                      <MeetingRow meeting={m} showTeam onOpen={() => open(m.id)} />
                    </li>
                  ))}
                </ul>
              </section>
            )
          })}
        </div>
      )}
      {openId && (
        <MeetingDrawer key={openId} meetingId={openId} myRole={openMeeting ? roleOf(openMeeting.workspaceId) : 'INSTRUCTOR'} onClose={() => open(null)} onDone={snackbar.show} />
      )}
      {snackbar.element}
    </>
  )
}
