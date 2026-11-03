import { useState } from 'react'
import { toApiError } from '@/shared/api/apiClient'
import { useAuth } from '@/shared/auth/AuthProvider'
import { ErrorState } from '@/shared/components/States'
import { Banner, Button, Dialog, Skeleton, StatusChip } from '@/shared/components/ui'
import { Drawer } from '@/features/workspace/components'
import { formatDateTime } from '@/features/workspace/format'
import { inputClass } from '@/features/semesters/Field'
import {
  minutesLines,
  useAnswerMeeting,
  useCancelMeeting,
  useConfirmSlot,
  useMeeting,
  useMinutesToTasks,
  useSaveMinutes,
  MAX_SLOTS,
  type MeetingDetail,
  type MeetingSlot,
} from './api'
import { AddSlotDialog } from './ProposeMeetingDialog'

const statusChip = {
  PROPOSED: { tone: 'warning', icon: 'how_to_vote', label: 'Chờ chốt' },
  SCHEDULED: { tone: 'success', icon: 'event_available', label: 'Đã chốt' },
  CANCELLED: { tone: 'neutral', icon: 'event_busy', label: 'Đã hủy' },
  DONE: { tone: 'info', icon: 'task_alt', label: 'Đã họp' },
} as const

/**
 * C08 – Chi tiết cuộc họp (FE-36, rule 25): khung giờ và câu trả lời Có / Không; người tổ chức chốt khung, thêm khung, hủy;
 * sau họp ghi biên bản và chuyển các dòng thành việc. Trạng thái: chờ trả lời · đã chốt · đã hủy · đã họp (có / chưa có biên bản).
 */
export function MeetingDrawer({ meetingId, myRole, onClose, onDone }: {
  meetingId: string
  myRole: string
  onClose: () => void
  onDone: (message: string) => void
}) {
  const meeting = useMeeting(meetingId)
  return (
    <Drawer open onClose={onClose} title={meeting.data?.title ?? 'Cuộc họp'}>
      {meeting.error ? (
        <ErrorState error={meeting.error} onRetry={() => void meeting.refetch()} />
      ) : !meeting.data ? (
        <div className="flex flex-col gap-3" role="status" aria-label="Đang tải">
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-32 w-full" />
        </div>
      ) : (
        <Details meeting={meeting.data} myRole={myRole} onDone={onDone} />
      )}
    </Drawer>
  )
}

function useIsOrganizer(meeting: MeetingDetail) {
  const { user } = useAuth()
  const me = { STUDENT: user?.studentId, LECTURER: user?.lecturerId, MENTOR: user?.mentorId }[meeting.organizer.personType]
  return !!me && me === meeting.organizer.personId
}

function Details({ meeting: m, myRole, onDone }: { meeting: MeetingDetail; myRole: string; onDone: (message: string) => void }) {
  const isOrganizer = useIsOrganizer(m)
  const chip = statusChip[m.status]
  const confirmed = m.slots.find((s) => s.startsAt === m.startsAt)
  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-center gap-2">
        <StatusChip tone={chip.tone} icon={chip.icon}>{chip.label}</StatusChip>
        {m.isMentorSession && <StatusChip tone="info" icon="support_agent">Buổi mentor</StatusChip>}
        {m.isWeekly && <StatusChip tone="info" icon="repeat">Hằng tuần</StatusChip>}
      </div>
      <dl className="grid grid-cols-[96px_1fr] gap-x-3 gap-y-1 text-body">
        <dt className="text-text-secondary">Tổ chức</dt>
        <dd>{m.organizer.displayName}</dd>
        {m.startsAt && (
          <>
            <dt className="text-text-secondary">Thời gian</dt>
            <dd>{formatDateTime(m.startsAt)}{confirmed ? ` · ${confirmed.durationMinutes} phút` : ''}</dd>
          </>
        )}
        <dt className="text-text-secondary">Nơi họp</dt>
        <dd className="break-all">{m.location || 'Chưa có'}</dd>
      </dl>
      {m.agenda && <p className="whitespace-pre-line text-body">{m.agenda}</p>}
      {m.status === 'CANCELLED' && <Banner tone="info" title="Cuộc họp đã hủy">{m.cancelReason}</Banner>}
      {m.status === 'PROPOSED' && <SlotVoting meeting={m} isOrganizer={isOrganizer} onDone={onDone} />}
      {(m.status === 'SCHEDULED' || m.status === 'DONE') && isOrganizer && <MinutesEditor meeting={m} onDone={onDone} />}
      {m.status === 'DONE' && !isOrganizer && <MinutesView meeting={m} />}
      {m.status === 'DONE' && myRole !== 'ADMIN' && <CreatedTasks meeting={m} canCreate={isOrganizer} onDone={onDone} />}
      {isOrganizer && (m.status === 'PROPOSED' || m.status === 'SCHEDULED') && <CancelAction meetingId={m.id} onDone={onDone} />}
    </div>
  )
}

