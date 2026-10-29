import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import type { Paged, TableQuery } from '@/shared/components/DataTable'

export interface CatalogRef {
  id: number
  code?: string | null
  name?: string | null
}

export interface StudentRef {
  id: string
  studentCode: string
  fullName: string
}

export interface TeamCardResponse {
  id: string
  name: string
  projectId?: string | null
  projectName?: string | null
  isProjectContinued: boolean
  courseCode: string
  status: string
  domains: CatalogRef[]
  memberCount: number
  openSlots: number
  leader?: StudentRef | null
  lecturerName?: string | null
  mentorName?: string | null
}

export interface ProjectVersion {
  version: number
  name: string
  description?: string | null
  changeType: string
  reason?: string | null
  semesterCode?: string | null
  courseCode: string
  changedByName?: string | null
  createdAt: string
}

export interface ProjectTeam {
  team: TeamCardResponse
  semesterCode?: string | null
  keptMemberPercent?: number | null
  instructorName?: string | null
  mentorName?: string | null
}

export interface ProjectDetail {
  id: string
  name: string
  description?: string | null
  currentVersion: number
  originCourseCode: string
  originSemesterCode?: string | null
  versions: ProjectVersion[]
  teams: ProjectTeam[]
}

export interface Semester {
  id: string
  code: string
  name: string
}

export const projectKeys = {
  all: ['projects'] as const,
  list: (semesterId: string | undefined, domain: string | undefined, pivotedOnly: boolean, query: TableQuery) =>
    [...projectKeys.all, 'list', semesterId, domain, pivotedOnly, query] as const,
  unlinked: (semesterId: string | undefined) => [...projectKeys.all, 'unlinked', semesterId] as const,
  semesters: ['semesters', 'all'] as const,
}

export async function fetchProjects(
  query: TableQuery,
  semesterId?: string,
  domain?: string,
  pivotedOnly?: boolean,
): Promise<Paged<ProjectDetail>> {
  const { data } = await api.get<Paged<ProjectDetail>>('/api/projects', {
    params: {
      page: query.page,
      pageSize: query.pageSize,
      search: query.search,
      semesterId: semesterId || undefined,
      domain: domain || undefined,
      pivotedOnly: pivotedOnly || undefined,
    },
  })
  return data
}

export async function fetchUnlinkedTeams(semesterId: string): Promise<TeamCardResponse[]> {
  const { data } = await api.get<TeamCardResponse[]>('/api/projects/unlinked-teams', {
    params: { semesterId },
  })
  return data
}

export function useSemesters() {
  return useQuery({
    queryKey: projectKeys.semesters,
    queryFn: async () => {
      const { data } = await api.get<Semester[]>('/api/semesters')
      return data
    },
    staleTime: 5 * 60_000,
  })
}

export function useUnlinkedTeams(semesterId: string | undefined) {
  return useQuery({
    queryKey: projectKeys.unlinked(semesterId),
    queryFn: () => (semesterId ? fetchUnlinkedTeams(semesterId) : Promise.resolve([])),
    enabled: !!semesterId,
  })
}

export function useLinkTeam() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async ({ projectId, teamId }: { projectId: string; teamId: string }) => {
      const { data } = await api.post<ProjectDetail>(`/api/projects/${projectId}/link-team`, { teamId })
      return data
    },
    onSuccess: () => {
      client.invalidateQueries({ queryKey: projectKeys.all })
    },
  })
}
