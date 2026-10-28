import { useMutation } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'

// Định nghĩa theo docs/contracts/openapi/notification.yaml (tag Announcements).

export type AnnouncementAudience =
  | 'ALL_STUDENTS'
  | 'STUDENTS_WITHOUT_TEAM'
  | 'ALL_LECTURERS'
  | 'ALL_MENTORS'
  | 'TEAMS_WITHOUT_MENTOR'
  | 'EVERYONE'

export interface AnnouncementInput {
  audience: AnnouncementAudience
  courseCode?: 'EXE101' | 'EXE201' | null
  title: string
  content: string
  sendEmail?: boolean
}

export interface AnnouncementResponse {
  recipientCount: number
}

export const AUDIENCE_OPTIONS: { value: AnnouncementAudience; label: string }[] = [
  { value: 'EVERYONE', label: 'Tất cả mọi người' },
  { value: 'ALL_STUDENTS', label: 'Tất cả sinh viên' },
  { value: 'STUDENTS_WITHOUT_TEAM', label: 'Sinh viên chưa có nhóm' },
  { value: 'ALL_LECTURERS', label: 'Tất cả giảng viên' },
  { value: 'ALL_MENTORS', label: 'Tất cả mentor' },
  { value: 'TEAMS_WITHOUT_MENTOR', label: 'Nhóm chưa có mentor' },
]

export const COURSE_OPTIONS: { value: 'EXE101' | 'EXE201' | ''; label: string }[] = [
  { value: '', label: 'Tất cả môn' },
  { value: 'EXE101', label: 'EXE101' },
  { value: 'EXE201', label: 'EXE201' },
]

/** W23 – Gửi thông báo chung (Admin). */
export function useSendAnnouncement() {
  return useMutation({
    mutationFn: async (input: AnnouncementInput) => {
      const res = await api.post<AnnouncementResponse>('/api/announcements', input)
      return res.data
    },
  })
}
