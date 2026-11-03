import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import type { TeamDetail } from '@/features/suggestions/api'

export interface LecturerRef {
  lecturerId: string
  fullName: string
  domains: { id: number; code: string; name: string }[]
  isInterested?: boolean
}

export interface PreferenceList {
  maxPreferences: number
  locked: boolean
  lockAt: string | null
  preferences: (LecturerRef & { rank: number; isInterested: boolean })[]
  interestedLecturers: LecturerRef[]
  previousInstructor: LecturerRef | null
}

export interface LecturerAssignment {
  lecturerId: string
  lecturerName: string
  reason: string
  preferenceRank: number | null
  source: string
  assignedAt: string
}

export interface LecturerCard {
  id: string
  fullName: string
  domains: { id: number; code: string; name: string }[]
}

export const preferenceKeys = {
  team: (teamId: string) => ['lecturer-preferences', teamId] as const,
  lecturers: (search: string) => ['lecturers', search] as const,
  assignment: (teamId: string) => ['team-lecturer', teamId] as const,
}

export function useTeamPreferences(teamId: string | undefined) {
  return useQuery({
    queryKey: preferenceKeys.team(teamId ?? ''),
    queryFn: async () => (await api.get<PreferenceList>(`/api/teams/${teamId}/lecturer-preferences`)).data,
    enabled: !!teamId,
  })
}

export function useLecturers(search: string) {
  return useQuery({
    queryKey: preferenceKeys.lecturers(search),
    queryFn: async () => (await api.get<{ items: LecturerCard[] }>('/api/lecturers', { params: { search, page: 1, pageSize: 50 } })).data.items,
  })
}

export function useSavePreferences(teamId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async (lecturerIds: string[]) =>
      (await api.put<PreferenceList>(`/api/teams/${teamId}/lecturer-preferences`, { lecturerIds })).data,
    onSuccess: (data) => {
      client.setQueryData(preferenceKeys.team(teamId), data)
      void client.invalidateQueries({ queryKey: ['teams', 'me'] })
    },
  })
}

export function useTeamLecturer(teamId: string | undefined) {
  return useQuery({
    queryKey: preferenceKeys.assignment(teamId ?? ''),
    queryFn: async () => (await api.get<LecturerAssignment | null>(`/api/teams/${teamId}/lecturer`)).data,
    enabled: !!teamId,
  })
}

export type { TeamDetail }
