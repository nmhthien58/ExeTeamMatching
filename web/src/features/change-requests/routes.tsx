import type { RouteObject } from 'react-router-dom'
import { ChangeRequestDetailPage } from './ChangeRequestDetailPage'
import { ChangeRequestsPage } from './ChangeRequestsPage'

/** Admin routes W17–W18, relative to `/admin`; wire into the shared app routes after review. */
export const changeRequestRoutes: RouteObject[] = [
  { path: 'doi-nhom', element: <ChangeRequestsPage /> },
  { path: 'doi-nhom/:id', element: <ChangeRequestDetailPage /> },
  { path: 'yeu-cau-doi-nhom', element: <ChangeRequestsPage /> },
  { path: 'yeu-cau-doi-nhom/:id', element: <ChangeRequestDetailPage /> },
]
