import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import type { CatalogRef, TeamCard, TeamDetail } from '@/features/suggestions/api'
import type { TaskCard } from '@/features/tasks/api'
import type { WorkspaceOverview } from '@/features/workspace/api'

export type AdvisorKind = 'LECTURER' | 'MENTOR'
export type CourseCode = 'EXE101' | 'EXE201'

export interface TopicInterest {
  id: string
  teamId: string
  advisorKind: AdvisorKind
  status: 'OPEN' | 'WITHDRAWN' | 'CLOSED'
}

export interface TopicRecommendation {
  team: TeamCard
  matchedDomains: CatalogRef[]
  otherInterestedCount: number
  myPreferenceRank?: number | null
  myInterest?: TopicInterest | null
  wasMyEXE101Team: boolean
  hasAdvisor: boolean
}

export interface TopicRecommendations {
  openInterestCount: number
  interestLimit: number
  remainingCapacity?: number | null
  interestOpen: boolean
  matchingAt?: string | null
  items: TopicRecommendation[]
}

export interface AdvisedTeam {
  team: TeamCard
  reason: string
  preferenceRank?: number | null
}

export interface LecturerTeams {
  maxTeams?: number | null
  instructing: AdvisedTeam[]
  mentoring: AdvisedTeam[]
}

export interface CheckpointItem {
  checkpointNo: number
  presented: boolean
  presentedOn?: string | null
  comment?: string | null
  targetWeek?: number | null
  overdue: boolean
  canUndo: boolean
}

export interface CheckpointProgress {
  workspaceId: string
  teamName: string
  doneCount: number
  items: CheckpointItem[]
}

export interface AdvisorMeeting {
  id: string
  status: 'PROPOSED' | 'SCHEDULED' | 'CANCELLED' | 'DONE'
  startsAt?: string | null
  isMentorSession: boolean
}

export interface MentorProfile {
  id: string
  fullName: string
  maxTeams: number
  preferredTeams?: number | null
  currentTeamCount: number
  domains: CatalogRef[]
}

export interface AdvisorTeamDetail extends TeamDetail {
  ideaDescription?: string | null
}

const keys = {
  topics: (kind: AdvisorKind, course?: CourseCode) => ['advisor', 'topics', kind, course] as const,
  interests: (kind: AdvisorKind) => ['advisor', 'interests', kind] as const,
  preferences: (course?: CourseCode) => ['advisor', 'preferences', course] as const,
  lecturerTeams: (course?: CourseCode) => ['advisor', 'lecturer-teams', course] as const,
  mentorTeams: ['advisor', 'mentor-teams'] as const,
  checkpoints: ['advisor', 'checkpoints'] as const,
  checkpoint: (workspaceId: string) => ['advisor', 'checkpoint', workspaceId] as const,
  mentorProfile: ['advisor', 'mentor-profile'] as const,
}

export function useTopics(kind: AdvisorKind, course?: CourseCode, enabled = true) {
  return useQuery({ queryKey: keys.topics(kind, course), queryFn: async () =>
    (await api.get<TopicRecommendations>('/api/topic-recommendations/me', { params: { advisorKind: kind, courseCode: course, page: 1, pageSize: 100 } })).data, enabled })
}

export function useInterests(kind: AdvisorKind) {
  return useQuery({ queryKey: keys.interests(kind), queryFn: async () =>
    (await api.get<TopicInterest[]>('/api/topic-interests/me', { params: { advisorKind: kind } })).data })
}

export function useInterestedTeams(interests: TopicInterest[] | undefined, enabled: boolean) {
  return useQuery({
    queryKey: ['advisor', 'interested-teams', interests?.map((item) => item.teamId).sort().join(',')],
    queryFn: async () => Promise.all((interests ?? []).map(async (item) =>
      (await api.get<TeamCard>(`/api/teams/${item.teamId}`)).data)),
    enabled: enabled && !!interests,
  })
}

export function useLecturerPreferences(course?: CourseCode, enabled = true) {
  return useQuery({ queryKey: keys.preferences(course), queryFn: async () =>
    (await api.get<{ team: TeamCard; rank: number; iAmInterested: boolean }[]>('/api/lecturer-preferences/me', { params: { courseCode: course } })).data, enabled })
}

export function useToggleInterest(kind: AdvisorKind, course?: CourseCode) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async ({ teamId, interestId }: { teamId: string; interestId?: string }) => {
      if (interestId) return api.delete(`/api/topic-interests/${interestId}`)
      return api.post(`/api/teams/${teamId}/topic-interests`, { advisorKind: kind })
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: keys.topics(kind, course) })
      void qc.invalidateQueries({ queryKey: keys.interests(kind) })
      void qc.invalidateQueries({ queryKey: ['advisor', 'preferences'] })
    },
  })
}

