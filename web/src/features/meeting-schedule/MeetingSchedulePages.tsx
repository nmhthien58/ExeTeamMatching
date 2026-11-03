import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { toApiError } from '@/shared/api/apiClient'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Banner, Button, Dialog, Icon, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { Card } from '@/features/workspace/components'
import { formatDateTime } from '@/features/workspace/format'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { useMyWorkspaces } from '@/features/workspace/api'
import { ListSkeleton } from '@/features/my_team/components'
import { RealtimeStatusBar } from '@/features/realtime/RealtimeStatusBar'
import { useHubEvent, useHubGroup } from '@/features/realtime/useRealtime'
import {
  DAY_NAMES,
  countSlots,
  scheduleErrors,
  scheduleKeys,
  slotLabel,
  useBookSlot,
  useMySchedule,
  useScheduleActions,
  useTeamSchedule,
  useTeamWorkspaceId,
  type MeetingSchedule,
  type ScheduleSlot,
  type ScheduleWindow,
} from './api'
import { WeeklySlotGrid } from './WeeklySlotGrid'

const fieldClass = 'h-10 rounded-sm border border-border-strong bg-surface px-3'
const errorText = (error: unknown) => {
  const e = toApiError(error)
  return scheduleErrors[e.code] ?? e.detail ?? e.title
}

/** Nghe `slotsChanged` của workspace (payload chỉ có scheduleId) rồi tải lại lịch qua REST. */
function useSlotsChanged(queryKey: readonly unknown[]) {
  const queryClient = useQueryClient()
  useHubEvent('slotsChanged', () => void queryClient.invalidateQueries({ queryKey }))
}

function JoinWorkspace({ id }: { id: string }) {
  useHubGroup('Workspace', id)
  return null
}

/**
 * C09 – Lịch họp hằng tuần của nhóm (FE-45, quy tắc 25): trưởng nhóm chọn / đổi một slot cố định tới hạn, ai chọn trước được trước;
 * slot vừa bị nhóm khác chọn thì báo tại ô và lưới tự tải lại (realtime `slotsChanged`). Thành viên chỉ xem slot của nhóm.
 */
