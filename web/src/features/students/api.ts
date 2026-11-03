import { useQuery } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import type { Paged, TableQuery } from '@/shared/components/DataTable'
import type { StudentProfile } from '@/features/profile/api'
import type { CatalogRef, TeamDetail } from '@/features/suggestions/api'

// academic.yaml tag Students (W09, W10).

export interface StudentSummary {
  id: string
  studentCode: string
  fullName: string
  email: string
  specialization?: CatalogRef | null
  classCode?: string | null
  courseCode?: 'EXE101' | 'EXE201' | null
  /** Từ Team Service; Academic tạm trả false cho tới khi Team có API trạng thái nhóm. */
  hasTeam: boolean
  isSeekingTeam: boolean
}

/** Admin xem được SĐT từ file import (rule 11). */
export interface AdminStudentProfile extends StudentProfile {
  phone?: string | null
}

interface CatalogItem extends CatalogRef {
  isActive: boolean
}

export const studentKeys = { all: ['students', 'admin'] as const }

/** W09 – danh sách sinh viên học kỳ hiện hành cho DataTable: tìm tên / MSSV, lọc môn, mã lớp, chuyên ngành, có nhóm. */
export const fetchStudents = async (query: TableQuery) =>
  (
    await api.get<Paged<StudentSummary>>('/api/students', {
      params: {
        search: query.search,
        courseCode: query.filters.courseCode,
        classCode: query.filters.classCode,
        specializationId: query.filters.specializationId,
        hasTeam: query.filters.hasTeam,
        page: query.page,
        pageSize: query.pageSize,
      },
    })
  ).data

/** W10 – hồ sơ đầy đủ (Admin thấy SĐT, email). */
export function useAdminStudent(id: string) {
  return useQuery({
    queryKey: [...studentKeys.all, id],
    queryFn: async () => (await api.get<AdminStudentProfile>(`/api/students/${id}`)).data,
  })
}

/** Nhóm hiện tại; chỉ gọi khi Academic đã trả `currentTeamId`. */
export function useCurrentTeam(teamId: string | null | undefined) {
  return useQuery({
    queryKey: ['teams', teamId],
    queryFn: async () => (await api.get<TeamDetail>(`/api/teams/${teamId}`)).data,
    enabled: !!teamId,
  })
}

const catalog = (path: string) => async () => (await api.get<CatalogItem[]>(path)).data

/** Bộ lọc W09: mã lớp, chuyên ngành (gồm cả mục đã ngừng vì sinh viên cũ vẫn mang). */
export function useStudentFilterCatalogs() {
  const classCodes = useQuery({ queryKey: ['catalogs', 'class-codes'], queryFn: catalog('/api/class-codes'), staleTime: 10 * 60_000 })
  const specializations = useQuery({ queryKey: ['catalogs', 'specializations'], queryFn: catalog('/api/specializations'), staleTime: 10 * 60_000 })
  return { classCodes: classCodes.data ?? [], specializations: specializations.data ?? [] }
}
