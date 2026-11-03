import type { RouteObject } from 'react-router-dom'
import { ChangeRequestDetailPage } from './ChangeRequestDetailPage'
import { ChangeRequestsPage } from './ChangeRequestsPage'
import { StudentChangeRequestCreatePage } from './StudentChangeRequestCreatePage'
import { StudentChangeRequestDetailPage } from './StudentChangeRequestDetailPage'
import { StudentChangeRequestsPage } from './StudentChangeRequestsPage'

/** Admin routes W17–W18, relative to `/admin`; wire into the shared app routes after review. */
export const changeRequestRoutes: RouteObject[] = [
  { path: 'doi-nhom', element: <ChangeRequestsPage /> },
  { path: 'doi-nhom/:id', element: <ChangeRequestDetailPage /> },
  { path: 'yeu-cau-doi-nhom', element: <ChangeRequestsPage /> },
  { path: 'yeu-cau-doi-nhom/:id', element: <ChangeRequestDetailPage /> },
]

/** Student routes S26–S29, relative to `/sv`. */
export const studentChangeRequestRoutes: RouteObject[] = [
  { path: 'yeu-cau-doi-nhom', element: <StudentChangeRequestsPage /> },
  { path: 'yeu-cau-doi-nhom/tao', element: <StudentChangeRequestCreatePage /> },
  { path: 'yeu-cau-doi-nhom/:id', element: <StudentChangeRequestDetailPage /> },
]
