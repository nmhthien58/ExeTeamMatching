import type { RouteObject } from 'react-router-dom'
import { LecturerMatchingPage } from './LecturerMatchingPage'

/** Route quản trị W19 (FE-25), tương đối với `/admin`. */
export const adminLecturerMatchingRoutes: RouteObject[] = [{ path: 'ghep-giang-vien', element: <LecturerMatchingPage /> }]
