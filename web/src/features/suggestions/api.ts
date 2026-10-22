import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, ApiError } from '@/shared/api/apiClient'

// Kiểu theo docs/contracts/openapi/team.yaml và academic.yaml – chỉ các trường màn hình gợi ý dùng.

export interface CatalogRef {
  id: number
  code: string
  name: string
}

export interface StudentRef {
  id: string
  studentCode: string
  fullName: string
  specialization?: CatalogRef | null
}

export interface TeamRequirement {
  memberCount: number
  minSize: number
  maxSize: number
  specializationCount: number
  minSpecializations: number
  isValid: boolean
  missingSpecializations: CatalogRef[]
}

export type TeamStatus = 'DRAFT' | 'ACTIVE' | 'NEEDS_REVIEW' | 'FINALIZED' | 'DISBANDED'

export interface TeamCard {
  id: string
  name: string
  projectName?: string | null
  courseCode: 'EXE101' | 'EXE201'
  status: TeamStatus
  domains: CatalogRef[]
  memberCount: number
  openSlots: number
  leader?: StudentRef
  lecturerName?: string | null
  mentorName?: string | null
  requirement?: TeamRequirement
}

export interface TeamMember extends StudentRef {
  role: 'LEADER' | 'MEMBER'
  phone?: string | null
  email?: string | null
}

export interface TeamDetail extends TeamCard {
  members: TeamMember[]
  isAutoAssigned?: boolean
  myRelation?: 'LEADER' | 'MEMBER' | 'INVITED' | 'REQUESTED' | 'NONE'
  finalizedAt?: string | null
}

/** GET /api/teams/suggested-for-me (S07) – Team gọi Matching.SuggestTeams. */
export interface TeamSuggestion {
  team: TeamCard
  score: number
  reasons: string[]
}

/** GET /api/teams/{id}/suggested-members (S18) – Team gọi Matching.SuggestMembers. */
export interface MemberSuggestions {
  missingSpecializations: CatalogRef[]
  items: { student: StudentRef; skills: string[]; score: number; reasons: string[]; alreadyInvited: boolean }[]
}

export interface StudentSummary extends StudentRef {
  classCode?: string
  hasTeam: boolean
  isSeekingTeam?: boolean
}

export interface Paged<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

/** Nhãn mức phù hợp – điểm chỉ để sắp xếp, không hiện số % (matching.proto). */
export function fitLabel(score: number): { text: string; tone: 'success' | 'secondary' | 'neutral' } {
  if (score >= 0.6) return { text: 'Rất phù hợp', tone: 'success' }
  if (score >= 0.3) return { text: 'Phù hợp', tone: 'secondary' }
  return { text: 'Có thể phù hợp', tone: 'neutral' }
}

export const suggestionKeys = {
  myTeam: ['teams', 'me'] as const,
  teamsForMe: ['teams', 'suggested-for-me'] as const,
  members: (teamId: string) => ['teams', teamId, 'suggested-members'] as const,
  students: (search: string, specializationId?: number) => ['students', 'search', search, specializationId ?? null] as const,
}

/** Nhóm của tôi; null nếu chưa có nhóm (API trả 404). */
export function useMyTeam() {
  return useQuery({
    queryKey: suggestionKeys.myTeam,
    queryFn: async () => {
      try {
        return (await api.get<TeamDetail>('/api/teams/me')).data
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) return null
        throw error
      }
    },
  })
}

export function useSuggestedTeams() {
  return useQuery({
    queryKey: suggestionKeys.teamsForMe,
    queryFn: async () => (await api.get<TeamSuggestion[]>('/api/teams/suggested-for-me', { params: { limit: 10 } })).data,
  })
}

export function useSuggestedMembers(teamId: string | undefined) {
  return useQuery({
    queryKey: suggestionKeys.members(teamId ?? ''),
    queryFn: async () => (await api.get<MemberSuggestions>(`/api/teams/${teamId}/suggested-members`, { params: { limit: 10 } })).data,
    enabled: !!teamId,
  })
}

/** S19 – sinh viên chưa có nhóm, tìm theo tên / MSSV, lọc chuyên ngành. */
export function useStudentSearch(search: string, specializationId?: number) {
  return useQuery({
    queryKey: suggestionKeys.students(search, specializationId),
    queryFn: async () =>
      (await api.get<Paged<StudentSummary>>('/api/students', { params: { search: search || undefined, specializationId, hasTeam: false, pageSize: 20 } }))
        .data,
  })
}

export function useSpecializations() {
  return useQuery({
    queryKey: ['catalogs', 'specializations'],
    queryFn: async () => (await api.get<CatalogRef[]>('/api/specializations')).data,
    staleTime: 10 * 60_000,
  })
}

/** Trưởng nhóm gửi lời mời (S18, S19) – API của module Invitations. */
export function useInvite(teamId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ studentId, message }: { studentId: string; message?: string }) =>
      (await api.post(`/api/teams/${teamId}/invitations`, { studentId, message })).data,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: suggestionKeys.members(teamId ?? '') })
    },
  })
}
