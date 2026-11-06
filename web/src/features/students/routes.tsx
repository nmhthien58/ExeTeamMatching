import type { RouteObject } from 'react-router-dom'
import { StudentsPage } from './StudentsPage'

/** W09, W10 (FE-15, FE-16), tương đối với `/admin`. */
export const adminStudentRoutes: RouteObject[] = [{ path: 'sinh-vien', element: <StudentsPage /> }]