function SlotVoting({ meeting: m, isOrganizer, onDone }: { meeting: MeetingDetail; isOrganizer: boolean; onDone: (message: string) => void }) {
  const answer = useAnswerMeeting(m.id)
  const confirm = useConfirmSlot(m.id)
  const [adding, setAdding] = useState(false)
  const error = answer.error ?? confirm.error
  const apiError = error ? toApiError(error) : null
  return (
    <section className="flex flex-col gap-2">
      <h3 className="font-semibold">Khung giờ – chọn Có / Không</h3>
      {apiError && <Banner tone="error" title={apiError.title}>{apiError.detail}</Banner>}
      <ul className="flex flex-col gap-2">
        {m.slots.map((s) => (
          <li key={s.id} className="flex flex-col gap-2 rounded-md border border-border p-3">
            <SlotSummary slot={s} suggested={s.id === m.suggestedSlotId} />
            <div className="flex flex-wrap items-center gap-2" role="group" aria-label={`Trả lời khung ${formatDateTime(s.startsAt)}`}>
              {(['YES', 'NO'] as const).map((a) => (
                <Button
                  key={a}
                  variant={s.myAnswer === a ? 'primary' : 'outline'}
                  aria-pressed={s.myAnswer === a}
                  disabled={answer.isPending}
                  onClick={() => answer.mutate([{ slotId: s.id, answer: a }])}
                >
                  {a === 'YES' ? 'Có' : 'Không'}
                </Button>
              ))}
              {isOrganizer && (
                <Button
                  variant="text"
                  icon="event_available"
                  loading={confirm.isPending && confirm.variables === s.id}
                  disabled={confirm.isPending}
                  onClick={() => confirm.mutate(s.id, { onSuccess: () => onDone('Đã chốt khung giờ') })}
                >
                  Chốt khung này
                </Button>
              )}
            </div>
          </li>
        ))}
      </ul>
      {isOrganizer && m.slots.length < MAX_SLOTS && (
        <Button variant="text" icon="add" className="self-start" onClick={() => setAdding(true)}>Thêm khung giờ</Button>
      )}
      {adding && <AddSlotDialog meetingId={m.id} onClose={() => setAdding(false)} />}
    </section>
  )
}

function SlotSummary({ slot: s, suggested }: { slot: MeetingSlot; suggested: boolean }) {
  return (
    <div className="flex flex-wrap items-center gap-2">
      <span className="font-medium">{formatDateTime(s.startsAt)}</span>
      <span className="text-caption text-text-secondary">{s.durationMinutes} phút</span>
      <span className="text-caption">· {s.yesCount}/{s.inviteeCount} người có thể</span>
      {suggested && <StatusChip tone="success" icon="star">Nhiều người chọn nhất</StatusChip>}
      {s.yesPeople.length > 0 && (
        <span className="w-full text-caption text-text-secondary">Có: {s.yesPeople.map((p) => p.displayName).join(', ')}</span>
      )}
    </div>
  )
}

function MinutesView({ meeting: m }: { meeting: MeetingDetail }) {
  return (
    <section className="flex flex-col gap-2">
      <h3 className="font-semibold">Biên bản</h3>
      {m.minutes ? (
        <>
          <p className="whitespace-pre-line text-body">{m.minutes.content}</p>
          <p className="text-caption text-text-secondary">{m.minutes.recordedByName} · {formatDateTime(m.minutes.recordedAt)}</p>
        </>
      ) : (
        <p className="text-body text-text-secondary">Chưa có biên bản.</p>
      )}
    </section>
  )
}

