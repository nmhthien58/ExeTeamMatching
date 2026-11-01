import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'

/** academic.yaml, tag Semesters. */
export interface Semester {
  id: string
  code: string
  name: string
  startDate: string
  endDate: string
  isCurrent: boolean
  mentoringStartWeek: number
  expectedMentorSessions: number
  slotBookingDeadlineWeek: number
  meetingSlotMinutes: number
  checkpointTargetWeeks: number[] | null
}

export type SemesterInput = Omit<Semester, 'id'>

export function useSemesterList() {
  return useQuery({
    queryKey: ['semesters'],
    queryFn: async () => (await api.get<Semester[]>('/api/semesters')).data,
  })
}

/** Có `id` thì sửa, không có thì tạo. Đổi học kỳ hiện hành làm đổi giai đoạn môn nên làm mới cả `phases`. */
export function useSaveSemester() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, input }: { id?: string; input: SemesterInput }) =>
      (id ? await api.put<Semester>(`/api/semesters/${id}`, input) : await api.post<Semester>('/api/semesters', input)).data,
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['semesters'] })
      void client.invalidateQueries({ queryKey: ['phases'] })
    },
  })
}
