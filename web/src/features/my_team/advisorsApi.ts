import { useQuery } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'

// Kiểu theo team.yaml (LecturerAssignment, MentorAssignment) và collab.yaml (CheckpointProgress). API trả rỗng khi chưa ghép.

interface LecturerAssignment {
  lecturerName: string
  reason?: string | null
  preferenceRank?: number | null
}

interface MentorAssignment {
  mentorName: string
  reason?: string | null
}

/** Nhãn lý do ghép (quy tắc 9). */
export function lecturerReasonLabel(reason?: string | null, rank?: number | null) {
  switch (reason) {
    case 'MUTUAL':
      return 'Hai bên cùng chọn'
    case 'PREFERENCE':
      return rank ? `Nguyện vọng ${rank}` : 'Nguyện vọng'
    case 'LECTURER_INTEREST':
      return 'Giảng viên quan tâm'
    case 'PREVIOUS_INSTRUCTOR':
      return 'Đã hướng dẫn ở EXE101'
    case 'DOMAIN':
      return 'Theo chuyên môn'
    case 'MANUAL':
      return 'Bộ môn phân công'
    default:
      return undefined
  }
}

/** Nhãn lý do ghép mentor (quy tắc 20). */
export function mentorReasonLabel(reason?: string | null) {
  return reason === 'MENTOR_INTEREST' ? 'Mentor quan tâm' : reason === 'DOMAIN' ? 'Theo chuyên môn' : reason === 'MANUAL' ? 'Bộ môn phân công' : undefined
}

export function useTeamLecturer(teamId: string) {
  return useQuery({
    queryKey: ['teams', teamId, 'lecturer'],
    queryFn: async () => (await api.get<LecturerAssignment | null>(`/api/teams/${teamId}/lecturer`)).data || null,
  })
}

export function useTeamMentor(teamId: string) {
  return useQuery({
    queryKey: ['teams', teamId, 'mentor'],
    queryFn: async () => (await api.get<MentorAssignment | null>(`/api/teams/${teamId}/mentor`)).data || null,
  })
}

/** Số checkpoint đã trình bày trên 4 (quy tắc 27). */
export function useCheckpointsDone(workspaceId: string | undefined) {
  return useQuery({
    queryKey: ['workspaces', workspaceId, 'checkpoints'],
    queryFn: async () => (await api.get<{ doneCount: number }>(`/api/workspaces/${workspaceId}/checkpoints`)).data.doneCount,
    enabled: !!workspaceId,
  })
}