export function TeamSchedulePage() {
  const { workspaceId, isLeader, isLoading, error } = useTeamWorkspaceId()
  const schedule = useTeamSchedule(workspaceId)
  useHubGroup('Workspace', workspaceId)
  useSlotsChanged(scheduleKeys.team(workspaceId ?? ''))
  const book = useBookSlot(workspaceId ?? '')
  const snackbar = useSnackbar()
  const [picking, setPicking] = useState<ScheduleSlot | null>(null)
  const [failedSlot, setFailedSlot] = useState<string | null>(null)

  const confirm = () =>
    picking &&
    book.mutate(picking.id, {
      onSuccess: () => {
        snackbar.show(`Đã chọn ${slotLabel(picking)}`)
        setPicking(null)
        setFailedSlot(null)
      },
      onError: () => {
        setFailedSlot(picking.id)
        setPicking(null)
        void schedule.refetch()
      },
    })

  const data = schedule.data
  return (
    <>
      <PageHeader title="Lịch họp hằng tuần" />
      <RealtimeStatusBar />
      {error || schedule.error ? (
        <ErrorState error={error ?? schedule.error} onRetry={() => void schedule.refetch()} />
      ) : isLoading || (workspaceId && !schedule.isFetched) ? (
        <ListSkeleton />
      ) : !workspaceId ? (
        <EmptyState icon="event_busy" title="Nhóm chưa có dự án" description="Lịch họp hằng tuần có sau khi nhóm được chốt và có dự án EXE201." />
      ) : !data ? (
        <EmptyState icon="event_note" title="Giảng viên chưa đăng lịch" description="Bạn sẽ nhận thông báo khi giảng viên hướng dẫn đăng lịch họp hằng tuần." />
      ) : (
        <div className="flex flex-col gap-4">
          <Card>
            <p className="text-title font-semibold">
              {data.mySlot ? `Họp hằng tuần · ${slotLabel(data.mySlot)}` : 'Nhóm chưa chọn slot'}
            </p>
            <p className="text-text-secondary">Giảng viên: {data.lecturerName}</p>
            {data.location && (
              <a href={data.location} target="_blank" rel="noopener noreferrer" className="mt-1 inline-flex min-h-10 items-center gap-1 font-medium text-info-dark">
                <Icon name="videocam" size={20} /> Link họp
              </a>
            )}
            <p className="mt-1">
              {data.canBook
                ? `Chọn trước ${formatDateTime(data.bookingDeadline)}`
                : new Date(data.bookingDeadline) < new Date()
                  ? 'Đã hết hạn chọn slot; nhóm chưa chọn được hệ thống xếp vào slot trống.'
                  : 'Trưởng nhóm chọn slot cho nhóm.'}
            </p>
          </Card>
          {failedSlot && (
            <Banner tone="error" title={errorText(book.error)}>
              Lưới đã cập nhật theo lựa chọn mới nhất.
            </Banner>
          )}
          {isLeader && (
            <Card>
              <h2 className="mb-3 font-semibold">Các slot</h2>
              <WeeklySlotGrid slots={data.slots} canPick={(s) => data.canBook && s.state === 'FREE'} onPick={setPicking} errorSlotId={failedSlot} />
            </Card>
          )}
        </div>
      )}
      {picking && (
        <Dialog
          open
          onClose={() => setPicking(null)}
          title={`Chọn ${slotLabel(picking)}?`}
          actions={
            <>
              <Button variant="text" onClick={() => setPicking(null)}>
                Hủy
              </Button>
              <Button loading={book.isPending} onClick={confirm}>
                {data?.mySlot ? 'Đổi slot' : 'Chọn slot'}
              </Button>
            </>
          }
        >
          Nhóm họp cố định {slotLabel(picking)}–{picking.endTime} mỗi tuần. Bạn đổi được tới hạn chọn slot.
        </Dialog>
      )}
      {snackbar.element}
    </>
  )
}

const emptyWindow: ScheduleWindow = { dayOfWeek: 2, startTime: '09:00', endTime: '11:00' }

/**
 * C09 – Lịch họp hằng tuần của giảng viên (FE-45): khung giờ, độ dài slot, link họp, xem trước số slot; lưu thì cắt slot, "Đăng lịch"
 * cho các nhóm EXE201 mình hướng dẫn (thiếu slot vẫn đăng, có cảnh báo). Chạm ô có nhóm → đổi slot cho nhóm đó (kể cả sau hạn).
 */
export function LecturerSchedulePage() {
  const schedule = useMySchedule()
  const workspaces = (useMyWorkspaces().data ?? []).filter((w) => w.courseCode === 'EXE201')
  useSlotsChanged(scheduleKeys.mine)
  return (
    <>
      <PageHeader title="Lịch họp hằng tuần" />
      <RealtimeStatusBar />
      {workspaces.map((w) => (
        <JoinWorkspace key={w.id} id={w.id} />
      ))}
      {schedule.error ? (
        <ErrorState error={schedule.error} onRetry={() => void schedule.refetch()} />
      ) : schedule.isLoading ? (
        <ListSkeleton />
      ) : (
        <LecturerSchedule key={schedule.data?.id ?? 'new'} schedule={schedule.data ?? null} />
      )}
    </>
  )
}

