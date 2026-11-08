import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import type { CatalogRef } from '@/features/suggestions/api'

// Kiểu theo docs/contracts/openapi/academic.yaml và notification.yaml – chỉ các trường màn hình hồ sơ dùng.

export const skills = [
  { key: 'TECHNOLOGY', label: 'Công nghệ' },
  { key: 'UI_UX_DESIGN', label: 'Thiết kế UI/UX' },
  { key: 'MANAGEMENT', label: 'Quản trị' },
  { key: 'MARKETING', label: 'Marketing' },
  { key: 'COMMUNICATION_PLANNING', label: 'Plan truyền thông' },
] as const

export type SkillName = (typeof skills)[number]['key']

export interface SkillRating {
  skill: SkillName
  level: number
}

export interface StudentProfile {
  id: string
  studentCode: string
  fullName: string
  email: string
  major: CatalogRef
  specialization?: CatalogRef | null
  classCode?: string | null
  courseCode?: 'EXE101' | 'EXE201' | null
  hasTeam: boolean
  isSeekingTeam: boolean
  contactEmailVisible: boolean
  skills: SkillRating[]
  description?: string | null
  profileCompleted: boolean
  currentTeamId?: string | null
}

export interface StudentProfileInput {
  specializationId?: number | null
  skills: SkillRating[]
  description?: string
  /** Bỏ trống thì giữ nguyên (ngoài giai đoạn lập nhóm S03 không có công tắc này). */
  isSeekingTeam?: boolean
}

export interface LecturerProfile {
  id: string
  fullName: string
  email: string
  domains: CatalogRef[]
  maxTeamsExe101?: number | null
  maxTeamsExe201?: number | null
  desiredTeamsExe101?: number | null
  desiredTeamsExe201?: number | null
  isProfileCompleted: boolean
  profileCompletedAt?: string | null
}

export interface LecturerProfileInput {
  domainIds: number[]
  desiredTeamsExe101?: number | null
  desiredTeamsExe201?: number | null
}

const keys = {
  me: ['students', 'me', 'profile'] as const,
  student: (id: string) => ['students', id] as const,
  lecturerMe: ['lecturers', 'me'] as const,
  preferences: ['notifications', 'preferences'] as const,
  sentInvitations: (teamId: string) => ['teams', teamId, 'invitations', 'PENDING'] as const,
}

/** Hồ sơ của tôi (A05, S02, S03). */
export function useMyProfile(enabled = true) {
  return useQuery({
    queryKey: keys.me,
    queryFn: async () => (await api.get<StudentProfile>('/api/students/me')).data,
    enabled,
  })
}

export function useSaveProfile() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: StudentProfileInput) => (await api.put<StudentProfile>('/api/students/me', input)).data,
    onSuccess: (profile) => {
      queryClient.setQueryData(keys.me, profile)
    },
  })
}

/** S04 – hồ sơ sinh viên khác; email rỗng và `contactEmailVisible = false` với người ngoài nhóm (rule 11). */
export function useStudentProfile(id: string) {
  return useQuery({
    queryKey: keys.student(id),
    queryFn: async () => (await api.get<StudentProfile>(`/api/students/${id}`)).data,
  })
}

/** Id sinh viên đang có lời mời còn hiệu lực của nhóm. API chỉ cho trưởng nhóm gọi. */
export function useInvitedStudentIds(teamId: string | undefined, enabled: boolean) {
  return useQuery({
    queryKey: keys.sentInvitations(teamId ?? ''),
    queryFn: async () =>
      (await api.get<{ invitee: { id: string } }[]>(`/api/teams/${teamId}/invitations`, { params: { status: 'PENDING' } })).data.map((i) => i.invitee.id),
    enabled: !!teamId && enabled,
  })
}

/** L06 – hồ sơ giảng viên của tôi. */
export function useLecturerProfile() {
  return useQuery({
    queryKey: keys.lecturerMe,
    queryFn: async () => (await api.get<LecturerProfile>('/api/lecturers/me')).data,
  })
}

export function useSaveLecturerProfile() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: LecturerProfileInput | number[]) => {
      const payload = Array.isArray(input) ? { domainIds: input } : input
      return (await api.put<LecturerProfile>('/api/lecturers/me', payload)).data
    },
    onSuccess: (profile) => {
      queryClient.setQueryData(keys.lecturerMe, profile)
    },
  })
}

export function useDomains() {
  return useQuery({
    queryKey: ['catalogs', 'domains'],
    queryFn: async () => (await api.get<(CatalogRef & { isActive?: boolean })[]>('/api/domains')).data.filter((d) => d.isActive !== false),
    staleTime: 10 * 60_000,
  })
}

/** A07 – bật / tắt nhận email thông báo. */
export function useEmailPreferences() {
  return useQuery({
    queryKey: keys.preferences,
    queryFn: async () => (await api.get<{ emailEnabled: boolean }>('/api/notifications/preferences')).data,
  })
}

export function useSaveEmailPreferences() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (emailEnabled: boolean) => {
      await api.put('/api/notifications/preferences', { emailEnabled })
      return { emailEnabled }
    },
    onSuccess: (preferences) => {
      queryClient.setQueryData(keys.preferences, preferences)
    },
  })
}
