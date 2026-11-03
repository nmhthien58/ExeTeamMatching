import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import { useMyWorkspaces } from '@/features/workspace/api'
import { useMyTeam } from '@/features/suggestions/api'

// Kiểu theo docs/contracts/openapi/collab.yaml (tag MeetingSchedules) – C09, quy tắc 25.

export type SlotState = 'FREE' | 'TAKEN' | 'MINE'

export interface ScheduleWindow {
  /** 1 = thứ Hai … 7 = Chủ nhật. */
  dayOfWeek: number
  startTime: string
  endTime: string
}

export interface ScheduleSlot {
  id: string
  dayOfWeek: number
  startTime: string
  endTime: string
  /** Giảng viên chỉ thấy FREE / TAKEN. */
  state: SlotState
  workspaceId?: string | null
  teamName?: string | null
}

/** Góc nhìn giảng viên. */
export interface MeetingSchedule {
  id: string
  status: 'DRAFT' | 'PUBLISHED' | 'CLOSED'
  slotMinutes: number
  location: string
  bookingDeadline: string
  windows: ScheduleWindow[]
  slots: ScheduleSlot[]
  teamCount: number
  teamsWithoutSlot: { workspaceId: string; teamName: string }[]
  warnings: 'NOT_ENOUGH_SLOTS'[]
}

export interface MeetingScheduleInput {
  windows: ScheduleWindow[]
  slotMinutes: number
  location: string
}

/** Góc nhìn nhóm – không có tên nhóm khác. */
export interface TeamMeetingSchedule {
  lecturerName: string
  status: 'PUBLISHED' | 'CLOSED'
  location: string
  bookingDeadline: string
  canBook: boolean
  mySlot?: ScheduleSlot | null
  slots: ScheduleSlot[]
}

export const DAY_NAMES = ['', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy', 'Chủ nhật']

/** "Thứ Ba 09:15". */
export const slotLabel = (slot: Pick<ScheduleSlot, 'dayOfWeek' | 'startTime'>) => `${DAY_NAMES[slot.dayOfWeek]} ${slot.startTime}`

const minutes = (time: string) => {
  const [h, m] = time.split(':').map(Number)
  return h * 60 + m
}

/** Xem trước số slot trước khi lưu (server cắt slot giống vậy); khung sai giờ tính 0. */
export const countSlots = (windows: ScheduleWindow[], slotMinutes: number) =>
  slotMinutes > 0 ? windows.reduce((n, w) => n + Math.max(0, Math.floor((minutes(w.endTime) - minutes(w.startTime)) / slotMinutes)), 0) : 0

export const scheduleKeys = {
  mine: ['meeting-schedules', 'me'] as const,
  team: (workspaceId: string) => ['meeting-schedules', 'workspace', workspaceId] as const,
}

/** Giảng viên – lịch của mình; null khi chưa tạo. */
export function useMySchedule() {
  return useQuery({
    queryKey: scheduleKeys.mine,
    queryFn: async () => (await api.get<MeetingSchedule | null>('/api/meeting-schedules/me')).data ?? null,
  })
}

/** Giảng viên lưu khung giờ, đăng lịch, đổi slot cho nhóm – mỗi thao tác trả lịch mới. */
export function useScheduleActions() {
  const queryClient = useQueryClient()
  const onSuccess = (schedule: MeetingSchedule) => queryClient.setQueryData(scheduleKeys.mine, schedule)
  return {
    save: useMutation({ mutationFn: async (input: MeetingScheduleInput) => (await api.put<MeetingSchedule>('/api/meeting-schedules/me', input)).data, onSuccess }),
    publish: useMutation({ mutationFn: async () => (await api.post<MeetingSchedule>('/api/meeting-schedules/me/publish')).data, onSuccess }),
    move: useMutation({
      mutationFn: async ({ workspaceId, slotId }: { workspaceId: string; slotId: string }) =>
        (await api.put<MeetingSchedule>(`/api/meeting-schedules/me/bookings/${workspaceId}`, { slotId })).data,
      onSuccess,
    }),
  }
}

/** Dự án của nhóm hiện tại (khớp theo nhóm, không lấy dự án đầu tiên khi sinh viên có dự án của kỳ trước). */
export function useTeamWorkspaceId() {
  const team = useMyTeam()
  const workspaces = useMyWorkspaces()
  return {
    workspaceId: workspaces.data?.find((w) => w.teamId === team.data?.id)?.id,
    isLeader: team.data?.myRelation === 'LEADER',
    isLoading: team.isLoading || workspaces.isLoading,
    error: team.error ?? workspaces.error,
  }
}

/** Nhóm – lịch của giảng viên hướng dẫn; null khi giảng viên chưa đăng. */
export function useTeamSchedule(workspaceId: string | undefined) {
  return useQuery({
    queryKey: scheduleKeys.team(workspaceId ?? ''),
    queryFn: async () => (await api.get<TeamMeetingSchedule | null>(`/api/workspaces/${workspaceId}/meeting-schedule`)).data ?? null,
    enabled: !!workspaceId,
  })
}

/** Trưởng nhóm đặt / đổi slot tới hạn. */
export function useBookSlot(workspaceId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (slotId: string) => (await api.put<TeamMeetingSchedule>(`/api/workspaces/${workspaceId}/slot-booking`, { slotId })).data,
    onSuccess: (schedule) => queryClient.setQueryData(scheduleKeys.team(workspaceId), schedule),
  })
}

/** Lỗi nghiệp vụ C09 → câu tiếng Việt. */
export const scheduleErrors: Record<string, string> = {
  'slot-taken': 'Slot này vừa có nhóm khác chọn. Hãy chọn slot còn trống.',
  'booking-closed': 'Đã hết hạn chọn slot. Liên hệ giảng viên nếu cần đổi.',
  'not-leader': 'Chỉ trưởng nhóm chọn được slot.',
  'slot-booked': 'Khung giờ mới bỏ mất slot đã có nhóm. Đổi nhóm sang slot khác trước.',
  'no-exe201-team': 'Bạn chưa hướng dẫn nhóm EXE201 nào.',
  'schedule-has-no-slot': 'Lịch chưa có slot. Thêm khung giờ trước khi đăng.',
}