export function useLecturerTeams(course?: CourseCode, enabled = true) {
  return useQuery({ queryKey: keys.lecturerTeams(course), queryFn: async () =>
    (await api.get<LecturerTeams>('/api/lecturer-teams/me', { params: { courseCode: course } })).data, enabled })
}

export function useMentorTeams(enabled = true) {
  return useQuery({ queryKey: keys.mentorTeams, queryFn: async () =>
    (await api.get<AdvisedTeam[]>('/api/mentor-teams/me')).data, enabled })
}

export function useAdvisorTeamDetail(teamId?: string) {
  return useQuery({ queryKey: ['advisor', 'team', teamId], queryFn: async () =>
    (await api.get<AdvisorTeamDetail>(`/api/teams/${teamId}`)).data, enabled: !!teamId })
}

export function useAllCheckpoints(enabled = true) {
  return useQuery({ queryKey: keys.checkpoints, queryFn: async () =>
    (await api.get<CheckpointProgress[]>('/api/checkpoints/me')).data, enabled })
}

export function useCheckpoint(workspaceId?: string) {
  return useQuery({ queryKey: keys.checkpoint(workspaceId ?? ''), queryFn: async () =>
    (await api.get<CheckpointProgress>(`/api/workspaces/${workspaceId}/checkpoints`)).data, enabled: !!workspaceId })
}

export function useMarkCheckpoint(workspaceId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async ({ no, presentedOn, comment }: { no: number; presentedOn: string; comment: string }) =>
      (await api.put<CheckpointProgress>(`/api/workspaces/${workspaceId}/checkpoints/${no}`, { presentedOn, comment: comment || null })).data,
    onSuccess: (progress) => {
      qc.setQueryData(keys.checkpoint(workspaceId), progress)
      void qc.invalidateQueries({ queryKey: keys.checkpoints })
    },
  })
}

export function useUndoCheckpoint(workspaceId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async (no: number) => api.delete(`/api/workspaces/${workspaceId}/checkpoints/${no}`),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: keys.checkpoint(workspaceId) })
      void qc.invalidateQueries({ queryKey: keys.checkpoints })
    },
  })
}

export function useMentorProfile() {
  return useQuery({ queryKey: keys.mentorProfile, queryFn: async () =>
    (await api.get<MentorProfile>('/api/mentors/me')).data })
}

export function useAdvisorDomains(kind: AdvisorKind) {
  return useQuery({ queryKey: ['advisor', 'domains', kind], queryFn: async () =>
    kind === 'LECTURER'
      ? (await api.get<{ domains: CatalogRef[] }>('/api/lecturers/me')).data.domains
      : (await api.get<{ domains: CatalogRef[] }>('/api/mentors/me')).data.domains })
}

export function useAdvisorQuestions() {
  // Key riêng: `useOpenQuestions` của tin nhắn lưu danh sách ở ['advisor', 'questions', 'OPEN'], còn đây chỉ lưu con số.
  return useQuery({ queryKey: ['advisor', 'questions', 'OPEN', 'count'], queryFn: async () =>
    (await api.get<{ totalCount: number }>('/api/advisor/questions', { params: { status: 'OPEN', page: 1, pageSize: 1 } })).data.totalCount })
}

export function useAdvisorActivity(workspaces: Pick<WorkspaceOverview, 'id' | 'myRole'>[] | undefined, includeDeliverables: boolean) {
  const rows = [...(workspaces ?? [])].sort((a, b) => a.id.localeCompare(b.id))
  return useQuery({
    queryKey: ['advisor', 'activity', includeDeliverables, rows.map((row) => `${row.id}:${row.myRole}`).join(',')],
    enabled: !!workspaces,
    queryFn: async () => Promise.all(rows.map(async ({ id, myRole }) => {
      const [meetings, tasks] = await Promise.all([
        api.get<{ mentorSessionsDone: number; expectedMentorSessions: number; items: AdvisorMeeting[] }>(`/api/workspaces/${id}/meetings`),
        includeDeliverables && myRole === 'INSTRUCTOR' ? api.get<TaskCard[]>(`/api/workspaces/${id}/tasks`) : Promise.resolve(null),
      ])
      return { workspaceId: id, meetings: meetings.data, tasks: tasks?.data ?? [], deliverables: tasks?.data.filter((item) => item.kind === 'DELIVERABLE') ?? [] }
    })),
  })
}

export function useSavePreferredTeams() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async (preferredTeams: number) =>
      (await api.put<MentorProfile>('/api/mentors/me', { preferredTeams })).data,
    onSuccess: (profile) => qc.setQueryData(keys.mentorProfile, profile),
  })
}
