import type { RouteObject } from 'react-router-dom'
import { AdminTeamDetailPage } from './AdminTeamDetailPage'
import { TeamsPage } from './TeamsPage'

/** Route quản trị của Cường (FE-21, FE-22), tương đối với `/admin`: W15 ở mục "Nhóm", W16 khi bấm một nhóm. */
export const adminTeamRoutes: RouteObject[] = [
  { path: 'nhom', element: <TeamsPage /> },
  { path: 'nhom/:teamId', element: <AdminTeamDetailPage /> },
]
