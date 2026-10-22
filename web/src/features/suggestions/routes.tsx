import type { RouteObject } from 'react-router-dom'
import { TeamFormationOnly } from '@/shared/phase/PhaseGuard'
import { AutoAssignedPage } from '@/features/round-closed/AutoAssignedPage'
import { RoundClosedPage } from '@/features/round-closed/RoundClosedPage'
import { InviteStudentsPage } from './InviteStudentsPage'
import { SuggestedMembersPage } from './SuggestedMembersPage'
import { SuggestedTeamsPage } from './SuggestedTeamsPage'

/**
 * Route của sinh viên (FE-40), tương đối với `/sv`: S07 nằm trong Khám phá (chỉ mở trong đợt lập nhóm – rule 18),
 * S18, S19 trong Nhóm của tôi; S30, S31 sau hạn chót (mở từ trang chủ, thông báo).
 */
export const suggestionRoutes: RouteObject[] = [
  { path: 'kham-pha/goi-y', element: <TeamFormationOnly><SuggestedTeamsPage /></TeamFormationOnly> },
  { path: 'nhom/goi-y', element: <SuggestedMembersPage /> },
  { path: 'nhom/moi', element: <InviteStudentsPage /> },
  { path: 'nhom/da-khoa', element: <RoundClosedPage /> },
  { path: 'nhom/ghep-tu-dong', element: <AutoAssignedPage /> },
]