function MinutesEditor({ meeting: m, onDone }: { meeting: MeetingDetail; onDone: (message: string) => void }) {
  const [content, setContent] = useState(m.minutes?.content ?? '')
  const save = useSaveMinutes(m.id)
  const apiError = save.error ? toApiError(save.error) : null
  return (
    <section className="flex flex-col gap-2">
      <h3 className="font-semibold">Biên bản</h3>
      {apiError && <Banner tone="error" title={apiError.title}>{apiError.detail}</Banner>}
      <label className="flex flex-col gap-1">
        <span className="sr-only">Nội dung biên bản</span>
        <textarea
          rows={6}
          maxLength={8000}
          value={content}
          onChange={(e) => setContent(e.target.value)}
          placeholder={'Mỗi dòng một ý, ví dụ:\n- Hoàn thành landing page\n- Phỏng vấn 5 khách hàng'}
          className={`${inputClass} h-auto py-2`}
        />
      </label>
      {m.minutes && <p className="text-caption text-text-secondary">Lần lưu gần nhất: {m.minutes.recordedByName} · {formatDateTime(m.minutes.recordedAt)}</p>}
      <Button
        className="self-end"
        loading={save.isPending}
        disabled={!content.trim() || content === m.minutes?.content}
        onClick={() => save.mutate(content.trim(), { onSuccess: () => onDone('Đã lưu biên bản') })}
      >
        Lưu biên bản
      </Button>
    </section>
  )
}

function CreatedTasks({ meeting: m, canCreate, onDone }: { meeting: MeetingDetail; canCreate: boolean; onDone: (message: string) => void }) {
  const lines = m.minutes ? minutesLines(m.minutes.content).slice(0, 20) : []
  const [picked, setPicked] = useState<Set<number>>(() => new Set())
  const create = useMinutesToTasks(m.id)
  const apiError = create.error ? toApiError(create.error) : null
  return (
    <section className="flex flex-col gap-2">
      <h3 className="font-semibold">Việc từ biên bản</h3>
      {m.createdTasks.length > 0 && (
        <ul className="list-disc pl-5 text-body">
          {m.createdTasks.map((t) => <li key={t.id}>{t.title}</li>)}
        </ul>
      )}
      {canCreate && lines.length > 0 && (
        <>
          {apiError && <Banner tone="error" title={apiError.title}>{apiError.detail}</Banner>}
          <p className="text-caption text-text-secondary">Chọn các dòng biên bản để tạo việc trong một bước.</p>
          {lines.map((line, i) => (
            <label key={i} className="flex items-start gap-2 text-body">
              <input
                type="checkbox"
                className="mt-1 size-4 accent-primary"
                checked={picked.has(i)}
                onChange={() => setPicked((p) => (p.has(i) ? new Set([...p].filter((x) => x !== i)) : new Set(p).add(i)))}
              />
              {line}
            </label>
          ))}
          <Button
            variant="outline"
            icon="add_task"
            className="self-end"
            loading={create.isPending}
            disabled={picked.size === 0}
            onClick={() =>
              create.mutate(lines.filter((_, i) => picked.has(i)), {
                onSuccess: (tasks) => {
                  setPicked(new Set())
                  onDone(`Đã tạo ${tasks.length} việc`)
                },
              })
            }
          >
            Tạo {picked.size || ''} việc
          </Button>
        </>
      )}
      {m.createdTasks.length === 0 && (!canCreate || lines.length === 0) && <p className="text-body text-text-secondary">Chưa có việc nào.</p>}
    </section>
  )
}

function CancelAction({ meetingId, onDone }: { meetingId: string; onDone: (message: string) => void }) {
  const [open, setOpen] = useState(false)
  const [reason, setReason] = useState('')
  const cancel = useCancelMeeting(meetingId)
  return (
    <>
      <Button variant="text" icon="event_busy" className="self-start text-error" onClick={() => setOpen(true)}>Hủy cuộc họp</Button>
      <Dialog
        open={open}
        onClose={() => setOpen(false)}
        title="Hủy cuộc họp?"
        actions={
          <>
            <Button variant="text" onClick={() => setOpen(false)}>Không</Button>
            <Button
              variant="danger"
              loading={cancel.isPending}
              disabled={!reason.trim()}
              onClick={() =>
                cancel.mutate(reason.trim(), {
                  onSuccess: () => {
                    setOpen(false)
                    onDone('Đã hủy cuộc họp')
                  },
                })
              }
            >
              Hủy họp
            </Button>
          </>
        }
      >
        <label className="flex flex-col gap-1 text-body font-medium">
          Lý do (mọi người sẽ thấy)
          <textarea rows={3} maxLength={500} value={reason} onChange={(e) => setReason(e.target.value)} className={`${inputClass} h-auto py-2`} />
        </label>
      </Dialog>
    </>
  )
}
