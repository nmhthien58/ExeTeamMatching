import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import type { PersonRef } from '@/features/workspace/api'

// collab.yaml tag Meetings (C07, C08, rule 25).

export type MeetingStatus = 'PROPOSED' | 'SCHEDULED' | 'CANCELLED' | 'DONE'

export interface MeetingCard {
  id: string
  workspaceId: string
  teamName: string
  title: string
  status: MeetingStatus
  organizer: PersonRef
  isMentorSession: boolean
  /** Khung đã chốt. */
  startsAt?: string | null
  slotCount: number
  location?: string | null
  myAnswerPending: boolean
  isWeekly: boolean
  weekNo?: number | null
}

export interface MeetingSlot {
  id: string
  startsAt: string
  durationMinutes: number
  yesCount: number
  inviteeCount: number
  myAnswer?: 'YES' | 'NO' | null
  yesPeople: PersonRef[]
}

export interface MeetingDetail extends MeetingCard {
  agenda?: string | null
  slots: MeetingSlot[]
  suggestedSlotId?: string | null
  cancelReason?: string | null
  minutes?: { content: string; recordedByName: string; recordedAt: string } | null
  createdTasks: { id: string; title: string }[]
}

export interface MeetingList {
  mentorSessionsDone: number
  expectedMentorSessions: number
  items: MeetingCard[]
}

export interface SlotInput {
  startsAt: string
  durationMinutes: number
}

export interface MeetingInput {
  title: string
  agenda?: string | null
  location?: string | null
  slots: SlotInput[]
}

export const MAX_SLOTS = 3

export const meetingKeys = {
  list: (workspaceId: string) => ['meetings', 'workspace', workspaceId] as const,
  detail: (id: string) => ['meetings', id] as const,
}

export function useMeetings(workspaceId: string) {
  return useQuery({
    queryKey: meetingKeys.list(workspaceId),
    queryFn: async () => (await api.get<MeetingList>(`/api/workspaces/${workspaceId}/meetings`)).data,
  })
}

export function useMeeting(id: string) {
  return useQuery({ queryKey: meetingKeys.detail(id), queryFn: async () => (await api.get<MeetingDetail>(`/api/meetings/${id}`)).data })
}

/** Mọi thao tác trên một cuộc họp trả MeetingDetail mới: ghi vào cache chi tiết và làm mới danh sách. */
function useMeetingMutation<T>(fn: (input: T) => Promise<MeetingDetail>) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: (meeting) => {
      client.setQueryData(meetingKeys.detail(meeting.id), meeting)
      void client.invalidateQueries({ queryKey: meetingKeys.list(meeting.workspaceId) })
    },
  })
}

export const useProposeMeeting = (workspaceId: string) =>
  useMeetingMutation(async (input: MeetingInput) => (await api.post<MeetingDetail>(`/api/workspaces/${workspaceId}/meetings`, input)).data)

export const useAnswerMeeting = (id: string) =>
  useMeetingMutation(async (answers: { slotId: string; answer: 'YES' | 'NO' }[]) => (await api.put<MeetingDetail>(`/api/meetings/${id}/responses`, { answers })).data)

export const useAddSlot = (id: string) => useMeetingMutation(async (slot: SlotInput) => (await api.post<MeetingDetail>(`/api/meetings/${id}/slots`, slot)).data)

export const useConfirmSlot = (id: string) =>
  useMeetingMutation(async (slotId: string) => (await api.post<MeetingDetail>(`/api/meetings/${id}/confirm`, { slotId })).data)

export const useCancelMeeting = (id: string) =>
  useMeetingMutation(async (reason: string) => (await api.post<MeetingDetail>(`/api/meetings/${id}/cancel`, { reason })).data)

export const useSaveMinutes = (id: string) =>
  useMeetingMutation(async (content: string) => (await api.put<MeetingDetail>(`/api/meetings/${id}/minutes`, { content })).data)

/** Chuyển các dòng biên bản thành việc trong một bước (sourceMeetingId được gắn). */
export function useMinutesToTasks(id: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async (titles: string[]) => (await api.post<{ id: string; title: string }[]>(`/api/meetings/${id}/minutes/tasks`, { items: titles.map((title) => ({ title })) })).data,
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: meetingKeys.detail(id) })
      void client.invalidateQueries({ queryKey: ['tasks'] })
    },
  })
}

/** Người được đề xuất họp: trưởng nhóm, giảng viên hướng dẫn, mentor (rule 25). */
export const canPropose = (role?: string) => role === 'LEADER' || role === 'INSTRUCTOR' || role === 'MENTOR'

/** Dòng biên bản → tên việc: bỏ gạch đầu dòng, bỏ dòng trống, cắt 200 ký tự. */
export const minutesLines = (content: string) =>
  content
    .split('\n')
    .map((l) => l.replace(/^\s*([-*•]|\d+[.)])\s*/, '').trim())
    .filter(Boolean)
    .map((l) => l.slice(0, 200))
