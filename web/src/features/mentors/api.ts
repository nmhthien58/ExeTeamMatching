import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import type { Paged, TableQuery } from '@/shared/components/DataTable'
import type { CatalogRef } from '@/features/suggestions/api'
import type { Lecturer } from '@/features/lecturers/api'

// academic.yaml tag Mentors (W27, rule 20).

export interface Mentor {
  id: string
  fullName: string
  email: string
  organization?: string | null
  isInternal: boolean
  lecturerId?: string | null
  description?: string | null
  domains: CatalogRef[]
  maxTeams: number
  preferredTeams?: number | null
  /** Từ Team Service. */
  currentTeamCount: number
  isActive: boolean
}

/** Mentor gắn giảng viên dùng email giảng viên nên không gửi `email`. */
export interface MentorInput {
  fullName: string
  email?: string
  organization: string | null
  isInternal: boolean
  lecturerId: string | null
  domainIds: number[]
  maxTeams: number
  isActive?: boolean
}

export const mentorKeys = { all: ['mentors'] as const }

/** W27 – danh sách mentor cho DataTable: tìm kiếm, lọc trong / ngoài trường, còn nhận nhóm, domain. */
export const fetchMentors = async (query: TableQuery) =>
  (
    await api.get<Paged<Mentor>>('/api/mentors', {
      params: {
        search: query.search,
        isInternal: query.filters.isInternal,
        hasCapacity: query.filters.hasCapacity,
        domainId: query.filters.domainId,
        page: query.page,
        pageSize: query.pageSize,
      },
    })
  ).data

export function useSaveMentor() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, input }: { id?: string; input: MentorInput }) =>
      (id ? await api.put<Mentor>(`/api/mentors/${id}`, input) : await api.post<Mentor>('/api/mentors', input)).data,
    onSuccess: () => void client.invalidateQueries({ queryKey: mentorKeys.all }),
  })
}

/** Giảng viên để gắn mentor trong trường (một trang 100 người là đủ cho một bộ môn). */
export function useLecturerOptions() {
  return useQuery({
    queryKey: ['lecturers', 'options'],
    queryFn: async () => (await api.get<Paged<Lecturer>>('/api/lecturers', { params: { page: 1, pageSize: 100 } })).data.items,
    staleTime: 60_000,
  })
}
