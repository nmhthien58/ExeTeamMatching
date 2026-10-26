import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import type { CatalogRef, StudentRef, TeamCard, TeamDetail } from '@/features/suggestions/api'

// Kiểu theo docs/contracts/openapi/team.yaml (tag Teams, Members, Invitations, JoinRequests). TeamCard / TeamDetail dùng chung với suggestions.

export interface FullTeam extends TeamDetail {
  ideaDescription?: string | null
  isInherited?: boolean
  hasPendingChanges?: boolean
  recruitmentPost?: { title: string; description?: string | null } | null
}

export interface TeamInput {
  name: string
  projectName?: string
  ideaDescription?: string
  domainIds: number[]
  primaryDomainId?: number
}

/** `appliedImmediately = false`: nhóm EXE201 kế thừa – thay đổi chờ Bộ môn duyệt (quy tắc 7). */
export interface MemberChangeResult {
  appliedImmediately: boolean
  team: FullTeam
}

export type RequestStatus = 'PENDING' | 'ACCEPTED' | 'DECLINED' | 'EXPIRED' | 'CANCELLED'

export interface Idea {
  title: string
  description?: string | null
}

export interface JoinRequest {
  id: string
  team?: TeamCard | null
  student: StudentRef
  message?: string | null
  ideas: Idea[]
  hasNoIdea: boolean
  status: RequestStatus
  createdAt: string
  expiresAt: string
}

export interface Invitation {
  id: string
  team?: TeamCard | null
  invitee: StudentRef
  message?: string | null
  status: RequestStatus
  createdAt: string
  expiresAt: string
}

/** Nhãn trạng thái nhóm (quy tắc 15). */
export const teamStatusLabel: Record<TeamDetail['status'], { text: string; tone: 'warning' | 'success' | 'info' | 'neutral'; icon: string }> = {
  DRAFT: { text: 'Nháp', tone: 'neutral', icon: 'edit_note' },
  ACTIVE: { text: 'Hoạt động', tone: 'success', icon: 'check_circle' },
  NEEDS_REVIEW: { text: 'Cần xem lại', tone: 'warning', icon: 'error' },
  FINALIZED: { text: 'Đã chốt', tone: 'info', icon: 'verified' },
  DISBANDED: { text: 'Đã giải tán', tone: 'neutral', icon: 'block' },
}

/** Nhãn trạng thái lời mời / yêu cầu – luôn có chữ (DESIGN.md: không chỉ màu). */
export const requestStatusLabel: Record<RequestStatus, { text: string; tone: 'warning' | 'success' | 'error' | 'neutral' }> = {
  PENDING: { text: 'Đang chờ', tone: 'warning' },
  ACCEPTED: { text: 'Đã chấp nhận', tone: 'success' },
  DECLINED: { text: 'Đã từ chối', tone: 'error' },
  EXPIRED: { text: 'Hết hạn', tone: 'neutral' },
  CANCELLED: { text: 'Đã hủy', tone: 'neutral' },
}

/** "Còn 2 ngày 3 giờ" – thời gian còn lại của lời mời / yêu cầu (quy tắc 8). */
export function timeLeft(expiresAt: string, now = Date.now()): string {
  const minutes = Math.floor((new Date(expiresAt).getTime() - now) / 60_000)
  if (minutes < 0) return 'Đã hết hạn'
  const hours = Math.floor(minutes / 60)
  if (hours >= 24) return `Còn ${Math.floor(hours / 24)} ngày ${hours % 24} giờ`
  return hours > 0 ? `Còn ${hours} giờ` : `Còn ${minutes} phút`
}

/** Sau khi nhóm / lời mời / yêu cầu đổi: tải lại mọi màn liên quan (S01, S12, S15, S18, S20). */
export function refreshTeams(queryClient: QueryClient) {
  for (const key of [['teams'], ['invitations'], ['join-requests'], ['home']]) void queryClient.invalidateQueries({ queryKey: key })
}

export function useDomains() {
  return useQuery({
    queryKey: ['catalogs', 'domains'],
    queryFn: async () => (await api.get<CatalogRef[]>('/api/domains')).data,
    staleTime: 10 * 60_000,
  })
}

/** S13 tạo / S16 sửa nhóm. */
export function useSaveTeam(teamId?: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: TeamInput) =>
      (teamId ? await api.put<FullTeam>(`/api/teams/${teamId}`, input) : await api.post<FullTeam>('/api/teams', input)).data,
    onSuccess: () => refreshTeams(queryClient),
  })
}

/** S17: xóa thành viên, rời nhóm, chuyển trưởng nhóm. */
export function useMemberAction(teamId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (action: { kind: 'remove' | 'transfer'; studentId: string } | { kind: 'leave' }) => {
      const response =
        action.kind === 'remove'
          ? await api.delete<MemberChangeResult>(`/api/teams/${teamId}/members/${action.studentId}`)
          : action.kind === 'transfer'
            ? await api.post<MemberChangeResult>(`/api/teams/${teamId}/transfer-leader`, { studentId: action.studentId })
            : await api.post<MemberChangeResult>(`/api/teams/${teamId}/leave`)
      return response.data
    },
    onSuccess: () => refreshTeams(queryClient),
  })
}

export function useTeamJoinRequests(teamId: string) {
  return useQuery({
    queryKey: ['join-requests', 'team', teamId],
    queryFn: async () => (await api.get<JoinRequest[]>(`/api/teams/${teamId}/join-requests`, { params: { status: 'PENDING' } })).data,
  })
}

export function useSentInvitations(teamId: string) {
  return useQuery({
    queryKey: ['invitations', 'team', teamId],
    queryFn: async () => (await api.get<Invitation[]>(`/api/teams/${teamId}/invitations`)).data,
  })
}

/** Nhận / từ chối yêu cầu tham gia, thu hồi lời mời (S20). */
export function useRequestDecision() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ kind, id }: { kind: 'accept' | 'decline' | 'cancel-invitation'; id: string }) => {
      const url = kind === 'cancel-invitation' ? `/api/invitations/${id}/cancel` : `/api/join-requests/${id}/${kind}`
      return (await api.post<MemberChangeResult | undefined>(url)).data
    },
    onSuccess: () => refreshTeams(queryClient),
  })
}
