import { useQuery } from '@tanstack/react-query'
import { api, ApiError } from '@/shared/api/apiClient'

// Kiểu theo docs/contracts/openapi/team.yaml (ProjectDetail, ProjectVersion) – S15, quy tắc 26.

export interface ProjectVersion {
  version: number
  name: string
  description?: string | null
  changeType: 'CREATED' | 'EDITED' | 'PIVOT'
  reason?: string | null
  semesterCode: string
  courseCode: 'EXE101' | 'EXE201'
  changedByName?: string | null
  createdAt: string
}

export interface ProjectDetail {
  id: string
  name: string
  originCourseCode: 'EXE101' | 'EXE201'
  originSemesterCode?: string | null
  /** Mới nhất trước. */
  versions: ProjectVersion[]
}

/** Đề tài của nhóm và lịch sử qua các kỳ (BE-76); null nếu nhóm chưa có đề tài (404). */
export function useTeamProject(teamId: string) {
  return useQuery({
    queryKey: ['teams', teamId, 'project'],
    queryFn: async () => {
      try {
        return (await api.get<ProjectDetail>(`/api/teams/${teamId}/project`)).data
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) return null
        throw error
      }
    },
  })
}

/** "Đổi đề tài: EduSpark → EduSpark AI · lý do…"; tên cũ là mốc liền sau (cũ hơn) trong danh sách. */
export function describeVersion(v: ProjectVersion, older?: ProjectVersion) {
  const text = v.changeType === 'CREATED' ? `Tạo đề tài: ${v.name}` : v.changeType === 'PIVOT' ? `Đổi đề tài: ${older ? `${older.name} → ` : ''}${v.name}` : `Sửa đề tài: ${v.name}`
  return v.reason ? `${text} · ${v.reason}` : text
}
