import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Button, Skeleton, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useWorkspaceContext } from '@/features/workspace/context'
import { formatDateTime } from '@/features/workspace/format'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { canPropose, useMeetings, type MeetingCard } from './api'
import { MeetingDrawer } from './MeetingDrawer'
import { ProposeMeetingDialog } from './ProposeMeetingDialog'

const sections: { id: string; title: string; match: (m: MeetingCard) => boolean }[] = [
  { id: 'proposed', title: 'Chờ chốt', match: (m) => m.status === 'PROPOSED' },
  { id: 'scheduled', title: 'Sắp tới', match: (m) => m.status === 'SCHEDULED' },
  { id: 'past', title: 'Đã họp', match: (m) => m.status === 'DONE' || m.status === 'CANCELLED' },
]

/**
 * C07 – Lịch họp (FE-36, rule 25): Chờ chốt · Sắp tới · Đã họp, số buổi mentor; trưởng nhóm, giảng viên, mentor đề xuất họp.
 * Cuộc họp đang mở nằm trên URL (`?hop=`) và hiện C08 trong drawer.
 */
export function MeetingsPage() {
  const { workspace } = useWorkspaceContext()
  const meetings = useMeetings(workspace.id)
  const [params, setParams] = useSearchParams()
  const openId = params.get('hop')
  const [proposing, setProposing] = useState(false)
  const snackbar = useSnackbar()
  const open = (id: string | null) => setParams(id ? { hop: id } : {}, { replace: true })
  const proposeButton = canPropose(workspace.myRole) ? (
    <Button icon="add" onClick={() => setProposing(true)}>Đề xuất họp</Button>
  ) : undefined

  return (
    <>
      <PageHeader title="Lịch họp" action={proposeButton} />
      {meetings.isError ? (
        <ErrorState error={meetings.error} onRetry={() => void meetings.refetch()} />
      ) : !meetings.data ? (
        <div className="flex flex-col gap-3" role="status" aria-label="Đang tải">
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-16 w-full" />
        </div>
      ) : (
        <div className="flex flex-col gap-6">
          {meetings.data.expectedMentorSessions > 0 && (
            <p className="text-body">
              Buổi mentor: <span className="font-semibold">{meetings.data.mentorSessionsDone}/{meetings.data.expectedMentorSessions}</span>
            </p>
          )}
          {meetings.data.items.length === 0 ? (
            <EmptyState
              icon="event"
              title="Chưa có cuộc họp"
              description="Đề xuất 1–3 khung giờ để cả nhóm chọn, hoặc chờ trưởng nhóm, giảng viên, mentor đề xuất."
              action={proposeButton}
            />
          ) : (
            sections.map((s) => {
              const items = meetings.data.items.filter(s.match)
              if (items.length === 0) return null
              return (
                <section key={s.id} aria-labelledby={`meetings-${s.id}`} className="flex flex-col gap-2">
                  <h2 id={`meetings-${s.id}`} className="text-subtitle font-semibold">{s.title} ({items.length})</h2>
                  <ul className="flex flex-col gap-2">
                    {items.map((m) => (
                      <li key={m.id}>
                        <MeetingRow meeting={m} onOpen={() => open(m.id)} />
                      </li>
                    ))}
                  </ul>
                </section>
              )
            })
          )}
        </div>
      )}
      {proposing && (
        <ProposeMeetingDialog
          workspaceId={workspace.id}
          onClose={() => setProposing(false)}
          onCreated={(id, scheduled) => {
            setProposing(false)
            snackbar.show(scheduled ? 'Đã tạo cuộc họp' : 'Đã gửi đề xuất họp')
            open(id)
          }}
        />
      )}
      {openId && <MeetingDrawer key={openId} meetingId={openId} myRole={workspace.myRole} onClose={() => open(null)} onDone={snackbar.show} />}
      {snackbar.element}
    </>
  )
}

/** Một dòng cuộc họp; `showTeam` hiện tên nhóm khi gom nhiều nhóm (lịch của giảng viên). */
export function MeetingRow({ meeting: m, onOpen, showTeam = false }: { meeting: MeetingCard; onOpen: () => void; showTeam?: boolean }) {
  return (
    <button
      type="button"
      onClick={onOpen}
      className="flex w-full flex-wrap items-center gap-3 rounded-lg bg-surface p-4 text-left shadow-elevation-1 hover:bg-background"
    >
      <span className="flex min-w-0 flex-1 flex-col">
        <span className="font-medium">{showTeam ? `${m.teamName} · ${m.title}` : m.title}</span>
        <span className="text-caption text-text-secondary">
          {m.startsAt ? formatDateTime(m.startsAt) : `${m.slotCount} khung giờ đề xuất`} · {m.organizer.displayName}
          {m.location ? ` · ${m.location}` : ''}
        </span>
      </span>
      {m.isWeekly && <StatusChip tone="info" icon="repeat">Hằng tuần{m.weekNo ? ` · tuần ${m.weekNo}` : ''}</StatusChip>}
      {m.isMentorSession && <StatusChip tone="info" icon="support_agent">Buổi mentor</StatusChip>}
      {m.status === 'PROPOSED' && m.myAnswerPending && <StatusChip tone="warning" icon="how_to_vote">Chưa trả lời</StatusChip>}
      {m.status === 'CANCELLED' && <StatusChip tone="neutral" icon="event_busy">Đã hủy</StatusChip>}
    </button>
  )
}
