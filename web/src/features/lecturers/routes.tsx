import type { RouteObject } from 'react-router-dom'
import { LecturersPage } from './LecturersPage'

/** W11 (FE-17), tương đối với `/admin`. */
export const adminLecturerRoutes: RouteObject[] = [{ path: 'giang-vien', element: <LecturersPage /> }]
