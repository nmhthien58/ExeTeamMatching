import type { RouteObject } from 'react-router-dom'
import { LecturerDetailPage } from './LecturerDetailPage'
import { LecturerPreferencesPage } from './LecturerPreferencesPage'
import { TeamLecturerPage } from './TeamLecturerPage'

export const studentAdvisorRoutes: RouteObject[] = [
  { path: 'nhom/giang-vien', element: <LecturerPreferencesPage /> },
  { path: 'nhom/giang-vien/:lecturerId', element: <LecturerDetailPage /> },
  { path: 'nhom/co-van', element: <TeamLecturerPage /> },
]