function LecturerSchedule({ schedule }: { schedule: MeetingSchedule | null }) {
  const { save, publish, move } = useScheduleActions()
  const snackbar = useSnackbar()
  const [windows, setWindows] = useState<ScheduleWindow[]>(schedule?.windows.length ? schedule.windows : [emptyWindow])
  const [slotMinutes, setSlotMinutes] = useState(schedule?.slotMinutes ?? 15)
  const [location, setLocation] = useState(schedule?.location ?? '')
  const [submitted, setSubmitted] = useState(false)
  const [moving, setMoving] = useState<ScheduleSlot | null>(null)

  const badWindow = windows.findIndex((w) => !w.startTime || !w.endTime || w.startTime >= w.endTime)
  const locationError = submitted && !location.trim() ? 'Nhập link họp.' : undefined
  const preview = countSlots(windows, slotMinutes)
  const setWindow = (i: number, patch: Partial<ScheduleWindow>) => setWindows(windows.map((w, j) => (j === i ? { ...w, ...patch } : w)))
  const actionError = save.error ?? publish.error

  const submit = () => {
    setSubmitted(true)
    if (badWindow >= 0 || !location.trim() || slotMinutes < 5 || slotMinutes > 60) return
    save.mutate({ windows, slotMinutes, location: location.trim() }, { onSuccess: () => snackbar.show('Đã lưu khung giờ') })
  }

  return (
    <div className="flex flex-col gap-4">
      <Card className="flex flex-col gap-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="font-semibold">Khung giờ họp</h2>
          {schedule && (
            <StatusChip tone={schedule.status === 'DRAFT' ? 'neutral' : 'success'} icon={schedule.status === 'DRAFT' ? 'edit_note' : 'check_circle'}>
              {schedule.status === 'DRAFT' ? 'Chưa đăng' : schedule.status === 'PUBLISHED' ? 'Đã đăng' : 'Đã chốt slot'}
            </StatusChip>
          )}
        </div>
        {actionError && <Banner tone="error" title={errorText(actionError)} />}
        <ul className="flex flex-col gap-2" aria-label="Khung giờ">
          {windows.map((w, i) => (
            <li key={i} className="flex flex-wrap items-end gap-2">
              <label className="flex flex-col gap-1">
                <span className="text-caption text-text-secondary">Thứ</span>
                <select value={w.dayOfWeek} onChange={(e) => setWindow(i, { dayOfWeek: Number(e.target.value) })} className={fieldClass}>
                  {DAY_NAMES.slice(1).map((name, d) => (
                    <option key={name} value={d + 1}>
                      {name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="flex flex-col gap-1">
                <span className="text-caption text-text-secondary">Bắt đầu</span>
                <input type="time" step={300} value={w.startTime} onChange={(e) => setWindow(i, { startTime: e.target.value })} className={fieldClass} />
              </label>
              <label className="flex flex-col gap-1">
                <span className="text-caption text-text-secondary">Kết thúc</span>
                <input type="time" step={300} value={w.endTime} onChange={(e) => setWindow(i, { endTime: e.target.value })} className={fieldClass} />
              </label>
              {windows.length > 1 && (
                <Button variant="text" icon="delete" aria-label={`Xóa khung ${i + 1}`} onClick={() => setWindows(windows.filter((_, j) => j !== i))}>
                  Xóa
                </Button>
              )}
              {submitted && badWindow === i && <span className="w-full text-caption text-error">Giờ kết thúc phải sau giờ bắt đầu.</span>}
            </li>
          ))}
        </ul>
        <Button variant="text" icon="add" className="self-start" onClick={() => setWindows([...windows, emptyWindow])}>
          Thêm khung giờ
        </Button>
        <div className="flex flex-wrap gap-4">
          <label className="flex flex-col gap-1">
            <span className="text-caption text-text-secondary">Độ dài slot (phút)</span>
            <input type="number" min={5} max={60} step={5} value={slotMinutes} onChange={(e) => setSlotMinutes(Number(e.target.value))} className={`${fieldClass} w-32`} />
          </label>
          <div className="flex min-w-64 flex-1 flex-col gap-1">
            <label htmlFor="schedule-location" className="text-caption text-text-secondary">
              Link họp
            </label>
            <input
              id="schedule-location"
              value={location}
              maxLength={500}
              onChange={(e) => setLocation(e.target.value)}
              placeholder="https://meet.google.com/…"
              aria-invalid={!!locationError}
              aria-describedby={locationError ? 'schedule-location-error' : undefined}
              className={`${fieldClass} ${locationError ? 'border-error' : ''}`}
            />
            {locationError && (
              <span id="schedule-location-error" className="text-caption text-error">
                {locationError}
              </span>
            )}
          </div>
        </div>
        <p className="text-text-secondary">Xem trước: {preview} slot</p>
        <span className="flex flex-wrap gap-2">
          <Button variant="outline" loading={save.isPending} onClick={submit}>
            Lưu khung giờ
          </Button>
          {schedule?.status === 'DRAFT' && (
            <Button icon="campaign" loading={publish.isPending} onClick={() => publish.mutate(undefined, { onSuccess: () => snackbar.show('Đã đăng lịch cho các nhóm') })}>
              Đăng lịch
            </Button>
          )}
        </span>
      </Card>

      {schedule && (
        <Card>
          <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
            <h2 className="font-semibold">Các slot</h2>
            <span className="text-text-secondary">
              {schedule.slots.length} slot · {schedule.teamCount} nhóm · còn {schedule.teamsWithoutSlot.length} nhóm chưa chọn
            </span>
          </div>
          {schedule.slots.length < schedule.teamCount && (
            <Banner tone="warning" title="Thiếu slot so với số nhóm">
              Có {schedule.slots.length} slot cho {schedule.teamCount} nhóm. Vẫn đăng được; thêm khung giờ để mọi nhóm có slot.
            </Banner>
          )}
          {schedule.status !== 'DRAFT' && <p className="mb-3">Hạn chọn slot của nhóm: {formatDateTime(schedule.bookingDeadline)}</p>}
          {schedule.slots.length === 0 ? (
            <p className="text-text-secondary">Chưa có slot. Lưu khung giờ để cắt slot.</p>
          ) : (
            <WeeklySlotGrid slots={schedule.slots} canPick={(s) => !!s.workspaceId} onPick={setMoving} />
          )}
          {schedule.teamsWithoutSlot.length > 0 && (
            <p className="mt-3 text-text-secondary">Chưa chọn: {schedule.teamsWithoutSlot.map((t) => t.teamName).join(', ')}</p>
          )}
        </Card>
      )}
      {moving && schedule && (
        <MoveDialog
          slot={moving}
          freeSlots={schedule.slots.filter((s) => !s.workspaceId)}
          pending={move.isPending}
          error={move.error ? errorText(move.error) : null}
          onClose={() => {
            setMoving(null)
            move.reset()
          }}
          onMove={(slotId) =>
            move.mutate(
              { workspaceId: moving.workspaceId!, slotId },
              {
                onSuccess: () => {
                  snackbar.show(`Đã đổi slot cho ${moving.teamName}`)
                  setMoving(null)
                },
              },
            )
          }
        />
      )}
      {snackbar.element}
    </div>
  )
}

function MoveDialog({ slot, freeSlots, pending, error, onClose, onMove }: { slot: ScheduleSlot; freeSlots: ScheduleSlot[]; pending: boolean; error: string | null; onClose: () => void; onMove: (slotId: string) => void }) {
  const [slotId, setSlotId] = useState('')
  return (
    <Dialog
      open
      onClose={onClose}
      title={`Đổi slot cho ${slot.teamName}`}
      actions={
        <>
          <Button variant="text" onClick={onClose}>
            Hủy
          </Button>
          <Button disabled={!slotId} loading={pending} onClick={() => onMove(slotId)}>
            Đổi slot
          </Button>
        </>
      }
    >
      <p className="mb-3 text-text-secondary">Hiện tại: {slotLabel(slot)}. Các buổi họp chưa diễn ra dời theo slot mới.</p>
      {error && (
        <p role="alert" className="mb-2 text-error">
          {error}
        </p>
      )}
      {freeSlots.length === 0 ? (
        <p>Không còn slot trống. Thêm khung giờ trước.</p>
      ) : (
        <label className="flex flex-col gap-1 text-text-primary">
          <span className="font-medium">Slot mới</span>
          <select value={slotId} onChange={(e) => setSlotId(e.target.value)} className={fieldClass}>
            <option value="">Chọn slot trống</option>
            {freeSlots.map((s) => (
              <option key={s.id} value={s.id}>
                {slotLabel(s)}–{s.endTime}
              </option>
            ))}
          </select>
        </label>
      )}
    </Dialog>
  )
}
