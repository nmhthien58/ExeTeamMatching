import type { RouteObject } from 'react-router-dom'
import { TeamFormationOnly } from '@/shared/phase/PhaseGuard'
import { DiscoverPage } from '@/features/discover/DiscoverPage'
import { TeamDetailPage } from '@/features/discover/TeamDetailPage'
import { HomePage } from '@/features/home/HomePage'
import { InvitationsPage } from '@/features/invitations/InvitationsPage'
import { MembersPage } from './MembersPage'
import { MyTeamPage } from './MyTeamPage'
import { RequestsPage } from './RequestsPage'
import { EditTeamPage, TeamFormPage } from './TeamFormPage'

/**
 * Route sinh viên của Cường (FE-37), tương đối với `/sv`: S01, S05/S06, S08/S09 (Khám phá chỉ trong đợt lập nhóm – rule 18),
 * S12, S13–S17, S20. App.tsx chỉ tự bọc TeamFormationOnly cho màn giữ chỗ nên Khám phá tự bọc ở đây.
 */
export const studentTeamRoutes: RouteObject[] = [
  { index: true, element: <HomePage /> },
  { path: 'kham-pha', element: <TeamFormationOnly><DiscoverPage /></TeamFormationOnly> },
  { path: 'kham-pha/nhom/:teamId', element: <TeamFormationOnly><TeamDetailPage /></TeamFormationOnly> },
  { path: 'loi-moi', element: <InvitationsPage /> },
  { path: 'nhom', element: <MyTeamPage /> },
  { path: 'nhom/tao', element: <TeamFormationOnly><TeamFormPage /></TeamFormationOnly> },
  { path: 'nhom/sua', element: <EditTeamPage /> },
  { path: 'nhom/thanh-vien', element: <MembersPage /> },
  { path: 'nhom/yeu-cau', element: <RequestsPage /> },
]
