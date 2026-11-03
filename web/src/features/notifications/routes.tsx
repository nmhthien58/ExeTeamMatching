import type { RouteObject } from 'react-router-dom'
import { NotificationsPage } from './NotificationsPage'

export const studentNotificationRoutes: RouteObject[] = [
  { path: 'thong-bao', element: <NotificationsPage roleRoot="/sv" /> },
]

export const lecturerNotificationRoutes: RouteObject[] = [
  { path: 'thong-bao', element: <NotificationsPage roleRoot="/gv" /> },
]

export const mentorNotificationRoutes: RouteObject[] = [
  { path: 'thong-bao', element: <NotificationsPage roleRoot="/mentor" /> },
]
