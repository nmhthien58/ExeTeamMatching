import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import type { Paged, TableQuery } from '@/shared/components/DataTable'
import type { CourseCode } from '@/shared/phase/usePhase'
import type { CatalogRef } from '@/features/suggestions/api'

// academic.yaml tag Lecturers (W11) và danh mục domain.

export interface Lecturer {
  id: string
  fullName: string
  email: string
  phone?: string | null
  domains: CatalogRef[]
  /** null = không giới hạn (rule 9). */
  maxTeamsExe101: number | null
  maxTeamsExe201: number | null
  isActive: boolean
}

export interface LecturerInput {
  fullName: string
  email: string
  phone: string | null
  domainIds: number[]
  maxTeamsExe101: number | null
  maxTeamsExe201: number | null
  isActive?: boolean
}

/** Domain trong danh mục; domain đã ngừng dùng chỉ được giữ ở người đang có. */
export interface DomainItem extends CatalogRef {
  isActive?: boolean
}

export const lecturerKeys = {
  all: ['lecturers'] as const,
  loads: (course: CourseCode) => ['advisor-matching', 'LECTURER', course, 'loads'] as const,
}

/** W11 – danh sách giảng viên cho DataTable (API chỉ tìm kiếm và phân trang, sắp theo họ tên). */
export const fetchLecturers = async (query: TableQuery) =>
  (await api.get<Paged<Lecturer>>('/api/lecturers', { params: { search: query.search, page: query.page, pageSize: query.pageSize } })).data

export function useSaveLecturer() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, input }: { id?: string; input: LecturerInput }) =>
      (id ? await api.put<Lecturer>(`/api/lecturers/${id}`, input) : await api.post<Lecturer>('/api/lecturers', input)).data,
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: lecturerKeys.all })
      // Khóa giảng viên thì mentor gắn với họ khóa theo.
      void client.invalidateQueries({ queryKey: ['mentors'] })
    },
  })
}

/**
 * Số nhóm đang hướng dẫn của từng giảng viên trong môn, lấy từ bảng ghép W19 (Team Service).
 * Chỉ để hiển thị: lỗi thì bảng hiện "–", không chặn màn hình.
 */
export function useLecturerLoads(course: CourseCode) {
  return useQuery({
    queryKey: lecturerKeys.loads(course),
    queryFn: async () => {
      const overview = (
        await api.get<{ advisorLoads: { advisorId: string; assignedCount: number }[] }>('/api/advisor-matching/LECTURER/overview', {
          params: { courseCode: course, page: 1, pageSize: 1 },
        })
      ).data
      return new Map(overview.advisorLoads.map((l) => [l.advisorId, l.assignedCount]))
    },
    retry: false,
  })
}

export function useDomainCatalog() {
  return useQuery({
    queryKey: ['catalogs', 'domains'],
    queryFn: async () => (await api.get<DomainItem[]>('/api/domains')).data,
    staleTime: 10 * 60_000,
  })
}
